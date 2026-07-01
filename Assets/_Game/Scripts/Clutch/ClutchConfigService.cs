using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Save;
using UnityEngine;
using Vehicles;

namespace Clutch
{
    /// <summary>
    /// Resolves Clutch remote-config flags through a cache-first-with-fallback flow and serves the
    /// resolved values to the game. The four startup scenarios all collapse to one rule applied per flag:
    ///
    ///   Clutch success  -> overwrite the prefs cache with the fetched value and use it   (scenarios 1 &amp; 2)
    ///   Clutch failure  -> use the cached value if present                               (scenario 4)
    ///                      else seed the cache from the fallback SO and use that          (scenario 3)
    ///
    /// After init the prefs cache is authoritative, so every <see cref="GetRawJson"/> reads from it.
    /// </summary>
    public class ClutchConfigService : IClutchConfigService
    {
        // Parsed string->int maps, memoized so repeated reads of the same flag don't reparse JSON.
        private readonly Dictionary<string, IReadOnlyDictionary<string, int>> _intMapCache =
            new Dictionary<string, IReadOnlyDictionary<string, int>>();

        // Parsed VehicleConfig map (vehicle key -> entry), memoized; null until first read after init.
        private Dictionary<string, VehicleConfigEntry> _vehicleConfigCache;

        // Parsed typed configs (flag key -> deserialized DTO), memoized so a per-frame GetConfig read does
        // not reparse JSON. Cleared on each init (see Finish) so the next read picks up the resolved value.
        private readonly Dictionary<string, object> _typedConfigCache = new Dictionary<string, object>();

        public bool IsReady { get; private set; }

        public event Action OnConfigUpdated;

        // Hard budget for the whole resolve. On expiry we abort the in-flight Clutch request and resolve
        // from cache/fallback so IsReady is true and the cache is authoritative before this task returns -
        // callers reading at boot always see an already-resolved value (no late IsReady flip / value flip).
        private const int InitializeTimeoutSeconds = 5;

        public async UniTask InitializeAsync(CancellationToken cancellationToken = default)
        {
            if (IsReady)
                return;

            ClutchSDKConfig config = ClutchSDKConfig.Instance;
            if (!config)
            {
                Debug.LogError("[ClutchConfigService] ClutchSDKConfig asset missing; using fallback values only.");
                ResolveFromFailure();
                Finish();
                return;
            }

            string[] keys = ClutchFlagKeys.All;

            Dictionary<string, string> fetched = null;
            using (CancellationTokenSource timeoutCts =
                   CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                timeoutCts.CancelAfter(TimeSpan.FromSeconds(InitializeTimeoutSeconds));
                try
                {
                    // Authenticate with the device id (no Nakama), then evaluate on the Bearer route so
                    // non-public flags are returned. Any auth/fetch failure falls through to cache/SO -
                    // we do NOT fall back to the public route (it returns nothing for non-public flags).
                    string userId = SaveManager.UserId;
                    await ClutchAuth.EnsureValidAccessTokenAsync(
                        config.BaseUrl, config.EnvironmentId, userId, timeoutCts.Token);

                    JObject properties = BuildProperties();

                    // Persist the properties so they're stored on the Clutch user (visible in the dashboard,
                    // used for audience targeting). Fire-and-forget best-effort: it must never block or fail
                    // the config fetch, so it runs detached with its own error handling and is NOT tied to
                    // the init timeout token. Pass a clone - the detached task and the evaluate call below
                    // both use the properties object, and JObject is not safe to share across the two.
                    PersistPropertiesAsync(config.BaseUrl, config.EnvironmentId, ClutchAuth.AccessToken, userId,
                        (JObject)properties.DeepClone()).Forget();

                    fetched = await ClutchClient.EvaluateAuthenticatedAsync(
                        config.BaseUrl, config.EnvironmentId, ClutchAuth.AccessToken, userId, keys, properties, timeoutCts.Token);
                }
                catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested &&
                                                         !cancellationToken.IsCancellationRequested)
                {
                    // Timed out (not an external cancel): fall through and resolve from cache/fallback.
                    Debug.LogError($"[ClutchConfigService] Clutch fetch timed out after {InitializeTimeoutSeconds}s, falling back.");
                }
                catch (OperationCanceledException)
                {
                    // External cancellation: leave IsReady false and propagate.
                    throw;
                }
                catch (Exception e)
                {
                    Debug.LogError($"[ClutchConfigService] Clutch auth/fetch failed, falling back: {e.Message}");
                }
            }

            if (fetched != null && fetched.Count > 0)
            {
                // Success: overwrite the cache with every flag Clutch returned (scenarios 1 & 2).
                // Flags Clutch did NOT return keep their existing cache / fallback (handled below).
                for (int i = 0; i < keys.Length; i++)
                {
                    if (fetched.TryGetValue(keys[i], out string json) && !string.IsNullOrEmpty(json))
                        ClutchConfigCache.Set(keys[i], json);
                    else
                        EnsureCachedOrSeedFallback(keys[i]);
                }

                Debug.Log($"[ClutchConfigService] Fetched {fetched.Count} flag(s) from Clutch and cached to prefs.");
                ClutchConfigCache.Save();
            }
            else
            {
                // Failure or timeout: cache wins if present (scenario 4), else seed from SO fallback (scenario 3).
                ResolveFromFailure();
                bool cached = ClutchConfigCache.HasAny();
                Debug.Log($"[ClutchConfigService] Clutch unavailable; using {(cached ? "cached prefs values" : "SO fallback values")}.");
            }

            // Flush the cache to disk NOW so a subsequent offline launch always finds it. This is the write
            // that makes "online once, then offline" reuse the real Clutch values instead of the SO fallback.
            Finish();
        }

        // Ensures every flag has a usable cached value after a total Clutch failure.
        private void ResolveFromFailure()
        {
            string[] keys = ClutchFlagKeys.All;
            for (int i = 0; i < keys.Length; i++)
                EnsureCachedOrSeedFallback(keys[i]);
        }

        // If the flag is already cached, leave it (scenario 4). Otherwise seed the cache from the
        // fallback SO so the value is available offline on a clean install (scenario 3).
        private void EnsureCachedOrSeedFallback(string flagKey)
        {
            if (ClutchConfigCache.Has(flagKey))
                return;

            if (ClutchConfig.Instance && ClutchConfig.Instance.TryGetFallback(flagKey, out string fallbackJson))
                ClutchConfigCache.Set(flagKey, fallbackJson);
            else
                Debug.LogError($"[ClutchConfigService] No Clutch value, no cache, and no fallback for '{flagKey}'.");
        }

        private void Finish()
        {
            _intMapCache.Clear();
            _vehicleConfigCache = null;
            _typedConfigCache.Clear();
            IsReady = true;
            OnConfigUpdated?.Invoke();
        }

        // Fire-and-forget persist of user properties. Best-effort telemetry: any failure is logged and
        // swallowed so it never affects config resolution or boot. Uses its own 10s timeout, independent of
        // the init timeout (which the flag fetch owns).
        private static async UniTaskVoid PersistPropertiesAsync(
            string baseUrl, string environmentId, string accessToken, string userId, JObject properties)
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                await ClutchClient.SendPropertiesAsync(baseUrl, environmentId, accessToken, userId, properties, cts.Token);
                Debug.Log($"[ClutchConfigService] Persisted {properties.Count} user propertie(s) to Clutch.");
            }
            catch (Exception e)
            {
                Debug.LogError($"[ClutchConfigService] Failed to persist user properties (non-blocking): {e.Message}");
            }
        }

        // Targeting attributes forwarded to Clutch (camelCase, per the project's Clutch schema). lastActive
        // and signUp are intentionally omitted until real session/first-open timestamps exist.
        private static JObject BuildProperties()
        {
            return new JObject
            {
                ["appVersion"] = Application.version,
                ["countryCode"] = ResolveCountryCode(),
                ["language"] = Application.systemLanguage.ToString(),
                ["platform"] = ResolvePlatform(),
                ["userId"] = SaveManager.UserId,
            };
        }

        private static string ResolvePlatform()
        {
#if UNITY_IOS
            return "ios";
#elif UNITY_ANDROID
            return "android";
#else
            return Application.platform.ToString();
#endif
        }

        private static string ResolveCountryCode()
        {
            try
            {
                return RegionInfo.CurrentRegion.TwoLetterISORegionName;
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        // ---------------------------------------------------------------------
        // Reads (cache is authoritative once IsReady)
        // ---------------------------------------------------------------------

        public string GetRawJson(string flagKey)
        {
            return ClutchConfigCache.TryGet(flagKey, out string json) ? json : null;
        }

        public IReadOnlyDictionary<string, int> GetIntMap(string flagKey)
        {
            if (string.IsNullOrEmpty(flagKey))
                return EmptyIntMap;

            if (_intMapCache.TryGetValue(flagKey, out IReadOnlyDictionary<string, int> cached))
                return cached;

            IReadOnlyDictionary<string, int> map = ParseIntMap(GetRawJson(flagKey), flagKey);
            _intMapCache[flagKey] = map;
            return map;
        }

        public int GetInt(string flagKey, string entryKey, int fallback)
        {
            return GetIntMap(flagKey).TryGetValue(entryKey, out int value) ? value : fallback;
        }

        public T GetConfig<T>(string flagKey) where T : class, new()
        {
            if (string.IsNullOrEmpty(flagKey))
                return new T();

            if (_typedConfigCache.TryGetValue(flagKey, out object cached) && cached is T typed)
                return typed;

            T result = ResolveConfig<T>(flagKey);
            _typedConfigCache[flagKey] = result;
            return result;
        }

        // Resolves a typed flag: the resolved Clutch value (prefs cache) first, then the ClutchConfig SO
        // fallback, then a default-constructed T (so a consumer never sees null). Mirrors GetVehicleConfig.
        private static T ResolveConfig<T>(string flagKey) where T : class, new()
        {
            string json = ClutchConfigCache.TryGet(flagKey, out string cachedJson) ? cachedJson : null;
            if (!string.IsNullOrEmpty(json))
            {
                try
                {
                    T parsed = JsonConvert.DeserializeObject<T>(json);
                    if (parsed != null)
                        return parsed;
                }
                catch (JsonException e)
                {
                    Debug.LogError($"[ClutchConfigService] Failed to parse '{flagKey}' as {typeof(T).Name}: {e.Message}");
                }
            }

            // Cache empty / unparseable (e.g. read before InitializeAsync, or a bad remote value): fall back
            // to the authored SO fallback so a usable value is always available.
            if (ClutchConfig.Instance && ClutchConfig.Instance.TryGetConfig(flagKey, out T fallback))
                return fallback;

            return new T();
        }

        private static readonly IReadOnlyDictionary<string, int> EmptyIntMap =
            new Dictionary<string, int>();

        private static IReadOnlyDictionary<string, int> ParseIntMap(string json, string flagKey)
        {
            if (string.IsNullOrEmpty(json))
                return EmptyIntMap;

            try
            {
                Dictionary<string, int> map = JsonConvert.DeserializeObject<Dictionary<string, int>>(json);
                return map ?? (IReadOnlyDictionary<string, int>)EmptyIntMap;
            }
            catch (JsonException e)
            {
                Debug.LogError($"[ClutchConfigService] Failed to parse '{flagKey}' as string->int map: {e.Message}");
                return EmptyIntMap;
            }
        }

        // ---------------------------------------------------------------------
        // VehicleConfig (per-vehicle obtain paths, each with its own value)
        // ---------------------------------------------------------------------

        public ResolvedVehicleConfig GetVehicleConfig(VehicleID id)
        {
            // Clutch keys are the VehicleID enum names (e.g. "GTR_R35"); see the fallback SO + dashboard.
            string vehicleKey = id.ToString();

            // 1) Resolved Clutch value (from the prefs cache, populated on a successful fetch).
            Dictionary<string, VehicleConfigEntry> map = GetVehicleConfigMap();
            if (map != null &&
                map.TryGetValue(vehicleKey, out VehicleConfigEntry entry) &&
                entry != null)
            {
                return ResolvedVehicleConfig.From(entry);
            }

            // 2) Offline fallback: the ClutchConfig SO (single source of truth for defaults, keyed by
            //    VehicleID name). The VehicleContainer no longer carries obtain data.
            if (ClutchConfig.Instance &&
                ClutchConfig.Instance.TryGetVehicleConfigEntry(id, out VehicleConfigEntry fallbackEntry))
            {
                return ResolvedVehicleConfig.From(fallbackEntry);
            }

            // 3) Neither Clutch nor SO has this vehicle: no obtain path.
            Debug.LogError($"[ClutchConfigService] No VehicleConfig for '{vehicleKey}' in Clutch cache or fallback SO.");
            return ResolvedVehicleConfig.None;
        }

        // Parses (and memoizes) the VehicleConfig flag into a vehicle-key -> entry map.
        private Dictionary<string, VehicleConfigEntry> GetVehicleConfigMap()
        {
            if (_vehicleConfigCache != null)
                return _vehicleConfigCache;

            string json = GetRawJson(ClutchFlagKeys.VehicleConfig);
            if (string.IsNullOrEmpty(json))
            {
                _vehicleConfigCache = new Dictionary<string, VehicleConfigEntry>();
                return _vehicleConfigCache;
            }

            try
            {
                _vehicleConfigCache =
                    JsonConvert.DeserializeObject<Dictionary<string, VehicleConfigEntry>>(json)
                    ?? new Dictionary<string, VehicleConfigEntry>();
            }
            catch (JsonException e)
            {
                Debug.LogError($"[ClutchConfigService] Failed to parse '{ClutchFlagKeys.VehicleConfig}': {e.Message}");
                _vehicleConfigCache = new Dictionary<string, VehicleConfigEntry>();
            }

            return _vehicleConfigCache;
        }
    }
}
