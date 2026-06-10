using System;
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
        [SerializeField] private VehicleNameType nameType;
        [SerializeField] private ResourceObject<GameObject> prefab = new ResourceObject<GameObject>();

        public VehicleNameType NameType => nameType;
        public ResourceObject<GameObject> Prefab => prefab;
    }
}
