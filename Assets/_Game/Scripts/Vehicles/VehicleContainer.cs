using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using SpektraGames.ResourceObject.Runtime;
using SpektraGames.SpektraUtilities.Runtime;
using UnityEngine;

namespace Vehicles
{
    /// <summary>
    /// Singleton ScriptableObject catalog of every vehicle in the game (prefab + id enum).
    /// This is static design-time data - the player's owned/selected state lives in the
    /// save system (SaveManager, in the Save namespace), keyed by the same <see cref="VehicleNameType"/>.
    ///
    /// Loaded automatically via <see cref="SingletonScriptableObject{T}"/>, which calls
    /// Resources.Load("VehicleContainer"). The asset MUST be named "VehicleContainer" and live
    /// directly inside a Resources folder (e.g. Assets/_Game/Resources/VehicleContainer.asset).
    ///
    /// Usage:
    ///     GameObject prefab = await VehicleContainer.Instance.LoadPrefabAsync(VehicleNameType.GTR_R35);
    /// </summary>
    [CreateAssetMenu(fileName = "VehicleContainer", menuName = "DRIVE01/Vehicle Container")]
    public class VehicleContainer : SingletonScriptableObject<VehicleContainer>
    {
        [SerializeField] private List<VehicleEntry> vehicles = new List<VehicleEntry>();

        /// <summary>
        /// All registered vehicles (read-only).
        /// </summary>
        public IReadOnlyList<VehicleEntry> Vehicles => vehicles;

        /// <summary>
        /// Returns the entry for a vehicle, or null (with an error) if it isn't registered.
        /// </summary>
        public VehicleEntry GetVehicle(VehicleNameType nameType)
        {
            VehicleEntry entry = FindEntry(nameType);
            if (entry == null)
                Debug.LogError($"[VehicleContainer] No vehicle registered for {nameType}.");

            return entry;
        }

        /// <summary>
        /// Returns the prefab resource handle for a vehicle, or null if it isn't registered.
        /// Call Load()/LoadAsync() on the result to get the actual prefab.
        /// </summary>
        public ResourceObject<GameObject> GetPrefab(VehicleNameType nameType)
        {
            VehicleEntry entry = GetVehicle(nameType);
            return entry?.Prefab;
        }

        /// <summary>
        /// Loads and returns the prefab for a vehicle asynchronously,
        /// or null if it isn't registered.
        /// </summary>
        public async UniTask<GameObject> LoadPrefabAsync(VehicleNameType nameType)
        {
            ResourceObject<GameObject> resource = GetPrefab(nameType);
            if (resource == null)
                return null;

            return await resource.LoadAsync();
        }

        /// <summary>
        /// True if the vehicle is registered in this container.
        /// </summary>
        public bool Contains(VehicleNameType nameType)
        {
            return FindEntry(nameType) != null;
        }

        // Linear scan is fine for the small roster (~10 cars) and avoids LINQ allocations.
        private VehicleEntry FindEntry(VehicleNameType nameType)
        {
            for (int i = 0; i < vehicles.Count; i++)
            {
                if (vehicles[i].NameType == nameType)
                    return vehicles[i];
            }

            return null;
        }
    }
}