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
        private List<VehicleID> NotOwnedVehicles => Application.isPlaying && VehicleContainer.Instance
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

        [ShowInInspector, ReadOnly, BoxGroup("Save State"), LabelText("Milestones Claimed")]
        private int MilestonesClaimed => Application.isPlaying ? SaveManager.DistanceMilestonesClaimed : 0;

        // Live (fractional) distance including the active car's uncommitted km - what the HUD bar shows.
        [ShowInInspector, ReadOnly, BoxGroup("Save State"), LabelText("Live Distance (km)")]
        private float LiveDistanceKm => Application.isPlaying ? Milestones.DistanceMilestoneManager.LiveDistanceKm : 0f;

        // Reached-but-unclaimed milestones queued for the pop-up.
        [ShowInInspector, ReadOnly, BoxGroup("Save State"), LabelText("Pending Milestones")]
        private int PendingMilestones => Application.isPlaying ? Milestones.DistanceMilestoneManager.PendingCount : 0;

        // Live view of the milestone currently being driven toward: driven / target km -> gold reward.
        [ShowInInspector, ReadOnly, BoxGroup("Save State"), LabelText("Driving Toward")]
        private string DrivingTowardMilestone
        {
            get
            {
                if (!Application.isPlaying)
                    return "-";

                Milestones.DistanceMilestoneInfo milestone = Milestones.DistanceMilestoneManager.CurrentMilestone;
                return milestone.IsValid
                    ? $"{SaveManager.DistanceDrivenKm} / {milestone.ThresholdKm} km -> {milestone.RewardGold} G"
                    : "-";
            }
        }

        // The oldest pending milestone the pop-up would show / claim next.
        [ShowInInspector, ReadOnly, BoxGroup("Save State"), LabelText("Next To Claim")]
        private string NextToClaim
        {
            get
            {
                if (!Application.isPlaying)
                    return "-";

                Milestones.DistanceMilestoneInfo milestone = Milestones.DistanceMilestoneManager.NextPending;
                return milestone.IsValid ? $"{milestone.ThresholdKm} km -> {milestone.RewardGold} G" : "-";
            }
        }
#endif

        // Claims the oldest pending milestone at its base reward (simulates the pop-up "Claim" / timer).
        [Button]
        private void ClaimNextMilestone()
        {
            if (!Application.isPlaying)
                return;

            Milestones.DistanceMilestoneManager.ClaimNextPending();
        }

        // Claims the oldest pending milestone at the 3X reward (simulates a successful "3X" rewarded ad).
        [Button]
        private void ClaimNextMilestone3X()
        {
            if (!Application.isPlaying)
                return;

            Milestones.DistanceMilestoneManager.ClaimNextPendingWithAdBonus();
        }

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