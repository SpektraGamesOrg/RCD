using System;

namespace Save
{
    /// <summary>
    /// Serializable, persisted cooldown state for a single gold collection point.
    /// Plain class (not MonoBehaviour / ScriptableObject) so it can be stored as JSON.
    /// A gold becomes passive when collected; <see cref="collectedUnixSeconds"/> records when,
    /// so it can reactivate after the configured cooldown even across app sessions.
    /// </summary>
    [Serializable]
    public class GoldCooldownData
    {
        public string id;
        public long collectedUnixSeconds;

        public GoldCooldownData() { }

        public GoldCooldownData(string id, long collectedUnixSeconds)
        {
            this.id = id;
            this.collectedUnixSeconds = collectedUnixSeconds;
        }
    }
}
