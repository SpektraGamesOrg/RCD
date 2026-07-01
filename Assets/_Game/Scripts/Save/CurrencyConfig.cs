using Newtonsoft.Json;

namespace Save
{
    /// <summary>
    /// Remote soft-currency tuning, resolved from the "CurrencyConfig" Clutch flag (remote, with the
    /// ClutchConfig SO fallback). Newtonsoft DTO (snake_case to match the flag JSON); never Unity-serialized.
    /// Read through <see cref="Clutch.ClutchConfigResolver"/>. Field initializers are the schema defaults used
    /// only when neither Clutch nor the SO fallback has the flag.
    /// </summary>
    public class CurrencyConfig
    {
        /// <summary>Gold balance granted on a fresh install (before the player has earned or spent anything).</summary>
        [JsonProperty("default_gold")]
        public int DefaultGold = 150;
    }
}
