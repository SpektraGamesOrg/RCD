using System;
using _Game.Scripts.Utils.VContainer;
using Analytics;
using Cysharp.Threading.Tasks;
using SpektraGames.RuntimeUI.Runtime;
using SpektraGames.SpektraUtilities.Runtime;

namespace Ads
{
    public abstract class AdHandler
    {
        protected readonly string AdUnitId;
        protected int RetryAttempt;
        protected bool IsLoading;
        protected InfoLogger _logger;
        protected string _handlerName;

        private readonly int _maxRetryAttempts = 6;
        protected virtual float RetryDelay => (float)Math.Pow(2, Math.Min(_maxRetryAttempts, RetryAttempt));

        public Action<AdInfo> OnAdShownEvent;
        public Action<AdInfo> OnAdRevenuePaidEvent;

        protected UniTaskCompletionSource<bool> _adCompletionSource;
        protected string _additionalAdParameter;

        protected IAnalyticsService _analyticsService;

        public abstract bool IsAdReady { get; }

        protected AdHandler(string adUnitId, InfoLogger logger)
        {
            AdUnitId = adUnitId;
            _logger = logger;
        }

        public void Initialize()
        {
            _handlerName = GetType().Name;
            if (!ServiceLocator.TryGetService<IAnalyticsService>(out _analyticsService))
            {
                _logger.LogError("Analytics service not found");
            }
            OnInitialize();
        }

        protected abstract void OnInitialize();
        protected abstract void LoadAd();

        protected void CompleteAd(bool success)
        {
            RuntimeUI.UnblockScreen();
            _adCompletionSource?.TrySetResult(success);
        }

        protected virtual async UniTaskVoid ReloadAd()
        {
            IsLoading = false;
            RetryAttempt++;
            _logger.Log($"[{_handlerName}] ReloadAd. Retry attempt: {RetryAttempt}. Retry delay: {RetryDelay}");
            await UniTask.WaitForSeconds(RetryDelay);
            LoadAd();
        }

        public UniTask<bool> ShowAdAsync(string placement)
        {
            _logger.Log($"[{_handlerName}] ShowAdAsync");
            _adCompletionSource = new UniTaskCompletionSource<bool>();
#if !UNITY_EDITOR
            RuntimeUI.BlockScreen();
#endif
            return OnShowAdAsync(placement);
        }

        protected abstract UniTask<bool> OnShowAdAsync(string placement);

        /// <summary>
        /// Whether <see cref="OnAdHidden"/> should raise <see cref="OnAdShownEvent"/>. True for the
        /// default (interstitial) path, where ad-hidden is the single "shown" signal. Rewarded
        /// handlers override to false: they raise the event from their reward-granted callback
        /// instead, so the event fires once and only when the reward is actually earned.
        /// </summary>
        protected virtual bool RaiseShownOnHidden => true;

        protected virtual void OnAdHidden(string adUnitId, AdInfo adInfo, bool success = true)
        {
            if (RaiseShownOnHidden)
                OnAdShownEvent.InvokeSafe(adInfo);
            LoadAd();
            CompleteAd(success);
            _logger.Log($"[{_handlerName}] OnAdHidden");
        }

        protected virtual void OnAdRevenuePaid(string adUnitId, AdInfo adInfo)
        {
            _logger.Log($"[{_handlerName}] OnAdRevenuePaid");
            OnAdRevenuePaidEvent.InvokeSafe(adInfo);
        }

        protected virtual void OnAdLoaded(string adUnitId, AdInfo adInfo)
        {
            _logger.Log($"[{_handlerName}] OnAdLoaded");
            IsLoading = false;
            RetryAttempt = 0;
        }

        protected virtual void OnAdLoadFailed(string adUnitId, ErrorInfo errorInfo)
        {
            string extraInfo = "no_extra_info";
            if (errorInfo != null)
            {
                extraInfo = errorInfo.ToString();
            }

            _logger.LogError($"[{_handlerName}] OnAdLoadFailed: " + extraInfo);
            ReloadAd().Forget();
        }

        protected virtual void OnAdDisplayFailed(string adUnitId, ErrorInfo errorInfo, AdInfo adInfo)
        {
            _logger.Log($"[{_handlerName}] OnAdDisplayFailed");
            ReloadAd().Forget();
            CompleteAd(false);
        }

        public virtual void SetRewardParameter(string rewardParameter)
        {
            _additionalAdParameter = rewardParameter;
        }
    }
}