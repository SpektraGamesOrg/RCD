using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Ads
{
    [Serializable]
    [JsonObject(NamingStrategyType = typeof(SnakeCaseNamingStrategy))]
    public class AdConfig
    {
        // Scalars are nullable so an absent key reads as null (preserving the "missing -> fallback"
        // behavior consumers rely on).
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

        public bool? InterstitialGroupRaceButton { get; set; }
        public bool? InterstitialGroupPausemenuButtons { get; set; }
        public bool? InterstitialGroupMainmenuBackButtons { get; set; }
        public bool? InterstitialGroupEndgameRestartButton { get; set; }
        public bool? InterstitialGroupEndgameReturnButtons { get; set; }
        public bool? InterstitialGroupEndgameNextraceButton { get; set; }

        public bool? GadsmeActive { get; set; }
    }
}