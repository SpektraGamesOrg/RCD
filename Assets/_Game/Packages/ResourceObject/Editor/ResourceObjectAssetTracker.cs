using System.Collections.Generic;
using System.IO;
using SpektraGames.ResourceObject.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SpektraGames.ResourceObject.Editor
{
    /// <summary>
    /// Listens to project asset changes:
    ///  - When a Resources asset is <b>moved/renamed</b>, every <c>ResourceObject</c> referencing it (in prefabs,
    ///    ScriptableObjects and currently-open scenes) has its serialized <c>resourcesPath</c> auto-updated from the
    ///    (stable) guid, so the runtime load key never goes stale.
    ///  - When a Resources asset is <b>deleted</b>, it logs an error so missing references are noticed.
    /// </summary>
    public class ResourceObjectAssetTracker : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            // --- Deletions: surface missing references ---
            if (deletedAssets != null)
            {
                foreach (var path in deletedAssets)
                {
                    if (IsUnderResources(path))
                    {
                        if (path.Contains("PerformanceTestRunSettings"))
                            break;

                        // Debug.LogError(
                        //     $"[ResourceObject] A Resources asset was deleted: '{path}'. " +
                        //     "Any ResourceObject referencing it will now show as missing - reassign or clear it.");
                    }
                }
            }

            // --- Moves/renames: collect the guids of Resources assets whose path changed ---
            var movedGuids = new HashSet<string>();
            if (movedAssets != null)
            {
                for (int i = 0; i < movedAssets.Length; i++)
                {
                    // Either the new or old location being under Resources means the load path is affected
                    // (moved within Resources, into Resources, or out of Resources).
                    if (IsUnderResources(movedAssets[i]) || IsUnderResources(movedFromAssetPaths[i]))
                    {
                        var guid = AssetDatabase.AssetPathToGUID(movedAssets[i]);
                        if (!string.IsNullOrEmpty(guid))
                            movedGuids.Add(guid);
                    }
                }
            }

            if (movedGuids.Count == 0)
                return;

            // Defer the actual patching: writing/saving assets from inside an import callback can re-enter the importer.
            EditorApplication.delayCall += () => RefreshReferencingAssets(movedGuids);
        }

        /// <summary>Find and refresh every ResourceObject whose path may have become stale because of a move.</summary>
        private static void RefreshReferencingAssets(HashSet<string> movedGuids)
        {
            // Patch persisted assets (prefabs + .asset/ScriptableObjects). A cheap text prefilter avoids loading
            // assets that don't even mention one of the moved guids.
            bool savedAnything = false;
            foreach (var path in AssetDatabase.GetAllAssetPaths())
            {
                if (!path.StartsWith("Assets/"))
                    continue;
                if (!path.EndsWith(".prefab") && !path.EndsWith(".asset"))
                    continue;

                if (!FileMentionsAnyGuid(path, movedGuids))
                    continue;

                bool changed = false;
                foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    if (obj != null && RefreshSerializedObject(new SerializedObject(obj)))
                        changed = true;
                }

                if (changed)
                {
                    var main = AssetDatabase.LoadMainAssetAtPath(path);
                    if (main != null)
                        EditorUtility.SetDirty(main);
                    savedAnything = true;
                }
            }

            if (savedAnything)
                AssetDatabase.SaveAssets();

            // Patch any currently-open scenes in memory (saving the scene is left to the user).
            for (int i = 0; i < EditorSceneManager.sceneCount; i++)
            {
                var scene = EditorSceneManager.GetSceneAt(i);
                if (!scene.isLoaded)
                    continue;

                bool sceneChanged = false;
                foreach (var root in scene.GetRootGameObjects())
                {
                    foreach (var component in root.GetComponentsInChildren<Component>(true))
                    {
                        if (component != null && RefreshSerializedObject(new SerializedObject(component)))
                            sceneChanged = true;
                    }
                }

                if (sceneChanged)
                    EditorSceneManager.MarkSceneDirty(scene);
            }
        }

        /// <summary>
        /// Walk every serialized property and, for each one that looks like a ResourceObject (a generic struct with
        /// string children named "guid" and "resourcesPath"), recompute resourcesPath from the guid. Returns true if
        /// anything changed.
        /// </summary>
        private static bool RefreshSerializedObject(SerializedObject serializedObject)
        {
            bool changed = false;
            var iterator = serializedObject.GetIterator();
            bool enterChildren = true;

            while (iterator.Next(enterChildren))
            {
                enterChildren = true;
                if (iterator.propertyType != SerializedPropertyType.Generic)
                    continue;

                var element = iterator.Copy();
                var guidProp = element.FindPropertyRelative("guid");
                var pathProp = element.FindPropertyRelative("resourcesPath");
                if (guidProp == null || pathProp == null ||
                    guidProp.propertyType != SerializedPropertyType.String ||
                    pathProp.propertyType != SerializedPropertyType.String)
                {
                    continue;
                }

                // This node is a ResourceObject - don't descend into its string children.
                enterChildren = false;

                var guid = guidProp.stringValue;
                if (string.IsNullOrEmpty(guid))
                    continue;

                var assetPath = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(assetPath))
                    continue; // deleted asset -> leave as-is, the drawer flags it as missing

                // Null means it is no longer under a Resources folder -> invalidate the load path.
                var desired = ResourceObject<Object>.ToResourcesPath(assetPath) ?? string.Empty;
                if (desired != pathProp.stringValue)
                {
                    pathProp.stringValue = desired;
                    changed = true;
                }
            }

            if (changed)
                serializedObject.ApplyModifiedPropertiesWithoutUndo();

            return changed;
        }

        private static bool FileMentionsAnyGuid(string assetPath, HashSet<string> guids)
        {
            string text;
            try
            {
                text = File.ReadAllText(assetPath);
            }
            catch
            {
                return false;
            }

            foreach (var guid in guids)
            {
                if (text.Contains(guid))
                    return true;
            }

            return false;
        }

        private static bool IsUnderResources(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath))
                return false;
            assetPath = assetPath.Replace('\\', '/');
            return assetPath.Contains("/Resources/") || assetPath.StartsWith("Resources/");
        }
    }
}