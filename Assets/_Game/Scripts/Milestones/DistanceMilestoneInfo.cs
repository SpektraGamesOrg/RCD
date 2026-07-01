namespace Milestones
{
    /// <summary>
    /// Immutable description of a single distance milestone: its 0-based <see cref="Index"/>, the total
    /// driven distance (km) at which it unlocks (<see cref="ThresholdKm"/>) and the soft-currency (gold)
    /// reward it grants (<see cref="RewardGold"/>). Pure data resolved from
    /// <see cref="MilestonesConfig"/>; it carries no behaviour.
    /// </summary>
    public readonly struct DistanceMilestoneInfo
    {
        /// <summary>0-based position in the milestone sequence (also equals the claimed count needed to reach it).</summary>
        public readonly int Index;

        /// <summary>Total driven distance, in whole kilometres, at which this milestone unlocks.</summary>
        public readonly int ThresholdKm;

        /// <summary>Gold granted when this milestone is reached.</summary>
        public readonly int RewardGold;

        public DistanceMilestoneInfo(int index, int thresholdKm, int rewardGold)
        {
            Index = index;
            ThresholdKm = thresholdKm;
            RewardGold = rewardGold;
        }

        /// <summary>
        /// True for a real milestone. The default struct (e.g. when the table is unavailable) has a zero
        /// threshold, which no real milestone uses (the first is 1 km), so it reads as invalid.
        /// </summary>
        public bool IsValid => ThresholdKm > 0;
    }
}
