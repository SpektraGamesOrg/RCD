using System;
using Clutch;
using Newtonsoft.Json;
using SpektraGames.SpektraUtilities.Runtime;
using UnityEngine;

namespace Ads
{
    /// <summary>
    /// Bridges the Clutch remote-config layer to a typed <see cref="AdConfig"/>. Reads the raw "AdConfig"
    /// flag JSON via <see cref="IClutchConfigService.GetRawJson"/> and deserializes it in one pass with
    /// Newtonsoft into the <see cref="AdConfig"/> POCO.
    ///
    /// Why not <see cref="IClutchConfigService.GetIntMap"/>: that path does
    /// <c>DeserializeObject&lt;Dictionary&lt;string,int&gt;&gt;</c> and THROWS on the RCD schema, which mixes
    /// ints, bools, a string list, and a nested <c>rewarded_multipliers</c> object. Deserializing the whole
    /// flag into <see cref="AdConfig"/> (SnakeCaseNamingStrategy) handles every type correctly and keeps the
    /// int-map API untouched for its existing consumers.
    /// </summary>
    public sealed class AdConfigProvider : IAdConfigProvider
    {
        private static readonly InfoLogger Logger = new InfoLogger("AdConfigProvider", "yellow");

        private readonly IClutchConfigService _clutch;
        private bool _subscribed;

        // Never null: seeded with an empty config so accessors return doc defaults before/without a parse.
        public AdConfig Current { get; private set; } = new AdConfig();

        public event Action OnAdConfigChanged;

        public AdConfigProvider(IClutchConfigService clutch)
        {
            _clutch = clutch;
        }

        public void Initialize()
        {
            if (_clutch == null)
            {
                Logger.LogError("Initialize called with no Clutch service; using AdConfig defaults.");
                return;
            }

            if (!_subscribed)
            {
                // OnConfigUpdated fires once, in ClutchConfigService.Finish(). Subscribe so that if we
                // initialize before Clutch resolves, the parse still runs when it does.
                _clutch.OnConfigUpdated += Reparse;
                _subscribed = true;
            }

            // If Clutch already resolved (boot order runs the Clutch init before the ad stack), parse now
            // so Current is populated synchronously for the first reader.
            if (_clutch.IsReady)
                Reparse();
        }

        // Re-reads and re-parses the flag, then notifies. In this codebase this runs at most once after boot.
        private void Reparse()
        {
            string json = _clutch.GetRawJson(ClutchFlagKeys.AdConfig);
            if (string.IsNullOrEmpty(json))
            {
                // No Clutch value, no cache, no fallback: keep the default-yielding empty config.
                Logger.Log("No AdConfig flag resolved; using AdConfig defaults.");
                OnAdConfigChanged.InvokeSafe();
                return;
            }

            try
            {
                AdConfig parsed = JsonConvert.DeserializeObject<AdConfig>(json);
                // DeserializeObject can return null for the literal "null"; guard so Current stays non-null.
                Current = parsed ?? new AdConfig();
                Logger.Log("Parsed AdConfig from Clutch flag.");
            }
            catch (JsonException e)
            {
                // Malformed remote value must not crash boot; fall back to defaults.
                Logger.LogError($"Failed to parse AdConfig flag; using defaults: {e.Message}");
                Current = new AdConfig();
            }

            OnAdConfigChanged.InvokeSafe();
        }
    }
}
