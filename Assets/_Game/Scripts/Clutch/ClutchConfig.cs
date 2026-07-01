using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using SpektraGames.SpektraUtilities.Runtime;
using UnityEngine;
using Vehicles;
#if UNITY_EDITOR
using System.Linq;
using System.Net.Http;
using System.Text;
using Newtonsoft.Json.Linq;
using Sirenix.OdinInspector;
using UnityEditor;
#endif

namespace Clutch
{
    /// <summary>
    /// Offline FALLBACK values for Clutch flags, authored in the editor and refreshable from live Clutch
    /// via the "Update from Clutch" buttons on this asset (or the matching Tools/Clutch menu items).
    /// A <see cref="SingletonScriptableObject{T}"/> so <see cref="Instance"/> resolves from Resources at
    /// runtime. Used only when Clutch fails AND the player has no cached value yet (clean install,
    /// offline) — see ClutchConfigService for the full resolution order.
    ///
    /// Each entry stores the flag's JSON verbatim (same shape Clutch returns) so the runtime path
    /// deserializes fallbacks identically to remote values. Generic by design: add a flag by adding an
    /// entry, no code change.
    /// </summary>
    [CreateAssetMenu(fileName = "ClutchConfig", menuName = "Clutch/Clutch Config (Fallback)")]
    public class ClutchConfig : SingletonScriptableObject<ClutchConfig>
    {
        [Serializable]
        public class ClutchFlagFallback
        {
            [Tooltip("Clutch flag key, e.g. \"VehicleConfig\" or \"AdConfig\".")]
            public string key;

            [Tooltip("Fallback value as JSON, matching the exact shape Clutch returns for this flag. " +
                     "Stored indented for easy editing; use the 'Format JSON' button to re-indent after edits.")]
            [TextArea(4, 20)]
            public string fallbackJson;
        }

        [SerializeField]
        [Tooltip("One entry per Clutch flag the game reads.")]
        private List<ClutchFlagFallback> fallbacks = new List<ClutchFlagFallback>();

        public IReadOnlyList<ClutchFlagFallback> Fallbacks => fallbacks;

        /// <summary>Returns the authored fallback JSON for a flag, or false when none is configured.</summary>
        public bool TryGetFallback(string flagKey, out string json)
        {
            json = null;
            if (string.IsNullOrEmpty(flagKey))
                return false;

            for (int i = 0; i < fallbacks.Count; i++)
            {
                if (fallbacks[i] != null && fallbacks[i].key == flagKey)
                {
                    json = fallbacks[i].fallbackJson;
                    return !string.IsNullOrEmpty(json);
                }
            }

            return false;
        }

        // Parsed VehicleConfig fallback (vehicle key -> entry), memoized. Null until first read.
        [NonSerialized] private Dictionary<string, VehicleConfigEntry> _vehicleConfigFallback;

        /// <summary>
        /// The offline fallback obtain config for a vehicle, parsed from this asset's "VehicleConfig"
        /// fallback JSON (keyed by <see cref="VehicleID"/> enum name). Returns false when the vehicle has no
        /// fallback entry. This is the single fallback source once the VehicleContainer no longer carries
        /// obtain data - both the runtime resolver and the synchronous boot starter-grant read it here.
        /// </summary>
        public bool TryGetVehicleConfigEntry(VehicleID id, out VehicleConfigEntry entry)
        {
            entry = null;
            Dictionary<string, VehicleConfigEntry> map = GetVehicleConfigFallbackMap();
            return map != null && map.TryGetValue(id.ToString(), out entry) && entry != null;
        }

        private Dictionary<string, VehicleConfigEntry> GetVehicleConfigFallbackMap()
        {
            if (_vehicleConfigFallback != null)
                return _vehicleConfigFallback;

            if (!TryGetFallback(ClutchFlagKeys.VehicleConfig, out string json) || string.IsNullOrEmpty(json))
            {
                _vehicleConfigFallback = new Dictionary<string, VehicleConfigEntry>();
                return _vehicleConfigFallback;
            }

            try
            {
                _vehicleConfigFallback =
                    JsonConvert.DeserializeObject<Dictionary<string, VehicleConfigEntry>>(json)
                    ?? new Dictionary<string, VehicleConfigEntry>();
            }
            catch (JsonException e)
            {
                Debug.LogError($"[ClutchConfig] VehicleConfig fallback is not valid JSON: {e.Message}");
                _vehicleConfigFallback = new Dictionary<string, VehicleConfigEntry>();
            }

            return _vehicleConfigFallback;
        }

        /// <summary>
        /// Deserializes a flag's authored fallback JSON into <typeparamref name="T"/>. Returns false when the
        /// flag has no fallback entry or the JSON is invalid. The single offline source for the typed config
        /// flags (MilestonesConfig, FreeGoldConfig, CurrencyConfig): the runtime service reads it when the
        /// prefs cache is empty, and static boot callers read it directly (via <see cref="ClutchConfigResolver"/>)
        /// before the DI service exists - mirroring <see cref="TryGetVehicleConfigEntry"/>.
        /// </summary>
        public bool TryGetConfig<T>(string flagKey, out T config) where T : class
        {
            config = null;
            if (!TryGetFallback(flagKey, out string json) || string.IsNullOrEmpty(json))
                return false;

            try
            {
                config = JsonConvert.DeserializeObject<T>(json);
                return config != null;
            }
            catch (JsonException e)
            {
                Debug.LogError($"[ClutchConfig] '{flagKey}' fallback is not valid JSON for {typeof(T).Name}: {e.Message}");
                return false;
            }
        }

#if UNITY_EDITOR
        private const int EditorFetchTimeoutSeconds = 15;

        /// <summary>
        /// Editor-only: writes (or replaces) a flag's fallback JSON. Caller is responsible for
        /// SetDirty/SaveAssets.
        /// </summary>
        public void SetFallbackEditor(string flagKey, string json)
        {
            if (string.IsNullOrEmpty(flagKey))
                return;

            // Invalidate the parsed VehicleConfig memo so the next read re-parses the updated JSON.
            _vehicleConfigFallback = null;

            for (int i = 0; i < fallbacks.Count; i++)
            {
                if (fallbacks[i] != null && fallbacks[i].key == flagKey)
                {
                    fallbacks[i].fallbackJson = json;
                    return;
                }
            }

            fallbacks.Add(new ClutchFlagFallback { key = flagKey, fallbackJson = json });
        }

        // ---------------------------------------------------------------------
        // Update from Clutch (editor-only, inspector buttons)
        // ---------------------------------------------------------------------
        // Click a button to fetch the live flags for an environment and write them into THIS ASSET's
        // fallbacks only. This is an EDITOR authoring step: it updates the offline-fallback config, it does
        // NOT touch PlayerPrefs (the runtime prefs cache is owned by ClutchConfigService at play time).
        // Uses the trusted SERVER route (X-API-Key, the same route HRP's ServerSideClutch uses) when an
        // editor API key is configured on ClutchSDKConfig, else the public route. The whole block is
        // #if UNITY_EDITOR so the API key never compiles into a player build.

        [Button("Update from Clutch (Dev)", ButtonSizes.Medium), PropertyOrder(-1)]
        private void UpdateFromClutchDev() => UpdateFromClutchEditor(useDev: true);

        [Button("Update from Clutch (Prod)", ButtonSizes.Medium), PropertyOrder(-1)]
        private void UpdateFromClutchProd() => UpdateFromClutchEditor(useDev: false);

        /// <summary>
        /// Editor-only: re-indents every fallback entry's JSON in place so it stays readable after manual
        /// edits. Invalid JSON is left untouched and logged, so a typo is caught here (not silently at
        /// runtime). Whitespace does not affect runtime deserialization.
        /// </summary>
        [Button("Format JSON", ButtonSizes.Medium), PropertyOrder(-1)]
        private void FormatFallbackJson()
        {
            Undo.RecordObject(this, "Format Clutch Config JSON");
            int formatted = 0;

            for (int i = 0; i < fallbacks.Count; i++)
            {
                ClutchFlagFallback f = fallbacks[i];
                if (f == null || string.IsNullOrEmpty(f.fallbackJson))
                    continue;

                try
                {
                    f.fallbackJson = Newtonsoft.Json.Linq.JToken.Parse(f.fallbackJson)
                        .ToString(Newtonsoft.Json.Formatting.Indented);
                    formatted++;
                }
                catch (JsonException e)
                {
                    Debug.LogError($"[ClutchConfig] '{f.key}' is not valid JSON, left unchanged: {e.Message}");
                }
            }

            _vehicleConfigFallback = null; // re-parse on next read
            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssets();
            Debug.Log($"[ClutchConfig] Formatted {formatted} fallback entr(ies).");
        }

        // Pretty-prints a JSON string; returns it unchanged if it can't be parsed (so a Clutch response
        // that isn't strictly JSON is never lost).
        private static string IndentJson(string json)
        {
            if (string.IsNullOrEmpty(json))
                return json;

            try
            {
                return Newtonsoft.Json.Linq.JToken.Parse(json).ToString(Newtonsoft.Json.Formatting.Indented);
            }
            catch (JsonException)
            {
                return json;
            }
        }

        /// <summary>
        /// Editor-only: fetches the configured flags from Clutch for the chosen environment and writes them
        /// into this asset's fallbacks (the offline config). Does NOT write PlayerPrefs - the runtime cache
        /// is populated by ClutchConfigService at play time. Also callable from the Tools/Clutch menu.
        /// </summary>
        public void UpdateFromClutchEditor(bool useDev)
        {
            ClutchSDKConfig sdk = ClutchSDKConfig.Instance;
            if (!sdk)
            {
                Debug.LogError("[ClutchConfig] ClutchSDKConfig asset not found in Resources.");
                return;
            }

            string baseUrl = useDev ? sdk.DevBaseUrl : sdk.ProdBaseUrl;
            string envId = useDev ? sdk.DevEnvironmentId : sdk.ProdEnvironmentId;
            string apiKey = useDev ? sdk.DevApiKey : sdk.ProdApiKey;
            string envLabel = useDev ? "Dev" : "Prod";

            if (string.IsNullOrEmpty(baseUrl) || string.IsNullOrEmpty(envId))
            {
                Debug.LogError($"[ClutchConfig] {envLabel} base URL / environment id is empty in ClutchSDKConfig.");
                return;
            }

            string[] keys = ClutchFlagKeys.All;
            bool useServerRoute = !string.IsNullOrEmpty(apiKey);
            string route = useServerRoute ? "server (X-API-Key)" : "public";

            Dictionary<string, string> fetched;
            try
            {
                EditorUtility.DisplayProgressBar("Clutch", $"Fetching flags from {envLabel} ({route})...", 0.5f);
                fetched = Fetch(baseUrl, envId, useServerRoute ? apiKey : null, keys);
            }
            catch (Exception e)
            {
                EditorUtility.ClearProgressBar();
                Debug.LogError($"[ClutchConfig] Fetch from {envLabel} ({route}) failed: {e.Message}");
                return;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            if (fetched.Count == 0)
            {
                Debug.LogError($"[ClutchConfig] {envLabel} ({route}) returned no flags for keys: {string.Join(", ", keys)}. " +
                               (useServerRoute
                                   ? "Check the flags exist/are deployed and the API key is correct."
                                   : "Check the flags are marked public, or set an editor API key for the server route."));
                return;
            }

            Undo.RecordObject(this, "Update Clutch Config");
            foreach (KeyValuePair<string, string> kvp in fetched)
            {
                // Store indented so the value is comfortable to read/edit in the Inspector. Runtime
                // deserialization is unaffected by whitespace.
                SetFallbackEditor(kvp.Key, IndentJson(kvp.Value));
                Debug.Log($"[ClutchConfig] {envLabel} {kvp.Key} = {kvp.Value}");
            }

            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssets();

            string[] missing = keys.Where(k => !fetched.ContainsKey(k)).ToArray();
            if (missing.Length > 0)
                Debug.LogError($"[ClutchConfig] {envLabel} did not return: {string.Join(", ", missing)} (kept existing fallback).");

            Debug.Log($"[ClutchConfig] Wrote {fetched.Count} flag(s) from {envLabel} ({route}) into the fallback SO.");
        }

        // Synchronous evaluate-batch. Server route (X-API-Key) when apiKey is non-null, else public route.
        // Returns key -> flag JSON string from the shared { "features": { ... } } response.
        private static Dictionary<string, string> Fetch(string baseUrl, string envId, string apiKey, IReadOnlyList<string> keys)
        {
            string segment = string.IsNullOrEmpty(apiKey) ? "client" : "server";
            string url = $"{baseUrl}/v1/{segment}/environments/{envId}/features/evaluate-batch";
            JObject body = new JObject { ["keys"] = new JArray(keys) };

            using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(body.ToString(Formatting.None), Encoding.UTF8, "application/json"),
            };
            if (!string.IsNullOrEmpty(apiKey))
                request.Headers.Add("X-API-Key", apiKey);
            // REQUIRED: the Clutch edge/WAF returns 403 for requests with no User-Agent. HttpClient sends
            // none by default, so set one explicitly (mirrors HRP's ClutchSDK "ClutchSDK-Unity/1.0").
            request.Headers.Add("User-Agent", "ClutchSDK-Unity/1.0");

            using HttpClient client = new HttpClient { Timeout = TimeSpan.FromSeconds(EditorFetchTimeoutSeconds) };
            HttpResponseMessage response = client.SendAsync(request).GetAwaiter().GetResult();
            string responseText = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();

            if (!response.IsSuccessStatusCode)
                throw new Exception($"{(int)response.StatusCode} {response.ReasonPhrase}: {responseText}");

            Dictionary<string, string> result = new Dictionary<string, string>();
            if (string.IsNullOrEmpty(responseText))
                return result;

            JObject parsed = JObject.Parse(responseText);
            if (parsed["features"] is JObject features)
            {
                foreach (KeyValuePair<string, JToken> kvp in features)
                {
                    if (kvp.Value == null || kvp.Value.Type == JTokenType.Null)
                        continue;

                    result[kvp.Key] = kvp.Value.ToString(Formatting.None);
                }
            }

            return result;
        }
#endif
    }
}
