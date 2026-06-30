using System;
using System.Globalization;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using Save;
using UnityEngine;
using UnityEngine.Networking;

namespace Clutch
{
    /// <summary>
    /// Clutch access/refresh token management, ported from HRP's ClutchSDK but adapted for PrototypeRacing,
    /// which has no Nakama: the initial token is minted by sending the player's <c>user_id</c> (a stable
    /// per-device GUID from <see cref="SaveManager.UserId"/>) to POST /v1/client/auth/token (the project has
    /// no token-validation configured, so Clutch accepts the user_id directly).
    ///
    /// Responsibilities (same shape as HRP):
    ///   * mint a token from the device id,
    ///   * refresh the access token via the refresh token (POST /v1/client/auth/refresh),
    ///   * re-mint when the refresh token itself expires/invalid (HRP re-authenticates via Nakama; here we
    ///     just re-mint from the device id),
    ///   * persist tokens + expiries in PlayerPrefs and reuse them across sessions,
    ///   * <see cref="EnsureValidAccessTokenAsync"/> so callers always send a fresh Bearer token.
    ///
    /// Editor/runtime safe: uses UnityWebRequest (main-thread, mobile-friendly), no API key (this is the
    /// player Bearer flow, not the server X-API-Key route).
    /// </summary>
    public static class ClutchAuth
    {
        private const int TimeoutSeconds = 10;
        // Refresh the access token if it expires within this window.
        private const int RefreshThresholdMinutes = 1;

        private static string _accessToken;
        private static string _refreshToken;
        private static DateTime _accessExpiresAt;
        private static DateTime _refreshExpiresAt;
        private static string _userId;
        private static string _environmentId;

        // Serializes concurrent auth so two callers never mint/refresh at once.
        private static bool _authInFlight;

        /// <summary>The current Bearer access token (may be expired - call EnsureValidAccessTokenAsync first).</summary>
        public static string AccessToken => _accessToken;

        /// <summary>True when an access token exists and has not expired.</summary>
        public static bool IsAuthenticated => !string.IsNullOrEmpty(_accessToken) && _accessExpiresAt > DateTime.UtcNow;

        public static DateTime AccessTokenExpiresAt => _accessExpiresAt;
        public static DateTime RefreshTokenExpiresAt => _refreshExpiresAt;

        /// <summary>
        /// Guarantees a valid (non-expired) access token for <paramref name="userId"/> + <paramref name="environmentId"/>,
        /// reusing the PlayerPrefs-cached token when possible, refreshing it when the access token is
        /// expiring, and re-minting when the refresh token is gone/expired or the user/env changed. Throws
        /// on a hard failure; callers treat any throw as "not authenticated, fall back to cache/SO".
        /// </summary>
        public static async UniTask EnsureValidAccessTokenAsync(
            string baseUrl, string environmentId, string userId, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(baseUrl) || string.IsNullOrEmpty(environmentId) || string.IsNullOrEmpty(userId))
                throw new InvalidOperationException("Clutch auth requires base URL, environment id, and user id.");

            // Single-flight: wait for any in-progress auth to finish, then re-check below.
            while (_authInFlight)
                await UniTask.Yield(ct);

            LoadFromPrefsIfNeeded(userId, environmentId);

            // Cached token still valid and not expiring soon -> nothing to do.
            if (IsAuthenticated && _accessExpiresAt > DateTime.UtcNow.AddMinutes(RefreshThresholdMinutes))
                return;

            _authInFlight = true;
            try
            {
                // Re-check after acquiring the flag (another caller may have just refreshed).
                if (IsAuthenticated && _accessExpiresAt > DateTime.UtcNow.AddMinutes(RefreshThresholdMinutes))
                    return;

                bool refreshUsable = !string.IsNullOrEmpty(_refreshToken) && _refreshExpiresAt > DateTime.UtcNow;
                if (refreshUsable)
                {
                    try
                    {
                        await RefreshAsync(baseUrl, ct);
                        return;
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"[ClutchAuth] Refresh failed ({e.Message}); re-minting from device id.");
                    }
                }

                // No usable refresh token (or refresh failed): mint a fresh pair from the device id.
                await MintAsync(baseUrl, environmentId, userId, ct);
            }
            finally
            {
                _authInFlight = false;
            }
        }

        // POST /v1/client/auth/token with { user_id, environment_id } -> full token pair.
        private static async UniTask MintAsync(string baseUrl, string environmentId, string userId, CancellationToken ct)
        {
            string json = JsonConvert.SerializeObject(new ClutchTokenRequest
            {
                user_id = userId,
                environment_id = environmentId,
            });

            ClutchTokenResponse response = await PostAsync($"{baseUrl}/v1/client/auth/token", json, ct);

            _userId = string.IsNullOrEmpty(response.user_id) ? userId : response.user_id;
            _environmentId = environmentId;
            _accessToken = response.access_token;
            _refreshToken = response.refresh_token;
            _accessExpiresAt = ParseExpiry(response.access_expires_at);
            _refreshExpiresAt = ParseExpiry(response.refresh_expires_at);

            SaveToPrefs();
        }

        // POST /v1/client/auth/refresh with { refresh_token } -> new access token (+ expiry) ONLY. The
        // refresh response does not carry a new refresh token, so we keep the existing refresh token/expiry.
        private static async UniTask RefreshAsync(string baseUrl, CancellationToken ct)
        {
            string json = JsonConvert.SerializeObject(new ClutchRefreshTokenRequest
            {
                refresh_token = _refreshToken,
            });

            ClutchTokenResponse response = await PostAsync($"{baseUrl}/v1/client/auth/refresh", json, ct);

            if (string.IsNullOrEmpty(response.access_token))
                throw new Exception("Refresh response had no access_token.");

            _accessToken = response.access_token;
            _accessExpiresAt = ParseExpiry(response.access_expires_at);

            // The refresh route may (rarely) rotate the refresh token; only overwrite when present.
            if (!string.IsNullOrEmpty(response.refresh_token))
            {
                _refreshToken = response.refresh_token;
                _refreshExpiresAt = ParseExpiry(response.refresh_expires_at);
            }

            SaveToPrefs();
        }

        // Shared POST + parse (no auth header; these are the public auth routes).
        private static async UniTask<ClutchTokenResponse> PostAsync(string url, string jsonBody, CancellationToken ct)
        {
            byte[] payload = Encoding.UTF8.GetBytes(jsonBody);

            using UnityWebRequest request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST);
            request.uploadHandler = new UploadHandlerRaw(payload);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Accept", "application/json");
            // The Clutch edge/WAF 403s requests with no User-Agent (see ClutchClient).
            request.SetRequestHeader("User-Agent", "ClutchSDK-Unity/1.0");
            request.timeout = TimeoutSeconds;

            await request.SendWebRequest().WithCancellation(ct);

            if (request.result != UnityWebRequest.Result.Success)
                throw new Exception($"Clutch auth request failed: {request.responseCode} {request.error}");

            string text = request.downloadHandler.text;
            if (string.IsNullOrEmpty(text))
                throw new Exception("Clutch auth response was empty.");

            ClutchTokenResponse response = JsonConvert.DeserializeObject<ClutchTokenResponse>(text);
            if (response == null)
                throw new Exception("Clutch auth response could not be parsed.");

            return response;
        }

        // ISO-8601 UTC expiry; treat unparseable as already-expired so we re-mint rather than trust a bad value.
        private static DateTime ParseExpiry(string value)
        {
            if (!string.IsNullOrEmpty(value) &&
                DateTime.TryParse(value, CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out DateTime parsed))
            {
                return parsed;
            }

            return DateTime.MinValue;
        }

        // ---------------------------------------------------------------------
        // PlayerPrefs persistence (reuse tokens across sessions)
        // ---------------------------------------------------------------------

        // Loads cached tokens once into memory when memory is empty; discards the cache if it belongs to a
        // different user/environment (so a device-id or env change forces a fresh mint).
        private static void LoadFromPrefsIfNeeded(string userId, string environmentId)
        {
            if (!string.IsNullOrEmpty(_accessToken) || !string.IsNullOrEmpty(_refreshToken))
                return; // already loaded/minted this session

            string cachedUser = PlayerPrefs.GetString(SaveKeys.ClutchAuthUserId, string.Empty);
            string cachedEnv = PlayerPrefs.GetString(SaveKeys.ClutchAuthEnvId, string.Empty);
            if (cachedUser != userId || cachedEnv != environmentId)
            {
                Clear();
                return;
            }

            _userId = cachedUser;
            _environmentId = cachedEnv;
            _accessToken = PlayerPrefs.GetString(SaveKeys.ClutchAccessToken, string.Empty);
            _refreshToken = PlayerPrefs.GetString(SaveKeys.ClutchRefreshToken, string.Empty);
            _accessExpiresAt = ParseExpiry(PlayerPrefs.GetString(SaveKeys.ClutchAccessExpires, string.Empty));
            _refreshExpiresAt = ParseExpiry(PlayerPrefs.GetString(SaveKeys.ClutchRefreshExpires, string.Empty));
        }

        private static void SaveToPrefs()
        {
            PlayerPrefs.SetString(SaveKeys.ClutchAccessToken, _accessToken ?? string.Empty);
            PlayerPrefs.SetString(SaveKeys.ClutchRefreshToken, _refreshToken ?? string.Empty);
            PlayerPrefs.SetString(SaveKeys.ClutchAccessExpires, _accessExpiresAt.ToString("O", CultureInfo.InvariantCulture));
            PlayerPrefs.SetString(SaveKeys.ClutchRefreshExpires, _refreshExpiresAt.ToString("O", CultureInfo.InvariantCulture));
            PlayerPrefs.SetString(SaveKeys.ClutchAuthUserId, _userId ?? string.Empty);
            PlayerPrefs.SetString(SaveKeys.ClutchAuthEnvId, _environmentId ?? string.Empty);
            PlayerPrefs.Save();
        }

        /// <summary>Clears tokens from memory and PlayerPrefs. Intended for sign-out / debug.</summary>
        public static void Clear()
        {
            _accessToken = null;
            _refreshToken = null;
            _accessExpiresAt = default;
            _refreshExpiresAt = default;
            _userId = null;
            _environmentId = null;

            PlayerPrefs.DeleteKey(SaveKeys.ClutchAccessToken);
            PlayerPrefs.DeleteKey(SaveKeys.ClutchRefreshToken);
            PlayerPrefs.DeleteKey(SaveKeys.ClutchAccessExpires);
            PlayerPrefs.DeleteKey(SaveKeys.ClutchRefreshExpires);
            PlayerPrefs.DeleteKey(SaveKeys.ClutchAuthUserId);
            PlayerPrefs.DeleteKey(SaveKeys.ClutchAuthEnvId);
            PlayerPrefs.Save();
        }
    }
}
