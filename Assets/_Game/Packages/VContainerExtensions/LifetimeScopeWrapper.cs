using System;
using Sirenix.OdinInspector;
using Unity.VisualScripting;
using UnityEngine;
using VContainer.Unity;

namespace _Game.Scripts.Utils.VContainer
{
    public class LifetimeScopeWrapper<T> : LifetimeScope where T : LifetimeScope
    {
#if UNITY_EDITOR
        [ShowInInspector]
        public LifetimeScope ActualParentLifeTimeScope
        {
            get { return Parent; }
            set { }
        }
#endif
        public static bool ApplicationQuiting { get; private set; }

        public static T Instance { get; private set; } = null;

        private static LifetimeScope _parentWillSet = null;

        public static T CreateInstance(LifetimeScope parentScope = null)
        {
            _parentWillSet = parentScope;

            GameObject obj = new GameObject("_" + typeof(T).Name);
            T scope = obj.AddComponent<T>();
            Instance = scope;

            if (!_parentWillSet)
            {
                DontDestroyOnLoad(obj);

                if (VContainerSettings.Instance.RootLifetimeScope &&
                    VContainerSettings.Instance.GetOrCreateRootLifetimeScopeInstance())
                {
                    obj.transform.SetParent(
                        VContainerSettings.Instance.GetOrCreateRootLifetimeScopeInstance().transform);
                }
            }
            else
            {
                obj.transform.SetParent(_parentWillSet.transform);
            }

            _parentWillSet = null;

            return scope;
        }

        protected void ForceSetInstance(T instance)
        {
            Instance = instance;
        }

        protected override void Awake()
        {
            base.Awake();

            Application.quitting += OnApplicationQuiting;

            // Build finished
            var subscribedScopes = InjectHelper.SubscribedScopes;
            foreach (var keyValuePair in subscribedScopes)
            {
                if (keyValuePair.Key == typeof(T) && keyValuePair.Value != null)
                {
                    var list = keyValuePair.Value;
                    for (var i = 0; i < list.Count; i++)
                    {
                        if (list[i] != null)
                        {
                            if (InjectHelper.DebugActive)
                            {
                                Debug.Log(
                                    $"Container inject for behaviour {list[i].GetType()}(Object name: {list[i].gameObject.name}) on this lifetime scope: {typeof(T).Name}",
                                    list[i].gameObject);
                            }

                            try
                            {
                                Container.Inject(list[i]);
                            }
                            catch (Exception e)
                            {
                                string str =
                                    $"Component name: {list[i].GetType().Name}, GameObject name: {list[i].gameObject.name}";
                                Debug.LogError(str, list[i].gameObject);
                                throw new Exception($"{str}{Environment.NewLine}{e.StackTrace}");
                            }
                        }
                    }
                }
            }

            if (InjectHelper.DebugActive)
            {
                Debug.Log("LifetimeScopeWrapper adding to ActiveLifetimeScopes for this type: " + typeof(T).Name,
                    gameObject);
            }

            InjectHelper.ActiveLifetimeScopes[typeof(T)] = this;
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();

            //Debug.LogError("OnDestroy: " + typeof(T).Name);

            if (InjectHelper.ActiveLifetimeScopes.ContainsKey(typeof(T)))
            {
                if (InjectHelper.DebugActive)
                {
                    Debug.Log(
                        "LifetimeScopeWrapper removing from ActiveLifetimeScopes for this type: " + typeof(T).Name,
                        gameObject);
                }

                InjectHelper.ActiveLifetimeScopes.Remove(typeof(T));
            }
            InjectHelper.InvalidateCacheForScope<T>();

            Instance = null;
        }

        protected override LifetimeScope FindParent()
        {
            if (_parentWillSet != null)
            {
                return _parentWillSet;
            }

            return null;
        }

        private void OnApplicationQuiting()
        {
            ApplicationQuiting = true;
        }

        private void OnApplicationQuit()
        {
            ApplicationQuiting = true;
        }
    }
}