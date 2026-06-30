using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace Clutch
{
    /// <summary>
    /// The networking for Clutch feature-flag evaluation. Two routes, same endpoint:
    ///   * <see cref="EvaluateAuthenticatedAsync"/> — Authorization: Bearer &lt;access token&gt;. Returns the
    ///     user's full flag set INCLUDING non-public flags. This is the route the runtime uses (the project's
    ///     flags are not marked public). Tokens come from <see cref="ClutchAuth"/> (device-id auth).
    ///   * <see cref="EvaluatePublicAsync"/> — no auth; returns only flags marked public.
    /// Each returned flag value is handed back as its raw JSON string (our flags are JSON maps); the caller
    /// deserializes as needed. UnityWebRequest keeps this on the Unity main thread (mobile-friendly).
    /// </summary>
    public static class ClutchClient
    {
        private const int TimeoutSeconds = 10;

        /// <summary>
        /// Authenticated evaluate-batch: sends the Bearer access token (and echoes user_id in the body, as
        /// the server expects) so non-public flags are returned. Throws on transport error / non-2xx /
        /// unparseable body — callers treat any throw as "Clutch failed, use cache/fallback".
        /// </summary>
        public static UniTask<Dictionary<string, string>> EvaluateAuthenticatedAsync(
            string baseUrl,
            string environmentId,
            string accessToken,
            string userId,
            IReadOnlyList<string> keys,
            JObject properties = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(accessToken))
                throw new InvalidOperationException("Clutch authenticated evaluate requires an access token.");

            return EvaluateAsync(baseUrl, environmentId, keys, properties, accessToken, userId, cancellationToken);
        }

        /// <summary>
        /// Public (unauthenticated) evaluate-batch — returns only flags marked public. See class summary.
        /// </summary>
        /// <param name="baseUrl">Clutch API base URL (e.g. https://api.clutch.spektragames.com).</param>
        /// <param name="environmentId">Clutch environment id.</param>
        /// <param name="keys">Flag keys to evaluate (e.g. "VehicleConfig", "AdConfig").</param>
        /// <param name="properties">Optional targeting attributes (camelCase) forwarded as "properties".</param>
        public static UniTask<Dictionary<string, string>> EvaluatePublicAsync(
            string baseUrl,
            string environmentId,
            IReadOnlyList<string> keys,
            JObject properties = null,
            CancellationToken cancellationToken = default)
        {
            return EvaluateAsync(baseUrl, environmentId, keys, properties, accessToken: null, userId: null, cancellationToken);
        }

        // Shared evaluate-batch. With an access token it sends Authorization: Bearer + user_id (authenticated
        // route); without, it sends neither (public route).
        private static async UniTask<Dictionary<string, string>> EvaluateAsync(
            string baseUrl,
            string environmentId,
            IReadOnlyList<string> keys,
            JObject properties,
            string accessToken,
            string userId,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(baseUrl) || string.IsNullOrEmpty(environmentId))
                throw new InvalidOperationException("Clutch base URL / environment id is not configured.");

            if (keys == null || keys.Count == 0)
                return new Dictionary<string, string>();

            string url = $"{baseUrl}/v1/client/environments/{environmentId}/features/evaluate-batch";

            JObject body = new JObject { ["keys"] = new JArray(keys) };
            if (!string.IsNullOrEmpty(accessToken) && !string.IsNullOrEmpty(userId))
                body["user_id"] = userId;
            if (properties != null && properties.Count > 0)
                body["properties"] = properties;
            byte[] payload = Encoding.UTF8.GetBytes(body.ToString(Formatting.None));

            using UnityWebRequest request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST);
            request.uploadHandler = new UploadHandlerRaw(payload);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Accept", "application/json");
            // REQUIRED: the Clutch edge/WAF returns 403 for requests with no User-Agent.
            request.SetRequestHeader("User-Agent", "ClutchSDK-Unity/1.0");
            if (!string.IsNullOrEmpty(accessToken))
                request.SetRequestHeader("Authorization", "Bearer " + accessToken);
            request.timeout = TimeoutSeconds;

            await request.SendWebRequest().WithCancellation(cancellationToken);

            if (request.result != UnityWebRequest.Result.Success)
                throw new Exception($"Clutch evaluate failed: {request.responseCode} {request.error}");

            string responseText = request.downloadHandler.text;
            if (string.IsNullOrEmpty(responseText))
                return new Dictionary<string, string>();

            return ParseFeatures(responseText);
        }

        // Reads the "features" object ({ "<key>": <raw json value> }) and serializes each value to a
        // JSON string. Objects/arrays/scalars are all preserved verbatim so callers can deserialize the
        // exact shape Clutch returned.
        private static Dictionary<string, string> ParseFeatures(string responseText)
        {
            Dictionary<string, string> result = new Dictionary<string, string>();

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
    }
}
