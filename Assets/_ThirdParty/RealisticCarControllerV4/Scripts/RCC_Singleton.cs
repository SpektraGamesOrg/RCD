//----------------------------------------------
//            Realistic Car Controller
//
// Copyright © 2014 - 2025 BoneCracker Games
// https://www.bonecrackergames.com
// Ekrem Bugra Ozdoganlar
//
//----------------------------------------------


// Singleton pattern: http://wiki.unity3d.com/index.php/Singleton
using UnityEngine;

/// <summary>
/// Thread-safe generic singleton base class for RCC manager components.
/// Automatically finds or creates an instance of the specified type when accessed.
/// Used by RCC_SceneManager, RCC_InputManager, RCC_SkidmarksManager, and other scene-wide managers.
/// </summary>
/// <typeparam name="T">The concrete RCC_Core-derived type to be used as a singleton.</typeparam>
public class RCC_Singleton<T> : RCC_Core where T : RCC_Core {

    /// <summary>
    /// The singleton instance, lazily initialized on first access.
    /// </summary>
    protected static T m_Instance;

    /// <summary>
    /// Lock object for thread-safe singleton access.
    /// </summary>
    private static readonly object m_Lock = new object();

    /// <summary>
    /// Prevents singleton creation during application shutdown to avoid stale objects.
    /// </summary>
    private static bool applicationIsQuitting = false;

    /// <summary>
    /// Gets the singleton instance. Searches the scene for an existing instance, or creates a new GameObject if none is found.
    /// Returns null during application shutdown to prevent stale object creation.
    /// </summary>
    public static T Instance {

        get {

            if (applicationIsQuitting)
                return null;

            lock (m_Lock) {

                if (m_Instance != null)
                    return m_Instance;

#if !UNITY_2022_1_OR_NEWER
                // Search for existing instance.
                m_Instance = (T)FindObjectOfType(typeof(T));
#else
                // Search for existing instance.
                m_Instance = (T)FindFirstObjectByType(typeof(T));
#endif
                // Create new instance if one doesn't already exist.
                if (m_Instance != null) return m_Instance;
                // Need to create a new GameObject to attach the singleton to.
                var singletonObject = new GameObject();
                m_Instance = singletonObject.AddComponent<T>();
                singletonObject.name = typeof(T).ToString();

                // Make instance persistent.
                //DontDestroyOnLoad(singletonObject);

                return m_Instance;

            }

        }

    }

    public bool Exist()
    {
        return m_Instance;
    }
    
    protected virtual void OnApplicationQuit() {

        applicationIsQuitting = true;

    }

    protected virtual void OnDestroy() {

        if (m_Instance == this)
            m_Instance = null;

    }

}
