using UnityEditor;
using UnityEngine;

namespace _Game.Scripts.Clutch.Editor
{
    /// <summary>
    /// Tools/Clutch menu entries for the remote-config layer. The actual fetch + write lives on the
    /// <see cref="global::Clutch.ClutchConfig"/> SO (its editor-only "Update from Clutch" buttons), so the
    /// menu items and the inspector buttons share one implementation. The fetch uses the trusted server
    /// route (X-API-Key) when an editor API key is configured on ClutchSDKConfig, else the public route;
    /// the key is editor-gated and never ships in a player build.
    /// </summary>
    public static class ClutchConfigEditor
    {
        [MenuItem("Tools/Clutch/Fetch & Write Config (Dev)")]
        private static void FetchDev() => UpdateFromClutch(useDev: true);

        [MenuItem("Tools/Clutch/Fetch & Write Config (Prod)")]
        private static void FetchProd() => UpdateFromClutch(useDev: false);

        [MenuItem("Tools/Clutch/Select Clutch Config (Fallback)")]
        private static void SelectFallbackAsset()
        {
            global::Clutch.ClutchConfig config = global::Clutch.ClutchConfig.Instance;
            if (!config)
            {
                Debug.LogError("[ClutchConfigEditor] ClutchConfig asset not found in Resources. Create one via Create > Clutch > Clutch Config (Fallback).");
                return;
            }

            Selection.activeObject = config;
            EditorGUIUtility.PingObject(config);
        }

        private static void UpdateFromClutch(bool useDev)
        {
            global::Clutch.ClutchConfig config = global::Clutch.ClutchConfig.Instance;
            if (!config)
            {
                Debug.LogError("[ClutchConfigEditor] ClutchConfig asset not found in Resources. Create one via Create > Clutch > Clutch Config (Fallback).");
                return;
            }

            config.UpdateFromClutchEditor(useDev);
        }
    }
}
