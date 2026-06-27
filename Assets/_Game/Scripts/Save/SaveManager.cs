using System.Collections.Generic;
using UnityEngine;
using Vehicles;

namespace Save
{
    /// <summary>
    /// Basic, modular save system built on PlayerPrefs (no encryption).
    /// Static accessors let you read/write any saved value in a single line, e.g.:
    ///
    ///     SaveManager.Coins += 250;
    ///     SaveManager.SelectVehicle(VehicleNameType.GTR_R35);
    ///     SaveManager.Save();
    ///
    /// To add a new saved property: add a key in <see cref="SaveKeys"/>, a default const below,
    /// and a property. Simple values are stored one key per field. Vehicles are kept in a single
    /// <see cref="VehicleList"/> stored as JSON, keyed by <see cref="VehicleID"/>.
    ///
    /// Note: PlayerPrefs.Set* only stages values in memory. Call <see cref="Save"/> to flush
    /// to disk (the auto-save helper also flushes on quit/pause).
    /// </summary>
    public static class SaveManager
    {
        // ---------------------------------------------------------------------
        // Default values
        // ---------------------------------------------------------------------
        private const int DefaultGold = 150;
        private const float DefaultMasterVolume = 1f;
        private const float DefaultSfxVolume = 1f;
        private const bool DefaultVibration = true;

        // ---------------------------------------------------------------------
        // Currency
        // ---------------------------------------------------------------------

        public static event System.Action<int> OnCoinsChanged;
        public static event System.Action<int> OnDistanceDrivenChanged;

        /// <summary>
        /// Fired after all saved data is wiped (<see cref="ResetAll"/>). Per-value events are not raised
        /// for a full reset, so listeners should re-read everything they display when this fires.
        /// </summary>
        public static event System.Action OnSaveReset;

        public static int Gold
        {
            get => PlayerPrefs.GetInt(SaveKeys.Gold, DefaultGold);
            set
            {
                PlayerPrefs.SetInt(SaveKeys.Gold, value);
                OnCoinsChanged?.Invoke(value);
            }
        }

        public static int DistanceDrivenKm
        {
            get => PlayerPrefs.GetInt(SaveKeys.DistanceDriven, 0);
            set
            {
                PlayerPrefs.SetInt(SaveKeys.DistanceDriven, value);
                OnDistanceDrivenChanged?.Invoke(value);
            }
        }

        /// <summary>
        /// Adds (or, with a negative amount, removes) coins and fires <see cref="OnCoinsChanged"/>.
        /// Convenience for reward paths so callers don't repeat the read-modify-write.
        /// </summary>
        public static void AddCoins(int amount)
        {
            Gold += amount;
        }

        public static void AddDistanceDriven(int amount)
        {
            DistanceDrivenKm += amount;
        }

        // ---------------------------------------------------------------------
        // Settings
        // ---------------------------------------------------------------------
        public static float MasterVolume
        {
            get => PlayerPrefs.GetFloat(SaveKeys.MasterVolume, DefaultMasterVolume);
            set => PlayerPrefs.SetFloat(SaveKeys.MasterVolume, value);
        }

        public static float SfxVolume
        {
            get => PlayerPrefs.GetFloat(SaveKeys.SfxVolume, DefaultSfxVolume);
            set => PlayerPrefs.SetFloat(SaveKeys.SfxVolume, value);
        }

        public static bool Vibration
        {
            get => PlayerPrefs.GetInt(SaveKeys.Vibration, DefaultVibration ? 1 : 0) == 1;
            set => PlayerPrefs.SetInt(SaveKeys.Vibration, value ? 1 : 0);
        }

        // ---------------------------------------------------------------------
        // Selected vehicle
        // ---------------------------------------------------------------------

        /// <summary>
        /// The currently selected vehicle. Set it through <see cref="SelectVehicle"/> so the
        /// choice is validated against owned vehicles.
        /// </summary>
        public static VehicleID SelectedVehicle
        {
            get => (VehicleID)PlayerPrefs.GetInt(SaveKeys.SelectedVehicle, (int)VehicleID.None);
            private set => PlayerPrefs.SetInt(SaveKeys.SelectedVehicle, (int)value);
        }

        /// <summary>
        /// Selects a vehicle, but only if the player owns it. Returns false (and changes nothing)
        /// if the vehicle is not owned.
        /// </summary>
        public static bool SelectVehicle(VehicleID id)
        {
            if (!IsOwned(id))
            {
                Debug.LogError($"[SaveManager] Cannot select {id} - vehicle is not owned.");
                return false;
            }

            SelectedVehicle = id;
            return true;
        }

        // ---------------------------------------------------------------------
        // Vehicles (stored as a single VehicleList JSON blob)
        // ---------------------------------------------------------------------

        /// <summary>
        /// The full list of vehicles the player has data for.
        /// </summary>
        public static VehicleList GetVehicleList()
        {
            string json = PlayerPrefs.GetString(SaveKeys.Vehicles, "");
            if (string.IsNullOrEmpty(json))
                return new VehicleList();

            return JsonUtility.FromJson<VehicleList>(json);
        }

        /// <summary>
        /// All vehicles the player currently owns.
        /// </summary>
        public static List<VehicleSaveData> GetOwnedVehicles()
        {
            return GetVehicleList().vehicles.FindAll(v => v.owned);
        }

        /// <summary>
        /// Returns the saved data for a vehicle, or a fresh default if it isn't in the list yet.
        /// </summary>
        public static VehicleSaveData GetVehicle(VehicleID id)
        {
            VehicleSaveData vehicle = GetVehicleList().vehicles.Find(v => v.id == id);
            return vehicle ?? new VehicleSaveData(id);
        }

        /// <summary>
        /// Stores a vehicle's data, adding it to the list or replacing the existing entry.
        /// </summary>
        public static void SetVehicle(VehicleSaveData vehicle)
        {
            if (vehicle == null || vehicle.id == VehicleID.None)
            {
                Debug.LogError("[SaveManager] SetVehicle called with null vehicle or None type.");
                return;
            }

            VehicleList list = GetVehicleList();
            int index = list.vehicles.FindIndex(v => v.id == vehicle.id);
            if (index >= 0)
                list.vehicles[index] = vehicle;
            else
                list.vehicles.Add(vehicle);

            SaveVehicleList(list);
        }

        /// <summary>
        /// True if the vehicle exists in the list and is owned.
        /// </summary>
        public static bool IsOwned(VehicleID id)
        {
            VehicleSaveData vehicle = GetVehicleList().vehicles.Find(v => v.id == id);
            return vehicle != null && vehicle.owned;
        }

        /// <summary>
        /// Marks a vehicle as owned, creating its entry if needed.
        /// </summary>
        public static void AddOwned(VehicleID id)
        {
            VehicleSaveData vehicle = GetVehicle(id);
            vehicle.owned = true;
            SetVehicle(vehicle);
        }

        /// <summary>
        /// How many rewarded ads the player has watched toward unlocking the given vehicle
        /// (used by <see cref="VehicleObtainType.ByWatchAds"/>). Distance milestones are global and
        /// read from <see cref="DistanceDrivenKm"/> instead - they are not tracked per vehicle.
        /// </summary>
        public static int GetVehicleWatchAdCount(VehicleID id)
        {
            VehicleSaveData vehicle = GetVehicleList().vehicles.Find(v => v.id == id);
            return vehicle?.watchAdCount ?? 0;
        }

        /// <summary>
        /// Stores the rewarded-ad watch count for a vehicle, creating its entry if needed.
        /// Does not flush; call <see cref="Save"/> to persist.
        /// </summary>
        public static void SetVehicleWatchAdCount(VehicleID id, int count)
        {
            if (id == VehicleID.None)
            {
                Debug.LogError("[SaveManager] SetVehicleWatchAdCount called with None id.");
                return;
            }

            VehicleSaveData vehicle = GetVehicle(id);
            vehicle.watchAdCount = count < 0 ? 0 : count;
            SetVehicle(vehicle);
        }

        // ---------------------------------------------------------------------
        // Gold collection cooldowns (stored as a single GoldCooldownList JSON blob)
        // ---------------------------------------------------------------------

        /// <summary>
        /// The full set of gold-point cooldown records the player has data for.
        /// </summary>
        public static GoldCooldownList GetGoldCooldownList()
        {
            string json = PlayerPrefs.GetString(SaveKeys.GoldCooldowns, "");
            if (string.IsNullOrEmpty(json))
                return new GoldCooldownList();

            return JsonUtility.FromJson<GoldCooldownList>(json);
        }

        /// <summary>
        /// The Unix time (seconds) the given gold point was last collected, or -1 if it has never been
        /// collected (i.e. it is currently active).
        /// </summary>
        public static long GetGoldCollectedTime(string goldId)
        {
            if (string.IsNullOrEmpty(goldId))
                return -1;

            GoldCooldownData data = GetGoldCooldownList().golds.Find(g => g.id == goldId);
            return data?.collectedUnixSeconds ?? -1;
        }

        /// <summary>
        /// Records that the given gold point was collected at <paramref name="collectedUnixSeconds"/>,
        /// adding or replacing its cooldown entry. Does not flush; call <see cref="Save"/> to persist.
        /// </summary>
        public static void SetGoldCollected(string goldId, long collectedUnixSeconds)
        {
            if (string.IsNullOrEmpty(goldId))
            {
                Debug.LogError("[SaveManager] SetGoldCollected called with empty goldId.");
                return;
            }

            GoldCooldownList list = GetGoldCooldownList();
            int index = list.golds.FindIndex(g => g.id == goldId);
            if (index >= 0)
                list.golds[index].collectedUnixSeconds = collectedUnixSeconds;
            else
                list.golds.Add(new GoldCooldownData(goldId, collectedUnixSeconds));

            SaveGoldCooldownList(list);
        }

        /// <summary>
        /// Clears the cooldown record for a gold point (it becomes active again). Used when a gold's
        /// cooldown has elapsed. Does not flush; call <see cref="Save"/> to persist.
        /// </summary>
        public static void ClearGoldCollected(string goldId)
        {
            if (string.IsNullOrEmpty(goldId))
                return;

            GoldCooldownList list = GetGoldCooldownList();
            int removed = list.golds.RemoveAll(g => g.id == goldId);
            if (removed > 0)
                SaveGoldCooldownList(list);
        }

        // ---------------------------------------------------------------------
        // Initialization
        // ---------------------------------------------------------------------

        /// <summary>
        /// Prepares the save data for use. Call once during startup (see GameInitializer).
        /// Guarantees the player always owns a drivable car by granting the first roster entry
        /// when nothing is owned yet.
        /// </summary>
        public static void Initialize()
        {
            EnsureStarterVehicle();
        }

        // Grants the first roster vehicle (the free starter) when the player owns nothing yet.
        private static void EnsureStarterVehicle()
        {
            VehicleContainer container = VehicleContainer.Instance;
            if (container == null || container.Vehicles.Count == 0)
            {
                Debug.LogError("[SaveManager] No vehicles registered; cannot grant a starter vehicle.");
                return;
            }

            bool anyChange = false;

            for (var i = 0; i < container.Vehicles.Count; i++)
            {
                var vehicle = container.Vehicles[i];

                if (vehicle.VehicleObtainType == VehicleObtainType.Free &&
                    !IsOwned(vehicle.ID))
                {
                    AddOwned(vehicle.ID);

                    if (SelectedVehicle == VehicleID.None)
                        SelectVehicle(vehicle.ID);

                    anyChange = true;
                }
            }

            if (anyChange)
            {
                Save();
            }
        }

        // ---------------------------------------------------------------------
        // Persistence
        // ---------------------------------------------------------------------

        /// <summary>
        /// Flushes all staged changes to disk.
        /// </summary>
        public static void Save()
        {
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Clears all saved data. Intended for development / testing.
        /// </summary>
        public static void ResetAll()
        {
            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();
            OnSaveReset?.Invoke();
        }

        // ---------------------------------------------------------------------
        // Helpers
        // ---------------------------------------------------------------------

        private static void SaveVehicleList(VehicleList list)
        {
            PlayerPrefs.SetString(SaveKeys.Vehicles, JsonUtility.ToJson(list));
        }

        private static void SaveGoldCooldownList(GoldCooldownList list)
        {
            PlayerPrefs.SetString(SaveKeys.GoldCooldowns, JsonUtility.ToJson(list));
        }
    }
}