using System;
using System.Collections.Generic;
using SpektraGames.SpektraUtilities.Runtime;
using UnityEngine;

namespace Milestones
{
    /// <summary>
    /// One explicit entry in the milestone table: the driven distance (km) at which it unlocks and the
    /// gold it grants. Mirrors the <see cref="Vehicles.VehicleEntry"/> style ([field: SerializeField]
    /// auto-properties) so the data stays read-only at runtime but editable in the inspector.
    /// </summary>
    [Serializable]
    public class MilestoneEntry
    {
        [field: SerializeField, Min(1)] public int ThresholdKm { get; private set; } = 1;
        [field: SerializeField, Min(0)] public int RewardGold { get; private set; } = 1000;
    }

    /// <summary>
    /// Singleton ScriptableObject catalog of the distance milestone table (the game's main progression
    /// driver per the GDD). Static design-time data only - the player's progress (total distance and how
    /// many milestones have been claimed) lives in the save system (<see cref="Save.SaveManager"/>).
    ///
    /// The table has a finite, explicit head (<see cref="milestones"/>, e.g. 1/3/6/10/15/20/25/30 km) and
    /// then repeats forever: once past the last explicit milestone, a new one unlocks every
    /// <see cref="repeatStepKm"/> km for <see cref="repeatReward"/> gold (GDD: "+5 km from here -> 5000 G").
    ///
    /// Loaded automatically via <see cref="SingletonScriptableObject{T}"/>, which calls
    /// Resources.Load("DistanceMilestoneContainer"). The asset MUST be named "DistanceMilestoneContainer"
    /// and live in a Resources folder (e.g. Assets/_Game/Data/Resources/DistanceMilestoneContainer.asset).
    /// Consumed by <see cref="DistanceMilestoneManager"/>.
    /// </summary>
    [CreateAssetMenu(fileName = "DistanceMilestoneContainer", menuName = "DRIVE01/Distance Milestone Container")]
    public class DistanceMilestoneContainer : SingletonScriptableObject<DistanceMilestoneContainer>
    {
        [Tooltip("Explicit head of the milestone table (the non-repeating early milestones). Thresholds " +
                 "MUST be strictly ascending.")]
        [SerializeField]
        private List<MilestoneEntry> milestones = new List<MilestoneEntry>();

        [Tooltip("Once past the last explicit milestone, a new milestone unlocks every this many km.")]
        [SerializeField, Min(1)]
        private int repeatStepKm = 5;

        [Tooltip("Gold granted by every repeating (post-table) milestone.")]
        [SerializeField, Min(0)]
        private int repeatReward = 5000;

        [Tooltip("Rewarded-ad multiplier offered on the MILESTONE COMPLETED pop-up (GDD: 3X). Total via " +
                 "ad = base * this; the ad grants the extra (this - 1) * base on top of the base reward.")]
        [SerializeField, Min(1)]
        private int rewardAdMultiplier = 3;

        /// <summary>Number of explicit (non-repeating) milestones at the head of the table.</summary>
        public int ExplicitCount => milestones?.Count ?? 0;

        /// <summary>Rewarded-ad multiplier offered on milestone completion (e.g. 3 -> "3X").</summary>
        public int RewardAdMultiplier => rewardAdMultiplier;

        /// <summary>
        /// Total driven distance (km) at which the milestone at <paramref name="index"/> unlocks. Explicit
        /// entries return their configured threshold; beyond the table the threshold extends by
        /// <see cref="repeatStepKm"/> per step. Returns <see cref="int.MaxValue"/> (i.e. unreachable, so
        /// nothing is ever granted) for a negative index or an empty table.
        /// </summary>
        public int GetThresholdKm(int index)
        {
            int count = ExplicitCount;
            if (count == 0 || index < 0)
                return int.MaxValue;

            if (index < count)
                return milestones[index].ThresholdKm;

            int step = repeatStepKm > 0 ? repeatStepKm : 1;
            int lastThreshold = milestones[count - 1].ThresholdKm;
            return lastThreshold + (index - (count - 1)) * step;
        }

        /// <summary>
        /// Gold granted by the milestone at <paramref name="index"/>. Explicit entries return their
        /// configured reward; beyond the table every milestone grants <see cref="repeatReward"/>.
        /// </summary>
        public int GetReward(int index)
        {
            int count = ExplicitCount;
            if (count == 0 || index < 0)
                return 0;

            if (index < count)
                return milestones[index].RewardGold;

            return repeatReward;
        }

        /// <summary>Resolves the full <see cref="DistanceMilestoneInfo"/> for the milestone at <paramref name="index"/>.</summary>
        public DistanceMilestoneInfo GetMilestone(int index)
        {
            return new DistanceMilestoneInfo(index, GetThresholdKm(index), GetReward(index));
        }

#if UNITY_EDITOR
        // Editor-only sanity check: the manager grants milestones in a while-loop over ascending
        // thresholds, so a non-ascending head (or a zero step) would misbehave. Surface it early.
        private void OnValidate()
        {
            for (int i = 1; i < ExplicitCount; i++)
            {
                if (milestones[i].ThresholdKm <= milestones[i - 1].ThresholdKm)
                {
                    Debug.LogError($"[DistanceMilestoneContainer] milestones[{i}] threshold " +
                                   $"({milestones[i].ThresholdKm} km) is not greater than the previous " +
                                   $"({milestones[i - 1].ThresholdKm} km). Thresholds must be strictly ascending.", this);
                }
            }

            if (ExplicitCount > 0 && repeatStepKm > 0)
            {
                int last = milestones[ExplicitCount - 1].ThresholdKm;
                if (last + repeatStepKm <= last)
                    Debug.LogError("[DistanceMilestoneContainer] repeatStepKm must be positive.", this);
            }
        }
#endif
    }
}
