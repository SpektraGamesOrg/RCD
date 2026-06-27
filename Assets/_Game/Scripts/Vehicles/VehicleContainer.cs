using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Sirenix.OdinInspector;
using SpektraGames.ResourceObject.Runtime;
using SpektraGames.SpektraUtilities.Runtime;
using UnityEngine;

namespace Vehicles
{
    /// <summary>
    /// Singleton ScriptableObject catalog of every vehicle in the game (prefab + id enum).
    /// This is static design-time data - the player's owned/selected state lives in the
    /// save system (SaveManager, in the Save namespace), keyed by the same <see cref="VehicleID"/>.
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
        public VehicleEntry GetVehicle(VehicleID id)
        {
            VehicleEntry entry = FindEntry(id);
            if (entry == null)
                Debug.LogError($"[VehicleContainer] No vehicle registered for {id}.");

            return entry;
        }

        /// <summary>
        /// Returns the prefab resource handle for a vehicle, or null if it isn't registered.
        /// Call Load()/LoadAsync() on the result to get the actual prefab.
        /// </summary>
        public ResourceObject<MainVehicleBehaviour> GetPrefab(VehicleID id)
        {
            VehicleEntry entry = GetVehicle(id);
            return entry?.MainBehaviour;
        }

        private void OnValidate()
        {
            UpdateVehicles();
        }

        [Button]
        private void UpdateVehicles()
        {
#if UNITY_EDITOR
            for (var i = 0; i < vehicles.Count; i++)
            {
                if (vehicles[i].ID == VehicleID.None)
                {
                    Debug.LogError($"vehicles[{i}].ID is is None", this);
                    continue;
                }

                if (UnityEditor.EditorApplication.isCompiling ||
                    UnityEditor.EditorApplication.isUpdating)
                {
                    continue;
                }

                if (!vehicles[i].MainBehaviour.IsValidForEditor)
                {
                    Debug.LogError($"vehicles[{i}] with id  {vehicles[i].ID} has invalid prefab", this);
                    continue;
                }

                MainVehicleBehaviour mainBehaviour = vehicles[i].MainBehaviour.GetEditorAsset();
                if (mainBehaviour.VehicleID != vehicles[i].ID)
                {
                    mainBehaviour.VehicleID = vehicles[i].ID;
                    UnityEditor.EditorUtility.SetDirty(mainBehaviour);
                }
                mainBehaviour.Validate();
            }

            // Validate() keeps exactly one wired odometer on each physics root. This second pass strips
            // any stray VehicleKmTracker left on a GameObject without a Rigidbody (e.g. on a nested
            // body-less copy of the car) - those would fail at runtime. Deferred + guarded so the
            // DestroyImmediate/Save never run during OnValidate and repeated calls don't stack.
            CleanupKmTrackersDeferred();
#endif
        }

#if UNITY_EDITOR
        // Guards against stacking duplicate delayCall cleanups while one is pending.
        [NonSerialized] private bool _kmTrackerCleanupQueued;

        private void CleanupKmTrackersDeferred()
        {
            if (_kmTrackerCleanupQueued)
                return;

            _kmTrackerCleanupQueued = true;
            VehicleContainer self = this;

            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (self)
                    self._kmTrackerCleanupQueued = false;

                if (!self ||
                    UnityEditor.EditorApplication.isCompiling ||
                    UnityEditor.EditorApplication.isUpdating)
                {
                    return;
                }

                self.CleanupKmTrackers();
            };
        }

        // Removes every VehicleKmTracker that sits on a GameObject without a Rigidbody to read - those
        // are the broken leftovers (duplicates, or a tracker on a nested body-less copy). The valid
        // tracker on each physics root is left alone; MainVehicleBehaviour.Validate keeps it wired.
        private void CleanupKmTrackers()
        {
            bool anyChange = false;

            for (int i = 0; i < vehicles.Count; i++)
            {
                if (vehicles[i].ID == VehicleID.None || !vehicles[i].MainBehaviour.IsValidForEditor)
                    continue;

                MainVehicleBehaviour root = vehicles[i].MainBehaviour.GetEditorAsset();
                if (!root)
                    continue;

                VehicleKmTracker[] trackers = root.GetComponentsInChildren<VehicleKmTracker>(true);
                for (int t = 0; t < trackers.Length; t++)
                {
                    VehicleKmTracker tracker = trackers[t];
                    if (tracker.GetComponent<Rigidbody>())
                        continue;

                    MainVehicleBehaviour owner = tracker.GetComponent<MainVehicleBehaviour>();
                    if (owner)
                        owner.EditorSetKmTracker(null);

                    DestroyImmediate(tracker, true);
                    UnityEditor.EditorUtility.SetDirty(root);
                    anyChange = true;
                }
            }

            if (anyChange)
                UnityEditor.AssetDatabase.SaveAssets();
        }
#endif

        /// <summary>
        /// Loads and returns the prefab for a vehicle asynchronously,
        /// or null if it isn't registered.
        /// </summary>
        public async UniTask<MainVehicleBehaviour> LoadPrefabAsync(VehicleID id)
        {
            ResourceObject<MainVehicleBehaviour> resource = GetPrefab(id);
            if (resource == null)
                return null;

            return await resource.LoadAsync();
        }

        /// <summary>
        /// True if the vehicle is registered in this container.
        /// </summary>
        public bool Contains(VehicleID id)
        {
            return FindEntry(id) != null;
        }

        // Linear scan is fine for the small roster (~10 cars) and avoids LINQ allocations.
        private VehicleEntry FindEntry(VehicleID id)
        {
            for (int i = 0; i < vehicles.Count; i++)
            {
                if (vehicles[i].ID == id)
                    return vehicles[i];
            }

            return null;
        }
    }
}