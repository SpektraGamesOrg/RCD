using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Pooling;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Gold
{
    /// <summary>
    /// Spawns gold pickups in the world at designer-placed marker positions. Designers drop empty child
    /// GameObjects under this holder to mark where gold should appear; at runtime the spawner pops a gold
    /// instance from a <see cref="ComponentPool{T}"/> at each marker's position, spread across several frames
    /// so a level full of gold doesn't cause a single-frame instantiation spike on mobile.
    ///
    /// Gold is pooled rather than plain-instantiated so a later proximity-culling pass can push out-of-range
    /// gold back to the pool and pop in-range gold without churning Instantiate/Destroy. For now every marker
    /// gets a live gold for the whole session (culling is a follow-up); the pool is already in place for it.
    ///
    /// Each gold gets a stable id derived from its marker name so its collection cooldown persists.
    /// </summary>
    public sealed class GoldSpawner : MonoBehaviour
    {
        // Name of the runtime pool container child, so marker collection can skip it.
        private const string PoolContainerName = "GoldPool";

        [Title("Spawn")]
        [Tooltip("The gold prefab popped at each marker. Must have a Gold component on its root.")]
        [SerializeField] private Gold goldPrefab;

        [Tooltip("How many gold objects to spawn per frame. Tune for the spike-vs-speed tradeoff on the " +
                 "target device.")]
        [SerializeField, Min(1)] private int objectsPerFrame = 4;

        [Tooltip("Spawn automatically on Start. Disable to trigger SpawnAsync() manually.")]
        [SerializeField] private bool spawnOnStart = true;

        [Tooltip("Parent the spawned gold under this spawner. Off by default to keep markers and live gold " +
                 "separate in the hierarchy.")]
        [SerializeField] private bool parentSpawnedToHolder = false;

        [Tooltip("Pre-instantiate this many gold instances into the pool before spawning, so the first burst " +
                 "of spawns reuses instead of instantiating. 0 = no prewarm (instances are created lazily).")]
        [SerializeField, Min(0)] private int prewarmCount = 0;

        [Title("Markers")]
        [Tooltip("The spawn markers. Populate with the 'Collect Markers' button below (it gathers the child " +
                 "marker transforms). At runtime a gold is popped at each. If left empty, the spawner falls " +
                 "back to scanning its children at Start.")]
        [SerializeField, ReadOnly] private List<Transform> markers = new List<Transform>();

        [ShowInInspector, ReadOnly]
        private bool _spawned;

        private ComponentPool<Gold> _pool;

        // Dedicated parent for pooled (idle and, when not parented to the holder, live) gold. Kept SEPARATE
        // from the spawner's own children so pooled instances are never mistaken for markers.
        private Transform _poolContainer;

        // Maps each live gold back to the marker it was spawned at, so a culling pass can push/pop by marker.
        private readonly Dictionary<Transform, Gold> _liveByMarker = new Dictionary<Transform, Gold>();

        private void Start()
        {
            if (spawnOnStart)
                SpawnAsync().Forget();
        }

        /// <summary>
        /// Pops a gold pickup at every child-transform marker, yielding every <see cref="objectsPerFrame"/>
        /// spawns to avoid a single-frame spike. Safe to call once.
        /// </summary>
        public async UniTask SpawnAsync(CancellationToken token = default)
        {
            if (_spawned)
                return;

            if (goldPrefab == null)
            {
                Debug.LogError("[GoldSpawner] No gold prefab assigned; nothing to spawn.", this);
                return;
            }

            _spawned = true;

            // Use the markers collected in the editor. If none were assigned, fall back to scanning children
            // once so the spawner still works without pressing the button.
            if (markers.Count == 0)
                CollectMarkersFromChildren();

            EnsurePool();
            if (prewarmCount > 0)
                _pool.Prewarm(prewarmCount);

            int spawnedThisFrame = 0;

            for (int i = 0; i < markers.Count; i++)
            {
                token.ThrowIfCancellationRequested();

                Transform marker = markers[i];
                if (marker == null)
                    continue;

                SpawnAtMarker(marker);

                spawnedThisFrame++;
                if (spawnedThisFrame >= objectsPerFrame)
                {
                    spawnedThisFrame = 0;
                    await UniTask.Yield(PlayerLoopTiming.Update, token);
                }
            }
        }

        /// <summary>
        /// Fills <see cref="markers"/> with this spawner's direct child transforms, skipping the runtime pool
        /// container so pooled gold is never treated as a marker. Editor button so designers wire the markers
        /// once instead of the spawner scanning the hierarchy every run.
        /// </summary>
        [Button("Collect Markers")]
        private void CollectMarkersFromChildren()
        {
            markers.Clear();
            int childCount = transform.childCount;
            for (int i = 0; i < childCount; i++)
            {
                Transform child = transform.GetChild(i);
                if (child == null || child == _poolContainer || child.name == PoolContainerName)
                    continue;

                markers.Add(child);
            }

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                UnityEditor.EditorUtility.SetDirty(this);
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
            }
#endif
        }

        /// <summary>
        /// Pops a gold at the given marker and registers it. No-op if a gold is already live there. Exposed
        /// for the future proximity-culling pass (pop gold that came back in range).
        /// </summary>
        public Gold SpawnAtMarker(Transform marker)
        {
            if (marker == null || _liveByMarker.ContainsKey(marker))
                return null;

            EnsurePool();

            // Parent under the marker when requested (keeps live gold organized next to its marker), otherwise
            // leave it in the pool container. Never parent under the spawner root, or it would be re-scanned
            // as a marker.
            Transform spawnParent = parentSpawnedToHolder ? marker : _poolContainer;
            Gold gold = _pool.Pop(marker.position, marker.rotation, spawnParent);

            // Stable id from the marker name keeps each world position's cooldown consistent.
            gold.SetGoldId($"{name}/{marker.name}");

            _liveByMarker[marker] = gold;
            return gold;
        }

        /// <summary>
        /// Returns the gold at the given marker to the pool, if one is live there. Exposed for the future
        /// proximity-culling pass (push gold that went out of range).
        /// </summary>
        public void DespawnAtMarker(Transform marker)
        {
            if (marker == null || !_liveByMarker.TryGetValue(marker, out Gold gold))
                return;

            _liveByMarker.Remove(marker);
            _pool.Push(gold);
        }

        private void EnsurePool()
        {
            if (_pool != null)
                return;

            // A dedicated container so pooled gold never sits among the spawner's marker children.
            var containerGo = new GameObject(PoolContainerName);
            _poolContainer = containerGo.transform;
            _poolContainer.SetParent(transform, worldPositionStays: false);

            _pool = new ComponentPool<Gold>(goldPrefab, _poolContainer);
        }
    }
}
