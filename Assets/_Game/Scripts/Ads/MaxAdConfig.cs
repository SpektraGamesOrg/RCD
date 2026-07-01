namespace Ads
{
    public static class MaxAdConfig
    {
        public static readonly string RewardedAdUnitId =
#if UNITY_IOS
        "62aad0c5db3cdb4c";
#elif UNITY_ANDROID
            "62aad0c5db3cdb4c";
#else
        ".";
#endif

        public static readonly string InterstitialAdUnitId =
#if UNITY_IOS
        "51b6591135d53997";
#elif UNITY_ANDROID
            "51b6591135d53997";
#else
        ".";
#endif

        // TODO: replace placeholder banner/MREC ad unit ids with real MAX ids before release.
        public static readonly string BannerAdUnitId =
#if UNITY_IOS
        "5dab29c54fc4f6c1";
#elif UNITY_ANDROID
            "5dab29c54fc4f6c1";
#else
        ".";
#endif

        public static readonly string MrecAdUnitId =
#if UNITY_IOS
        "3fa47dcf4267e7dc";
#elif UNITY_ANDROID
            "3fa47dcf4267e7dc";
#else
        ".";
#endif

        // MAX selective init ONLY serves ad units passed to InitializeSdk (see CTR-6257). This unit is added
        // to MaxAdService.InitializeSdk when AppOpenConfigured is true; keep the two in sync if this changes.
        public static readonly string AppOpenAdUnitId =
#if UNITY_IOS
        "8de80b642d3d9966";
#elif UNITY_ANDROID
            "8de80b642d3d9966";
#else
        ".";
#endif

        /// <summary>
        /// True only when a real App Open ad unit id has been configured. Every App Open path (SDK init
        /// registration, lifecycle show) must be guarded by this so a blank id can never reach the SDK.
        /// </summary>
        public static bool AppOpenConfigured => !string.IsNullOrEmpty(AppOpenAdUnitId);
    }
}