using System;
using System.Collections.Generic;

namespace Save
{
    /// <summary>
    /// Serializable container holding the cooldown state for every gold point the player has collected.
    /// Stored as a single JSON blob (one PlayerPrefs key) so all gold cooldowns persist together.
    /// JsonUtility needs a wrapper class like this to serialize a List.
    /// </summary>
    [Serializable]
    public class GoldCooldownList
    {
        public List<GoldCooldownData> golds = new List<GoldCooldownData>();
    }
}
