using System;
using System.Collections.Generic;
using Analytics;
using AppsFlyerSDK;
using Cysharp.Threading.Tasks;
using SpektraGames.SpektraUtilities.Runtime;
using UnityEngine;

namespace Ads
{
    /// <summary>
    /// App Open ad handler. Mirrors <see cref="MaxInterstitialAdHandler"/> (load-on-init, retry with
    /// backoff, analytics + AppsFlyer ad revenue) but for the MAX App Open format. App Open shares the
    /// fullscreen cooldown with interstitials but does NOT consume the interstitial session/daily caps —
    /// the caller (see <see cref="AppOpenAdController"/>) arms the shared cooldown via
    /// <see cref="IAdGatingService.ArmSharedCooldown"/> on a confirmed show, never <c>NoteInterstitialShown</c>.
    ///
    /// Guarded end-to-end by <see cref="MaxAdConfig.AppOpenConfigured"/>: with no real ad unit id the
    /// handler no-ops and is never registered with the SDK (see CTR-6257 note in MaxAdConfig).
    /// </summary>
    public sealed class MaxAppOpenAdHandler : AdHandler
    {
        public MaxAppOpenAdHandler(string adUnitId, InfoLogger logger) : base(adUnitId, logger)
        {
        }

        public override bool IsAdReady => MaxAdConfig.AppOpenConfigured && MaxSdk.IsAppOpenAdReady(AdUnitId);

        protected override void OnInitialize()
        {
            if (!MaxAdConfig.AppOpenConfigured)
            {
                // No real ad unit yet: do not subscribe or load. AppOpenConfigured also keeps this unit out
                // of MaxAdService.InitializeSdk, so the SDK never sees an uninitialized App Open unit.
                _logger.Log("App Open not configured; handler idle (see CTR-6257).");
                return;
            }

            _logger.Log("Initialize app open ads called");

            MaxSdkCallbacks.AppOpen.OnAdHiddenEvent += (adUnitId, adInfo) => OnAdHidden(adUnitId, new AdInfo(adInfo)
            {
                Placement = adInfo.Placement,
                AdUnitIdentifier = AdUnitId,
                AdType = AdType.Interstitial,
            });
            MaxSdkCallbacks.AppOpen.OnAdRevenuePaidEvent +=
                (adUnitId, adInfo) => OnAdRevenuePaid(adUnitId, new AdInfo(adInfo)
                {
                    Placement = adInfo.Placement,
                    AdUnitIdentifier = AdUnitId,
                });
            MaxSdkCallbacks.AppOpen.OnAdLoadedEvent += (adUnitId, adInfo) => OnAdLoaded(adUnitId, new AdInfo(adInfo)
            {
                Placement = adInfo.Placement,
                AdUnitIdentifier = AdUnitId,
            });
            MaxSdkCallbacks.AppOpen.OnAdDisplayFailedEvent += (adUnitId, errorInfo, adInfo) =>
                OnAdDisplayFailed(adUnitId, new ErrorInfo(errorInfo, errorInfo.Message), new AdInfo(adInfo)
                {
                    Placement = adInfo.Placement,
                    AdUnitIdentifier = AdUnitId,
                });
            MaxSdkCallbacks.AppOpen.OnAdLoadFailedEvent +=
                (adUnitId, errorInfo) => OnAdLoadFailed(adUnitId, new ErrorInfo(errorInfo, errorInfo.Message));

            LoadAd();
        }

        protected override void LoadAd()
        {
            if (!MaxAdConfig.AppOpenConfigured)
                return;

            if (IsAdReady)
            {
                _logger.Log("App open ad already loaded");
                return;
            }

            if (IsLoading)
            {
                _logger.Log("App open ad is already loading");
                return;
            }

            IsLoading = true;
            MaxSdk.LoadAppOpenAd(AdUnitId);
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
                MaxSdk.ShowAppOpenAd(AdUnitId, placement);
            }
            else
            {
                _logger.Log("App open ad is not ready");
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
                    { "ad_format", AnalyticsEventParameter.StringParam("app_open") },
                    { "ad_platform", AnalyticsEventParameter.StringParam("") },
                    { "ad_source", AnalyticsEventParameter.StringParam("") },
                    { "ad_unit_name", AnalyticsEventParameter.StringParam(adUnitId) }
                });
        }

        protected override void OnAdRevenuePaid(string adUnitId, AdInfo adInfo)
        {
            base.OnAdRevenuePaid(adUnitId, adInfo);
            var maxAdInfo = (MaxSdkBase.AdInfo)adInfo.AdDataObject;

            _logger.Log("Max App Open ad OnAdRevenuePaid: " + maxAdInfo.Revenue);

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
                additionalParams.Add("ad_type_custom", "app_open");
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
        }

        protected override void OnAdDisplayFailed(string adUnitId, ErrorInfo errorInfo, AdInfo adInfo)
        {
            base.OnAdDisplayFailed(adUnitId, errorInfo, adInfo);
            var maxErrorInfo = errorInfo.GetErrorData<MaxSdkBase.ErrorInfo>();
            _logger.Log($"App open ad display error: \n{maxErrorInfo.ToString()}");
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
