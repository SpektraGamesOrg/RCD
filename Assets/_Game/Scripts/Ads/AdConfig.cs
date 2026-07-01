using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Ads
{
    /// <summary>
    /// Typed view of the Clutch "AdConfig" remote-config flag. The flag is ONE JSON object holding every
    /// ad-tuning key; it is deserialized once (see AdConfigProvider) into this POCO with the
    /// SnakeCaseNamingStrategy, so a JSON key like "ad_fullscreen_cooldown_sec" binds to
    /// <see cref="AdFullscreenCooldownSec"/>.
    ///
    /// Two families of fields live here:
    ///   * The RCD "Ad Placements" schema (the new keys, prefixed AdXxx / RewardedMultipliers) that drive
    ///     the shared fullscreen cooldown, caps, commercial breaks, MREC/banner surfaces, and per-placement
    ///     rewarded multipliers.
    ///   * Legacy HRP-port toggles kept ONLY because <see cref="BannerPlacementTable"/> still selects on the
    ///     BannerGroup* bools. See the legacy-field notes below; do not add new consumers of them.
    ///
    /// Every scalar is nullable so an absent JSON key reads as null and the typed accessors fall back to the
    /// doc default (preserving the "missing -> default" contract consumers rely on). Read tuned values
    /// THROUGH the accessor properties/methods, not the raw nullable fields, so defaults are applied in one
    /// place.
    /// </summary>
    [Serializable]
    [JsonObject(NamingStrategyType = typeof(SnakeCaseNamingStrategy))]
    public class AdConfig
    {
        // ------------------------------------------------------------------
        // RCD "Ad Placements" schema (see the Confluence Remote Config Schema table).
        // Raw nullable fields bind directly to the flag JSON; read via the accessors below.
        // ------------------------------------------------------------------

        /// <summary>Shared App Open + Interstitial cooldown, seconds. JSON: ad_fullscreen_cooldown_sec.</summary>
        public int? AdFullscreenCooldownSec { get; set; }

        /// <summary>Min background time before a warm-resume App Open may fire, seconds. JSON: ad_app_open_min_bg_sec.</summary>
        public int? AdAppOpenMinBgSec { get; set; }

        /// <summary>Master on/off for timed Commercial Break interstitials. JSON: ad_commercial_break_enabled.</summary>
        public bool? AdCommercialBreakEnabled { get; set; }

        /// <summary>Active-gameplay seconds between Commercial Breaks. JSON: ad_commercial_break_interval_sec.</summary>
        public int? AdCommercialBreakIntervalSec { get; set; }

        /// <summary>Hard ceiling on interstitials per session. JSON: ad_max_inters_per_session.</summary>
        public int? AdMaxIntersPerSession { get; set; }

        /// <summary>Per-user/day interstitial ceiling. JSON: ad_daily_inter_cap.</summary>
        public int? AdDailyInterCap { get; set; }

        /// <summary>Master toggle for screen-transition button interstitials. JSON: ad_button_inter_enabled.</summary>
        public bool? AdButtonInterEnabled { get; set; }

        /// <summary>
        /// Whitelist of screen-transition button ids that may fire an interstitial (functional game buttons
        /// like gas/brake/arrows/nitro/drift are never included). JSON: ad_button_inter_whitelist.
        /// </summary>
        public List<string> AdButtonInterWhitelist { get; set; }

        /// <summary>Whether Close on a milestone popup fires an interstitial. JSON: ad_milestone_close_inter.</summary>
        public bool? AdMilestoneCloseInter { get; set; }

        /// <summary>Tutorial on/off. JSON: tutorial_enabled.</summary>
        public bool? TutorialEnabled { get; set; }

        /// <summary>Global banner kill-switch (persistent in-game banner). JSON: ad_banner_enabled.</summary>
        public bool? AdBannerEnabled { get; set; }

        /// <summary>Loading-screen MREC on/off. JSON: ad_loading_mrec_enabled.</summary>
        public bool? AdLoadingMrecEnabled { get; set; }

        /// <summary>City-start overlay (MREC + banner) on/off. JSON: ad_city_start_overlay.</summary>
        public bool? AdCityStartOverlay { get; set; }

        /// <summary>Pause-menu MREC on/off. JSON: ad_pause_mrec_enabled.</summary>
        public bool? AdPauseMrecEnabled { get; set; }

        /// <summary>
        /// Per-placement rewarded reward multipliers/amounts, keyed by placement name
        /// (nitro, milestone_completion, free_cash, restore). JSON: rewarded_multipliers (nested object).
        /// Read via <see cref="RewardedMultiplier"/>; a missing key falls back to the caller's default.
        /// </summary>
        public Dictionary<string, double> RewardedMultipliers { get; set; }

        // ------------------------------------------------------------------
        // Legacy HRP-port fields. KEPT because BannerPlacementTable's selectors read the BannerGroup* bools
        // and removing them breaks compilation. Not part of the RCD schema; no new consumers. The interstitial
        // gating no longer reads InterstitialFrequency et al. — AdGatingService owns fullscreen policy now.
        // ------------------------------------------------------------------

        public bool? InterstitialEnabled { get; set; }
        public int? InterstitialFrequency { get; set; }
        public int? InterstitialDailyLimit { get; set; }
        public int? InterstitialSessionLimit { get; set; }
        public int? InterstitialLimitResetInterval { get; set; }
        public int? FirstInterstitialLevelLimit { get; set; }
        public int? OutOfTimeRewardedBonus { get; set; }
        public List<double> LevelCompleteRewardedMultiplier { get; set; }
        public bool? RewardedAffectsInterstitialFrequency { get; set; }
        public int? RewardedAffectsInterstitialFrequencyDuration { get; set; }

        public bool? BannerGroupMapsBottom { get; set; }
        public bool? BannerGroupOrganizeBottom { get; set; }
        public bool? BannerGroupPausemenuMiddle { get; set; }
        public bool? BannerGroupLoadingscreenRight { get; set; }
        public bool? BannerGroupDriverlicenseBottom { get; set; }
        public bool? BannerGroupMainmenuBelowbuttons { get; set; }

        public bool? GadsmeActive { get; set; }

        // ------------------------------------------------------------------
        // Typed accessors — apply the RCD doc defaults in one place so consumers never branch on null.
        // ------------------------------------------------------------------

        /// <summary>Doc default 30s. Shared by App Open and interstitials.</summary>
        public int FullscreenCooldownSec => AdFullscreenCooldownSec ?? 30;

        /// <summary>Doc-referenced min background time before a warm App Open. Defaults to 60s.</summary>
        public int AppOpenMinBackgroundSec => AdAppOpenMinBgSec ?? 60;

        /// <summary>Doc default true.</summary>
        public bool CommercialBreakEnabled => AdCommercialBreakEnabled ?? true;

        /// <summary>Doc default 60s of active gameplay between breaks.</summary>
        public int CommercialBreakIntervalSec => AdCommercialBreakIntervalSec ?? 60;

        /// <summary>Doc default 12.</summary>
        public int MaxIntersPerSession => AdMaxIntersPerSession ?? 12;

        /// <summary>Doc default 20.</summary>
        public int DailyInterCap => AdDailyInterCap ?? 20;

        /// <summary>Doc default true.</summary>
        public bool ButtonInterEnabled => AdButtonInterEnabled ?? true;

        /// <summary>Doc default true.</summary>
        public bool MilestoneCloseInter => AdMilestoneCloseInter ?? true;

        /// <summary>Doc default true.</summary>
        public bool TutorialOn => TutorialEnabled ?? true;

        /// <summary>Doc default true.</summary>
        public bool BannerEnabled => AdBannerEnabled ?? true;

        /// <summary>Doc default true (MREC is a display unit, not cooldown-gated).</summary>
        public bool LoadingMrecEnabled => AdLoadingMrecEnabled ?? true;

        /// <summary>Doc default true.</summary>
        public bool CityStartOverlayEnabled => AdCityStartOverlay ?? true;

        /// <summary>Doc default true.</summary>
        public bool PauseMrecEnabled => AdPauseMrecEnabled ?? true;

        /// <summary>
        /// True when the given screen-transition button id may fire an interstitial. Requires the master
        /// <see cref="ButtonInterEnabled"/> toggle AND presence in the whitelist. A missing/empty whitelist
        /// means no button fires (safe default: opt-in, never opt-out of the functional-button exclusions).
        /// </summary>
        public bool IsButtonInterWhitelisted(string buttonId)
        {
            if (!ButtonInterEnabled || string.IsNullOrEmpty(buttonId) || AdButtonInterWhitelist == null)
                return false;

            // Small list, called on discrete button taps (not a per-frame hot path); linear scan avoids a
            // set allocation and keeps the config a plain deserialized POCO.
            for (int i = 0; i < AdButtonInterWhitelist.Count; i++)
            {
                if (AdButtonInterWhitelist[i] == buttonId)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// The rewarded reward multiplier/amount for a placement key (nitro, milestone_completion,
        /// free_cash, restore), or <paramref name="fallback"/> when the key is absent. Kept as double so a
        /// fractional multiplier (e.g. 1.5) is representable; callers that need an int round at the grant.
        /// </summary>
        public double RewardedMultiplier(string placementKey, double fallback)
        {
            if (RewardedMultipliers != null &&
                !string.IsNullOrEmpty(placementKey) &&
                RewardedMultipliers.TryGetValue(placementKey, out double value))
            {
                return value;
            }

            return fallback;
        }
    }
}
