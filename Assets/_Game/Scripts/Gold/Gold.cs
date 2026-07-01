using System;
using Clutch;
using Save;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Gold
{
    /// <summary>
    /// A single scattered gold collection point. One script owns the whole behaviour:
    /// spin animation, trigger pickup, the base reward grant, the passive/cooldown lifecycle
    /// (dimmed visual + a world-space countdown that reactivates after the cooldown), and raising
    /// the "CLAIM Nx" rewarded-ad request.
    ///
    /// Cooldown is tracked in real (wall-clock) time and persisted per <see cref="goldId"/>, so a
    /// collected gold stays passive across app sessions until its cooldown elapses. The spawner
    /// assigns <see cref="goldId"/> from each marker so the same world position keeps its state.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public sealed class Gold : MonoBehaviour
    {
        /// <summary>
        /// Raised when an active gold is collected, so the UI layer can show the "CLAIM Nx" popup.
        /// Carries the bonus reward (the extra coins granted on top of the base, if the ad is watched),
        /// the multiplier (for display), the popup display duration, and the callback to invoke if the
        /// player successfully claims via the rewarded ad.
        /// </summary>
        public static event Action<GoldClaimRequest> ClaimRequested;

        [Title("Identity")]
        [Tooltip("Unique id used to persist this gold's cooldown. Usually assigned by the spawner from " +
                 "the marker name; set manually only for hand-placed gold.")]
        [SerializeField] private string goldId = "";

        [Title("Reward")]
        [Tooltip("Coins granted immediately when the gold is collected.")]
        [SerializeField, Min(0)] private int baseReward = 1000;

        [Tooltip("CLAIM multiplier offered via rewarded ad. The GDD is inconsistent (5X title vs 6X body), " +
                 "so this is configurable. Total = baseReward * multiplier; the ad grants the extra " +
                 "(multiplier - 1) * baseReward on top of the base.")]
        [SerializeField, Min(1)] private int claimMultiplier = 5;

        [Title("Cooldown")]
        [Tooltip("Seconds the gold stays passive after collection before reactivating. GDD: 30 minutes.")]
        [SerializeField, Min(0)] private float cooldownSeconds = 1800f;

        [Tooltip("Scale (as a fraction of the full size) the gold shrinks to while passive/on cooldown, so it " +
                 "stays visible but smaller. 1 = no shrink, 0 = disappears. GDD wants the gold to remain " +
                 "visible while passive.")]
        [SerializeField, Range(0f, 1f)] private float passiveScale = 0.5f;

        [Title("Spin")]
        [Tooltip("Degrees per second the active gold rotates around the world up axis.")]
        [SerializeField] private float rotationSpeed = 90f;

        [Tooltip("Transform that actually spins (usually the coin visual child, so the cooldown timer and " +
                 "any other root children don't rotate with it). Falls back to the active visual, then this " +
                 "GameObject, when left unassigned.")]
        [SerializeField] private Transform rotationTarget;

        [Title("Detection")]
        [Tooltip("Tag the colliding object's root must have to collect the gold (the player vehicle).")]
        [SerializeField] private string vehicleTag = "Vehicle";

        [Title("Visuals")]
        [Tooltip("Shown while the gold is active and collectable.")]
        [SerializeField] private GameObject activeVisual;

        [Tooltip("Shown while the gold is on cooldown (dimmed). Stays visible per GDD.")]
        [SerializeField] private GameObject passiveVisual;

        [Tooltip("World-space countdown widget shown above a passive gold (the project TimerTextWidget). " +
                 "Enabled only while on cooldown; Gold drives it via StartTimer/StopTimer and reactivates " +
                 "the gold on its OnTimerCompleted.")]
        [SerializeField] private UISystem.Runtime.Scripts.Mono.TimerTextWidget timerWidget;

        [Tooltip("Optional collect VFX driver (the coin's ShrinkOnTrigger). Gold owns the trigger, so the " +
                 "VFX component's own trigger handling should be left unused; Gold plays it on collect.")]
        [SerializeField] private ShrinkOnTrigger collectVfx;

        [ShowInInspector, ReadOnly]
        private bool _isPassive;

        // Wall-clock time (Unix seconds) the gold reactivates; valid only while passive.
        private long _reactivateUnixSeconds;

        // Captured at Awake so the gold can be restored after the collect VFX shrinks it to the passive size.
        private Vector3 _activeScale = Vector3.one;

        // The transform that gets scaled for the passive shrink: the rotation target (coin visual) so the
        // cooldown timer and other root children keep their size. Falls back to this GameObject.
        private Transform ScaleTarget => rotationTarget != null ? rotationTarget : transform;

        /// <summary>Configures the persistent id. Called by the spawner before the gold goes live.</summary>
        public void SetGoldId(string id)
        {
            goldId = id;
        }

        private void Awake()
        {
            // Pickup detection needs a trigger collider.
            var col = GetComponent<Collider>();
            col.isTrigger = true;

            // Remember the full-size scale so reactivation can undo the collect VFX shrink.
            _activeScale = ScaleTarget.localScale;
        }

        private void Start()
        {
            RestoreStateFromSave();
        }

        private void Update()
        {
            if (_isPassive)
            {
                // The TimerTextWidget ticks itself and reactivates the gold via OnTimerCompleted. Only when no
                // widget is wired do we poll the wall clock here as a fallback so the gold still reactivates.
                if (timerWidget == null)
                    CheckCooldownElapsed();
            }
            else if (rotationSpeed != 0f)
            {
                // Spin the dedicated rotation target (the coin visual) so root children like the cooldown
                // timer don't rotate with it. Fall back to the active visual, then this GameObject, when no
                // target is assigned. Spin around the world Y axis so the coin spins like a standing coin
                // regardless of how the marker oriented it (markers tilt the coin upright on X).
                Transform spinTarget = rotationTarget != null
                    ? rotationTarget
                    : (activeVisual != null ? activeVisual.transform : transform);
                spinTarget.Rotate(Vector3.up, rotationSpeed * Time.deltaTime, Space.World);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_isPassive)
                return;

            if (other == null || !other.transform.root.CompareTag(vehicleTag))
                return;

            Collect();
        }

        // -----------------------------------------------------------------
        // Lifecycle
        // -----------------------------------------------------------------

        private void Collect()
        {
            // Base reward is granted immediately, unconditionally.
            SaveManager.AddGolds(baseReward);

            // Play the collect VFX: particle burst + shrink to the passive size (keeps the coin proportions).
            if (collectVfx != null)
                collectVfx.TriggerShrink(_activeScale * passiveScale);

            // Persist the collection time so the cooldown survives app restarts.
            long now = Now();
            if (!string.IsNullOrEmpty(goldId))
            {
                SaveManager.SetGoldCollected(goldId, now);
                SaveManager.Save();
            }

            // The collect VFX animates the shrink, so don't snap the scale here.
            EnterPassive(now + (long)cooldownSeconds, animateShrink: true);

            // Offer the "CLAIM Nx" bonus via rewarded ad. The bonus is the extra over the base. The pop-up
            // close time comes from the FreeGoldConfig Clutch flag (remote, with the ClutchConfig SO fallback).
            int bonus = baseReward * claimMultiplier - baseReward;
            float claimPopupSeconds = ClutchConfigResolver.Get<FreeGoldConfig>(ClutchFlagKeys.FreeGoldConfig).ClaimPopupSeconds;
            ClaimRequested?.Invoke(new GoldClaimRequest(baseReward, claimMultiplier, bonus, claimPopupSeconds, OnBonusClaimed));
        }

        private void OnBonusClaimed(int bonus)
        {
            // Invoked by the popup only if the player watched the rewarded ad.
            SaveManager.AddGolds(bonus);
            SaveManager.Save();
        }

        // animateShrink: true when entering passive via a fresh collect (the collect VFX animates the shrink
        // to the passive scale). false when restoring a still-cooling gold at startup, where we snap straight
        // to the passive scale with no animation.
        private void EnterPassive(long reactivateUnixSeconds, bool animateShrink)
        {
            _isPassive = true;
            _reactivateUnixSeconds = reactivateUnixSeconds;

            if (activeVisual != null) activeVisual.SetActive(false);
            if (passiveVisual != null) passiveVisual.SetActive(true);

            // On restore (no collect animation), snap the coin to the passive size immediately.
            if (!animateShrink)
                ScaleTarget.localScale = _activeScale * passiveScale;

            // Enable the timer widget and start it for the remaining cooldown. The widget ticks itself via the
            // global ticker and raises OnTimerCompleted, which reactivates the gold - so no per-frame polling.
            if (timerWidget != null)
            {
                long remaining = reactivateUnixSeconds - Now();
                if (remaining < 0) remaining = 0;

                timerWidget.gameObject.SetActive(true);
                timerWidget.OnTimerCompleted -= OnCooldownCompleted; // guard against double subscription
                timerWidget.OnTimerCompleted += OnCooldownCompleted;
                timerWidget.StartTimer(remaining);
            }
        }

        private void EnterActive()
        {
            _isPassive = false;

            if (!string.IsNullOrEmpty(goldId))
            {
                SaveManager.ClearGoldCollected(goldId);
                SaveManager.Save();
            }

            if (timerWidget != null)
            {
                timerWidget.OnTimerCompleted -= OnCooldownCompleted;
                timerWidget.StopTimer();
                timerWidget.gameObject.SetActive(false);
            }

            if (passiveVisual != null) passiveVisual.SetActive(false);
            if (activeVisual != null) activeVisual.SetActive(true);

            // Undo any collect-VFX shrink so the reactivated gold is full size again.
            ScaleTarget.localScale = _activeScale;
        }

        // Raised by the timer widget when the cooldown reaches zero.
        private void OnCooldownCompleted()
        {
            if (_isPassive)
                EnterActive();
        }

        // Fallback used only when no timer widget is wired: reactivate once the wall-clock cooldown elapses.
        private void CheckCooldownElapsed()
        {
            if (_reactivateUnixSeconds - Now() <= 0)
                EnterActive();
        }

        // Restores active/passive state from the persisted cooldown, accounting for time elapsed
        // while the app was closed.
        private void RestoreStateFromSave()
        {
            if (string.IsNullOrEmpty(goldId))
            {
                EnterActive();
                return;
            }

            long collected = SaveManager.GetGoldCollectedTime(goldId);
            if (collected < 0)
            {
                EnterActive();
                return;
            }

            long reactivateAt = collected + (long)cooldownSeconds;
            if (Now() >= reactivateAt)
                EnterActive();
            else
                EnterPassive(reactivateAt, animateShrink: false); // restored: snap to passive scale, no VFX
        }

        // -----------------------------------------------------------------
        // Helpers
        // -----------------------------------------------------------------

        private static long Now()
        {
            return DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }
    }
}
