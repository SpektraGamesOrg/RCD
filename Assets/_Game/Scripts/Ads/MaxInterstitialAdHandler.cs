using System;
using System.Collections.Generic;
using _Game.Scripts.Utils.VContainer;
using Analytics;
using AppsFlyerSDK;
using Cysharp.Threading.Tasks;
using SpektraGames.SpektraUtilities.Runtime;
using UnityEngine;

namespace Ads
{
    public class MaxInterstitialAdHandler : AdHandler
    {
        public MaxInterstitialAdHandler(string adUnitId, InfoLogger logger) : base(adUnitId, logger)
        {
        }

        public override bool IsAdReady => MaxSdk.IsInterstitialReady(AdUnitId);

        protected override void OnInitialize()
        {
            _logger.Log("Initialize interstitial ads called");

            MaxSdkCallbacks.Interstitial.OnAdHiddenEvent += (adUnitId, adInfo) => OnAdHidden(adUnitId, new AdInfo(adInfo)
            {
                Placement = adInfo.Placement,
                AdUnitIdentifier = AdUnitId,
                AdType = AdType.Interstitial,
            });
            MaxSdkCallbacks.Interstitial.OnAdRevenuePaidEvent +=
                (adUnitId, adInfo) => OnAdRevenuePaid(adUnitId, new AdInfo(adInfo)
                {
                    Placement = adInfo.Placement,
                    AdUnitIdentifier = AdUnitId,
                });
            MaxSdkCallbacks.Interstitial.OnAdLoadedEvent += (adUnitId, adInfo) => OnAdLoaded(adUnitId, new AdInfo(adInfo)
            {
                Placement = adInfo.Placement,
                AdUnitIdentifier = AdUnitId,
            });
            MaxSdkCallbacks.Interstitial.OnAdDisplayFailedEvent += (adUnitId, errorInfo, adInfo) =>
                OnAdDisplayFailed(adUnitId, new ErrorInfo(errorInfo, errorInfo.Message), new AdInfo(adInfo)
                {
                    Placement = adInfo.Placement,
                    AdUnitIdentifier = AdUnitId,
                });
            MaxSdkCallbacks.Interstitial.OnAdLoadFailedEvent +=
                (adUnitId, errorInfo) => OnAdLoadFailed(adUnitId, new ErrorInfo(errorInfo, errorInfo.Message));
            MaxSdkCallbacks.Interstitial.OnAdDisplayedEvent += (adUnitId, adInfo) =>
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

        protected override void LoadAd()
        {
            if (IsAdReady)
            {
                _logger.Log("Interstitial ad already loaded");
                return;
            }

            if (IsLoading)
            {
                _logger.Log("Interstitial ad is already loading");
                return;
            }

            IsLoading = true;
            MaxSdk.LoadInterstitial(AdUnitId);
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
                MaxSdk.ShowInterstitial(AdUnitId, placement);
            }
            else
            {
                _logger.Log("Interstitial ad is not ready");
                CompleteAd(false);
            }

            return _adCompletionSource.Task;
        }

        protected override void OnAdHidden(string adUnitId, AdInfo adInfo, bool success = true)
        {
            base.OnAdHidden(adUnitId, adInfo);

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
                    { "ad_format", AnalyticsEventParameter.StringParam("interstitial") },
                    { "ad_platform", AnalyticsEventParameter.StringParam("") },
                    { "ad_source", AnalyticsEventParameter.StringParam("") },
                    { "ad_unit_name", AnalyticsEventParameter.StringParam(adUnitId) }
                });
        }

        protected override void OnAdRevenuePaid(string adUnitId, AdInfo adInfo)
        {
            base.OnAdRevenuePaid(adUnitId, adInfo);
            MaxSdkBase.AdInfo maxAdInfo = (MaxSdkBase.AdInfo)adInfo.AdDataObject;

            _logger.Log("Max Interstitial ad OnAdRevenuePaid: " + maxAdInfo.Revenue);

            _analyticsService?.ReportEvent("adRevenuePaid",
                new Dictionary<string, AnalyticsEventParameter>()
                {
                    { "status", AnalyticsEventParameter.StringParam("adRevenuePaid") },
                    { "ad_format", AnalyticsEventParameter.StringParam(maxAdInfo.AdFormat) },
                    { "ad_platform", AnalyticsEventParameter.StringParam(maxAdInfo.DspName) },
                    { "ad_source", AnalyticsEventParameter.StringParam(maxAdInfo.NetworkName) },
                    { "ad_unit_name", AnalyticsEventParameter.StringParam(adUnitId) }
                });

            try
            {
                Dictionary<string, string> additionalParams = new Dictionary<string, string>();
                additionalParams.Add(AdRevenueScheme.COUNTRY, "XX");
                additionalParams.Add(AdRevenueScheme.AD_UNIT, maxAdInfo.AdUnitIdentifier);
                additionalParams.Add("ad_type_custom", "interstitial");
                additionalParams.Add(AdRevenueScheme.AD_TYPE, maxAdInfo.AdFormat);
                additionalParams.Add(AdRevenueScheme.PLACEMENT, maxAdInfo.Placement);
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

            _logger.Log("Ad revenue paid");
        }

        protected override void OnAdDisplayFailed(string adUnitId, ErrorInfo errorInfo, AdInfo adInfo)
        {
            base.OnAdDisplayFailed(adUnitId, errorInfo, adInfo);
            var maxErrorInfo = errorInfo.GetErrorData<MaxSdkBase.ErrorInfo>();
            _logger.Log($"Interstitial ad display error: \n{maxErrorInfo.ToString()}");
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
    }
}