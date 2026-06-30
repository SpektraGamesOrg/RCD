namespace Clutch
{
    /// <summary>
    /// Canonical Clutch flag keys the game reads. Keep these in sync with the flag keys configured in the
    /// Clutch dashboard and with the entries in the <see cref="ClutchConfig"/> fallback asset.
    /// </summary>
    public static class ClutchFlagKeys
    {
        /// <summary>
        /// Per-vehicle obtain config, keyed by vehicle key (VehicleID enum name). Each value is an object
        /// whose KEYS are the obtain paths a car offers, each carrying that path's own target value:
        /// "by_gold" (gold price), "by_watch_ads" (ad count), "distance_km" (milestone km). A path's
        /// presence turns it on; a car can offer several at once with distinct values. "free":true marks
        /// the car free and is exclusive of the path keys. Example:
        /// {"G63":{"by_gold":5000,"by_watch_ads":8},"GTR_R35":{"by_gold":1500},"Supra":{"free":true}}.
        /// </summary>
        public const string VehicleConfig = "VehicleConfig";

        /// <summary>string -> int ad tuning, e.g. {"interstitial_frequency":200}.</summary>
        public const string AdConfig = "AdConfig";

        /// <summary>All keys requested from Clutch in one evaluate-batch call.</summary>
        public static readonly string[] All = { VehicleConfig, AdConfig };
    }
}
