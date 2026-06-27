using System;
using Sirenix.OdinInspector;
using SpektraGames.ResourceObject.Runtime;
using UnityEngine;

namespace Vehicles
{
    /// <summary>
    /// Static, design-time data for a single vehicle: which enum it maps to and a soft reference
    /// to its prefab. The prefab is a <see cref="ResourceObject{T}"/> (Resources-backed, lazy-loaded),
    /// so it is NOT baked as a hard dependency into whatever holds the container. Extend with icon,
    /// display name, price, etc. as the game grows.
    /// </summary>
    [Serializable]
    public class VehicleEntry
    {
        [field: SerializeField] public VehicleID ID { get; private set; }

        [field: SerializeField] public ResourceObject<MainVehicleBehaviour> MainBehaviour { get; private set; } = new();

        [field: SerializeField] public VehicleObtainType VehicleObtainType { get; private set; } = VehicleObtainType.ByGold;

        [field: SerializeField, HideIf(nameof(IsFree))]
        public int VehicleObtainTargetAmount { get; private set; } = 1500;

        // Free cars are auto-granted (see SaveManager.EnsureStarterVehicle) and have no unlock target,
        // so the target amount is hidden in the inspector when this entry is Free.
        private bool IsFree => (VehicleObtainType & VehicleObtainType.Free) != 0;
    }
}