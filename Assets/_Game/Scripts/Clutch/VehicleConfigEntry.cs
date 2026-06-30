using Newtonsoft.Json;
using Vehicles;

namespace Clutch
{
    /// <summary>
    /// One vehicle's remote obtain config from the Clutch "VehicleConfig" flag. Each obtain path is its own
    /// key, and a key's PRESENCE turns that path on while its value is that path's target (so a car offered
    /// multiple ways carries a distinct value per way). Example:
    /// <code>
    /// "G63":      {"by_gold":5000,"by_watch_ads":8}
    /// "GTR_R35":  {"by_gold":1500}
    /// "SilviaS15":{"distance_km":3000}
    /// "Supra":    {"free":true}
    /// </code>
    /// Path values are nullable so an absent key reads as "path off". <see cref="free"/> is exclusive:
    /// when true the car is Free regardless of the other keys. Field names are snake_case to match the
    /// flag JSON verbatim. This is a Newtonsoft JSON DTO (never Unity-serialized), so it carries no
    /// [Serializable] attribute - that would only invite the UAC1001 nullable-field analyzer warning.
    /// </summary>
    public class VehicleConfigEntry
    {
        [JsonProperty("by_gold")]
        public int? by_gold;

        [JsonProperty("by_watch_ads")]
        public int? by_watch_ads;

        [JsonProperty("distance_km")]
        public int? distance_km;

        [JsonProperty("free")]
        public bool free;

        /// <summary>The <see cref="VehicleObtainType"/> flags implied by which keys are present.</summary>
        public VehicleObtainType ToObtainType()
        {
            if (free)
                return VehicleObtainType.Free;

            VehicleObtainType type = default;
            if (by_gold.HasValue)
                type |= VehicleObtainType.ByGold;
            if (by_watch_ads.HasValue)
                type |= VehicleObtainType.ByWatchAds;
            if (distance_km.HasValue)
                type |= VehicleObtainType.DistanceMilestoneKm;
            return type;
        }
    }

    /// <summary>
    /// The effective obtain config for a vehicle after resolving Clutch over the SO fallback: which obtain
    /// path(s) apply, plus the target value for EACH path (gold price, ad count, milestone km). Consumers
    /// use this instead of reading <see cref="VehicleEntry"/> fields directly, so remote config can change
    /// both the available paths and their per-path values.
    /// </summary>
    public readonly struct ResolvedVehicleConfig
    {
        public readonly VehicleObtainType ObtainType;
        public readonly int GoldValue;
        public readonly int AdsValue;
        public readonly int DistanceKm;

        public ResolvedVehicleConfig(VehicleObtainType obtainType, int goldValue, int adsValue, int distanceKm)
        {
            ObtainType = obtainType;
            GoldValue = goldValue;
            AdsValue = adsValue;
            DistanceKm = distanceKm;
        }

        public bool Has(VehicleObtainType flag) => (ObtainType & flag) != 0;
    }
}
