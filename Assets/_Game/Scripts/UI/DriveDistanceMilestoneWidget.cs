using System;
using System.Globalization;
using Milestones;
using TMPro;
using UISystem.Runtime.Scripts;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// Always-on in-game HUD widget that shows the current distance milestone (the next one being driven
    /// toward) and live progress toward it.
    ///
    /// It POLLS each frame in <see cref="Update"/> rather than listening to a save event, because the
    /// saved odometer only changes at commit boundaries (despawn / pause / quit), which would leave the
    /// bar frozen while driving. Instead it reads the milestone service's live, fractional distance
    /// (<see cref="DistanceMilestoneManager.LiveDistanceKm"/>), fed continuously by the active
    /// VehicleKmTracker. The fill eases toward its target via <see cref="Mathf.SmoothDamp"/> so it never
    /// snaps; the label strings are rebuilt only when the displayed whole-km / reward value actually
    /// changes, so there is no per-frame allocation.
    ///
    /// Reward granting is unaffected: this widget is display-only. If the milestone system is unavailable
    /// (no table / no valid milestone) or anything throws, the whole <see cref="content"/> is hidden.
    /// </summary>
    public class DriveDistanceMilestoneWidget : UIWidget
    {
        [SerializeField] private GameObject content;
        [SerializeField] private Image fillImage;
        [SerializeField] private TMP_Text progressText;
        [SerializeField] private TMP_Text rewardAmountText;

        [Tooltip("Approximate seconds for the fill to ease to its target. Higher = smoother/slower. " +
                 "0 = instant (no smoothing).")]
        [SerializeField, Min(0f)] private float fillSmoothTime = 0.25f;

        // Cached last-shown values so the (allocating) label strings are only rebuilt on a real change.
        private int _lastShownKm = -1;
        private int _lastShownTargetKm = -1;
        private int _lastShownReward = -1;

        // Fill-smoothing state. _snapNext makes the next paint jump straight to the target (no ease) so the
        // bar doesn't visibly sweep when the HUD (re)appears or after it was hidden.
        private float _fillVelocity;
        private bool _snapNext = true;

        private void OnEnable()
        {
            _snapNext = true;
        }

        private void Update()
        {
            Refresh();
        }

        // Re-reads the live milestone state and repaints. Hides content on any error or when there is no
        // valid milestone to show.
        private void Refresh()
        {
            try
            {
                // Single snapshot per frame (avoids recomputing the milestone / re-reading distance twice).
                DistanceMilestoneInfo milestone = DistanceMilestoneManager.LiveCurrentMilestone;
                if (!milestone.IsValid)
                {
                    SetContentActive(false);
                    _snapNext = true;
                    return;
                }

                SetContentActive(true);

                int target = milestone.ThresholdKm;
                float liveKm = DistanceMilestoneManager.LiveDistanceKm;
                float targetFill = target > 0 ? Mathf.Clamp01(liveKm / target) : 0f;

                if (fillImage)
                {
                    if (_snapNext)
                    {
                        // First valid frame after appearing/becoming valid: jump to the value, no sweep.
                        fillImage.fillAmount = targetFill;
                        _fillVelocity = 0f;
                    }
                    else
                    {
                        // Ease toward the target so filling (and the drop when the target milestone
                        // advances) is smooth instead of stepping. unscaledDeltaTime so it animates even
                        // while the game is paused.
                        fillImage.fillAmount = Mathf.SmoothDamp(
                            fillImage.fillAmount, targetFill, ref _fillVelocity, fillSmoothTime,
                            Mathf.Infinity, Time.unscaledDeltaTime);
                    }
                }

                _snapNext = false;

                // Floor so the number only ticks up when a whole km is actually completed; clamp so it can
                // never read past the target. Only rebuild the string when it changes.
                int shownKm = Mathf.Min(Mathf.FloorToInt(liveKm), target);
                if (progressText && (shownKm != _lastShownKm || target != _lastShownTargetKm))
                {
                    _lastShownKm = shownKm;
                    _lastShownTargetKm = target;
                    progressText.text = $"{shownKm:N0} / {target:N0} KM";
                }

                if (rewardAmountText && milestone.RewardGold != _lastShownReward)
                {
                    _lastShownReward = milestone.RewardGold;
                    rewardAmountText.text = Abbreviate(milestone.RewardGold);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[DriveDistanceMilestoneWidget] Failed to refresh milestone display: {e}", this);
                SetContentActive(false);
                _snapNext = true;
            }
        }

        private void SetContentActive(bool active)
        {
            if (content && content.activeSelf != active)
                content.SetActive(active);
        }

        // Compact, human-readable amount: "950", "1K", "10K", "150K", "1.5M", "15M", "1.2B".
        // One optional decimal (dropped when whole). Invariant culture so the decimal is always a dot,
        // regardless of the active locale.
        private static string Abbreviate(int value)
        {
            if (value < 1_000)
                return value.ToString(CultureInfo.InvariantCulture);
            if (value < 1_000_000)
                return (value / 1_000d).ToString("0.#", CultureInfo.InvariantCulture) + "K";
            if (value < 1_000_000_000)
                return (value / 1_000_000d).ToString("0.#", CultureInfo.InvariantCulture) + "M";
            return (value / 1_000_000_000d).ToString("0.#", CultureInfo.InvariantCulture) + "B";
        }
    }
}
