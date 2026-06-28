using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using SpektraGames.SpektraUtilities.Runtime;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace _Game.Scripts.Utils.VContainer
{
    public static class InjectHelper
    {
        private static readonly bool _debug = false;
        public static bool DebugActive => _debug;

        public static readonly Dictionary<Type, List<MonoBehaviour>> SubscribedScopes =
            new Dictionary<Type, List<MonoBehaviour>>();
        public static Dictionary<Type, LifetimeScope> ActiveLifetimeScopes = new();

        public static async UniTask WaitForScopeReady<TScope>() where TScope : LifetimeScope
        {
            Type scopeType = typeof(TScope);

            while (true)
            {
                using var enumerator = ActiveLifetimeScopes.GetEnumerator();
                while (enumerator.MoveNext())
                {
                    var element = enumerator.Current;

                    if (element.Key == scopeType)
                    {
                        return;
                    }
                }

                await UniTask.WaitForEndOfFrame();
            }
        }

        public static void InvalidateCacheForScope<TScope>() where TScope : LifetimeScope
        {
            ServiceLocator.InvalidateCacheForScope<TScope>();
        }

#if !DISABLE_SRDEBUGGER
        // Maps MonoBehaviour reference (by identity) to its type name at inject time
        private static Dictionary<MonoBehaviour, string> _injectDebugNames = new Dictionary<MonoBehaviour, string>(ReferenceEqualityComparer.Instance);

        private class ReferenceEqualityComparer : IEqualityComparer<MonoBehaviour>
        {
            public static readonly ReferenceEqualityComparer Instance = new ReferenceEqualityComparer();
            public bool Equals(MonoBehaviour x, MonoBehaviour y) => ReferenceEquals(x, y);
            public int GetHashCode(MonoBehaviour obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
        }

        public static void LogSubscribedScopeCounts()
        {
            int total = 0;
            int nullCount = 0;
            foreach (var kvp in SubscribedScopes)
            {
                int count = kvp.Value.Count;
                int nulls = 0;
                foreach (var mb in kvp.Value)
                {
                    if (mb == null)
                    {
                        nulls++;
                        if (_injectDebugNames.TryGetValue(mb, out var debugName))
                            Debug.LogError($"[MemLeak:InjectHelper] LEAKED: {debugName} (in scope {kvp.Key.Name})");
                        else
                            Debug.LogError($"[MemLeak:InjectHelper] LEAKED: unknown (in scope {kvp.Key.Name})");
                    }
                }
                total += count;
                nullCount += nulls;
                Debug.LogError($"[MemLeak:InjectHelper] Scope {kvp.Key.Name}: {count} entries ({nulls} null/destroyed)");
            }
            Debug.LogError($"[MemLeak:InjectHelper] TOTAL: {total} entries ({nullCount} null/destroyed)");
        }
#endif

        public static void Inject(IInjectableBehaviour injectable, MonoBehaviour behaviour)
        {
            if (_debug)
            {
                Debug.Log($"Injecting this behaviour: ({behaviour.GetType().Name}) - {behaviour.gameObject.name}",
                    behaviour.gameObject);
            }

            if (injectable.SubscribedLifeTimeScopeType != null)
            {
                var type = injectable.SubscribedLifeTimeScopeType;

                if (SubscribedScopes.TryGetValue(type, out List<MonoBehaviour> list))
                {
                    list.Add(behaviour);
                }
                else
                {
                    SubscribedScopes.Add(type, new List<MonoBehaviour>() { behaviour });
                }
#if !DISABLE_SRDEBUGGER
                _injectDebugNames[behaviour] = $"{behaviour.GetType().Name} on {behaviour.gameObject.name}";
#endif

                if (ActiveLifetimeScopes.ContainsKey(type))
                {
                    if (_debug)
                    {
                        Debug.Log(
                            $"Injecting to {type.Name} scope: ({behaviour.GetType().Name}) - {behaviour.gameObject.name}",
                            behaviour.gameObject);
                    }

                    try
                    {
                        ActiveLifetimeScopes[type].Container.Inject(behaviour);
                    }
                    catch (Exception e)
                    {
                        string str =
                            $"Component name: {behaviour.name}, GameObject name: {behaviour.gameObject.name}";
                        Debug.LogError(
                            $"Injecting to {type.Name} scope: ({behaviour.GetType().Name}) - {behaviour.gameObject.name}",
                            behaviour.gameObject);
                        Debug.LogError(str, behaviour.gameObject);
                        throw new Exception($"{str}{Environment.NewLine}{e.ParseException()}");
                    }
                }
            }
        }

        public static void Deinject(IInjectableBehaviour injectable, MonoBehaviour behaviour)
        {
            if (_debug)
            {
                Debug.Log($"De-Injecting this behaviour: ({behaviour.GetType().Name}) - {behaviour.gameObject.name}",
                    behaviour.gameObject);
            }

            if (injectable.SubscribedLifeTimeScopeType != null)
            {
                foreach (var keyValuePair in SubscribedScopes)
                {
                    var list = keyValuePair.Value;
                    if (list != null)
                    {
                        if (list.Contains(behaviour))
                        {
                            list.Remove(behaviour);
                        }
                    }
                }
            }

#if !DISABLE_SRDEBUGGER
            _injectDebugNames.Remove(behaviour);
#endif
        }
    }
}