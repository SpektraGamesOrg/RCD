using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Events
{
    /// <summary>
    /// One authored event level (a Jump Challenge or Time Trial layout). Pure design-time data: the ordered list
    /// of placed pieces plus per-level tuning. Created and edited with the Level Designer window
    /// (Tools/EventSystem/Level Designer), which bakes the scene layout into <see cref="Placements"/>.
    ///
    /// Levels are plain (many-instance) ScriptableObjects - one asset per level - and are referenced from the
    /// single <see cref="EventLevelContainer"/> which orders them per mode. At runtime <see cref="EventManager"/>
    /// spawns each placement at its authored WORLD transform and teleports the car to the Start piece.
    /// </summary>
    [CreateAssetMenu(fileName = "LevelData", menuName = "EventSystem/Event Level Data")]
    public sealed class LevelData : ScriptableObject
    {
        [Title("Level")]
        [Tooltip("Which mode this level belongs to. Only Jump Challenge and Time Trial use levels.")]
        [SerializeField] private EventType eventType = EventType.JumpChallenge;

        [Tooltip("1-based level number, shown as \"LV n\" on the entry pop-up (display only; order comes from " +
                 "the EventLevelContainer).")]
        [SerializeField, Min(1)] private int levelNumber = 1;

        [Title("Reward & Rules")]
        [SerializeField, Min(0)] private int winRewardGold = 2000;
        [SerializeField, Min(0)] private int bonusRewardGold = 180;

        [Tooltip("Time Trial only: seconds allowed to reach the finish. Ignored by Jump Challenge.")]
        [SerializeField, Min(1f)] private float timeLimitSeconds = 30f;

        [Title("Layout")]
        [Tooltip("Pieces that make up the level, each with a transform local to the event area origin. Baked by " +
                 "the Level Designer window.")]
        [SerializeField] private List<LevelPlacement> placements = new List<LevelPlacement>();

        public EventType EventType => eventType;
        public int LevelNumber => levelNumber;
        public int WinRewardGold => winRewardGold;
        public int BonusRewardGold => bonusRewardGold;
        public float TimeLimitSeconds => timeLimitSeconds;
        public IReadOnlyList<LevelPlacement> Placements => placements;

#if UNITY_EDITOR
        // Keep the EventLevelContainer's per-mode ordered lists in sync when a level's mode or number changes in
        // the inspector. Deferred + coalesced so it never scans the project on every keystroke.
        private void OnValidate()
        {
            EventLevelContainer.EditorSyncDeferred();
        }

        /// <summary>
        /// Editor-only: replaces the baked layout from the Level Designer window. Marks the asset dirty so the
        /// caller only needs to save the asset database.
        /// </summary>
        public void EditorSetPlacements(List<LevelPlacement> newPlacements)
        {
            placements = newPlacements ?? new List<LevelPlacement>();
            UnityEditor.EditorUtility.SetDirty(this);
        }

        /// <summary>Editor-only: set the level's mode. Direct field write (no OnValidate) - callers re-sync.</summary>
        public void EditorSetEventType(EventType type)
        {
            if (eventType == type)
                return;

            eventType = type;
            UnityEditor.EditorUtility.SetDirty(this);
        }

        /// <summary>Editor-only: set the 1-based level number. Direct field write, so it never loops OnValidate.</summary>
        public void EditorSetLevelNumber(int number)
        {
            int clamped = number < 1 ? 1 : number;
            if (levelNumber == clamped)
                return;

            levelNumber = clamped;
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif
    }
}
