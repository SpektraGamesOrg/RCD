using System;

namespace Clutch
{
    /// <summary>
    /// Request body for the token mint route (POST /v1/client/auth/token). PrototypeRacing has no Nakama
    /// session token, and the Clutch project has no token-validation configured, so we send the player's
    /// <c>user_id</c> directly (a stable per-device GUID) instead of an upstream token.
    /// </summary>
    [Serializable]
    public class ClutchTokenRequest
    {
        public string user_id;
        public string environment_id;
    }

    /// <summary>Request body for the refresh route (POST /v1/client/auth/refresh).</summary>
    [Serializable]
    public class ClutchRefreshTokenRequest
    {
        public string refresh_token;
    }

    /// <summary>
    /// Response from the token mint or refresh route. The MINT route returns all fields; the REFRESH route
    /// returns only <see cref="access_token"/> + <see cref="access_expires_at"/> (no new refresh token), so
    /// consumers must preserve the existing refresh token when those fields come back empty.
    /// </summary>
    [Serializable]
    public class ClutchTokenResponse
    {
        public string access_token;
        public string access_expires_at;
        public string refresh_token;
        public string refresh_expires_at;
        public string user_id;
    }
}
