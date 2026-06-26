using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SpektraGames.ResourceObject.Runtime
{
    /// <summary>
    /// A lightweight, Addressables-style reference to a <see cref="UnityEngine.Object"/> that lives inside a
    /// <c>Resources</c> folder. Works for prefabs, prefab components, ScriptableObjects, and any other saved asset.
    ///
    /// Why this exists: assigning a raw <c>public GameObject prefab;</c> field bakes a HARD reference into the build,
    /// forcing the asset (and its dependencies) to load with the owner. <see cref="ResourceObject{T}"/> serializes ONLY
    /// two strings - a guid (editor identity) and a Resources-relative path (runtime load key) - so the real asset is
    /// never referenced at build runtime. It is loaded on demand via <see cref="Resources"/>, exactly like an
    /// <c>AssetReferenceT</c> is loaded via Addressables.
    ///
    /// Usage:
    /// <code>
    /// public ResourceObject&lt;GameObject&gt; carPrefab;        // a prefab
    /// public ResourceObject&lt;CarController&gt; carController;  // a component on a prefab
    /// public ResourceObject&lt;VehicleSaveData&gt; defaults;     // a ScriptableObject
    ///
    /// var go = await carPrefab.LoadAsync();
    /// carPrefab.Unload();
    /// </code>
    /// The asset MUST live under a <c>Resources/</c> folder, otherwise it cannot be loaded in a build.
    /// </summary>
    [Serializable]
    public class ResourceObject<T> : IResourceObjectCache where T : Object
    {
        // Whether T is (or derives from) Component. Prefab components are loaded by loading the prefab GameObject and
        // then GetComponent<T>(). Computed once per closed generic type, so there is no per-call cost.
        private static readonly bool IsComponentType = typeof(Component).IsAssignableFrom(typeof(T));

        // ----- Serialized data (the ONLY things that persist) -----
        // guid: editor-side source of truth. Survives moves/renames and lets the editor resolve the asset for display.
        // resourcesPath: runtime load key, relative to a Resources folder, without file extension (e.g. "Vehicles/Car").
        [SerializeField] private string guid;
        [SerializeField] private string resourcesPath;

        // ----- Runtime-only cache (never serialized, so no hard asset reference ends up in the build) -----
        [NonSerialized] private T cachedAsset;
        [NonSerialized] private Object loadedResource; // the underlying loaded asset, kept for correct unloading
        [NonSerialized] private bool isLoaded;

        public ResourceObject()
        {
        }

        /// <summary>Construct directly from a stored guid + Resources path (e.g. when restoring from your own save).</summary>
        public ResourceObject(string guid, string resourcesPath)
        {
            this.guid = guid;
            this.resourcesPath = resourcesPath;
        }

        /// <summary>The asset's project guid (editor identity). Empty when nothing is assigned.</summary>
        public string Guid => guid;

        /// <summary>The Resources-relative load path, without extension. Empty when nothing is assigned.</summary>
        public string ResourcesPath => resourcesPath;

        /// <summary>True when a load path is set (i.e. something is assigned and loadable).</summary>
        public bool IsValid => !string.IsNullOrEmpty(resourcesPath);

        /// <summary>
        /// Like <see cref="IsValid"/>, but in the editor it also confirms the asset actually exists, still lives under a
        /// Resources folder at the stored path, and is the right type - without loading it into memory. In a build there is
        /// no AssetDatabase, so it falls back to <see cref="IsValid"/>.
        /// </summary>
        public bool IsValidForEditor
        {
            get
            {
#if UNITY_EDITOR
                if (!IsValid || string.IsNullOrEmpty(guid))
                    return false;

                // Resolve the asset from its guid. None of these AssetDatabase calls load the asset object into memory;
                // they only read import metadata.
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path))
                    return false; // asset was deleted

                // It must still be under a Resources folder and its load key must match what we have stored.
                if (ToResourcesPath(path) != resourcesPath)
                    return false;

                // Verify the asset type matches T (a GameObject when T is a prefab component) without loading it.
                System.Type mainType = UnityEditor.AssetDatabase.GetMainAssetTypeAtPath(path);
                if (mainType == null)
                {
                    Debug.LogError("mainType is null");
                    return false;
                }

                System.Type requiredType = IsComponentType ? typeof(GameObject) : typeof(T);
                return requiredType.IsAssignableFrom(mainType);
#else
                return IsValid;
#endif
            }
        }

        /// <summary>True when the asset is currently loaded in memory.</summary>
        public bool IsLoaded => isLoaded && cachedAsset != null;

        /// <summary>The currently loaded asset, or null if not loaded yet.</summary>
        public T Asset => cachedAsset;

        // ---------------------------------------------------------------------
        // Save / serialize
        // ---------------------------------------------------------------------

        /// <summary>
        /// Serialize this reference to a plain string (the Resources path) for your own persistence (JSON, PlayerPrefs).
        /// Inspector-assigned fields persist automatically through Unity serialization; this is only for manual saving.
        /// </summary>
        public string Serialize() => resourcesPath ?? string.Empty;

        /// <summary>Restore this reference from a string previously produced by <see cref="Serialize"/>.</summary>
        public void Deserialize(string serializedResourcesPath)
        {
            resourcesPath = serializedResourcesPath;
        }

        // ---------------------------------------------------------------------
        // Load
        // ---------------------------------------------------------------------

        /// <summary>Load the asset synchronously and cache it. Returns null (and logs) when not assigned or not found.</summary>
        public T Load()
        {
            if (IsLoaded)
                return cachedAsset;

#if UNITY_EDITOR
            // In the editor the guid is authoritative; keep the runtime path fresh if the asset was moved/renamed.
            EditorSyncFromGuid();
#endif
            if (!IsValid)
            {
                Debug.LogError("[ResourceObject] Cannot Load: no resource assigned.");
                return null;
            }

            if (IsComponentType)
            {
                var prefab = Resources.Load<GameObject>(resourcesPath);
                loadedResource = prefab;
                cachedAsset = prefab != null ? prefab.GetComponent<T>() : null;
            }
            else
            {
                var asset = Resources.Load<T>(resourcesPath);
                loadedResource = asset;
                cachedAsset = asset;
            }

            FinishLoad();
            return cachedAsset;
        }

        /// <summary>Load the asset asynchronously and cache it. Returns null (and logs) when not assigned or not found.</summary>
        public async UniTask<T> LoadAsync(IProgress<float> progress = null)
        {
            if (IsLoaded)
                return cachedAsset;

#if UNITY_EDITOR
            EditorSyncFromGuid();
#endif
            if (!IsValid)
            {
                Debug.LogError("[ResourceObject] Cannot LoadAsync: no resource assigned.");
                return null;
            }

            if (IsComponentType)
            {
                var request = Resources.LoadAsync<GameObject>(resourcesPath);
                await request.ToUniTask(progress: progress);
                var prefab = request.asset as GameObject;
                loadedResource = prefab;
                cachedAsset = prefab != null ? prefab.GetComponent<T>() : null;
            }
            else
            {
                var request = Resources.LoadAsync<T>(resourcesPath);
                await request.ToUniTask(progress: progress);
                loadedResource = request.asset;
                cachedAsset = request.asset as T;
            }

            FinishLoad();
            return cachedAsset;
        }

        private void FinishLoad()
        {
            if (cachedAsset == null)
                Debug.LogError($"[ResourceObject] Failed to load '{resourcesPath}'. Make sure the asset is inside a 'Resources' folder.");
            isLoaded = cachedAsset != null;

            if (isLoaded)
                ResourceObjectCleaner.Register(this);
        }

        // ---------------------------------------------------------------------
        // Unload
        // ---------------------------------------------------------------------

        /// <summary>
        /// Drop the cached asset. Plain assets (textures, ScriptableObjects, materials, audio) are freed immediately via
        /// <see cref="Resources.UnloadAsset"/>. Prefabs/components cannot be unloaded individually, so their reference is
        /// dropped and the memory is reclaimed on the next <see cref="Resources.UnloadUnusedAssets"/> (see <see cref="UnloadAsync"/>).
        /// </summary>
        public void Unload()
        {
            ResourceObjectCleaner.Unregister(this);
            cachedAsset = null;
            isLoaded = false;

            if (loadedResource == null)
                return;

            if (loadedResource is GameObject || loadedResource is Component)
            {
                loadedResource = null;
                return;
            }

            Resources.UnloadAsset(loadedResource);
            loadedResource = null;
        }

        /// <summary>
        /// Like <see cref="Unload"/>, but for prefab/component assets it also awaits
        /// <see cref="Resources.UnloadUnusedAssets"/> so the memory is actually reclaimed.
        /// </summary>
        public async UniTask UnloadAsync()
        {
            bool wasGameObjectAsset = loadedResource is GameObject || loadedResource is Component;
            Unload();
            if (wasGameObjectAsset)
                await Resources.UnloadUnusedAssets().ToUniTask();
        }

#if UNITY_EDITOR
        // =====================================================================
        // Editor-only API (used by the property drawer and asset tracker).
        // Compiled out of player builds - none of this ships in the game.
        // =====================================================================

        /// <summary>Resolve the assigned asset as <typeparamref name="T"/> for inspector display. Null when unassigned or missing.</summary>
        public T GetEditorAsset()
        {
            if (string.IsNullOrEmpty(guid))
                return null;
            var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path))
                return null;

            // Mirror runtime Load(): for a component T the asset on disk is the prefab GameObject, so resolve the
            // component off it (LoadAssetAtPath<Component> on a prefab path is unreliable).
            if (IsComponentType)
            {
                var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
                return prefab != null ? prefab.GetComponent<T>() : null;
            }

            return UnityEditor.AssetDatabase.LoadAssetAtPath<T>(path);
        }

        /// <summary>True when something is assigned (guid set) but the asset no longer exists in the project.</summary>
        public bool EditorIsMissing()
        {
            if (string.IsNullOrEmpty(guid))
                return false;
            return string.IsNullOrEmpty(UnityEditor.AssetDatabase.GUIDToAssetPath(guid));
        }

        /// <summary>
        /// Assign an asset from the inspector. Returns false (and logs) if the object is not a saved asset or is not
        /// inside a Resources folder, in which case nothing is changed.
        /// </summary>
        public bool SetEditorAsset(Object asset)
        {
            if (asset == null)
            {
                Clear();
                return true;
            }

            var path = UnityEditor.AssetDatabase.GetAssetPath(asset);
            if (string.IsNullOrEmpty(path))
            {
                Debug.LogError("[ResourceObject] The assigned object is not a saved project asset.");
                return false;
            }

            var newResourcesPath = ToResourcesPath(path);
            if (string.IsNullOrEmpty(newResourcesPath))
            {
                Debug.LogError($"[ResourceObject] '{path}' is not inside a 'Resources' folder, so it cannot be loaded at runtime.");
                return false;
            }

            guid = UnityEditor.AssetDatabase.AssetPathToGUID(path);
            resourcesPath = newResourcesPath;
            return true;
        }

        /// <summary>Clear the reference.</summary>
        public void Clear()
        {
            guid = string.Empty;
            resourcesPath = string.Empty;
        }

        /// <summary>
        /// Recompute <see cref="ResourcesPath"/> from the guid so moved/renamed assets keep working. Returns true if the
        /// stored path changed. Does nothing when unassigned or when the asset was deleted (handled by the drawer).
        /// </summary>
        public bool EditorSyncFromGuid()
        {
            if (string.IsNullOrEmpty(guid))
                return false;

            var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path))
                return false;

            // Null means the asset is no longer under a Resources folder -> invalidate the path (it can't be loaded).
            var newResourcesPath = ToResourcesPath(path) ?? string.Empty;
            if (newResourcesPath == resourcesPath)
                return false;

            resourcesPath = newResourcesPath;
            return true;
        }

        /// <summary>Convert a full asset path into a Resources-relative path without extension, or null if not under Resources.</summary>
        public static string ToResourcesPath(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath))
                return null;

            assetPath = assetPath.Replace('\\', '/');

            const string marker = "/Resources/";
            int index = assetPath.LastIndexOf(marker, StringComparison.Ordinal);
            string relative;
            if (index >= 0)
            {
                relative = assetPath.Substring(index + marker.Length);
            }
            else if (assetPath.StartsWith("Resources/", StringComparison.Ordinal))
            {
                relative = assetPath.Substring("Resources/".Length);
            }
            else
            {
                return null;
            }

            int dot = relative.LastIndexOf('.');
            return dot >= 0 ? relative.Substring(0, dot) : relative;
        }
#endif
    }
}
