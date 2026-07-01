using System;
using Clutch;
using Save;
using UnityEngine;

namespace Milestones
{
    /// <summary>
    /// Device-level distance milestone service - the game's main progression driver (GDD). Watches the
    /// total driven distance (<see cref="SaveManager.DistanceDrivenKm"/>) and exposes the milestones the
    /// player has reached but not yet been rewarded for as a FIFO <b>pending queue</b>.
    ///
    /// Reward model: a milestone's gold is granted ONLY when it is claimed - never automatically on
    /// reaching it. So completing several milestones in a row (e.g. crossing 1 km, then 3 km before the
    /// first pop-up is resolved) simply leaves them all queued; none are skipped and none are paid until
    /// claimed. The (future) "MILESTONE COMPLETED" pop-up drives this: it shows <see cref="NextPending"/>,
    /// then calls <see cref="ClaimNextPending"/> (the "Claim" button or the 3-second auto-dismiss timer,
    /// granting the base reward) or <see cref="ClaimNextPendingWithAdBonus"/> (the "3X" rewarded-ad button,
    /// granting base * <see cref="RewardAdMultiplier"/>), and repeats while <see cref="HasPending"/>.
    ///
    /// The queue is DERIVED, not separately stored: pending = the milestone indices in
    /// [<see cref="MilestonesClaimed"/>, <see cref="ReachedCount"/>). Both ends come from persisted save
    /// values (driven distance and claimed count), so pending milestones survive an app restart for free
    /// and a reward can never be lost by closing the game.
    ///
    /// Distance is global - driving with any car counts (read from the single saved odometer). This
    /// service is intentionally NOT wired to any UI. Call <see cref="Initialize"/> once at startup (see
    /// GameInitializer); the table itself lives in the "MilestonesConfig" Clutch flag (see
    /// <see cref="MilestonesConfig"/>), resolved remote-first with the ClutchConfig SO fallback.
    /// </summary>
    public static class DistanceMilestoneManager
    {
        // The resolved milestone table + tuning from the "MilestonesConfig" Clutch flag. Never null. Resolved
        // through the DI service when available - memoized and re-parsed after each Clutch init, so a value
        // read from the SO fallback at boot is automatically upgraded to the resolved Clutch value - else the
        // ClutchConfig SO fallback directly. Cheap enough to read on the per-frame HUD path.
        private static MilestonesConfig Config =>
            ClutchConfigResolver.Get<MilestonesConfig>(ClutchFlagKeys.MilestonesConfig);

        /// <summary>
        /// Raised whenever the always-on progress display could change - the distance changed (so the bar
        /// / "X / Y KM" moved) or a milestone was claimed. The HUD displayer subscribes to this and
        /// re-reads <see cref="CurrentMilestone"/> / <see cref="CurrentProgress01"/>.
        /// </summary>
        public static event Action OnProgressChanged;

        /// <summary>
        /// Raised whenever the pending queue could change - a new milestone was reached or a pending one
        /// was claimed. The (future) "MILESTONE COMPLETED" pop-up subscribes to this; on each call it
        /// shows the next one while <see cref="HasPending"/> and nothing is already on screen.
        /// </summary>
        public static event Action OnPendingChanged;

        // Safety bound for the reached-count scan: it can never legitimately exceed this in one go. Guards
        // against a debug jump to a huge distance (or a misconfigured table) turning the scan into a spin.
        private const int MaxMilestonesPerPass = 1000;

        private static bool _initialized;

        // Live uncommitted distance (km) reported by the active VehicleKmTracker each physics step. The
        // saved odometer (DistanceDrivenKm) only changes at commit boundaries (despawn/pause/quit), so this
        // is what lets the HUD bar move smoothly WHILE driving. Display-only: it is deliberately NOT fed
        // into the reward/pending/completion logic (that stays on the committed whole-km value). Reset to 0
        // whenever the committed value changes (a commit just flushed it) or the save is wiped.
        private static float _sessionPendingKm;

        /// <summary>Total distance driven across all cars, in whole km (the saved odometer).</summary>
        public static int DistanceDrivenKm => SaveManager.DistanceDrivenKm;

        /// <summary>
        /// Live driven distance (km, fractional) = the committed odometer plus the active car's not-yet-
        /// committed distance. Updates continuously while driving (unlike <see cref="DistanceDrivenKm"/>),
        /// so it is the value the HUD bar should display. Display-only - it does not drive rewards.
        /// </summary>
        public static float LiveDistanceKm => SaveManager.DistanceDrivenKm + _sessionPendingKm;

        /// <summary>
        /// Reports the active vehicle's current uncommitted distance (km) for the live HUD display. Called
        /// by <see cref="Vehicles.VehicleKmTracker"/> every physics step - a plain float assignment, so it
        /// allocates nothing and never touches PlayerPrefs. Does not grant or queue anything.
        /// </summary>
        public static void ReportSessionDistanceKm(float pendingKm)
        {
            _sessionPendingKm = pendingKm > 0f ? pendingKm : 0f;
        }

        /// <summary>How many milestones have been claimed (and rewarded). Equals the index of the oldest unclaimed milestone.</summary>
        public static int MilestonesClaimed => SaveManager.DistanceMilestonesClaimed;

        /// <summary>Rewarded-ad multiplier offered on milestone completion (e.g. 3 -> "3X"), clamped to at least 1.</summary>
        public static int RewardAdMultiplier => Mathf.Max(1, Config.RewardAdMultiplier);

        /// <summary>
        /// Seconds the MILESTONE COMPLETED pop-up stays up before it auto-claims the base reward (also drives
        /// the shared multiplier upsell's countdown so they close together). Clamped to a small positive min.
        /// </summary>
        public static float PopupCloseSeconds => Mathf.Max(0.1f, Config.PopupCloseSeconds);

        /// <summary>
        /// Number of milestones whose distance threshold has been reached (claimed or not). Computed from
        /// the driven distance against the table; not stored.
        /// </summary>
        public static int ReachedCount => ComputeReachedCount(Config, SaveManager.DistanceDrivenKm);

        /// <summary>How many reached milestones are still awaiting a claim (the pending queue length).</summary>
        public static int PendingCount => Mathf.Max(0, ReachedCount - SaveManager.DistanceMilestonesClaimed);

        /// <summary>True while at least one completed milestone is waiting to be claimed.</summary>
        public static bool HasPending => PendingCount > 0;

        /// <summary>
        /// The oldest unclaimed completed milestone - the one the pop-up should show next - or an invalid
        /// info (<see cref="DistanceMilestoneInfo.IsValid"/> == false) when nothing is pending.
        /// </summary>
        public static DistanceMilestoneInfo NextPending
        {
            get
            {
                if (!HasPending)
                    return default;

                return Config.GetMilestone(SaveManager.DistanceMilestonesClaimed);
            }
        }

        /// <summary>
        /// The milestone currently being driven toward - the next UNREACHED milestone - for the always-on
        /// displayer. (This is what makes "1.2 / 3 KM" correct even while an earlier milestone is still
        /// queued: progress points at the next target, not at one already passed.) Invalid when the table
        /// is unavailable.
        /// </summary>
        public static DistanceMilestoneInfo CurrentMilestone
        {
            get
            {
                return Config.GetMilestone(ReachedCount);
            }
        }

        /// <summary>
        /// Absolute progress toward the current (next unreached) milestone in 0..1 (driven / threshold),
        /// matching the GDD "X / Y KM" framing. Returns 0 when there is no valid current milestone. Note:
        /// the saved distance is whole-km precision, so this advances in 1 km steps.
        /// </summary>
        public static float CurrentProgress01
        {
            get
            {
                DistanceMilestoneInfo milestone = CurrentMilestone;
                if (!milestone.IsValid || milestone.ThresholdKm <= 0)
                    return 0f;

                return Mathf.Clamp01((float)SaveManager.DistanceDrivenKm / milestone.ThresholdKm);
            }
        }

        /// <summary>
        /// Live counterpart of <see cref="CurrentMilestone"/> for the HUD: the next milestone not yet
        /// reached by the LIVE distance (so the displayer advances milestone-to-milestone while driving,
        /// instead of stalling until the next commit). Invalid when the table is unavailable.
        /// </summary>
        public static DistanceMilestoneInfo LiveCurrentMilestone
        {
            get
            {
                // floor(liveKm) >= threshold(int) is equivalent to liveKm >= threshold, so the existing
                // whole-km scan can be reused directly.
                MilestonesConfig config = Config;
                int reached = ComputeReachedCount(config, Mathf.FloorToInt(LiveDistanceKm));
                return config.GetMilestone(reached);
            }
        }

        /// <summary>
        /// Live, fractional progress (0..1) toward <see cref="LiveCurrentMilestone"/> = liveKm / threshold.
        /// This is what makes the HUD fill move smoothly while driving. 0 when no valid milestone.
        /// </summary>
        public static float LiveCurrentProgress01
        {
            get
            {
                DistanceMilestoneInfo milestone = LiveCurrentMilestone;
                if (!milestone.IsValid || milestone.ThresholdKm <= 0)
                    return 0f;

                return Mathf.Clamp01(LiveDistanceKm / milestone.ThresholdKm);
            }
        }

        /// <summary>
        /// True when the LIVE distance has crossed a milestone threshold the committed odometer
        /// (<see cref="DistanceDrivenKm"/>) has not yet reached - i.e. the player just drove past a milestone
        /// line but the whole-km commit that makes it claimable hasn't happened. The km tracker polls this each
        /// physics step and commits on the way past, so the MILESTONE COMPLETED pop-up can appear at the moment
        /// of crossing instead of waiting for the next despawn/pause commit. Cheap: a couple of comparisons
        /// against the ascending table. False (no forced commit) when the table is unavailable.
        /// </summary>
        public static bool HasUncommittedReachedMilestone()
        {
            MilestonesConfig config = Config;
            int committedReached = ComputeReachedCount(config, SaveManager.DistanceDrivenKm);
            int liveReached = ComputeReachedCount(config, Mathf.FloorToInt(LiveDistanceKm));
            return liveReached > committedReached;
        }

        /// <summary>
        /// Subscribes to distance changes. Pending milestones from a previous session stay pending (their
        /// rewards are not auto-granted), ready for the pop-up to claim. Idempotent - safe to call once at
        /// startup; repeated calls are a no-op.
        /// </summary>
        public static void Initialize()
        {
            if (_initialized)
                return;

            _initialized = true;

            // The config always resolves (remote -> SO fallback -> schema defaults), so the service is never
            // "disabled" for a missing asset; only warn when it resolves to an empty table (a misconfiguration).
            if (Config.ExplicitCount == 0)
                Debug.LogError("[DistanceMilestoneManager] MilestonesConfig has no milestones; distance " +
                               "milestones will grant nothing. Check the 'MilestonesConfig' Clutch flag and the " +
                               "ClutchConfig fallback asset (Assets/_Game/Data/Resources/ClutchConfig.asset).");

            // Defensive -= before += so a domain reload that kept these static delegates can't stack a
            // duplicate subscription (mirrors the pattern used by the save-value widgets).
            SaveManager.OnDistanceDrivenChanged -= HandleDistanceChanged;
            SaveManager.OnDistanceDrivenChanged += HandleDistanceChanged;
            SaveManager.OnSaveReset -= HandleSaveReset;
            SaveManager.OnSaveReset += HandleSaveReset;

            // Sync any listener that is already active.
            OnProgressChanged?.Invoke();
            OnPendingChanged?.Invoke();
        }

        // Distance moved: the HUD progress changed, and we may have reached new milestone(s). No reward is
        // granted here - newly reached milestones just become claimable (see HasPending / NextPending).
        private static void HandleDistanceChanged(int _)
        {
            // The committed odometer just moved (a commit flushed the pending distance, or a debug set).
            // Clear the live session delta so LiveDistanceKm rebaselines onto the new committed value and
            // can't double-count the distance the commit just absorbed.
            _sessionPendingKm = 0f;

            OnProgressChanged?.Invoke();
            OnPendingChanged?.Invoke();
        }

        // A full save wipe reset both the distance and the claimed count to 0, so the queue is empty again;
        // just re-sync the listeners.
        private static void HandleSaveReset()
        {
            _sessionPendingKm = 0f;

            OnProgressChanged?.Invoke();
            OnPendingChanged?.Invoke();
        }

        /// <summary>
        /// Claims the next pending milestone at its base reward (1x) and advances the claimed count. This
        /// is the "Claim" button and the pop-up's auto-dismiss timer (so a reward is never lost by letting
        /// the timer run out). Returns false (no-op) when nothing is pending.
        /// </summary>
        public static bool ClaimNextPending() => Resolve(useAdBonus: false);

        /// <summary>
        /// Claims the next pending milestone at the rewarded-ad multiplier (base * <see cref="RewardAdMultiplier"/>)
        /// and advances the claimed count. Call only after a successful rewarded ad (the "3X" button).
        /// Returns false (no-op) when nothing is pending.
        /// </summary>
        public static bool ClaimNextPendingWithAdBonus() => Resolve(useAdBonus: true);

        // Grants the oldest pending milestone (base, or base * multiplier with the ad bonus), advances and
        // persists the claimed count, and notifies listeners. No-op when nothing is pending.
        private static bool Resolve(bool useAdBonus)
        {
            if (!HasPending)
                return false;

            MilestonesConfig config = Config;
            int index = SaveManager.DistanceMilestonesClaimed;
            DistanceMilestoneInfo milestone = config.GetMilestone(index);
            int multiplier = Mathf.Max(1, config.RewardAdMultiplier);
            int amount = useAdBonus ? milestone.RewardGold * multiplier : milestone.RewardGold;

            SaveManager.AddGolds(amount);
            SaveManager.DistanceMilestonesClaimed = index + 1;
            SaveManager.Save();

            OnPendingChanged?.Invoke();
            OnProgressChanged?.Invoke();
            return true;
        }

        // Counts milestones whose threshold has been reached. Starts at the claimed count (claimed
        // milestones are by definition reached) and walks up the ascending thresholds while the driven
        // distance still covers them. The guard only ever trips on a misconfigured table or a debug jump.
        private static int ComputeReachedCount(MilestonesConfig config, int driven)
        {
            int reached = SaveManager.DistanceMilestonesClaimed;
            int guard = 0;

            while (driven >= config.GetThresholdKm(reached))
            {
                if (++guard > MaxMilestonesPerPass)
                {
                    Debug.LogError("[DistanceMilestoneManager] Reached-count scan hit the safety cap; check the " +
                                   "milestone table for a non-ascending or zero-step configuration.");
                    break;
                }

                reached++;
            }

            return reached;
        }
    }
}
