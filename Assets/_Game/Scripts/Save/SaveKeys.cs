namespace Save
{
    /// <summary>
    /// Central registry of all PlayerPrefs keys used by the save system.
    /// Keeping every key in one place makes them easy to find and change.
    /// </summary>
    public static class SaveKeys
    {
        public const string UserId           = "user_id";
        public const string Gold           = "coins";
        public const string SelectedVehicle = "selected_vehicle"; // stored as (int)VehicleNameType
        public const string DistanceDriven = "distance_driven"; // total km driven, all cars (whole km)
        public const string DistanceMilestonesClaimed = "distance_milestones_claimed"; // how many distance milestones have been granted

        // Settings
        public const string MasterVolume    = "master_volume";
        public const string SfxVolume       = "sfx_volume";
        public const string Vibration       = "vibration";

        // Vehicles
        public const string Vehicles        = "vehicles"; // VehicleList serialized as JSON

        // Gold collection
        public const string GoldCooldowns   = "gold_cooldowns"; // GoldCooldownList serialized as JSON

        // Nitro
        public const string NitroCount      = "nitro_count"; // remaining free nitro charges (device-level)

        // In-game events (device-level current level per mode; 1-based, endless loop). See Events.EventManager.
        public const string JumpChallengeLevel = "jump_challenge_level";
        public const string TimeTrialLevel     = "time_trial_level";

        // Save durability
        public const string SaveCounter     = "save_counter"; // monotonic counter bumped on every Save(); read by the editor-only Save System Forensics tool to detect a PlayerPrefs rollback

        // Ads — per-user/day interstitial cap (see AdGatingService). Count resets when the stored day changes.
        public const string AdDailyInterDay   = "ad_daily_inter_day";   // yyyy-MM-dd (UTC) the count below belongs to
        public const string AdDailyInterCount = "ad_daily_inter_count"; // interstitials shown on AdDailyInterDay

        // Clutch remote config
        public const string ClutchConfig    = "clutch_config_cache"; // combined flag JSON blob (see ClutchConfigCache)

        // Clutch auth tokens (see ClutchAuth)
        public const string ClutchAccessToken   = "clutch_access_token";
        public const string ClutchRefreshToken  = "clutch_refresh_token";
        public const string ClutchAccessExpires  = "clutch_access_expires";
        public const string ClutchRefreshExpires = "clutch_refresh_expires";
        public const string ClutchAuthUserId    = "clutch_auth_user_id";
        public const string ClutchAuthEnvId     = "clutch_auth_env_id";
    }
}
