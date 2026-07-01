using System;
using _Game.Scripts.Utils.VContainer;
using Cysharp.Threading.Tasks;
using SpektraGames.SpektraUtilities.Runtime;
using UnityEngine;

namespace Ads
{
    /// <summary>
    /// Timed Commercial Break interstitials (RCD "Ad Placements"): every
    /// <c>ad_commercial_break_interval_sec</c> of ACTIVE gameplay, show a 3s countdown and then a
    /// (cooldown/cap-gated) interstitial.
    ///
    /// Active-gameplay time is accumulated only while <see cref="_isActive"/> is set by the owning gameplay
    /// HUD (see GameplayScreen), so paused/menu time does not count. The interstitial always routes through
    /// <see cref="IAdGatingService"/> via <see cref="IAdService.ShowInterstitialAdAsync"/>, so the shared
    /// cooldown and session/daily caps still apply.
    ///
    /// Re-arm rules:
    ///   * On a shown break, the accumulator resets to 0 (next break is a full interval away).
    ///   * If the gate refuses at interval (cooldown not elapsed / cap hit), the accumulator is clamped just
    ///     below the interval so it retries shortly rather than waiting a whole interval again.
    ///   * <see cref="_countdownActive"/> prevents overlapping breaks.
    ///
    /// The 3s countdown UI is an OPTIONAL injected callback: when the HUD provides one, it is awaited before
    /// the ad; otherwise a plain delay is used so the timing still matches the doc without requiring the
    /// overlay to be wired.
    /// </summary>
    public sealed class CommercialBreakController : MonoBehaviour
    {
        private static readonly InfoLogger Logger = new InfoLogger("CommercialBreakController", "yellow");

        private const float CountdownSeconds = 3f;

        private IAdService _adService;
        private IAdGatingService _gating;

        private float _activeSeconds;
        private bool _isActive;
        private bool _countdownActive;

        // Optional 3s-countdown presenter supplied by the HUD. Receives the whole-second value (3,2,1) to
        // display; returns a task that completes when the countdown finishes. Null => plain delay, no UI.
        private Func<int, UniTask> _countdownPresenter;

        public void Configure(Func<int, UniTask> countdownPresenter)
        {
            _countdownPresenter = countdownPresenter;
            ServiceLocator.TryGetService(out _adService);
            ServiceLocator.TryGetService(out _gating);
        }

        /// <summary>Enable/disable active-gameplay time accumulation (HUD shown & not paused => true).</summary>
        public void SetActive(bool active)
        {
            _isActive = active;
        }

        private void Update()
        {
            if (!_isActive || _countdownActive || _gating == null)
                return;

            if (!_gating.CommercialBreakEnabled)
                return;

            _activeSeconds += Time.unscaledDeltaTime;

            int interval = _gating.CommercialBreakIntervalSec;
            if (interval <= 0)
                return;

            if (_activeSeconds < interval)
                return;

            if (!_gating.CanShowInterstitial())
            {
                // Gate refused (cooldown/cap). Clamp so we retry shortly instead of waiting another interval.
                _activeSeconds = interval - 1f;
                return;
            }

            RunBreakAsync(interval).Forget();
        }

        private async UniTaskVoid RunBreakAsync(int interval)
        {
            _countdownActive = true;
            try
            {
                await RunCountdownAsync();

                // Re-check the gate after the countdown: cooldown/caps may have changed during the 3s.
                if (_adService != null && _gating != null && _gating.CanShowInterstitial())
                {
                    bool shown = await _adService.ShowInterstitialAdAsync("commercial_break");
                    Logger.Log($"Commercial break shown={shown}.");
                }
                else
                {
                    Logger.Log("Commercial break gate closed after countdown; skipped.");
                }

                // Reset to a full interval regardless of shown/skipped, so breaks stay spaced by the interval.
                _activeSeconds = 0f;
            }
            finally
            {
                _countdownActive = false;
            }
        }

        private async UniTask RunCountdownAsync()
        {
            if (_countdownPresenter == null)
            {
                await UniTask.Delay(TimeSpan.FromSeconds(CountdownSeconds), ignoreTimeScale: true);
                return;
            }

            // Present whole-second ticks 3 -> 2 -> 1, each held ~1s.
            for (int remaining = (int)CountdownSeconds; remaining >= 1; remaining--)
            {
                await _countdownPresenter(remaining);
                await UniTask.Delay(TimeSpan.FromSeconds(1f), ignoreTimeScale: true);
            }
        }
    }
}
