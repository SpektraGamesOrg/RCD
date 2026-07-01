using System;
using _Game.Scripts.Utils.VContainer;
using Cysharp.Threading.Tasks;

namespace Ads
{
    public interface IAdService : IService
    {
        event Action<AdInfo> OnAdRevenuePaid;
        event Action<AdInfo> OnAdShown;

        bool IsInitialized { get; }
        bool IsInitializing { get; }
        void Initialize(bool hasTrackingConsent);
        UniTask<bool> ShowRewardedAdAsync(string placement, string parameter = "");
        UniTask<bool> ShowInterstitialAdAsync(string placement = "default", bool forceShow = false);

        /// <summary>
        /// Shows an App Open ad if one is configured and the shared fullscreen cooldown allows it. App Open
        /// shares only the cooldown with interstitials; it does NOT consume the interstitial session/daily
        /// caps. No-ops (returns false) when no real App Open ad unit id is configured (see CTR-6257).
        /// </summary>
        UniTask<bool> ShowAppOpenAdAsync(string placement = "app_open", bool forceShow = false);

        /// <summary>
        /// Shows the banner for the given placement. Auto-creates + loads + shows on first call,
        /// re-shows if already created. No-op if the placement's AdConfig toggle is off/missing or
        /// the SDK is not initialized. Banners are persistent (not show-once); callers do not await.
        /// </summary>
        void ShowBanner(BannerPlacement placement);

        /// <summary>Hides the banner for the placement (keeps it loaded for cheap re-show). No-op if none.</summary>
        void HideBanner(BannerPlacement placement);

        /// <summary>Destroys the banner for the placement and frees its SDK view. No-op if none.</summary>
        void DestroyBanner(BannerPlacement placement);

        void SetUserId(string userId);
    }
}