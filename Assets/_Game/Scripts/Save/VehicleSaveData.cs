using System;
using SpektraGames.AddressableLoader.Runtime;
using UnityEngine.AddressableAssets;
using Vehicles;

namespace Save
{
    /// <summary>
    /// Serializable, persisted state for a single vehicle.
    /// Plain class (not MonoBehaviour / ScriptableObject) so it can be stored as JSON.
    /// Add new customization fields here as the game grows - everything in this class
    /// is saved/loaded automatically.
    /// </summary>
    [Serializable]
    public class VehicleSaveData
    {
        public VehicleID id;
        public bool owned;

        // Rewarded ads watched toward unlocking this vehicle (VehicleObtainType.ByWatchAds).
        // Ignored once the vehicle is owned.
        public int watchAdCount;

        public VehicleSaveData() { }
        
        public VehicleSaveData(VehicleID id)
        {
            this.id = id;
        }
    }
}
