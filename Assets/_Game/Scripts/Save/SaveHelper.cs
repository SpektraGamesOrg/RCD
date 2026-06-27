using System.Collections.Generic;
using System.Linq;
using Sirenix.OdinInspector;
using SpektraGames.SpektraUtilities.Runtime;
using UnityEngine;
using Vehicles;

namespace Save
{
    /// <summary>
    /// Tiny persistent component that auto-flushes the save system to disk when the app
    /// is paused or quit, so mobile players never lose progress without an explicit Save().
    /// Self-spawns at runtime, so no scene or prefab wiring is required.
    /// </summary>
    public class SaveHelper : SingletonComponent<SaveHelper>
    {
#if UNITY_EDITOR
        [ShowInInspector, ReadOnly, BoxGroup("Save State")]
        private int CurrentCoins => Application.isPlaying ? SaveManager.Gold : 0;

        [ShowInInspector, ReadOnly, BoxGroup("Save State")]
        private List<VehicleID> OwnedVehicles => Application.isPlaying
            ? SaveManager.GetOwnedVehicles().Select(v => v.id).ToList()
            : new List<VehicleID>();

        [ShowInInspector, ReadOnly, BoxGroup("Save State")]
        private List<VehicleID> NotOwnedVehicles => Application.isPlaying && VehicleContainer.Instance != null
            ? VehicleContainer.Instance.Vehicles.Where(v => !SaveManager.IsOwned(v.ID)).Select(v => v.ID).ToList()
            : new List<VehicleID>();

        // Editable while playing: set the total driven distance to test distance-milestone unlocks.
        // Writing it fires SaveManager.OnDistanceDrivenChanged, which refreshes the menu on its own.
        [ShowInInspector, BoxGroup("Save State"), LabelText("Distance Driven (km)")]
        private int DebugDistanceDrivenKm
        {
            get => Application.isPlaying ? SaveManager.DistanceDrivenKm : 0;
            set
            {
                if (!Application.isPlaying)
                    return;

                SaveManager.DistanceDrivenKm = value < 0 ? 0 : value;
                SaveManager.Save();
            }
        }
#endif

        [Button]
        private void AddCurrency()
        {
            int half = int.MaxValue / 2;
            int current = SaveManager.Gold;
            SaveManager.Gold = current > int.MaxValue - half ? int.MaxValue : current + half;
            SaveManager.Save();
        }

        [Button]
        private void ClearPlayerPrefs()
        {
            SaveManager.ResetAll();
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused)
                SaveManager.Save();
        }

        private void OnApplicationQuit()
        {
            SaveManager.Save();
        }
    }
}