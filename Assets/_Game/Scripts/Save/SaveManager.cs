using System;
using System.Collections.Generic;
using Clutch;
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
        private const int DefaultNitro = 2;
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

        public static string UserId
        {
            get
            {
                string userId = PlayerPrefs.GetString(SaveKeys.UserId, string.Empty);

                if (string.IsNullOrEmpty(userId))
                {
                    userId = Guid.NewGuid().ToString();
                    PlayerPrefs.SetString(SaveKeys.UserId, userId);
                    PlayerPrefs.Save();
                }

                return userId;
            }
        }

        public static int Gold
        {
            // The fresh-install default comes from the CurrencyConfig Clutch flag; only resolve it when no
            // balance is saved yet, so the common read path stays a single PlayerPrefs hit.
            get => PlayerPrefs.HasKey(SaveKeys.Gold)
                ? PlayerPrefs.GetInt(SaveKeys.Gold)
                : DefaultStartingGold;
            set
            {
                PlayerPrefs.SetInt(SaveKeys.Gold, value);
                OnCoinsChanged?.Invoke(value);
            }
        }

        // Starting gold granted on a fresh install (before the player has earned or spent anything), sourced
        // from the "CurrencyConfig" Clutch flag (remote, with the ClutchConfig SO fallback). Replaces the old
        // hard-coded DefaultGold constant.
        private static int DefaultStartingGold =>
            ClutchConfigResolver.Get<CurrencyConfig>(ClutchFlagKeys.CurrencyConfig).DefaultGold;

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
        public static void AddGolds(int amount)
        {
            Gold += amount;
        }

        public static void AddDistanceDriven(int amount)
        {
            DistanceDrivenKm += amount;
        }

        // ---------------------------------------------------------------------
        // Nitro
        // ---------------------------------------------------------------------

        /// <summary>
        /// Fired whenever the remaining nitro count changes, carrying the new (clamped) value.
        /// </summary>
        public static event System.Action<int> OnNitroChanged;

        /// <summary>
        /// Remaining free nitro charges. Device-level (not per vehicle); players start with
        /// <see cref="DefaultNitro"/>. Never goes below 0. Does not flush; call <see cref="Save"/>
        /// to persist.
        /// </summary>
        public static int NitroCount
        {
            get => PlayerPrefs.GetInt(SaveKeys.NitroCount, DefaultNitro);
            set
            {
                int clamped = value < 0 ? 0 : value;
                PlayerPrefs.SetInt(SaveKeys.NitroCount, clamped);
                OnNitroChanged?.Invoke(clamped);
            }
        }

        /// <summary>
        /// Adds (or, with a negative amount, removes) nitro charges and fires
        /// <see cref="OnNitroChanged"/>. Convenience for the activate/reward paths so callers don't
        /// repeat the read-modify-write.
        /// </summary>
        public static void AddNitro(int amount)
        {
            NitroCount += amount;
        }

        /// <summary>
        /// How many distance milestones have been completed and rewarded so far (the global progression
        /// counter; distance milestones are device-level, not per vehicle). Equals the index of the next
        /// unclaimed milestone. Owned by the milestone service (see Milestones.DistanceMilestoneManager);
        /// raises no event of its own - that service raises its own progress event. Negative values clamp
        /// to 0. Does not flush; call <see cref="Save"/> to persist.
        /// </summary>
        public static int DistanceMilestonesClaimed
        {
            get => PlayerPrefs.GetInt(SaveKeys.DistanceMilestonesClaimed, 0);
            set => PlayerPrefs.SetInt(SaveKeys.DistanceMilestonesClaimed, value < 0 ? 0 : value);
        }

        // ---------------------------------------------------------------------
        // In-game events (Jump Challenge / Time Trial)
        // ---------------------------------------------------------------------

        /// <summary>
        /// The player's current Jump Challenge level (1-based). Every Jump area in the map plays this level; a
        /// win advances it and it wraps back to 1 after the last level (endless loop). Owned by
        /// <see cref="Events.EventManager"/>. Does not flush; call <see cref="Save"/> to persist.
        /// </summary>
        public static int JumpChallengeLevel
        {
            get => PlayerPrefs.GetInt(SaveKeys.JumpChallengeLevel, 1);
            set => PlayerPrefs.SetInt(SaveKeys.JumpChallengeLevel, value < 1 ? 1 : value);
        }

        /// <summary>
        /// The player's current Time Trial level (1-based). Behaves exactly like <see cref="JumpChallengeLevel"/>
        /// for the Time Trial mode.
        /// </summary>
        public static int TimeTrialLevel
        {
            get => PlayerPrefs.GetInt(SaveKeys.TimeTrialLevel, 1);
            set => PlayerPrefs.SetInt(SaveKeys.TimeTrialLevel, value < 1 ? 1 : value);
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
            string userId = UserId; // To generate user id
            EnsureStarterVehicle();
        }

        // Grants the free starter car(s) when the player owns nothing yet. Runs synchronously at boot BEFORE
        // Clutch loads, so it reads "which car is Free" from the ClutchConfig SO fallback (available without
        // network) - the container no longer carries obtain data. A car promoted to Free remotely (present
        // in Clutch but not the SO) is handled later by GrantClutchFreeVehicles once Clutch resolves.
        private static void EnsureStarterVehicle()
        {
            VehicleContainer container = VehicleContainer.Instance;
            if (!container || container.Vehicles.Count == 0)
            {
                Debug.LogError("[SaveManager] No vehicles registered; cannot grant a starter vehicle.");
                return;
            }

            ClutchConfig fallback = ClutchConfig.Instance;
            if (!fallback)
            {
                Debug.LogError("[SaveManager] ClutchConfig fallback asset missing; cannot determine the free starter.");
                return;
            }

            bool anyChange = false;

            for (var i = 0; i < container.Vehicles.Count; i++)
            {
                VehicleEntry vehicle = container.Vehicles[i];
                if (vehicle == null || IsOwned(vehicle.ID))
                    continue;

                if (!fallback.TryGetVehicleConfigEntry(vehicle.ID, out VehicleConfigEntry entry) ||
                    entry.ToObtainType() != VehicleObtainType.Free)
                {
                    continue;
                }

                AddOwned(vehicle.ID);
                if (SelectedVehicle == VehicleID.None)
                    SelectVehicle(vehicle.ID);

                anyChange = true;
            }

            if (anyChange)
                Save();
        }

        /// <summary>
        /// Second-phase free grant, run AFTER Clutch resolves (see GameInitializer). Grants any vehicle
        /// whose Clutch-resolved obtain type is <see cref="VehicleObtainType.Free"/> but that the player does
        /// not own yet - so the product team can promote a car to Free remotely even when the SO fallback
        /// says otherwise. Phase one (<see cref="EnsureStarterVehicle"/>) already runs synchronously at boot
        /// from the ClutchConfig SO fallback, guaranteeing an owned car before this Clutch-dependent pass;
        /// this pass only ever ADDS ownership.
        /// </summary>
        public static void GrantClutchFreeVehicles(IClutchConfigService clutchConfig)
        {
            if (clutchConfig == null)
                return;

            VehicleContainer container = VehicleContainer.Instance;
            if (!container || container.Vehicles.Count == 0)
                return;

            bool anyChange = false;

            for (var i = 0; i < container.Vehicles.Count; i++)
            {
                VehicleEntry vehicle = container.Vehicles[i];
                if (vehicle == null || IsOwned(vehicle.ID))
                    continue;

                ResolvedVehicleConfig resolved = clutchConfig.GetVehicleConfig(vehicle.ID);

                // Free is exclusive (enforced by the obtain-type drawer and the parser), so test equality.
                if (resolved.ObtainType != VehicleObtainType.Free)
                    continue;

                AddOwned(vehicle.ID);
                if (SelectedVehicle == VehicleID.None)
                    SelectVehicle(vehicle.ID);

                anyChange = true;
            }

            if (anyChange)
                Save();
        }

        // ---------------------------------------------------------------------
        // Persistence
        // ---------------------------------------------------------------------

        /// <summary>
        /// Flushes all staged changes to disk. Also bumps a monotonic save version counter: it only ever
        /// increases, so a value that later reads LOWER proves PlayerPrefs rolled back. It is read by the
        /// editor-only Save System Forensics tool to DETECT the reset bug; it does not recover any data.
        /// </summary>
        public static void Save()
        {
            int counter = PlayerPrefs.GetInt(SaveKeys.SaveCounter, 0) + 1;
            PlayerPrefs.SetInt(SaveKeys.SaveCounter, counter);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Clears all saved data. Intended for development / testing.
        /// </summary>
        public static void ResetAll()
        {
            // Loud + stack-traced on purpose: this wipes ALL PlayerPrefs (coins, vehicles, Clutch cache,
            // tokens). If saved data ever "resets on next open", this log tells you exactly who called it.
            Debug.LogError($"[SaveManager] ResetAll() wiping ALL PlayerPrefs.\n{System.Environment.StackTrace}");
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