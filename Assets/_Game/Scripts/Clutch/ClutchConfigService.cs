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
            ClutchConfigCache.Save();
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
            IsReady = true;
            OnConfigUpdated?.Invoke();
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

        public ResolvedVehicleConfig GetVehicleConfig(VehicleID id, VehicleObtainType fallbackType, int fallbackAmount)
        {
            // Clutch keys are the VehicleID enum names (e.g. "GTR_R35"); see the fallback SO + dashboard.
            string vehicleKey = id.ToString();

            Dictionary<string, VehicleConfigEntry> map = GetVehicleConfigMap();
            if (map != null &&
                map.TryGetValue(vehicleKey, out VehicleConfigEntry entry) &&
                entry != null)
            {
                // Clutch entry present: the paths it declares (by key presence) and their per-path values
                // are authoritative. Absent path values default to 0.
                return new ResolvedVehicleConfig(
                    entry.ToObtainType(),
                    entry.by_gold ?? 0,
                    entry.by_watch_ads ?? 0,
                    entry.distance_km ?? 0);
            }

            // No Clutch entry: keep the serialized type, and use the single serialized amount for whichever
            // path(s) it enables (the SO has no per-path values - this preserves pre-migration behavior).
            return new ResolvedVehicleConfig(fallbackType, fallbackAmount, fallbackAmount, fallbackAmount);
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
