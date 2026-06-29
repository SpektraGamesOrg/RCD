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
    }
}
