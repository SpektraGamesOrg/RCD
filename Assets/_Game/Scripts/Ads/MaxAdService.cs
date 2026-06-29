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
        private readonly MaxBannerController _bannerController = new MaxBannerController();

        public bool IsInitialized { get; private set; }
        public bool IsInitializing { get; private set; }

        public event Action<AdInfo> OnAdRevenuePaid;
        public event Action<AdInfo> OnAdShown;

        private readonly InfoLogger _logger;

        public MaxAdService()
        {
            _logger = new InfoLogger("MaxAdService", "yellow");
            _rewardedAdHandler = new MaxRewardedAdHandler(MaxAdConfig.RewardedAdUnitId, _logger);
            _interstitialAdHandler = new MaxInterstitialAdHandler(MaxAdConfig.InterstitialAdUnitId, _logger);
            _rewardedAdHandler.OnAdRevenuePaidEvent += adInfo => OnAdRevenuePaid?.Invoke(adInfo);
            _rewardedAdHandler.OnAdShownEvent += adInfo => OnAdShown?.Invoke(adInfo);
            _interstitialAdHandler.OnAdShownEvent += adInfo => OnAdShown?.Invoke(adInfo);
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
            MaxSdk.InitializeSdk(new[]
            {
                MaxAdConfig.RewardedAdUnitId,
                MaxAdConfig.InterstitialAdUnitId,
                MaxAdConfig.BannerAdUnitId,
                MaxAdConfig.MrecAdUnitId,
            });
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

        public UniTask<bool> ShowInterstitialAdAsync(string placement = "default", bool forceShow = false)
        {
            // Gating is owned by AdGatingService. This is pure transport: show and report success.
            _logger.Log($"ShowInterstitialAdAsync (transport) placement: {placement}");
            return _interstitialAdHandler.ShowAdAsync(placement);
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