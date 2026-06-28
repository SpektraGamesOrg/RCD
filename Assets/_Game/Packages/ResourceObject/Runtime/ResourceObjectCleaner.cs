using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpektraGames.ResourceObject.Runtime
{
    internal interface IResourceObjectCache
    {
        void Unload();
    }

    /// <summary>Provides global cleanup for every loaded <see cref="ResourceObject{T}"/> cache.</summary>
    public static class ResourceObjectCleaner
    {
        private static readonly List<WeakReference<IResourceObjectCache>> LoadedObjects = new();

        internal static void Register(IResourceObjectCache resourceObject)
        {
            for (int i = LoadedObjects.Count - 1; i >= 0; i--)
            {
                if (!LoadedObjects[i].TryGetTarget(out IResourceObjectCache loadedObject))
                {
                    LoadedObjects.RemoveAt(i);
                    continue;
                }

                if (ReferenceEquals(loadedObject, resourceObject))
                    return;
            }

            LoadedObjects.Add(new WeakReference<IResourceObjectCache>(resourceObject));
        }

        internal static void Unregister(IResourceObjectCache resourceObject)
        {
            for (int i = LoadedObjects.Count - 1; i >= 0; i--)
            {
                if (!LoadedObjects[i].TryGetTarget(out IResourceObjectCache loadedObject) ||
                    ReferenceEquals(loadedObject, resourceObject))
                {
                    LoadedObjects.RemoveAt(i);
                }
            }
        }

        /// <summary>Unload the cache held by every currently loaded <see cref="ResourceObject{T}"/>.</summary>
        public static void UnloadAll()
        {
            var loadedObjects = new List<IResourceObjectCache>(LoadedObjects.Count);

            for (int i = 0; i < LoadedObjects.Count; i++)
            {
                if (LoadedObjects[i].TryGetTarget(out IResourceObjectCache loadedObject))
                    loadedObjects.Add(loadedObject);
            }

            // Clear before invoking Unload because each object unregisters itself.
            LoadedObjects.Clear();

            for (int i = 0; i < loadedObjects.Count; i++)
                loadedObjects[i].Unload();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            LoadedObjects.Clear();
        }
    }
}