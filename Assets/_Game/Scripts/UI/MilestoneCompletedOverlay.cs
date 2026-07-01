using Milestones;
using TMPro;
using UIManager;
using UnityEngine;
using UnityEngine.UI;
using Utils;

namespace UI
{
    /// <summary>
    /// The "MILESTONE COMPLETED" pop-up. Driven by <see cref="DistanceMilestoneManager.OnPendingChanged"/>:
    /// while the gameplay HUD is up and a completed milestone is waiting to be claimed, it shows the oldest
    /// pending milestone's reward and lets the player claim it.
    ///
    /// While shown it:
    ///  - hides the always-on <see cref="DriveDistanceMilestoneOverlay"/> HUD bar (restored when it closes);
    ///  - opens the shared <see cref="ClaimGoldMultiplierWithAdsOverlay"/> as a "watch ad to multiply" upsell,
    ///    sharing this pop-up's close time - unless that overlay is already busy (e.g. a gold pickup is using
    ///    it), in which case the upsell is skipped so the two flows never conflict;
    ///  - runs the shared close-time countdown: if nothing is pressed before it elapses, the base reward is
    ///    auto-claimed (so a reward is never lost) and both pop-ups close together.
    ///
    /// Claiming is funnelled through one guarded path (<see cref="Resolve"/>) so the milestone is granted
    /// exactly once no matter which trigger fires first - the claim button, the auto-close timer, or either ad
    /// outcome reported by the upsell. When several milestones are pending they are shown one after another.
    /// </summary>
    public sealed class MilestoneCompletedOverlay : OverlayBase
    {
        [SerializeField] private TMP_Text rewardText;
        [SerializeField] private Button claimButton;

        [Tooltip("Shared pop-up close time (seconds). Drives both this pop-up's auto-claim AND the multiplier " +
                 "upsell's countdown so they close together. If nothing is pressed in this time the base reward " +
                 "is auto-claimed.")]
        [SerializeField, Min(0.1f)] private float popupCloseSeconds = 6f;

        private float _timeLeft;

        // Set once the milestone has been granted this round so no second path can double-grant or double-close.
        private bool _resolved;

        // Set while the upsell's ad is in flight: freezes the auto-claim countdown so the ad result (not the
        // timer) decides the outcome.
        private bool _awaitingAd;

        // The offer handed to the upsell this round, so we close only the upsell we actually own (never a gold
        // pickup's offer that happens to be on the shared overlay).
        private GoldMultiplierAdOffer _offer;

        // Sibling overlays, resolved lazily from the manager's registry (not a scene scan) and cached. Used
        // only on show/hide, never per-frame.
        private ClaimGoldMultiplierWithAdsOverlay _multiplierOverlay;
        private DriveDistanceMilestoneOverlay _driveDistanceOverlay;

        // Set by ForceClose (scene teardown) so OnHidden does not chain to the next milestone or restore the HUD.
        private bool _forceClosing;

        private ClaimGoldMultiplierWithAdsOverlay MultiplierOverlay => _multiplierOverlay
            ? _multiplierOverlay
            : (_multiplierOverlay = GameUIManagerInstance.GetOverlayUI<ClaimGoldMultiplierWithAdsOverlay>());

        private DriveDistanceMilestoneOverlay DriveDistanceOverlay => _driveDistanceOverlay
            ? _driveDistanceOverlay
            : (_driveDistanceOverlay = GameUIManagerInstance.GetOverlayUI<DriveDistanceMilestoneOverlay>());

        // The pop-up belongs to gameplay: only surface it while the gameplay screen is active, so a pending
        // milestone carried over from a previous session can't pop over the menu / loading screens.
        private static bool IsGameplayActive =>
            GameUIManager.Instance && GameUIManager.Instance.ActiveScreen.screen is GameplayScreen;

        // Not fully hidden -> already showing/closing a pop-up; covers the in-between Showing/Hiding states.
        private bool IsBusy => VisibilityState != UIVisibilityState.Hidden;

        protected override void Awake()
        {
            base.Awake();
            if (claimButton)
                claimButton.onClick.AddListener(OnClaimClicked);
        }

        private void OnEnable()
        {
            DistanceMilestoneManager.OnPendingChanged += HandlePendingChanged;
            HandlePendingChanged();
        }

        private void OnDisable()
        {
            DistanceMilestoneManager.OnPendingChanged -= HandlePendingChanged;
        }

        // A milestone may have become claimable (or the queue otherwise changed). Surface the pop-up when the
        // HUD is up, something is pending, and we are not already showing / closing one.
        private void HandlePendingChanged()
        {
            if (IsBusy || !IsGameplayActive || !DistanceMilestoneManager.HasPending)
                return;

            Show();
        }

        protected override void OnShowed(bool immediate, object uiData = null)
        {
            base.OnShowed(immediate, uiData);

            _resolved = false;
            _awaitingAd = false;
            _timeLeft = popupCloseSeconds;

            DistanceMilestoneInfo milestone = DistanceMilestoneManager.NextPending;
            if (rewardText)
                rewardText.text = milestone.IsValid ? GoldFormat.Abbreviate(milestone.RewardGold) : string.Empty;

            if (claimButton)
                claimButton.interactable = true;

            // Hide the always-on HUD bar while the completion pop-up is up.
            DriveDistanceMilestoneOverlay hud = DriveDistanceOverlay;
            if (hud && hud.ShowingOrShown)
                hud.Hide();

            // Open the "watch ad to multiply" upsell alongside, sharing this pop-up's close time.
            TryShowMultiplierUpsell();
        }

        private void TryShowMultiplierUpsell()
        {
            _offer = null;

            ClaimGoldMultiplierWithAdsOverlay overlay = MultiplierOverlay;
            if (!overlay)
            {
                Debug.LogError("ClaimGoldMultiplierWithAdsOverlay not found");
                return;
            }
            
            if (overlay.IsBusy)
            {
                Debug.LogError("ClaimGoldMultiplierWithAdsOverlay is busy");
                return;
            }
            

            var offer = new GoldMultiplierAdOffer(
                multiplier: DistanceMilestoneManager.RewardAdMultiplier,
                closeSeconds: popupCloseSeconds,
                onRewarded: () => Resolve(useAdBonus: true),
                onAdFailed: () => Resolve(useAdBonus: false),
                onExpired: null,                       // our own countdown auto-claims the base at the same time
                onClaimInitiated: OnUpsellClaimInitiated);

            if (overlay.TryShowOffer(offer))
                _offer = offer;
        }

        // The player pressed CLAIM on the upsell: the ad is now in flight, so stop our auto-claim countdown and
        // lock our own claim button - the ad outcome (not the timer) decides the reward.
        private void OnUpsellClaimInitiated()
        {
            _awaitingAd = true;
            if (claimButton)
                claimButton.interactable = false;
        }

        private void Update()
        {
            if (_resolved || _awaitingAd || VisibilityState != UIVisibilityState.Showed)
                return;

            _timeLeft -= Time.unscaledDeltaTime;
            if (_timeLeft <= 0f)
                Resolve(useAdBonus: false); // auto-claim the base reward so it is never lost
        }

        private void OnClaimClicked() => Resolve(useAdBonus: false);

        // Single guarded claim+close path. Grants the milestone once (base, or base * ad multiplier), closes
        // the upsell we own, then closes self.
        private void Resolve(bool useAdBonus)
        {
            if (_resolved)
                return;

            _resolved = true;

            if (useAdBonus)
                DistanceMilestoneManager.ClaimNextPendingWithAdBonus();
            else
                DistanceMilestoneManager.ClaimNextPending();

            // Close the upsell, but only the offer we opened (never a gold pickup's) - RequestClose checks
            // ownership and defers if it is still animating in, so we never issue a Hide mid-show.
            ClaimGoldMultiplierWithAdsOverlay overlay = _multiplierOverlay;
            if (overlay && _offer != null)
                overlay.RequestClose(_offer);
            _offer = null;

            if (ShowingOrShown)
                Hide();
        }

        protected override void OnHidden(bool immediate = false)
        {
            base.OnHidden(immediate);

            if (_forceClosing)
            {
                _forceClosing = false;
                return;
            }

            // Chain straight to the next pending milestone (keeping the HUD hidden), otherwise restore the HUD.
            if (IsGameplayActive && DistanceMilestoneManager.HasPending)
            {
                Show();
                return;
            }

            DriveDistanceMilestoneOverlay hud = DriveDistanceOverlay;
            if (IsGameplayActive && hud && !hud.ShowingOrShown)
                hud.Show();
        }

        /// <summary>
        /// Force the pop-up shut without claiming, chaining, or restoring the HUD. Called when the gameplay
        /// scene tears down (see <c>GameManager.OnDestroy</c>) so this DontDestroyOnLoad view never lingers
        /// over the menu. Any in-flight ad callback is neutralised by the <see cref="_resolved"/> guard.
        /// </summary>
        public void ForceClose()
        {
            _resolved = true;
            _offer = null;
            if (ShowingOrShown || VisibilityState == UIVisibilityState.Showing)
            {
                _forceClosing = true;
                Hide(true);
            }
        }
    }
}
