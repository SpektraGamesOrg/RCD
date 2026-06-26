using System;
using UnityEngine;

namespace Ads
{
    /// <summary>
    /// Development stand-in for a rewarded-ad provider: "shows" an ad by immediately succeeding.
    /// Lets reward flows be fully wired and testable before a real ad SDK is integrated.
    /// Replace by assigning a real <see cref="IRewardedAdService"/> to <see cref="RewardedAds.Service"/>.
    /// </summary>
    public sealed class DummyRewardedAdService : IRewardedAdService
    {
        public void Show(Action onRewarded, Action onFailed = null)
        {
            Debug.Log("[DummyRewardedAdService] No ad SDK integrated - granting reward immediately.");
            onRewarded?.Invoke();
        }
    }
}
