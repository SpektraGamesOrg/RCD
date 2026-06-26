using System;

namespace Ads
{
    /// <summary>
    /// Abstraction over a rewarded-ad provider. Game code requests an ad and reacts to the outcome
    /// without knowing which SDK (if any) is behind it. Swap the implementation behind
    /// <see cref="RewardedAds"/> when a real ad SDK is integrated.
    /// </summary>
    public interface IRewardedAdService
    {
        /// <summary>
        /// Shows a rewarded ad. Invokes <paramref name="onRewarded"/> when the ad completes and the
        /// reward should be granted, or <paramref name="onFailed"/> if it could not be shown / was
        /// dismissed early.
        /// </summary>
        void Show(Action onRewarded, Action onFailed = null);
    }
}
