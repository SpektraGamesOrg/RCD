using System.Collections.Generic;
using Sirenix.OdinInspector;
using SpektraGames.SpektraUtilities.Runtime;
using UnityEngine;
#if UNITY_EDITOR
using System.Linq;
#endif

namespace Events
{
    /// <summary>
    /// The ordered level tables for the level-based events (GDD: 15 levels each for Jump Challenge and Time
    /// Trial). A single <see cref="SingletonScriptableObject{T}"/> loaded from Resources by type name, so the
    /// asset MUST be named "EventLevelContainer" and live in a Resources folder
    /// (e.g. Assets/_Game/Data/Resources/EventLevelContainer.asset).
    ///
    /// Levels are NOT tied to a specific area: every area of a mode plays the player's CURRENT level for that
    /// mode (tracked in <see cref="Save.SaveManager"/>). Beating a level advances the counter for the whole map,
    /// and it wraps back to level 1 after the last one - an endless loop (per the design answer).
    /// </summary>
    [CreateAssetMenu(fileName = "EventLevelContainer", menuName = "EventSystem/Event Level Container")]
    public sealed class EventLevelContainer : SingletonScriptableObject<EventLevelContainer>
    {
        [Title("Jump Challenge")]
        [Tooltip("Ordered Jump Challenge levels (index 0 = level 1). Played in order, then wraps.")]
        [SerializeField] private List<LevelData> jumpChallengeLevels = new List<LevelData>();

        [Title("Time Trial")]
        [Tooltip("Ordered Time Trial levels (index 0 = level 1). Played in order, then wraps.")]
        [SerializeField] private List<LevelData> timeTrialLevels = new List<LevelData>();

        /// <summary>Number of authored levels for a mode (0 for Watch &amp; Earn, which has no levels).</summary>
        public int GetLevelCount(EventType type)
        {
            List<LevelData> list = ListFor(type);
            return list?.Count ?? 0;
        }

        /// <summary>
        /// Resolves the <see cref="LevelData"/> for a 1-based level number, wrapping past the end so the loop is
        /// endless. Returns null (with an error) when the mode has no levels authored.
        /// </summary>
        public LevelData GetLevel(EventType type, int levelNumber)
        {
            List<LevelData> list = ListFor(type);
            if (list == null || list.Count == 0)
            {
                Debug.LogError($"[EventLevelContainer] No levels authored for {type}.", this);
                return null;
            }

            int index = (levelNumber - 1) % list.Count;
            if (index < 0)
                index += list.Count;

            return list[index];
        }

        private List<LevelData> ListFor(EventType type)
        {
            switch (type)
            {
                case EventType.JumpChallenge: return jumpChallengeLevels;
                case EventType.TimeTrial: return timeTrialLevels;
                default: return null; // Watch & Earn has no levels
            }
        }

#if UNITY_EDITOR
        // Coalesces a burst of change notifications (many OnValidate calls, an import, etc.) into one rebuild.
        private static bool _syncQueued;

        [Title("Editor")]
        [Button(ButtonSizes.Large), GUIColor(0.4f, 0.8f, 1f)]
        private void SyncFromProject() => EditorSync();

        /// <summary>
        /// Schedules a single deferred rebuild. Called by <see cref="LevelData"/>.OnValidate and the asset
        /// postprocessor so the container stays in sync automatically when levels are added / removed / retyped /
        /// renumbered - without doing a project scan on every keystroke.
        /// </summary>
        public static void EditorSyncDeferred()
        {
            if (_syncQueued)
                return;

            _syncQueued = true;
            UnityEditor.EditorApplication.delayCall += () =>
            {
                _syncQueued = false;
                if (UnityEditor.EditorApplication.isCompiling || UnityEditor.EditorApplication.isUpdating)
                {
                    EditorSyncDeferred(); // try again once the editor is idle
                    return;
                }

                EditorSync();
            };
        }

        /// <summary>Rebuilds both ordered lists from every <see cref="LevelData"/> asset in the project.</summary>
        public static void EditorSync()
        {
            EventLevelContainer container = Instance;
            if (!container)
            {
                Debug.LogError("[EventLevelContainer] Asset not found in Resources; cannot sync.");
                return;
            }

            container.RebuildFromProject();
        }

        private void RebuildFromProject()
        {
            var all = new List<LevelData>();
            string[] guids = UnityEditor.AssetDatabase.FindAssets("t:LevelData");
            for (int i = 0; i < guids.Length; i++)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[i]);
                var level = UnityEditor.AssetDatabase.LoadAssetAtPath<LevelData>(path);
                if (level)
                    all.Add(level);
            }

            jumpChallengeLevels = BuildOrdered(all, EventType.JumpChallenge);
            timeTrialLevels = BuildOrdered(all, EventType.TimeTrial);

            UnityEditor.EditorUtility.SetDirty(this);
            UnityEditor.AssetDatabase.SaveAssets();
        }

        // Levels of a mode, ordered by their current number (then name for stability), then renumbered 1..N so the
        // stored LevelNumber always matches the play order. Editor LINQ is allowed (CLAUDE.md).
        private static List<LevelData> BuildOrdered(List<LevelData> all, EventType type)
        {
            List<LevelData> list = all
                .Where(l => l.EventType == type)
                .OrderBy(l => l.LevelNumber)
                .ThenBy(l => l.name)
                .ToList();

            for (int i = 0; i < list.Count; i++)
                list[i].EditorSetLevelNumber(i + 1);

            return list;
        }
#endif
    }
}
