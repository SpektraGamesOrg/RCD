using UnityEditor;
using UnityEngine;

namespace SpektraGames.ResourceObject.Editor
{
    /// <summary>
    /// Inspector for the <see cref="ResourceObjectRegistry"/> container: shows index stats and exposes the on-demand
    /// actions (full Sync, Heal All, Scan pending scenes). A specific CustomEditor takes precedence over Odin's catch-all
    /// editor, so the buttons render predictably with plain IMGUI and no extra dependencies.
    /// </summary>
    [CustomEditor(typeof(ResourceObjectRegistry))]
    public class ResourceObjectRegistryEditor : UnityEditor.Editor
    {
        private Vector2 scroll;
        private bool showOwners;

        private const int MaxRowsShown = 300;

        public override void OnInspectorGUI()
        {
            var registry = (ResourceObjectRegistry)target;

            EditorGUILayout.HelpBox(
                "Index of every asset that contains a ResourceObject, keyed by the asset it points at. It lets " +
                "moves/renames heal instantly instead of scanning the whole project. Maintained automatically; press " +
                "Sync to fully rebuild from the current project structure.",
                MessageType.Info);

            EditorGUILayout.LabelField("Indexed owners", registry.OwnerCount.ToString());
            EditorGUILayout.LabelField("Distinct referenced assets", registry.DistinctReferencedCount.ToString());

            // if (registry.PendingSceneCount > 0)
            // {
            //     EditorGUILayout.HelpBox(
            //         $"{registry.PendingSceneCount} closed scene(s) were imported since the last scan and are not indexed " +
            //         "yet. Run Sync or \"Scan Pending Scenes\" to cover them.",
            //         MessageType.Warning);
            // }

            EditorGUILayout.Space();

            bool playMode = EditorApplication.isPlayingOrWillChangePlaymode;
            using (new EditorGUI.DisabledScope(playMode))
            {
                if (GUILayout.Button("Sync With Project (Full Rebuild)", GUILayout.Height(28)))
                    registry.SyncWithProject();

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Heal All Owners"))
                    {
                        int changed = registry.HealAllOwners();
                        Debug.Log($"[ResourceObject] Heal All: {changed} owner(s) updated.");
                    }

                    // using (new EditorGUI.DisabledScope(registry.PendingSceneCount == 0))
                    // {
                    //     if (GUILayout.Button("Scan Pending Scenes"))
                    //         registry.ScanPendingScenes();
                    // }
                }
            }

            if (playMode)
                EditorGUILayout.HelpBox("Sync/Heal are disabled in Play Mode (scenes can't be opened).", MessageType.None);

            EditorGUILayout.Space();
            showOwners = EditorGUILayout.Foldout(showOwners, "Indexed owners", true);
            if (showOwners)
                DrawOwners(registry);
        }

        private void DrawOwners(ResourceObjectRegistry registry)
        {
            var owners = registry.Owners;
            int shown = Mathf.Min(owners.Count, MaxRowsShown);

            scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.MaxHeight(240));
            for (int i = 0; i < shown; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(owners[i].ownerGuid);
                int refCount = owners[i].referencedGuids != null ? owners[i].referencedGuids.Length : 0;

                using (new EditorGUILayout.HorizontalScope())
                {
                    var asset = string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadMainAssetAtPath(path);
                    using (new EditorGUI.DisabledScope(!asset))
                        EditorGUILayout.ObjectField(asset, typeof(Object), false);
                    EditorGUILayout.LabelField($"{refCount} ref", GUILayout.Width(60));
                }
            }

            if (owners.Count > shown)
                EditorGUILayout.LabelField($"... and {owners.Count - shown} more");
            EditorGUILayout.EndScrollView();
        }

        // ----- Menu shortcuts -----

        [MenuItem("Tools/Spektra/ResourceObject/Select or Create Registry")]
        private static void SelectOrCreate()
        {
            var registry = ResourceObjectRegistry.GetOrCreate();
            Selection.activeObject = registry;
            EditorGUIUtility.PingObject(registry);
        }

        [MenuItem("Tools/Spektra/ResourceObject/Sync With Project")]
        private static void SyncMenu()
        {
            var registry = ResourceObjectRegistry.GetOrCreate();
            registry.SyncWithProject();
            Selection.activeObject = registry;
            EditorGUIUtility.PingObject(registry);
        }
    }
}
