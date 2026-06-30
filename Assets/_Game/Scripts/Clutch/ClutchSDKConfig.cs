using SpektraGames.SpektraUtilities.Runtime;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

namespace Clutch
{
    /// <summary>
    /// Connection settings for the Clutch remote-config backend. A <see cref="SingletonScriptableObject{T}"/>
    /// so <see cref="ClutchSDKConfig.Instance"/> resolves the asset from Resources at runtime (no scene wiring).
    ///
    /// PrototypeRacing has no Nakama, so the client only ever uses Clutch's PUBLIC feature-flag endpoint
    /// (no auth). Only <see cref="BaseUrl"/> + <see cref="EnvironmentId"/> are needed; which pair is used is
    /// chosen by the build environment define (dev vs prod), mirroring the rest of the project
    /// (see GameInitializer / MaxAdConfig usage of DEV_GAME_ENVIRONMENT).
    /// </summary>
    [CreateAssetMenu(fileName = "ClutchSDKConfig", menuName = "Clutch/Clutch SDK Config")]
    public class ClutchSDKConfig : SingletonScriptableObject<ClutchSDKConfig>
    {
        [Header("Project")]
        [SerializeField]
        [Tooltip("Clutch project id. Stored for reference / editor tooling; the runtime fetch only needs the environment id.")]
        private string projectId = "a7461151-8b35-4f62-95f8-963d469b08a2";

        [Header("Development")]
        [SerializeField]
        [Tooltip("Base URL for the Clutch API (Development build).")]
        private string devBaseUrl = "https://api.clutch.spektragames.com";

        [SerializeField]
        [Tooltip("Clutch environment id (Development build).")]
        private string devEnvironmentId = "3bee7bf6-bf45-4fed-864e-0a12359c28b1";

        [Header("Production")]
        [SerializeField]
        [Tooltip("Base URL for the Clutch API (Production build).")]
        private string prodBaseUrl = "https://api.clutch.spektragames.com";

        [SerializeField]
        [Tooltip("Clutch environment id (Production build).")]
        private string prodEnvironmentId = "539e9af2-ce3e-49b7-8963-1d5e5612e33c";

#if UNITY_EDITOR
        // EDITOR-ONLY project API keys for the trusted server route (X-API-Key) used by the
        // "update from Clutch" tooling. These are gated behind UNITY_EDITOR so the key NEVER compiles
        // into a client (Android/iOS) player build - mirrors HRP's ServerSideClutch security model.
        // Left blank in source: paste the real keys in the Inspector (keeps them out of git history).
        [Header("Editor API Keys (server route - never shipped)")]
        [SerializeField]
        [Tooltip("EDITOR-ONLY Clutch project API key for the Development environment (X-API-Key). Never compiled into player builds.")]
        private string devApiKey = "";

        [SerializeField]
        [Tooltip("EDITOR-ONLY Clutch project API key for the Production environment (X-API-Key). Never compiled into player builds.")]
        private string prodApiKey = "";
#endif

        /// <summary>Clutch project id (reference / editor tooling only).</summary>
        public string ProjectId => projectId;

        /// <summary>Base URL selected by the active build environment define.</summary>
        public string BaseUrl
        {
            get
            {
#if DEV_GAME_ENVIRONMENT
                return devBaseUrl;
#else
                return prodBaseUrl;
#endif
            }
        }

        /// <summary>Clutch environment id selected by the active build environment define.</summary>
        public string EnvironmentId
        {
            get
            {
#if DEV_GAME_ENVIRONMENT
                return devEnvironmentId;
#else
                return prodEnvironmentId;
#endif
            }
        }

#if UNITY_EDITOR
        // Editor tooling needs to fetch from a specific environment regardless of the active build define,
        // so expose the raw pairs to editor code only.
        public string DevBaseUrl => devBaseUrl;
        public string DevEnvironmentId => devEnvironmentId;
        public string DevApiKey => devApiKey;
        public string ProdBaseUrl => prodBaseUrl;
        public string ProdEnvironmentId => prodEnvironmentId;
        public string ProdApiKey => prodApiKey;

        [MenuItem("Tools/Clutch/Select Clutch SDK Config")]
        public static void SelectAsset()
        {
            Selection.activeObject = Instance;
            EditorGUIUtility.PingObject(Instance);
        }
#endif
    }
}
