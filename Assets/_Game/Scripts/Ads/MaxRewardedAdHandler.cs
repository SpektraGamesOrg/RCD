using System;
using System.Collections.Generic;
using Analytics;
using AppsFlyerSDK;
using Cysharp.Threading.Tasks;
using SpektraGames.SpektraUtilities.Runtime;
using UnityEngine;

namespace Ads
{
    public class MaxRewardedAdHandler : AdHandler
    {
        public MaxRewardedAdHandler(string adUnitId, InfoLogger logger) : base(adUnitId, logger)
        {
        }

        public override bool IsAdReady => MaxSdk.IsRewardedAdReady(AdUnitId);

        // Rewarded raises OnAdShown from OnRewardedAdReceivedReward (reward earned), so the base
        // OnAdHidden must NOT also raise it — otherwise the event (and cooldown) fires twice.
        protected override bool RaiseShownOnHidden => false;

        protected override void OnInitialize()
        {
            _logger.Log("Initialize rewarded ads called");

            MaxSdkCallbacks.Rewarded.OnAdHiddenEvent += (adUnitId, adInfo) => OnAdHidden(adUnitId, new AdInfo(adInfo)
            {
                Placement = adInfo.Placement,
                AdUnitIdentifier = AdUnitId,
                AdType = AdType.Rewarded,
            });
            MaxSdkCallbacks.Rewarded.OnAdReceivedRewardEvent += OnRewardedAdReceivedReward;
            MaxSdkCallbacks.Rewarded.OnAdRevenuePaidEvent +=
                (adUnitId, adInfo) => OnAdRevenuePaid(adUnitId, new AdInfo(adInfo)
                {
                    Placement = adInfo.Placement,
                    AdUnitIdentifier = AdUnitId,
                });
            MaxSdkCallbacks.Rewarded.OnAdLoadedEvent += (adUnitId, adInfo) => OnAdLoaded(adUnitId, new AdInfo(adInfo)
            {
                Placement = adInfo.Placement,
                AdUnitIdentifier = AdUnitId,
            });
            MaxSdkCallbacks.Rewarded.OnAdDisplayFailedEvent += (adUnitId, errorInfo, adInfo) =>
                OnAdDisplayFailed(adUnitId, new ErrorInfo(errorInfo, errorInfo.Message), new AdInfo(adInfo)
                {
                    Placement = adInfo.Placement,
                    AdUnitIdentifier = AdUnitId,
                });
            MaxSdkCallbacks.Rewarded.OnAdLoadFailedEvent +=
                (adUnitId, errorInfo) => OnAdLoadFailed(adUnitId, new ErrorInfo(errorInfo, errorInfo.Message));
            MaxSdkCallbacks.Rewarded.OnAdDisplayedEvent += (adUnitId, adInfo) =>
                _analyticsService?.ReportEvent("adDisplayed",
                    new Dictionary<string, AnalyticsEventParameter>()
                    {
                        { "status", AnalyticsEventParameter.StringParam("adDisplayed") },
                        { "ad_format", AnalyticsEventParameter.StringParam(adInfo.AdFormat) },
                        { "ad_platform", AnalyticsEventParameter.StringParam(adInfo.DspName) },
                        { "ad_source", AnalyticsEventParameter.StringParam(adInfo.NetworkName) },
                        { "ad_unit_name", AnalyticsEventParameter.StringParam(adUnitId) }
                    });

            LoadAd();
        }

        private void OnRewardedAdReceivedReward(string adUnitId, MaxSdkBase.Reward reward, MaxSdkBase.AdInfo adInfo)
        {
            OnAdShownEvent.InvokeSafe(new AdInfo(adInfo)
            {
                Placement = adInfo.Placement,
                AdUnitIdentifier = AdUnitId,
                AdType = AdType.Rewarded,
            });
            CompleteAd(true);
            _logger.Log("OnRewardedAdReceivedReward");
            MaxSdkBase.AdInfo maxAdInfo = adInfo;

            _analyticsService?.ReportEvent("adRewarded",
                new Dictionary<string, AnalyticsEventParameter>()
                {
                    { "status", AnalyticsEventParameter.StringParam("adRewarded") },
                    { "ad_format", AnalyticsEventParameter.StringParam(maxAdInfo.AdFormat) },
                    { "ad_platform", AnalyticsEventParameter.StringParam(maxAdInfo.DspName) },
                    { "ad_source", AnalyticsEventParameter.StringParam(maxAdInfo.NetworkName) },
                    { "ad_unit_name", AnalyticsEventParameter.StringParam(adUnitId) }
                });

            try
            {
                Dictionary<string, string> additionalParams = new Dictionary<string, string>();
                additionalParams.Add(AdRevenueScheme.COUNTRY, "XX");
                additionalParams.Add(AdRevenueScheme.AD_UNIT, adInfo.AdUnitIdentifier);
                additionalParams.Add("ad_type_custom", "rewarded");
                additionalParams.Add(AdRevenueScheme.AD_TYPE, adInfo.AdFormat);
                additionalParams.Add(AdRevenueScheme.PLACEMENT, adInfo.Placement);
                AppsFlyer.logAdRevenue(new AFAdRevenueData(
                    string.IsNullOrEmpty(maxAdInfo.NetworkName) ? "NAN" : maxAdInfo.NetworkName,
                    MediationNetwork.ApplovinMax,
                    "USD",
                    maxAdInfo.Revenue), additionalParams);
            }
            catch (Exception e)
            {
                _logger.LogError(e.StackTrace);
            }
        }

        protected override void LoadAd()
        {
            // if (IsAdReady || IsLoading)
            //     return;
            //
            // IsLoading = true;
            // MaxSdk.LoadRewardedAd(AdUnitId);

            // Log("LoadRewardedAd");

            if (IsAdReady)
            {
                _logger.Log("Rewarded ad already loaded");
                return;
            }

            if (IsLoading)
            {
                _logger.Log("Rewarded ad is already loading");
                return;
            }

            IsLoading = true;

            MaxSdk.LoadRewardedAd(AdUnitId);
        }

        protected override UniTask<bool> OnShowAdAsync(string placement)
        {
#if DEV_GAME_ENVIRONMENT || PROD_STAGING_GAME_ENVIRONMENT
            if (PlayerPrefs.GetInt("ForceDisableAds", 0) == 1)
            {
                CompleteAd(true);
            }
            else
#endif
            if (IsAdReady)
            {
                MaxSdk.ShowRewardedAd(AdUnitId, placement);
            }
            else
            {
                CompleteAd(false);
            }
            return _adCompletionSource.Task;
        }

        protected override void OnAdHidden(string adUnitId, AdInfo adInfo, bool success = true)
        {
            base.OnAdHidden(adUnitId, adInfo, false);
            var maxAdInfo = (MaxSdkBase.AdInfo)adInfo.AdDataObject;
            _analyticsService?.ReportEvent("adHidden",
                new Dictionary<string, AnalyticsEventParameter>()
                {
                    { "status", AnalyticsEventParameter.StringParam("adHidden") },
                    { "ad_format", AnalyticsEventParameter.StringParam(maxAdInfo.AdFormat) },
                    { "ad_platform", AnalyticsEventParameter.StringParam(maxAdInfo.DspName) },
                    { "ad_source", AnalyticsEventParameter.StringParam(maxAdInfo.NetworkName) },
                    { "ad_unit_name", AnalyticsEventParameter.StringParam(adUnitId) }
                });
        }

        protected override void OnAdDisplayFailed(string adUnitId, ErrorInfo errorInfo, AdInfo adInfo)
        {
            base.OnAdDisplayFailed(adUnitId, errorInfo, adInfo);
            var maxErrorInfo = errorInfo.GetErrorData<MaxSdk.ErrorInfo>();
            _logger.Log($"Rewarded ad display error: \n{maxErrorInfo.ToString()}");
            var maxAdInfo = (MaxSdkBase.AdInfo)adInfo.AdDataObject;
            _analyticsService?.ReportEvent("adDisplayFailed",
                new Dictionary<string, AnalyticsEventParameter>()
                {
                    { "status", AnalyticsEventParameter.StringParam("adDisplayFailed") },
                    { "ad_format", AnalyticsEventParameter.StringParam(maxAdInfo.AdFormat) },
                    { "ad_platform", AnalyticsEventParameter.StringParam(maxAdInfo.DspName) },
                    { "ad_source", AnalyticsEventParameter.StringParam(maxAdInfo.NetworkName) },
                    { "ad_unit_name", AnalyticsEventParameter.StringParam(adUnitId) }
                });
        }

        protected override void OnAdLoaded(string adUnitId, AdInfo adInfo)
        {
            base.OnAdLoaded(adUnitId, adInfo);
            var maxAdInfo = (MaxSdkBase.AdInfo)adInfo.AdDataObject;
            _analyticsService?.ReportEvent("adLoaded",
                new Dictionary<string, AnalyticsEventParameter>()
                {
                    { "status", AnalyticsEventParameter.StringParam("adLoaded") },
                    { "ad_format", AnalyticsEventParameter.StringParam(maxAdInfo.AdFormat) },
                    { "ad_platform", AnalyticsEventParameter.StringParam(maxAdInfo.DspName) },
                    { "ad_source", AnalyticsEventParameter.StringParam(maxAdInfo.NetworkName) },
                    { "ad_unit_name", AnalyticsEventParameter.StringParam(adUnitId) }
                });
        }

        protected override void OnAdLoadFailed(string adUnitId, ErrorInfo errorInfo)
        {
            base.OnAdLoadFailed(adUnitId, errorInfo);
            _analyticsService?.ReportEvent("adLoadFailed",
                new Dictionary<string, AnalyticsEventParameter>()
                {
                    { "status", AnalyticsEventParameter.StringParam("adLoadFailed") },
                    { "ad_format", AnalyticsEventParameter.StringParam("rewarded") },
                    { "ad_platform", AnalyticsEventParameter.StringParam("") },
                    { "ad_source", AnalyticsEventParameter.StringParam("") },
                    { "ad_unit_name", AnalyticsEventParameter.StringParam(adUnitId) }
                });
        }

        protected override void OnAdRevenuePaid(string adUnitId, AdInfo adInfo)
        {
            base.OnAdRevenuePaid(adUnitId, adInfo);
            var maxAdInfo = (MaxSdkBase.AdInfo)adInfo.AdDataObject;

            _logger.Log("Max Rewarded ad OnAdRevenuePaid: " + maxAdInfo.Revenue);

            _analyticsService?.ReportEvent("adRevenuePaid",
                new Dictionary<string, AnalyticsEventParameter>()
                {
                    { "status", AnalyticsEventParameter.StringParam("adRevenuePaid") },
                    { "ad_format", AnalyticsEventParameter.StringParam(maxAdInfo.AdFormat) },
                    { "ad_platform", AnalyticsEventParameter.StringParam(maxAdInfo.DspName) },
                    { "ad_source", AnalyticsEventParameter.StringParam(maxAdInfo.NetworkName) },
                    { "ad_unit_name", AnalyticsEventParameter.StringParam(adUnitId) }
                });
        }
    }
}