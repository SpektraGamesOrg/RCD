using System;

namespace Gold
{
    /// <summary>
    /// Data passed to the UI layer when an active gold is collected, so the "CLAIM Nx" popup can be
    /// shown. The base reward has already been granted; <see cref="Bonus"/> is the extra amount granted
    /// only if the player watches the rewarded ad, via <see cref="OnClaimed"/>.
    /// </summary>
    public readonly struct GoldClaimRequest
    {
        /// <summary>Coins already granted on pickup (shown for context).</summary>
        public readonly int BaseReward;

        /// <summary>Multiplier offered (e.g. 5 -> "CLAIM 5X"). For display.</summary>
        public readonly int Multiplier;

        /// <summary>Extra coins granted on top of the base if the ad is watched.</summary>
        public readonly int Bonus;

        /// <summary>Invoked (with <see cref="Bonus"/>) only when the player successfully claims via ad.</summary>
        public readonly Action<int> OnClaimed;

        public GoldClaimRequest(int baseReward, int multiplier, int bonus, Action<int> onClaimed)
        {
            BaseReward = baseReward;
            Multiplier = multiplier;
            Bonus = bonus;
            OnClaimed = onClaimed;
        }
    }
}
