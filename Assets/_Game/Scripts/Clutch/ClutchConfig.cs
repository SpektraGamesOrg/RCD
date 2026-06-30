using System;
using System.Collections.Generic;
using SpektraGames.SpektraUtilities.Runtime;
using UnityEngine;
#if UNITY_EDITOR
using System.Linq;
using System.Net.Http;
using System.Text;
using Newtonsoft.Json;
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

            [Tooltip("Fallback value as JSON, matching the exact shape Clutch returns for this flag.")]
            [TextArea(2, 8)]
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
        // Click a button to fetch the live flags for an environment and write them into this asset's
        // fallbacks + the PlayerPrefs cache. Uses the trusted SERVER route (X-API-Key, the same route HRP's
        // ServerSideClutch uses) when an editor API key is configured on ClutchSDKConfig, else the public
        // route. The whole block is #if UNITY_EDITOR so the API key never compiles into a player build.

        [Button("Update from Clutch (Dev)", ButtonSizes.Medium), PropertyOrder(-1)]
        private void UpdateFromClutchDev() => UpdateFromClutchEditor(useDev: true);

        [Button("Update from Clutch (Prod)", ButtonSizes.Medium), PropertyOrder(-1)]
        private void UpdateFromClutchProd() => UpdateFromClutchEditor(useDev: false);

        /// <summary>
        /// Editor-only: fetches the configured flags from Clutch for the chosen environment and writes them
        /// into this asset's fallbacks and the PlayerPrefs cache. Also callable from the Tools/Clutch menu.
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
                SetFallbackEditor(kvp.Key, kvp.Value);
                WriteToCache(kvp.Key, kvp.Value);
                Debug.Log($"[ClutchConfig] {envLabel} {kvp.Key} = {kvp.Value}");
            }

            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssets();
            PlayerPrefs.Save();

            string[] missing = keys.Where(k => !fetched.ContainsKey(k)).ToArray();
            if (missing.Length > 0)
                Debug.LogError($"[ClutchConfig] {envLabel} did not return: {string.Join(", ", missing)} (kept existing fallback).");

            Debug.Log($"[ClutchConfig] Wrote {fetched.Count} flag(s) from {envLabel} ({route}) into fallbacks + PlayerPrefs cache.");
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

        // Mirrors ClutchConfigCache's combined-blob layout (one PlayerPrefs key, key -> json-string).
        private static void WriteToCache(string flagKey, string json)
        {
            string raw = PlayerPrefs.GetString(Save.SaveKeys.ClutchConfig, string.Empty);
            JObject blob;
            try
            {
                blob = string.IsNullOrEmpty(raw) ? new JObject() : JObject.Parse(raw);
            }
            catch (JsonException)
            {
                blob = new JObject();
            }

            blob[flagKey] = json ?? string.Empty;
            PlayerPrefs.SetString(Save.SaveKeys.ClutchConfig, blob.ToString(Formatting.None));
        }
#endif
    }
}
