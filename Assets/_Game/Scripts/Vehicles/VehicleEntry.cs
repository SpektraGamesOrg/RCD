using System;
using SpektraGames.ResourceObject.Runtime;
using UnityEngine;

namespace Vehicles
{
    /// <summary>
    /// Static, design-time data for a single vehicle: which enum it maps to and a soft reference
    /// to its prefab. The prefab is a <see cref="ResourceObject{T}"/> (Resources-backed, lazy-loaded),
    /// so it is NOT baked as a hard dependency into whatever holds the container.
    ///
    /// Obtain data (how a car is unlocked and its gold/ad/km target) is NOT stored here - it lives in the
    /// Clutch "VehicleConfig" flag with the ClutchConfig SO as the offline fallback, both keyed by the
    /// VehicleID enum name. Resolve it via IClutchConfigService.GetVehicleConfig(id). Extend this entry with
    /// icon, display name, etc. as the game grows.
    /// </summary>
    [Serializable]
    public class VehicleEntry
    {
        [field: SerializeField] public VehicleID ID { get; private set; }

        [field: SerializeField] public ResourceObject<MainVehicleBehaviour> MainBehaviour { get; private set; } = new();
    }
}
