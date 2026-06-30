using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using Vehicles;

namespace _Game.Scripts.Clutch.Editor
{
    /// <summary>
    /// Editor guardrail for the Clutch "VehicleConfig" remote flag. The runtime resolves a vehicle's obtain
    /// config by looking it up in VehicleConfig keyed by the <see cref="VehicleID"/> enum name
    /// (see ClutchConfigService.GetVehicleConfig); a missing or malformed entry silently falls back to the
    /// serialized values. This validator checks the <see cref="global::Clutch.ClutchConfig"/> fallback
    /// asset against the live vehicle roster and reports:
    ///   * fallback entries that declare no obtain path (no by_gold/by_watch_ads/distance_km and not free),
    ///     mix free with path values, or carry a non-positive path value,
    ///   * fallback keys that match no vehicle in the container (dead keys / typos),
    ///   * roster vehicles that have no fallback entry (offline they use the serialized obtain config).
    /// Editor-only; uses Debug.LogError per project rules.
    /// </summary>
    public static class ClutchVehicleConfigValidator
    {
        [MenuItem("Tools/Clutch/Validate Vehicle Config")]
        public static void Validate()
        {
            VehicleContainer container = VehicleContainer.Instance;
            if (!container)
            {
                Debug.LogError("[ClutchVehicleConfigValidator] VehicleContainer asset not found in Resources.");
                return;
            }

            global::Clutch.ClutchConfig fallbackConfig = global::Clutch.ClutchConfig.Instance;
            if (!fallbackConfig)
            {
                Debug.LogError("[ClutchVehicleConfigValidator] ClutchConfig (fallback) asset not found in Resources.");
                return;
            }

            // Parse the VehicleConfig fallback into key -> entry.
            Dictionary<string, global::Clutch.VehicleConfigEntry> fallbackMap = null;
            if (fallbackConfig.TryGetFallback(global::Clutch.ClutchFlagKeys.VehicleConfig, out string vehicleConfigJson))
            {
                try
                {
                    fallbackMap = JsonConvert.DeserializeObject<Dictionary<string, global::Clutch.VehicleConfigEntry>>(vehicleConfigJson);
                }
                catch (JsonException e)
                {
                    Debug.LogError($"[ClutchVehicleConfigValidator] VehicleConfig fallback is not valid JSON: {e.Message}");
                    return;
                }
            }
            else
            {
                Debug.LogError("[ClutchVehicleConfigValidator] No VehicleConfig fallback found in ClutchConfig; all cars fall back to serialized obtain config offline.");
                return;
            }

            if (fallbackMap == null)
            {
                Debug.LogError("[ClutchVehicleConfigValidator] VehicleConfig fallback parsed to null.");
                return;
            }

            int problems = 0;

            // 1) Every fallback entry must declare at least one obtain path (a per-path value or free=true);
            //    free is exclusive (a free car must not also carry path values); present values must be > 0.
            foreach (KeyValuePair<string, global::Clutch.VehicleConfigEntry> kvp in fallbackMap)
            {
                global::Clutch.VehicleConfigEntry entry = kvp.Value;
                if (entry == null)
                {
                    Debug.LogError($"[ClutchVehicleConfigValidator] VehicleConfig key '{kvp.Key}' has a null entry.");
                    problems++;
                    continue;
                }

                bool hasPathValue = entry.by_gold.HasValue || entry.by_watch_ads.HasValue || entry.distance_km.HasValue;

                if (!entry.free && !hasPathValue)
                {
                    Debug.LogError($"[ClutchVehicleConfigValidator] VehicleConfig key '{kvp.Key}' declares no obtain path (no by_gold/by_watch_ads/distance_km and free=false).");
                    problems++;
                }

                if (entry.free && hasPathValue)
                {
                    Debug.LogError($"[ClutchVehicleConfigValidator] VehicleConfig key '{kvp.Key}' is free=true but also carries path values; free is exclusive.");
                    problems++;
                }

                if (entry.by_gold is <= 0 || entry.by_watch_ads is <= 0 || entry.distance_km is <= 0)
                {
                    Debug.LogError($"[ClutchVehicleConfigValidator] VehicleConfig key '{kvp.Key}' has a non-positive path value (by_gold={entry.by_gold}, by_watch_ads={entry.by_watch_ads}, distance_km={entry.distance_km}).");
                    problems++;
                }
            }

            // Build the set of valid roster keys (VehicleID enum names) and cross-check both directions.
            HashSet<string> rosterKeys = new HashSet<string>();
            IReadOnlyList<VehicleEntry> vehicles = container.Vehicles;
            for (int i = 0; i < vehicles.Count; i++)
            {
                if (vehicles[i] == null || vehicles[i].ID == VehicleID.None)
                    continue;
                rosterKeys.Add(vehicles[i].ID.ToString());
            }

            // 2) Fallback keys that match no vehicle (typos / dead config).
            foreach (string fallbackKey in fallbackMap.Keys)
            {
                if (!rosterKeys.Contains(fallbackKey))
                {
                    Debug.LogError($"[ClutchVehicleConfigValidator] VehicleConfig key '{fallbackKey}' matches no vehicle in the container (typo or dead key).");
                    problems++;
                }
            }

            // 3) Roster vehicles with no fallback entry (offline they use the serialized obtain config).
            foreach (string rosterKey in rosterKeys)
            {
                if (!fallbackMap.ContainsKey(rosterKey))
                    Debug.LogError($"[ClutchVehicleConfigValidator] Vehicle '{rosterKey}' has no VehicleConfig fallback entry; offline it uses its serialized obtain config.");
            }

            if (problems == 0)
                Debug.Log($"[ClutchVehicleConfigValidator] OK: {fallbackMap.Count} VehicleConfig fallback entr(ies) validated against {rosterKeys.Count} roster vehicle(s).");
            else
                Debug.LogError($"[ClutchVehicleConfigValidator] Found {problems} problem(s) in VehicleConfig.");
        }
    }
}
