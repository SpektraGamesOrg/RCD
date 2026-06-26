namespace Ads
{
    /// <summary>
    /// Static access point for the active rewarded-ad service. Defaults to
    /// <see cref="DummyRewardedAdService"/> so reward flows work out of the box during development.
    /// When a real ad SDK is integrated, assign it once at startup:
    ///
    ///     RewardedAds.Service = new MyRealRewardedAdService();
    ///
    /// Game code calls <c>RewardedAds.Service.Show(onRewarded, onFailed)</c> without caring which
    /// implementation is in use.
    /// </summary>
    public static class RewardedAds
    {
        private static IRewardedAdService _service = new DummyRewardedAdService();

        public static IRewardedAdService Service
        {
            get => _service;
            set => _service = value ?? new DummyRewardedAdService();
        }
    }
}
