using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SpektraGames.ResourceObject.Editor
{
    /// <summary>
    /// Listens to project asset changes and keeps the <see cref="ResourceObjectRegistry"/> index current, then uses that
    /// index to heal references cheaply:
    ///  - When a Resources asset is <b>moved/renamed</b>, only the owners the index says reference it (prefabs,
    ///    ScriptableObjects, loaded scenes, and indexed closed scenes) get their serialized <c>resourcesPath</c> recomputed
    ///    from the (stable) guid - no project-wide file scan.
    ///  - When an owner asset is <b>imported</b>, its index entry is refreshed by scanning just that asset.
    ///  - When an asset is <b>deleted</b>, dead owners are pruned from the index.
    ///
    /// The old behavior (disk-scanning every prefab + .asset on every move via <c>File.ReadAllText</c>) is replaced by an
    /// index lookup; a full rebuild is available on demand from the registry's "Sync" button.
    /// </summary>
    public class ResourceObjectAssetTracker : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            // Ignore the imports our own SaveAssets/SaveScene calls produce; the index already reflects them.
            if (ResourceObjectRegistry.SuppressTracking)
                return;

            // --- Moves/renames: guids of Resources assets whose load path changed (moved within/into/out of Resources). ---
            var movedResourceGuids = new HashSet<string>();
            if (movedAssets != null)
            {
                for (int i = 0; i < movedAssets.Length; i++)
                {
                    if (!ResourceObjectScanner.IsUnderResources(movedAssets[i]) &&
                        !ResourceObjectScanner.IsUnderResources(movedFromAssetPaths[i]))
                    {
                        continue;
                    }

                    var guid = AssetDatabase.AssetPathToGUID(movedAssets[i]);
                    if (!string.IsNullOrEmpty(guid))
                        movedResourceGuids.Add(guid);
                }
            }

            // --- Imported owners that we may need to (re)index. ---
            List<string> importedAssetOwners = null;
            List<string> importedScenes = null;
            if (importedAssets != null)
            {
                for (int i = 0; i < importedAssets.Length; i++)
                {
                    var path = importedAssets[i];
                    if (!path.StartsWith("Assets/"))
                        continue;

                    if (ResourceObjectScanner.IsAssetOwnerPath(path))
                        (importedAssetOwners ??= new List<string>()).Add(path);
                    else if (ResourceObjectScanner.IsScenePath(path))
                        (importedScenes ??= new List<string>()).Add(path);
                }
            }

            bool deletedAny = deletedAssets != null && deletedAssets.Length > 0;

            if (movedResourceGuids.Count == 0 && importedAssetOwners == null && importedScenes == null && !deletedAny)
                return;

            // Defer: writing/saving assets or scenes from inside an import callback can re-enter the importer.
            EditorApplication.delayCall += () =>
                ProcessDeferred(movedResourceGuids, importedAssetOwners, importedScenes, deletedAny);
        }

        private static void ProcessDeferred(
            HashSet<string> movedResourceGuids,
            List<string> importedAssetOwners,
            List<string> importedScenes,
            bool deletedAny)
        {
            bool previousSuppress = ResourceObjectRegistry.SuppressTracking;
            ResourceObjectRegistry.SuppressTracking = true;
            try
            {
                var registry = ResourceObjectRegistry.TryGet();

                // No registry yet: only bootstrap (create + full sync) when there is real ResourceObject work to do. Don't
                // materialize the asset just because some unrelated asset was deleted.
                if (!registry)
                {
                    if (!ShouldBootstrap(movedResourceGuids, importedAssetOwners))
                        return;
                    registry = ResourceObjectRegistry.GetOrCreate(); // creates + full sync (indexes everything, incl. new imports)
                }

                if (deletedAny)
                    registry.PruneDeadOwners();

                RescanImportedAssetOwners(registry, importedAssetOwners);
                RescanImportedScenes(registry, importedScenes);

                if (movedResourceGuids.Count > 0)
                    HealMovedReferences(registry, movedResourceGuids);

                registry.SaveIfDirty();
            }
            finally
            {
                ResourceObjectRegistry.SuppressTracking = previousSuppress;
            }
        }

        /// <summary>True if a freshly bootstrapped registry would have anything meaningful to do.</summary>
        private static bool ShouldBootstrap(HashSet<string> movedResourceGuids, List<string> importedAssetOwners)
        {
            if (movedResourceGuids.Count > 0)
                return true;
            if (importedAssetOwners == null)
                return false;

            for (int i = 0; i < importedAssetOwners.Count; i++)
            {
                if (ResourceObjectScanner.ShouldInspectFile(importedAssetOwners[i]))
                    return true;
            }

            return false;
        }

        private static void RescanImportedAssetOwners(ResourceObjectRegistry registry, List<string> importedAssetOwners)
        {
            if (importedAssetOwners == null)
                return;

            var referenced = new HashSet<string>();
            for (int i = 0; i < importedAssetOwners.Count; i++)
            {
                var path = importedAssetOwners[i];
                var guid = AssetDatabase.AssetPathToGUID(path);
                if (string.IsNullOrEmpty(guid))
                    continue;

                referenced.Clear();
                // Only parse the file if it could possibly contain a ResourceObject; otherwise an empty set removes any
                // stale entry for this owner.
                if (ResourceObjectScanner.ShouldInspectFile(path))
                    ResourceObjectScanner.CollectFromAssetOwner(path, referenced);

                registry.UpsertOwner(guid, referenced);
            }
        }

        private static void RescanImportedScenes(ResourceObjectRegistry registry, List<string> importedScenes)
        {
            if (importedScenes == null)
                return;

            var referenced = new HashSet<string>();
            for (int i = 0; i < importedScenes.Count; i++)
            {
                var path = importedScenes[i];
                var guid = AssetDatabase.AssetPathToGUID(path);
                if (string.IsNullOrEmpty(guid))
                    continue;

                // Scan loaded scenes in place; defer closed ones (opening a scene during a routine import is too disruptive).
                if (ResourceObjectScanner.TryGetLoadedScene(path, out var scene))
                {
                    referenced.Clear();
                    ResourceObjectScanner.CollectFromScene(scene, referenced);
                    registry.UpsertOwner(guid, referenced);
                    //registry.ClearPendingScene(guid);
                }
                else
                {
                    //registry.MarkPendingScene(guid);
                }
            }
        }

        private static void HealMovedReferences(ResourceObjectRegistry registry, HashSet<string> movedResourceGuids)
        {
            var ownerGuids = new List<string>();
            registry.CollectOwnersReferencing(movedResourceGuids, ownerGuids);

            bool savedAssets = false;
            List<string> closedScenesToHeal = null;

            for (int i = 0; i < ownerGuids.Count; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(ownerGuids[i]);
                if (string.IsNullOrEmpty(path))
                    continue;

                if (ResourceObjectScanner.IsAssetOwnerPath(path))
                {
                    if (ResourceObjectScanner.HealAssetOwner(path, movedResourceGuids))
                        savedAssets = true;
                }
                else if (ResourceObjectScanner.IsScenePath(path) &&
                         !ResourceObjectScanner.TryGetLoadedScene(path, out _))
                {
                    // Loaded scenes are handled below (index-independent); only closed scenes need the open/save/close path.
                    (closedScenesToHeal ??= new List<string>()).Add(path);
                }
            }

            if (savedAssets)
                AssetDatabase.SaveAssets();

            // Heal every currently-loaded scene in memory regardless of the index, so open scenes always stay correct even
            // if their entry hasn't been built yet. Saving the scene is left to the user (matches the original behavior).
            HealAllLoadedScenes(movedResourceGuids);

            if (closedScenesToHeal != null)
            {
                for (int i = 0; i < closedScenesToHeal.Count; i++)
                    ResourceObjectScanner.HealSceneAtPath(closedScenesToHeal[i], movedResourceGuids);
            }

            // Closed scenes that were imported but never indexed live only in pendingSceneGuids, so the index lookup above
            // can't find them. Consult their file text directly and heal+index any that actually reference a moved guid.
            HealPendingScenesReferencing(registry, movedResourceGuids);
        }

        private static void HealPendingScenesReferencing(ResourceObjectRegistry registry, HashSet<string> movedResourceGuids)
        {
            // // Closed scenes can't be opened in play mode; leave them pending until the editor is back in edit mode.
            // if (EditorApplication.isPlayingOrWillChangePlaymode)
            //     return;
            //
            // var pending = registry.PendingSceneGuids;
            // if (pending.Count == 0)
            //     return;
            //
            // // Snapshot: healing+indexing a pending scene mutates the registry's pending list as we go.
            // var pendingGuids = new List<string>(pending);
            // var referenced = new HashSet<string>();
            // for (int i = 0; i < pendingGuids.Count; i++)
            // {
            //     var guid = pendingGuids[i];
            //     var path = AssetDatabase.GUIDToAssetPath(guid);
            //     if (string.IsNullOrEmpty(path) || !ResourceObjectScanner.IsScenePath(path))
            //     {
            //         registry.ClearPendingScene(guid);
            //         continue;
            //     }
            //
            //     // A loaded pending scene was already healed in memory above; skip the disk path for it.
            //     if (ResourceObjectScanner.TryGetLoadedScene(path, out _))
            //         continue;
            //
            //     // Cheap text prefilter: only open scenes whose file actually mentions one of the moved guids.
            //     if (!ResourceObjectScanner.FileMentionsAnyGuid(path, movedResourceGuids))
            //         continue;
            //
            //     // Open once to both heal the stale paths and learn what the scene references, then index it so future
            //     // moves resolve it through the index and it leaves the pending list.
            //     referenced.Clear();
            //     ResourceObjectScanner.HealAndCollectSceneAtPath(path, movedResourceGuids, referenced);
            //     registry.UpsertOwner(guid, referenced);
            //     registry.ClearPendingScene(guid);
            // }
        }

        private static void HealAllLoadedScenes(HashSet<string> movedResourceGuids)
        {
            for (int i = 0; i < UnityEditor.SceneManagement.EditorSceneManager.sceneCount; i++)
            {
                var scene = UnityEditor.SceneManagement.EditorSceneManager.GetSceneAt(i);
                if (scene.isLoaded)
                    ResourceObjectScanner.HealLoadedScene(scene, movedResourceGuids);
            }
        }
    }
}
