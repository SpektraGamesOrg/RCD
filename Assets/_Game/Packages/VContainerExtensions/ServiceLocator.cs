using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace _Game.Scripts.Utils.VContainer
{
    public static class ServiceLocator
    {
        private readonly struct ServiceCacheEntry
        {
            public readonly Type ScopeType;
            public readonly LifetimeScope Scope;
            public readonly object Service;

            public ServiceCacheEntry(Type scopeType, LifetimeScope scope, object service)
            {
                ScopeType = scopeType;
                Scope = scope;
                Service = service;
            }
        }

        private readonly struct ScopedServiceKey : IEquatable<ScopedServiceKey>
        {
            public readonly Type ScopeType;
            public readonly Type ServiceType;

            public ScopedServiceKey(Type scopeType, Type serviceType)
            {
                ScopeType = scopeType;
                ServiceType = serviceType;
            }

            public bool Equals(ScopedServiceKey other)
            {
                return ScopeType == other.ScopeType && ServiceType == other.ServiceType;
            }

            public override bool Equals(object obj)
            {
                return obj is ScopedServiceKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int h = ScopeType != null ? ScopeType.GetHashCode() : 0;
                    return (h * 397) ^ (ServiceType != null ? ServiceType.GetHashCode() : 0);
                }
            }
        }

        // service type -> (scope, resolved instance) for global lookups
        private static readonly Dictionary<Type, ServiceCacheEntry> _serviceCache =
            new Dictionary<Type, ServiceCacheEntry>(64);

        // (scope type, service type) -> (scope, resolved instance) for scoped lookups
        private static readonly Dictionary<ScopedServiceKey, ServiceCacheEntry> _scopedServiceCache =
            new Dictionary<ScopedServiceKey, ServiceCacheEntry>(64);

        // Reusable buffer used to evict cache entries that point at a now-invalid scope
        // without allocating mid-iteration.
        private static readonly List<Type> _serviceCacheEvictionBuffer = new List<Type>(16);
        private static readonly List<ScopedServiceKey> _scopedCacheEvictionBuffer = new List<ScopedServiceKey>(16);

        // Guards every read/mutate of the caches and eviction buffers above. Today all known
        // callers resolve on the Unity main thread (service refreshes that hop to the thread pool
        // switch back before touching the locator), so this is defensive hardening for the
        // overlapped startup (parallel meta-init waves): it keeps a future or incidental
        // pool-thread lookup from corrupting the dictionaries on a cold cache. Note it does NOT
        // cover InjectHelper.ActiveLifetimeScopes, which is only ever mutated from scope
        // Awake/OnDestroy (main thread) — a truly off-thread resolver would still race scope
        // creation/teardown. Uncontended on the single-threaded hot path, so behavior is
        // unchanged for sequential callers.
        private static readonly object _cacheLock = new object();

        /// <summary>
        /// Returns true if the cached scope reference is still alive, still registered
        /// under the same type, and still the same instance we cached. Any failure here
        /// means the cache entry is stale and must be evicted.
        /// </summary>
        private static bool IsCachedScopeValid(Type scopeType, LifetimeScope cachedScope)
        {
            // Unity overloaded null check: catches destroyed GameObjects / scopes.
            if (!cachedScope)
                return false;

            // Wrapper removes itself from ActiveLifetimeScopes on OnDestroy; if missing,
            // the scope is considered torn down (covers dispose / unload paths).
            if (!InjectHelper.ActiveLifetimeScopes.TryGetValue(scopeType, out LifetimeScope current))
                return false;

            // A scope of the same type may have been replaced by a new instance.
            if (!ReferenceEquals(current, cachedScope))
                return false;

            // Disposed container guard — if VContainer cleared it for any reason.
            if (cachedScope.Container == null)
                return false;

            return true;
        }

        /// <summary>Drops every cached service resolution. Safe to call at any time.</summary>
        public static void ClearCache()
        {
            lock (_cacheLock)
            {
                _serviceCache.Clear();
                _scopedServiceCache.Clear();
            }
        }

        /// <summary>
        /// Evicts every cache entry that resolves through the given scope type.
        /// Optional hook — the cache also self-heals via validation on each lookup,
        /// but call this from teardown paths if you want eager release.
        /// </summary>
        public static void InvalidateCacheForScope(Type scopeType)
        {
            if (scopeType == null)
                return;

            lock (_cacheLock)
            {
                _serviceCacheEvictionBuffer.Clear();
                foreach (var kvp in _serviceCache)
                {
                    if (kvp.Value.ScopeType == scopeType)
                        _serviceCacheEvictionBuffer.Add(kvp.Key);
                }
                for (int i = 0; i < _serviceCacheEvictionBuffer.Count; i++)
                    _serviceCache.Remove(_serviceCacheEvictionBuffer[i]);
                _serviceCacheEvictionBuffer.Clear();

                _scopedCacheEvictionBuffer.Clear();
                foreach (var kvp in _scopedServiceCache)
                {
                    if (kvp.Key.ScopeType == scopeType)
                        _scopedCacheEvictionBuffer.Add(kvp.Key);
                }
                for (int i = 0; i < _scopedCacheEvictionBuffer.Count; i++)
                    _scopedServiceCache.Remove(_scopedCacheEvictionBuffer[i]);
                _scopedCacheEvictionBuffer.Clear();
            }
        }

        public static void InvalidateCacheForScope<TScope>() where TScope : LifetimeScope
        {
            InvalidateCacheForScope(typeof(TScope));
        }

#if UNITY_EDITOR
        [Button]
        public static List<System.Object> GetAllServicesForEditor()
        {
            List<System.Object> list = new List<System.Object>();

            var interfaceType = typeof(IService);

            var allServiceTypes = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => a.GetTypes())
                .Where(t =>
                    t.IsClass &&
                    !t.IsAbstract &&
                    interfaceType.IsAssignableFrom(t)).ToList();

            var method = typeof(ServiceLocator)
                .GetMethod(nameof(ServiceLocator.TryGetService), BindingFlags.Public | BindingFlags.Static);

            for (var i = 0; i < allServiceTypes.Count; i++)
            {
                var genericMethod = method.MakeGenericMethod(allServiceTypes[i]);
                var parameters = new object[] { null };

                // Call: TryGetService<TService>(out TService service)
                var result = (bool)genericMethod.Invoke(null, parameters);

                // If resolved, parameters[0] now holds the service instance
                if (result && parameters[0] != null)
                {
                    var serviceInstance = parameters[0];
                    if (serviceInstance != null && !list.Contains(serviceInstance))
                        list.Add(serviceInstance);
                }
            }

            return list;
        }
#endif

        public static bool TryGetResolverFromScope<TScope>(out IObjectResolver resolver) where TScope : LifetimeScope
        {
            Type tType = typeof(TScope);

            if (InjectHelper.ActiveLifetimeScopes.TryGetValue(tType, out LifetimeScope scope) && scope)
            {
                resolver = scope.Container;
                return true;
            }

            resolver = null;
            return false;
        }

        public static bool TryGetServiceFromScope<TService>(
            this LifetimeScope lifeTimeScope, out TService service)
            where TService : IService
        {
            if (lifeTimeScope &&
                lifeTimeScope.Container != null &&
                lifeTimeScope.Container.TryResolve<TService>(out TService resolved))
            {
                service = resolved;
                return true;
            }

            service = default;
            return false;
        }

        public static TService GetServiceFromScope<TService>(
            this LifetimeScope lifeTimeScope)
            where TService : IService
        {
            if (lifeTimeScope &&
                lifeTimeScope.Container != null &&
                lifeTimeScope.Container.TryResolve<TService>(out TService resolved))
            {
                return resolved;
            }

            if (!lifeTimeScope)
                Debug.LogError("lifeTimeScope is null");
            Debug.LogError("Service not found for " + typeof(TService));
            return default;
        }

        public static bool TryGetService<TService>(out TService service) where TService : IService
        {
            lock (_cacheLock)
            {
                Type serviceType = typeof(TService);

                // Fast path: validated cache hit.
                if (_serviceCache.TryGetValue(serviceType, out ServiceCacheEntry entry))
                {
                    if (IsCachedScopeValid(entry.ScopeType, entry.Scope) && entry.Service is TService cached)
                    {
                        service = cached;
                        return true;
                    }

                    _serviceCache.Remove(serviceType);
                }

                // Slow path: iterate active scopes and refresh the cache.
                foreach (var kvp in InjectHelper.ActiveLifetimeScopes)
                {
                    LifetimeScope scope = kvp.Value;
                    if (scope == null)
                        continue;

                    IObjectResolver resolver = scope.Container;
                    if (resolver == null)
                        continue;

                    if (resolver.TryResolve<TService>(out TService resolved))
                    {
                        _serviceCache[serviceType] = new ServiceCacheEntry(kvp.Key, scope, resolved);
                        service = resolved;
                        return true;
                    }
                }

                service = default;
                return false;
            }
        }

        public static TService GetService<TService>() where TService : IService
        {
            if (TryGetService(out TService service))
                return service;

            Debug.LogError("Service not found for " + typeof(TService));
            return default;
        }

        public static bool TryGetServiceFromScope<TService, TScope>(out TService service)
            where TService : IService
            where TScope : LifetimeScope
        {
            lock (_cacheLock)
            {
                Type scopeType = typeof(TScope);
                Type serviceType = typeof(TService);
                ScopedServiceKey key = new ScopedServiceKey(scopeType, serviceType);

                // Fast path: validated cache hit.
                if (_scopedServiceCache.TryGetValue(key, out ServiceCacheEntry entry))
                {
                    if (IsCachedScopeValid(entry.ScopeType, entry.Scope) && entry.Service is TService cached)
                    {
                        service = cached;
                        return true;
                    }

                    _scopedServiceCache.Remove(key);
                }

                // Slow path: resolve from the specific scope and refresh the cache.
                if (InjectHelper.ActiveLifetimeScopes.TryGetValue(scopeType, out LifetimeScope scope) &&
                    scope &&
                    scope.Container != null &&
                    scope.Container.TryResolve<TService>(out TService resolved))
                {
                    _scopedServiceCache[key] = new ServiceCacheEntry(scopeType, scope, resolved);
                    service = resolved;
                    return true;
                }

                service = default;
                return false;
            }
        }

        public static bool TryGetScope<TScope>(out TScope scope)
            where TScope : LifetimeScope
        {
            if (InjectHelper.ActiveLifetimeScopes.TryGetValue(typeof(TScope), out LifetimeScope scopeNotCasted) &&
                scopeNotCasted)
            {
                scope = (TScope)scopeNotCasted;
                return true;
            }

            scope = null;
            return false;
        }

        public static TScope GetScope<TScope>() where TScope : LifetimeScope
        {
            if (InjectHelper.ActiveLifetimeScopes.TryGetValue(typeof(TScope), out LifetimeScope scopeNotCasted) &&
                scopeNotCasted)
            {
                return (TScope)scopeNotCasted;
            }

            Debug.LogError("Scope not found: " + typeof(TScope));
            return null;
        }
    }
}
