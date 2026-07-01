using System;
using Cysharp.Threading.Tasks;
using SpektraGames.SpektraUtilities.Runtime;
using UnityEngine;

namespace Ads
{
    public class MaxAdService : IAdService, IDisposable
    {
        private readonly AdHandler _rewardedAdHandler;
        private readonly AdHandler _interstitialAdHandler;
        private readonly AdHandler _appOpenAdHandler;
        private readonly MaxBannerController _bannerController;
        private readonly IAdGatingService _gatingService;

        public bool IsInitialized { get; private set; }
        public bool IsInitializing { get; private set; }

        public event Action<AdInfo> OnAdRevenuePaid;
        public event Action<AdInfo> OnAdShown;

        private readonly InfoLogger _logger;

        public MaxAdService(IAdConfigProvider configProvider, IAdGatingService gatingService)
        {
            _gatingService = gatingService;
            _bannerController = new MaxBannerController(configProvider);
            _logger = new InfoLogger("MaxAdService", "yellow");
            _rewardedAdHandler = new MaxRewardedAdHandler(MaxAdConfig.RewardedAdUnitId, _logger);
            _interstitialAdHandler = new MaxInterstitialAdHandler(MaxAdConfig.InterstitialAdUnitId, _logger);
            _appOpenAdHandler = new MaxAppOpenAdHandler(MaxAdConfig.AppOpenAdUnitId, _logger);
            _rewardedAdHandler.OnAdRevenuePaidEvent += adInfo => OnAdRevenuePaid?.Invoke(adInfo);
            _rewardedAdHandler.OnAdShownEvent += adInfo => OnAdShown?.Invoke(adInfo);
            _interstitialAdHandler.OnAdShownEvent += adInfo => OnAdShown?.Invoke(adInfo);
            _appOpenAdHandler.OnAdShownEvent += adInfo => OnAdShown?.Invoke(adInfo);
            _appOpenAdHandler.OnAdRevenuePaidEvent += adInfo => OnAdRevenuePaid?.Invoke(adInfo);
        }

        public void SetUserId(string userId)
        {
            if (Application.isEditor)
                return;

            if (string.IsNullOrEmpty(userId))
                return;

            try
            {
                MaxSdk.SetUserId(userId);
            }
            catch (Exception e)
            {
                Debug.LogError("SetUserID.Max: " + e.ToString());
            }
        }

        public void Initialize(bool hasTrackingConsent)
        {
            if (IsInitialized)
                return;
            _logger.Log($"Initialize ad service (hasTrackingConsent={hasTrackingConsent})");
            IsInitializing = true;
            MaxSdk.SetVerboseLogging(_logger.EnableLog);

            MaxSdkCallbacks.OnSdkInitializedEvent += OnSdkInitialized;
            // Selective init: MAX only serves ad units passed here. Banner/MREC units MUST be listed or
            // loading them throws "Ad Unit ID ... has not been initialized" and crashes (see CTR-6257).
            // The App Open unit is included ONLY when a real id is configured, so a blank id can never be
            // registered (and the App Open handler stays idle) — same CTR-6257 guard.
            var adUnitIds = new System.Collections.Generic.List<string>
            {
                MaxAdConfig.RewardedAdUnitId,
                MaxAdConfig.InterstitialAdUnitId,
                MaxAdConfig.BannerAdUnitId,
                MaxAdConfig.MrecAdUnitId,
            };
            if (MaxAdConfig.AppOpenConfigured)
                adUnitIds.Add(MaxAdConfig.AppOpenAdUnitId);

            MaxSdk.InitializeSdk(adUnitIds.ToArray());
            MaxSdk.SetExtraParameter("pisw", "true");
        }

        private void OnSdkInitialized(MaxSdkBase.SdkConfiguration sdkConfiguration)
        {
            IsInitialized = sdkConfiguration.IsSuccessfullyInitialized;

            if (IsInitialized)
            {
                _logger.Log("Max sdk initialized");
                _rewardedAdHandler.Initialize();
                _interstitialAdHandler.Initialize();
                // No-ops internally when App Open is not configured (CTR-6257 guard).
                _appOpenAdHandler.Initialize();
            }
            else
            {
                _logger.LogError("Max sdk initialization failed");
            }

            IsInitializing = false;
        }

        public UniTask<bool> ShowRewardedAdAsync(string placement = "default", string parameter = "")
        {
            _rewardedAdHandler.SetRewardParameter(parameter);
            return _rewardedAdHandler.ShowAdAsync(placement);
        }

        public async UniTask<bool> ShowInterstitialAdAsync(string placement = "default", bool forceShow = false)
        {
            // AdGatingService owns fullscreen policy (shared cooldown + session/daily caps). forceShow
            // bypasses the gate for debug/QA (see SROptions) and for callers that have already gated.
            if (!forceShow && _gatingService != null && !_gatingService.CanShowInterstitial())
            {
                _logger.Log($"Interstitial gated off (cooldown/caps) placement: {placement}");
                return false;
            }

            _logger.Log($"ShowInterstitialAdAsync placement: {placement} (forceShow={forceShow})");
            bool shown = await _interstitialAdHandler.ShowAdAsync(placement);

            // Only a confirmed show consumes the cooldown + interstitial caps; a no-fill leaves budget intact.
            if (shown)
                _gatingService?.NoteInterstitialShown();

            return shown;
        }

        public async UniTask<bool> ShowAppOpenAdAsync(string placement = "app_open", bool forceShow = false)
        {
            if (!MaxAdConfig.AppOpenConfigured)
                return false;

            // App Open shares ONLY the fullscreen cooldown (not the interstitial caps). forceShow bypasses.
            if (!forceShow && _gatingService != null && !_gatingService.CanShowAppOpen())
            {
                _logger.Log($"App Open gated off (cooldown) placement: {placement}");
                return false;
            }

            _logger.Log($"ShowAppOpenAdAsync placement: {placement} (forceShow={forceShow})");
            bool shown = await _appOpenAdHandler.ShowAdAsync(placement);

            // Arm ONLY the shared cooldown on a confirmed show — App Open never consumes interstitial budget.
            if (shown)
                _gatingService?.ArmSharedCooldown();

            return shown;
        }

        public void ShowBanner(BannerPlacement placement) => _bannerController.ShowBanner(placement);

        public void HideBanner(BannerPlacement placement) => _bannerController.HideBanner(placement);

        public void DestroyBanner(BannerPlacement placement) => _bannerController.DestroyBanner(placement);

        public void Dispose()
        {
            _bannerController.DestroyAll();
            MaxSdkCallbacks.OnSdkInitializedEvent -= OnSdkInitialized;
            _rewardedAdHandler.OnAdRevenuePaidEvent -= adInfo => OnAdRevenuePaid?.Invoke(adInfo);

            // _interstitialAdHandler.Dispose();
            // _rewardedAdHandler.Dispose();
        }
    }
}