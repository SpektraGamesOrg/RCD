using System;
using System.Collections.Generic;
using SpektraGames.ResourceObject.Runtime;
using UnityEditor;
using UnityEngine;

namespace SpektraGames.ResourceObject.Editor
{
    /// <summary>
    /// Editor-only "container" for the ResourceObject system, modeled on the Unity Addressables settings asset. It holds an
    /// inverted index "referenced asset guid -> owner assets that contain a <see cref="ResourceObject{T}"/> pointing at it",
    /// so when a Resources asset is moved/renamed we patch ONLY the handful of real owners instead of disk-scanning every
    /// prefab and ScriptableObject in the project (which froze the editor for seconds in
    /// <c>ResourceObjectAssetTracker.RefreshReferencingAssets</c>).
    ///
    /// The serialized <see cref="owners"/> list is the source of truth and is committed as a single .asset. It is kept
    /// current automatically by <see cref="ResourceObjectAssetTracker"/> on every import/move/delete, and can be fully
    /// rebuilt on demand with <see cref="SyncWithProject"/> (the inspector's "Sync" button).
    ///
    /// This type lives in the Editor assembly on purpose: it is pure tooling and must never ship in a player build.
    /// </summary>
    public class ResourceObjectRegistry : ScriptableObject
    {
        /// <summary>One indexed owner: an asset/scene that contains ResourceObjects, plus the guids those point at.</summary>
        [Serializable]
        internal struct OwnerRecord
        {
            // guid of the prefab / .asset / .unity that contains one or more ResourceObject values.
            public string ownerGuid;
            // Distinct guids referenced by those ResourceObjects (the runtime load target). Order is not significant.
            public string[] referencedGuids;
        }

        private const string DefaultFolder = "Assets/_Game/Data";
        private const string DefaultAssetPath = DefaultFolder + "/ResourceObjectRegistry.asset";

        [SerializeField] private List<OwnerRecord> owners = new();

        // Closed scenes that were imported but not loaded, so we could not scan them in place without disruptively opening
        // them. Surfaced in the inspector; cleared by a full Sync or "Scan pending scenes".
        //[SerializeField] private List<string> pendingSceneGuids = new();

        // ----- runtime-only lookup (the inverted index, rebuilt lazily from `owners`, never serialized) -----
        [NonSerialized] private Dictionary<string, List<string>> ownersByReferencedGuid;
        [NonSerialized] private bool invertedDirty = true;
        [NonSerialized] private bool needsSave;

        private static ResourceObjectRegistry cachedInstance;

        /// <summary>
        /// While true, <see cref="ResourceObjectAssetTracker"/> ignores import callbacks. Set (with save/restore for safe
        /// nesting) around every operation that writes to the project on the tool's behalf, so our own SaveAssets/SaveScene
        /// never re-enters the tracker. Self-heals on exceptions because callers restore the previous value in a finally.
        /// </summary>
        internal static bool SuppressTracking;

        // =====================================================================
        // Instance access / creation
        // =====================================================================

        /// <summary>Find the existing registry asset, or null if none exists yet. Does not create anything.</summary>
        public static ResourceObjectRegistry TryGet()
        {
            if (cachedInstance)
                return cachedInstance;

            var guids = AssetDatabase.FindAssets("t:ResourceObjectRegistry");
            if (guids.Length == 0)
                return null;

            if (guids.Length > 1)
            {
                Debug.LogError(
                    $"[ResourceObject] Found {guids.Length} ResourceObjectRegistry assets. Using the first; " +
                    "delete the extras so healing stays consistent.");
            }

            var path = AssetDatabase.GUIDToAssetPath(guids[0]);
            cachedInstance = AssetDatabase.LoadAssetAtPath<ResourceObjectRegistry>(path);
            return cachedInstance;
        }

        /// <summary>
        /// Return the registry, creating it (and doing a one-time full <see cref="SyncWithProject"/>) if it does not exist.
        /// Creating the asset and rebuilding writes to disk, so only call this from a deferred/menu context, never from
        /// inside an asset import callback.
        /// </summary>
        public static ResourceObjectRegistry GetOrCreate()
        {
            var existing = TryGet();
            if (existing)
                return existing;

            bool previousSuppress = SuppressTracking;
            SuppressTracking = true;
            try
            {
                EnsureFolder(DefaultFolder);
                var registry = CreateInstance<ResourceObjectRegistry>();
                cachedInstance = registry; // set before saving so any re-entrant TryGet resolves to it
                AssetDatabase.CreateAsset(registry, DefaultAssetPath);
                AssetDatabase.SaveAssets();
                Debug.Log($"[ResourceObject] Created index container at '{DefaultAssetPath}'.");

                // Populate it from the current project so it is accurate from the very first move.
                registry.SyncWithProject(showProgress: true);
                return registry;
            }
            finally
            {
                SuppressTracking = previousSuppress;
            }
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder))
                return;

            var parts = folder.Split('/');
            var current = parts[0]; // "Assets"
            for (int i = 1; i < parts.Length; i++)
            {
                var next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        // =====================================================================
        // Read API (used by the tracker to find owners and by the inspector)
        // =====================================================================

        internal int OwnerCount => owners.Count;
        //internal int PendingSceneCount => pendingSceneGuids.Count;
        internal IReadOnlyList<OwnerRecord> Owners => owners;
        //internal IReadOnlyList<string> PendingSceneGuids => pendingSceneGuids;

        internal int DistinctReferencedCount
        {
            get
            {
                EnsureInverted();
                return ownersByReferencedGuid.Count;
            }
        }

        /// <summary>Append the guids of every owner that references any of <paramref name="referencedGuids"/> (deduplicated).</summary>
        internal void CollectOwnersReferencing(HashSet<string> referencedGuids, List<string> ownerGuidsOut)
        {
            EnsureInverted();
            foreach (var guid in referencedGuids)
            {
                if (!ownersByReferencedGuid.TryGetValue(guid, out var list))
                    continue;
                for (int i = 0; i < list.Count; i++)
                {
                    if (!ownerGuidsOut.Contains(list[i]))
                        ownerGuidsOut.Add(list[i]);
                }
            }
        }

        // =====================================================================
        // Write API (incremental maintenance, called by the tracker)
        // =====================================================================

        /// <summary>
        /// Insert or replace an owner's referenced-guid set. Passing an empty/null set removes the owner (it no longer holds
        /// any assigned ResourceObject). Returns true if the index actually changed.
        /// </summary>
        internal bool UpsertOwner(string ownerGuid, HashSet<string> referencedGuids)
        {
            if (string.IsNullOrEmpty(ownerGuid))
                return false;

            // Linear scan: `owners` only ever holds assets/scenes that actually contain a ResourceObject (a small set), so
            // this is cheap and avoids rebuilding any lookup per call during a batch import.
            int index = IndexOfOwner(ownerGuid);
            bool exists = index >= 0;

            if (referencedGuids == null || referencedGuids.Count == 0)
            {
                if (!exists)
                    return false;
                owners.RemoveAt(index);
                InvalidateAndDirty();
                return true;
            }

            if (exists && SameSet(owners[index].referencedGuids, referencedGuids))
                return false;

            var array = new string[referencedGuids.Count];
            referencedGuids.CopyTo(array);

            if (exists)
            {
                var record = owners[index];
                record.referencedGuids = array;
                owners[index] = record;
            }
            else
            {
                owners.Add(new OwnerRecord { ownerGuid = ownerGuid, referencedGuids = array });
            }

            InvalidateAndDirty();
            return true;
        }

        /// <summary>Drop every owner whose guid no longer resolves to a project asset (i.e. it was deleted).</summary>
        internal bool PruneDeadOwners()
        {
            bool removed = false;
            for (int i = owners.Count - 1; i >= 0; i--)
            {
                var guid = owners[i].ownerGuid;
                if (string.IsNullOrEmpty(guid) || string.IsNullOrEmpty(AssetDatabase.GUIDToAssetPath(guid)))
                {
                    owners.RemoveAt(i);
                    removed = true;
                }
            }

            // for (int i = pendingSceneGuids.Count - 1; i >= 0; i--)
            // {
            //     var guid = pendingSceneGuids[i];
            //     if (string.IsNullOrEmpty(guid) || string.IsNullOrEmpty(AssetDatabase.GUIDToAssetPath(guid)))
            //     {
            //         pendingSceneGuids.RemoveAt(i);
            //         removed = true;
            //     }
            // }

            if (removed)
                InvalidateAndDirty();
            return removed;
        }

        // internal void MarkPendingScene(string sceneGuid)
        // {
        //     if (string.IsNullOrEmpty(sceneGuid) || pendingSceneGuids.Contains(sceneGuid))
        //         return;
        //     pendingSceneGuids.Add(sceneGuid);
        //     needsSave = true;
        //     EditorUtility.SetDirty(this);
        // }

        // internal void ClearPendingScene(string sceneGuid)
        // {
        //     if (pendingSceneGuids.Remove(sceneGuid))
        //     {
        //         needsSave = true;
        //         EditorUtility.SetDirty(this);
        //     }
        // }

        /// <summary>Persist the asset only if something changed since the last save. Cheap to call repeatedly.</summary>
        internal void SaveIfDirty()
        {
            if (!needsSave)
                return;
            needsSave = false;
            AssetDatabase.SaveAssets();
        }

        // =====================================================================
        // Full sync ("Sync" button) - rebuild the whole index from the project
        // =====================================================================

        /// <summary>
        /// Rebuild the entire index by scanning every prefab, ScriptableObject and scene in the project. This is the one
        /// expensive operation (it reads project files), so it is on-demand. Closed scenes are opened additively, scanned,
        /// and closed again. No-op in play mode.
        /// </summary>
        public void SyncWithProject(bool showProgress = true)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[ResourceObject] Cannot Sync in Play Mode (scenes can't be opened). Exit Play Mode and retry.");
                return;
            }

            bool previousSuppress = SuppressTracking;
            SuppressTracking = true;
            try
            {
                owners.Clear();
                //pendingSceneGuids.Clear();
                invertedDirty = true;

                var allPaths = AssetDatabase.GetAllAssetPaths();
                var referenced = new HashSet<string>();

                // Pass 1: prefabs + ScriptableObjects (cheap text prefilter avoids parsing files that can't match).
                for (int i = 0; i < allPaths.Length; i++)
                {
                    var path = allPaths[i];
                    if (showProgress && (i & 127) == 0)
                        ReportProgress("Sync: assets", path, i, allPaths.Length);

                    if (!path.StartsWith("Assets/", StringComparison.Ordinal))
                        continue;
                    if (!ResourceObjectScanner.IsAssetOwnerPath(path))
                        continue;
                    if (!ResourceObjectScanner.ShouldInspectFile(path))
                        continue;

                    referenced.Clear();
                    ResourceObjectScanner.CollectFromAssetOwner(path, referenced);
                    AddOwner(AssetDatabase.AssetPathToGUID(path), referenced);
                }

                // Pass 2: scenes (may open closed scenes; kept separate so progress reads clearly). The same text prefilter
                // used for assets gates this so we never open a scene that can't contain a ResourceObject.
                for (int i = 0; i < allPaths.Length; i++)
                {
                    var path = allPaths[i];
                    if (!path.StartsWith("Assets/", StringComparison.Ordinal))
                        continue;
                    if (!ResourceObjectScanner.IsScenePath(path))
                        continue;
                    if (!ResourceObjectScanner.ShouldInspectFile(path))
                        continue;

                    if (showProgress)
                        ReportProgress("Sync: scenes", path, i, allPaths.Length);

                    referenced.Clear();
                    ResourceObjectScanner.CollectFromSceneAtPath(path, referenced);
                    AddOwner(AssetDatabase.AssetPathToGUID(path), referenced);
                }

                invertedDirty = true;
                needsSave = true;
                EditorUtility.SetDirty(this);
                AssetDatabase.SaveAssets();
            }
            finally
            {
                SuppressTracking = previousSuppress;
                if (showProgress)
                    EditorUtility.ClearProgressBar();
            }
        }

        /// <summary>Scan the scenes that were marked pending (imported while closed) and fold them into the index.</summary>
        // public void ScanPendingScenes(bool showProgress = true)
        // {
        //     if (EditorApplication.isPlayingOrWillChangePlaymode)
        //     {
        //         Debug.LogError("[ResourceObject] Cannot scan scenes in Play Mode. Exit Play Mode and retry.");
        //         return;
        //     }
        //
        //     bool previousSuppress = SuppressTracking;
        //     SuppressTracking = true;
        //     try
        //     {
        //         var referenced = new HashSet<string>();
        //         for (int i = pendingSceneGuids.Count - 1; i >= 0; i--)
        //         {
        //             var guid = pendingSceneGuids[i];
        //             var path = AssetDatabase.GUIDToAssetPath(guid);
        //             pendingSceneGuids.RemoveAt(i);
        //             needsSave = true;
        //
        //             if (string.IsNullOrEmpty(path) || !ResourceObjectScanner.IsScenePath(path))
        //                 continue;
        //
        //             if (showProgress)
        //                 ReportProgress("Scan pending scenes", path, pendingSceneGuids.Count - i, pendingSceneGuids.Count + 1);
        //
        //             referenced.Clear();
        //             // Only open the scene if it could contain a ResourceObject; otherwise an empty set removes any stale entry.
        //             if (ResourceObjectScanner.ShouldInspectFile(path))
        //                 ResourceObjectScanner.CollectFromSceneAtPath(path, referenced);
        //             UpsertOwner(guid, referenced);
        //         }
        //
        //         EditorUtility.SetDirty(this);
        //         SaveIfDirty();
        //     }
        //     finally
        //     {
        //         SuppressTracking = previousSuppress;
        //         if (showProgress)
        //             EditorUtility.ClearProgressBar();
        //     }
        // }

        /// <summary>
        /// Proactively re-derive <c>resourcesPath</c> for every ResourceObject in every indexed owner (not just moved ones).
        /// Useful after importing the registry from VCS or to repair drift. Returns the number of owners changed.
        /// </summary>
        public int HealAllOwners(bool showProgress = true)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[ResourceObject] Cannot heal scenes in Play Mode. Exit Play Mode and retry.");
                return 0;
            }

            int changedOwners = 0;
            bool savedAssets = false;
            bool previousSuppress = SuppressTracking;
            SuppressTracking = true;
            try
            {
                for (int i = 0; i < owners.Count; i++)
                {
                    var path = AssetDatabase.GUIDToAssetPath(owners[i].ownerGuid);
                    if (string.IsNullOrEmpty(path))
                        continue;

                    if (showProgress)
                        ReportProgress("Heal all owners", path, i, owners.Count);

                    if (ResourceObjectScanner.IsAssetOwnerPath(path))
                    {
                        if (ResourceObjectScanner.HealAssetOwner(path, null))
                        {
                            changedOwners++;
                            savedAssets = true;
                        }
                    }
                    else if (ResourceObjectScanner.IsScenePath(path))
                    {
                        if (ResourceObjectScanner.TryGetLoadedScene(path, out var loaded))
                        {
                            if (ResourceObjectScanner.HealLoadedScene(loaded, null))
                                changedOwners++;
                        }
                        else if (ResourceObjectScanner.HealSceneAtPath(path, null))
                        {
                            changedOwners++;
                        }
                    }
                }

                if (savedAssets)
                    AssetDatabase.SaveAssets();
            }
            finally
            {
                SuppressTracking = previousSuppress;
                if (showProgress)
                    EditorUtility.ClearProgressBar();
            }

            return changedOwners;
        }

        // =====================================================================
        // Internals
        // =====================================================================

        private void AddOwner(string ownerGuid, HashSet<string> referencedGuids)
        {
            if (string.IsNullOrEmpty(ownerGuid) || referencedGuids.Count == 0)
                return;
            var array = new string[referencedGuids.Count];
            referencedGuids.CopyTo(array);
            owners.Add(new OwnerRecord { ownerGuid = ownerGuid, referencedGuids = array });
        }

        private int IndexOfOwner(string ownerGuid)
        {
            for (int i = 0; i < owners.Count; i++)
            {
                if (owners[i].ownerGuid == ownerGuid)
                    return i;
            }

            return -1;
        }

        /// <summary>
        /// (Re)build the inverted index "referenced guid -> owner guids" lazily. Only the consumers that need it
        /// (CollectOwnersReferencing, DistinctReferencedCount) pay for it, so a batch of UpsertOwner calls rebuilds it at
        /// most once - not once per call.
        /// </summary>
        private void EnsureInverted()
        {
            if (!invertedDirty && ownersByReferencedGuid != null)
                return;

            ownersByReferencedGuid = new Dictionary<string, List<string>>();

            for (int i = 0; i < owners.Count; i++)
            {
                var record = owners[i];
                if (string.IsNullOrEmpty(record.ownerGuid) || record.referencedGuids == null)
                    continue;

                for (int r = 0; r < record.referencedGuids.Length; r++)
                {
                    var referenced = record.referencedGuids[r];
                    if (string.IsNullOrEmpty(referenced))
                        continue;

                    if (!ownersByReferencedGuid.TryGetValue(referenced, out var list))
                    {
                        list = new List<string>();
                        ownersByReferencedGuid[referenced] = list;
                    }

                    if (!list.Contains(record.ownerGuid))
                        list.Add(record.ownerGuid);
                }
            }

            invertedDirty = false;
        }

        private void InvalidateAndDirty()
        {
            invertedDirty = true;
            needsSave = true;
            EditorUtility.SetDirty(this);
        }

        private static bool SameSet(string[] existing, HashSet<string> incoming)
        {
            if (existing == null)
                return incoming.Count == 0;
            if (existing.Length != incoming.Count)
                return false;
            for (int i = 0; i < existing.Length; i++)
            {
                if (!incoming.Contains(existing[i]))
                    return false;
            }

            return true;
        }

        private static void ReportProgress(string title, string info, int current, int total)
        {
            float progress = total > 0 ? (float)current / total : 1f;
            EditorUtility.DisplayProgressBar(title, info, progress);
        }
    }
}
