using _Game.Scripts.Utils.VContainer;
using Core;
using Cysharp.Threading.Tasks;
using SpektraGames.SpektraUtilities.Runtime;
using UnityEngine;

namespace Ads
{
    /// <summary>
    /// Drives App Open ads on cold start and warm resume, per the RCD "Ad Placements" doc:
    ///   * Cold start: <see cref="TryShowColdStart"/> is called once by GameInitializer after SDK init,
    ///     before the first interactive frame.
    ///   * Warm resume: on returning to the foreground after being backgrounded for at least
    ///     <c>ad_app_open_min_bg_sec</c>, an App Open is shown.
    ///
    /// Both paths are subject to the SHARED fullscreen cooldown (via <see cref="IAdService.ShowAppOpenAdAsync"/>
    /// -> <see cref="IAdGatingService.CanShowAppOpen"/>) and never fire mid-loading
    /// (<see cref="CustomSceneManager.IsLoading"/>).
    ///
    /// Lifecycle-event dedup: Android raises OnApplicationPause AND OnApplicationFocus for the same
    /// background/foreground transition. Both funnel through <see cref="SetBackgrounded"/>, which is a single
    /// edge gate on <see cref="_isBackgrounded"/> so the transition is handled exactly once (mirrors
    /// SessionTimer's focus handling).
    ///
    /// Entirely inert until a real App Open ad unit id is configured (see CTR-6257 in MaxAdConfig): the
    /// service call returns false and this controller shows nothing.
    /// </summary>
    public sealed class AppOpenAdController : SingletonComponent<AppOpenAdController>
    {
        private static readonly InfoLogger Logger = new InfoLogger("AppOpenAdController", "yellow");

        private IAdService _adService;
        private IAdConfigProvider _configProvider;

        private bool _isBackgrounded;
        private float _backgroundedAtRealtime;
        private bool _showInFlight;

        protected override void Awake()
        {
            base.Awake();
            isDontDestroyOnLoad = true;
            DontDestroyOnLoad(gameObject);

            // Resolve dependencies once here — never per lifecycle event.
            ServiceLocator.TryGetService(out _adService);
            ServiceLocator.TryGetService(out _configProvider);
        }

        /// <summary>
        /// Cold-start App Open. Called by GameInitializer after the ad SDK reports initialized. No-ops when
        /// App Open is unconfigured, a scene load is in progress, or a show is already running.
        /// </summary>
        public void TryShowColdStart()
        {
            TryShow("app_open_cold");
        }

        private void OnApplicationPause(bool paused)
        {
            SetBackgrounded(paused);
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            SetBackgrounded(!hasFocus);
        }

        // Single edge gate: only acts on an actual background<->foreground transition, so the paired
        // pause+focus callbacks for one transition are collapsed into one handling.
        private void SetBackgrounded(bool backgrounded)
        {
            if (backgrounded == _isBackgrounded)
                return;

            _isBackgrounded = backgrounded;

            if (backgrounded)
            {
                _backgroundedAtRealtime = Time.realtimeSinceStartup;
                return;
            }

            // Just returned to the foreground: honor the min-background-time before a warm App Open.
            float backgroundSeconds = Time.realtimeSinceStartup - _backgroundedAtRealtime;
            int minBackground = _configProvider != null ? _configProvider.Current.AppOpenMinBackgroundSec : 60;
            if (backgroundSeconds < minBackground)
            {
                Logger.Log($"Warm resume after {backgroundSeconds:F1}s (< {minBackground}s); no App Open.");
                return;
            }

            TryShow("app_open_warm");
        }

        private void TryShow(string placement)
        {
            if (!MaxAdConfig.AppOpenConfigured)
                return;

            if (_showInFlight)
                return;

            // Never fire mid-loading (doc: App Open must never fire mid-loading).
            if (CustomSceneManager.Exists() && CustomSceneManager.Instance && CustomSceneManager.Instance.IsLoading)
            {
                Logger.Log($"Skipping App Open ({placement}); a scene load is in progress.");
                return;
            }

            if (_adService == null)
            {
                Logger.LogError("No ad service resolved; cannot show App Open.");
                return;
            }

            ShowAsync(placement).Forget();
        }

        private async UniTaskVoid ShowAsync(string placement)
        {
            _showInFlight = true;
            try
            {
                bool shown = await _adService.ShowAppOpenAdAsync(placement);
                Logger.Log($"App Open ({placement}) shown={shown}.");
            }
            finally
            {
                _showInFlight = false;
            }
        }
    }
}
