namespace Save
{
    /// <summary>
    /// Central registry of all PlayerPrefs keys used by the save system.
    /// Keeping every key in one place makes them easy to find and change.
    /// </summary>
    public static class SaveKeys
    {
        public const string Coins           = "coins";
        public const string SelectedVehicle = "selected_vehicle"; // stored as (int)VehicleNameType

        // Settings
        public const string MasterVolume    = "master_volume";
        public const string SfxVolume       = "sfx_volume";
        public const string Vibration       = "vibration";

        // Vehicles
        public const string Vehicles        = "vehicles"; // VehicleList serialized as JSON

        // Gold collection
        public const string GoldCooldowns   = "gold_cooldowns"; // GoldCooldownList serialized as JSON
    }
}
