using Newtonsoft.Json;

namespace Gold
{
    /// <summary>
    /// Remote tuning for the scattered "free gold" pickups, resolved from the "FreeGoldConfig" Clutch flag
    /// (remote, with the ClutchConfig SO fallback). Newtonsoft DTO (snake_case to match the flag JSON); never
    /// Unity-serialized. Read through <see cref="ClutchConfigResolver"/>. Field initializers are the schema
    /// defaults used only when neither Clutch nor the SO fallback has the flag.
    /// </summary>
    public class FreeGoldConfig
    {
        /// <summary>
        /// Seconds the "CLAIM Nx" ad-bonus pop-up stays up before auto-dismissing (the player keeps the base
        /// reward). Passed to the shared ClaimGoldMultiplierWithAdsOverlay as its close time.
        /// </summary>
        [JsonProperty("claim_popup_seconds")]
        public float ClaimPopupSeconds = 5f;
    }
}
