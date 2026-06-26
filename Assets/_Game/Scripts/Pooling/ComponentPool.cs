using System.Collections.Generic;
using UnityEngine;

namespace Pooling
{
    /// <summary>
    /// A minimal, allocation-conscious object pool for a single <typeparamref name="T"/> component prefab.
    /// Pop an instance when you need it, Push it back when you're done; instances are deactivated while
    /// pooled instead of destroyed, so frequently spawned/despawned objects (gold pickups, VFX, etc.) don't
    /// churn the GC or hitch on Instantiate.
    ///
    /// This is intentionally small and self-contained (no singleton, no manager): own one per prefab via
    /// composition. The spawner that needs gold creates a pool, prewarms it, then pops/pushes. Designed so a
    /// future proximity-culling pass can simply <see cref="Push"/> out-of-range objects and <see cref="Pop"/>
    /// in-range ones without any changes here.
    /// </summary>
    /// <typeparam name="T">The component on the pooled prefab's root.</typeparam>
    public sealed class ComponentPool<T> where T : Component
    {
        private readonly T _prefab;
        private readonly Transform _parent;
        private readonly Stack<T> _idle = new Stack<T>();

        // Live count is tracked so callers can reason about how many are out, and for prewarm math.
        private int _liveCount;

        /// <summary>Number of instances currently checked out (popped and not yet pushed back).</summary>
        public int LiveCount => _liveCount;

        /// <summary>Number of inactive instances sitting in the pool, ready to be reused.</summary>
        public int IdleCount => _idle.Count;

        /// <summary>
        /// Creates a pool for <paramref name="prefab"/>. Pooled (idle) instances are parented under
        /// <paramref name="parent"/> so they don't clutter the scene root; popped instances are unparented
        /// (or re-parented by the caller) as needed.
        /// </summary>
        public ComponentPool(T prefab, Transform parent = null)
        {
            _prefab = prefab;
            _parent = parent;
        }

        /// <summary>
        /// Instantiates <paramref name="count"/> instances up front and returns them to the pool, so the
        /// first burst of <see cref="Pop"/> calls reuses instead of instantiating. Spread the cost over
        /// frames at the call site if needed.
        /// </summary>
        public void Prewarm(int count)
        {
            for (int i = 0; i < count; i++)
            {
                T instance = CreateInstance();
                instance.gameObject.SetActive(false);
                _idle.Push(instance);
            }
        }

        /// <summary>
        /// Takes an instance from the pool (or instantiates one if the pool is empty), positions it, activates
        /// it, and returns it. The returned instance is parented to <paramref name="parent"/> when given,
        /// otherwise it keeps the pool's parent.
        /// </summary>
        public T Pop(Vector3 position, Quaternion rotation, Transform parent = null)
        {
            T instance = _idle.Count > 0 ? _idle.Pop() : CreateInstance();

            Transform t = instance.transform;
            if (parent != null)
                t.SetParent(parent, worldPositionStays: false);

            t.SetPositionAndRotation(position, rotation);
            instance.gameObject.SetActive(true);

            _liveCount++;
            return instance;
        }

        /// <summary>
        /// Returns an instance to the pool: deactivates it and re-parents it under the pool's container so it
        /// can be reused by a later <see cref="Pop"/>. Safe to call with null.
        /// </summary>
        public void Push(T instance)
        {
            if (instance == null)
                return;

            instance.gameObject.SetActive(false);
            if (_parent != null)
                instance.transform.SetParent(_parent, worldPositionStays: false);

            _idle.Push(instance);
            if (_liveCount > 0)
                _liveCount--;
        }

        private T CreateInstance()
        {
            return Object.Instantiate(_prefab, _parent);
        }
    }
}
