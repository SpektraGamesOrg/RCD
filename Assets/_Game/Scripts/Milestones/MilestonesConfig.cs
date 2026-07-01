using System.Collections.Generic;
using Newtonsoft.Json;

namespace Milestones
{
    /// <summary>
    /// One explicit entry in the milestone table: the driven distance (km) at which it unlocks and the gold
    /// it grants. Newtonsoft DTO deserialized from the "MilestonesConfig" Clutch flag (snake_case to match
    /// the flag JSON verbatim); never Unity-serialized, so it carries no [Serializable] attribute.
    /// </summary>
    public class MilestoneConfigEntry
    {
        [JsonProperty("threshold_km")] public int ThresholdKm = 1;
        [JsonProperty("reward_gold")] public int RewardGold = 1000;
    }

    /// <summary>
    /// The distance-milestone table + tuning, resolved from the "MilestonesConfig" Clutch flag (remote, with
    /// the ClutchConfig SO fallback). Replaces the old DistanceMilestoneContainer ScriptableObject so the whole
    /// milestone economy is remote-tunable; the player's progress (driven distance, claimed count) still lives
    /// in <see cref="Save.SaveManager"/>. Read through <see cref="DistanceMilestoneManager"/>.
    ///
    /// The table has a finite, explicit head (<see cref="Milestones"/>, e.g. 1/3/6/10/15/20/25/30 km) and then
    /// repeats forever: once past the last explicit milestone, a new one unlocks every <see cref="RepeatStepKm"/>
    /// km for <see cref="RepeatReward"/> gold (GDD: "+5 km from here -> 5000 G"). Field initializers below are
    /// the schema defaults used only if neither Clutch nor the SO fallback has the flag.
    /// </summary>
    public class MilestonesConfig
    {
        [JsonProperty("milestones")]
        public List<MilestoneConfigEntry> Milestones = new List<MilestoneConfigEntry>();

        [JsonProperty("repeat_step_km")]
        public int RepeatStepKm = 5;

        [JsonProperty("repeat_reward")]
        public int RepeatReward = 5000;

        [JsonProperty("reward_ad_multiplier")]
        public int RewardAdMultiplier = 3;

        [JsonProperty("popup_close_seconds")]
        public float PopupCloseSeconds = 6f;

        /// <summary>Number of explicit (non-repeating) milestones at the head of the table.</summary>
        public int ExplicitCount => Milestones?.Count ?? 0;

        /// <summary>
        /// Total driven distance (km) at which the milestone at <paramref name="index"/> unlocks. Explicit
        /// entries return their configured threshold; beyond the table the threshold extends by
        /// <see cref="RepeatStepKm"/> per step. Returns <see cref="int.MaxValue"/> (unreachable, so nothing is
        /// ever granted) for a negative index or an empty table.
        /// </summary>
        public int GetThresholdKm(int index)
        {
            int count = ExplicitCount;
            if (count == 0 || index < 0)
                return int.MaxValue;

            if (index < count)
                return Milestones[index].ThresholdKm;

            int step = RepeatStepKm > 0 ? RepeatStepKm : 1;
            int lastThreshold = Milestones[count - 1].ThresholdKm;
            return lastThreshold + (index - (count - 1)) * step;
        }

        /// <summary>
        /// Gold granted by the milestone at <paramref name="index"/>. Explicit entries return their configured
        /// reward; beyond the table every milestone grants <see cref="RepeatReward"/>.
        /// </summary>
        public int GetReward(int index)
        {
            int count = ExplicitCount;
            if (count == 0 || index < 0)
                return 0;

            if (index < count)
                return Milestones[index].RewardGold;

            return RepeatReward;
        }

        /// <summary>Resolves the full <see cref="DistanceMilestoneInfo"/> for the milestone at <paramref name="index"/>.</summary>
        public DistanceMilestoneInfo GetMilestone(int index)
        {
            return new DistanceMilestoneInfo(index, GetThresholdKm(index), GetReward(index));
        }
    }
}
