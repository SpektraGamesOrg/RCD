using UnityEditor;
using UnityEngine;

namespace UISystem.Editor
{
    /// <summary>
    /// A static class responsible for instantiating various UI widgets in the Unity Editor.
    /// </summary>
    public static class WidgetGenerator
    {
        private const string WidgetPrefabsSoPath = "Assets/_Game/Packages/UISystem/Editor/WidgetPrefabsSO.asset";

        /// <summary>
        /// Instantiates a ButtonWidget prefab.
        /// </summary>
        /// <param name="menuCommand">The menu command context.</param>
        [MenuItem("GameObject/UI Widgets/ButtonWidget", false, 10)]
        private static void InstantiateButtonWidget(MenuCommand menuCommand)
        {
            InstantiateWidget(menuCommand, WidgetPrefabsSO.WidgetType.ButtonWidget);
        }

        /// <summary>
        /// Instantiates a TextWidget prefab.
        /// </summary>
        /// <param name="menuCommand">The menu command context.</param>
        [MenuItem("GameObject/UI Widgets/TextWidget", false, 11)]
        private static void InstantiateTextWidget(MenuCommand menuCommand)
        {
            InstantiateWidget(menuCommand, WidgetPrefabsSO.WidgetType.TextWidget);
        }

        /// <summary>
        /// Instantiates an ImageWidget prefab.
        /// </summary>
        /// <param name="menuCommand">The menu command context.</param>
        [MenuItem("GameObject/UI Widgets/ImageWidget", false, 12)]
        private static void InstantiateImageWidget(MenuCommand menuCommand)
        {
            InstantiateWidget(menuCommand, WidgetPrefabsSO.WidgetType.ImageWidget);
        }

        /// <summary>
        /// Instantiates an ExpandableImageTextWidget prefab.
        /// </summary>
        /// <param name="menuCommand">The menu command context.</param>
        [MenuItem("GameObject/UI Widgets/ExpandableImageTextWidget", false, 13)]
        private static void InstantiateExpandableImageTextWidget(MenuCommand menuCommand)
        {
            InstantiateWidget(menuCommand, WidgetPrefabsSO.WidgetType.ExpandableImageTextWidget);
        }

        /// <summary>
        /// Instantiates an ExpandableContainerTextWidget prefab.
        /// </summary>
        /// <param name="menuCommand">The menu command context.</param>
        [MenuItem("GameObject/UI Widgets/ExpandableContainerTextWidget", false, 14)]
        private static void InstantiateExpandableContainerTextWidget(MenuCommand menuCommand)
        {
            InstantiateWidget(menuCommand, WidgetPrefabsSO.WidgetType.ExpandableContainerTextWidget);
        }

        /// <summary>
        /// Instantiates a ConsumableTextWidget prefab.
        /// </summary>
        /// <param name="menuCommand">The menu command context.</param>
        [MenuItem("GameObject/UI Widgets/ConsumableTextWidget", false, 15)]
        private static void InstantiateConsumableTextWidget(MenuCommand menuCommand)
        {
            InstantiateWidget(menuCommand, WidgetPrefabsSO.WidgetType.ConsumableTextWidget);
        }

        /// <summary>
        /// Instantiates a ProgressionWidget prefab.
        /// </summary>
        /// <param name="menuCommand">The menu command context.</param>
        [MenuItem("GameObject/UI Widgets/ProgressionWidget", false, 16)]
        private static void InstantiateProgressionWidget(MenuCommand menuCommand)
        {
            InstantiateWidget(menuCommand, WidgetPrefabsSO.WidgetType.ProgressionWidget);
        }

        /// <summary>
        /// Instantiates a ToggleWidget prefab.
        /// </summary>
        /// <param name="menuCommand">The menu command context.</param>
        [MenuItem("GameObject/UI Widgets/ToggleWidget", false, 17)]
        private static void InstantiateToggleWidget(MenuCommand menuCommand)
        {
            InstantiateWidget(menuCommand, WidgetPrefabsSO.WidgetType.ToggleWidget);
        }

        /// <summary>
        /// Instantiates a TimerTextWidget prefab.
        /// </summary>
        /// <param name="menuCommand">The menu command context.</param>
        [MenuItem("GameObject/UI Widgets/TimerTextWidget", false, 18)]
        private static void InstantiateTimerTextWidget(MenuCommand menuCommand)
        {
            InstantiateWidget(menuCommand, WidgetPrefabsSO.WidgetType.TimerTextWidget);
        }

        /// <summary>
        /// Instantiates a ScrollViewWidgetHorizontal prefab.
        /// </summary>
        /// <param name="menuCommand">The menu command context.</param>
        [MenuItem("GameObject/UI Widgets/ScrollView/ScrollViewWidgetHorizontal", false, 19)]
        private static void InstantiateScrollViewWidgetHorizontal(MenuCommand menuCommand)
        {
            InstantiateWidget(menuCommand, WidgetPrefabsSO.WidgetType.ScrollViewWidgetHorizontal);
        }

        /// <summary>
        /// Instantiates a ScrollViewWidgetVertical prefab.
        /// </summary>
        /// <param name="menuCommand">The menu command context.</param>
        [MenuItem("GameObject/UI Widgets/ScrollView/ScrollViewWidgetVertical", false, 20)]
        private static void InstantiateScrollViewWidgetVertical(MenuCommand menuCommand)
        {
            InstantiateWidget(menuCommand, WidgetPrefabsSO.WidgetType.ScrollViewWidgetVertical);
        }

        /// <summary>
        /// Instantiates a ScrollViewWidgetVerticalSticky prefab.
        /// </summary>
        /// <param name="menuCommand">The menu command context.</param>
        [MenuItem("GameObject/UI Widgets/ScrollView/ScrollViewWidgetVerticalSticky", false, 21)]
        private static void InstantiateScrollViewWidgetVerticalSticky(MenuCommand menuCommand)
        {
            InstantiateWidget(menuCommand, WidgetPrefabsSO.WidgetType.ScrollViewWidgetVerticalSticky);
        }

        /// <summary>
        /// Instantiates a ScrollViewWidgetHorizontalSticky prefab.
        /// </summary>
        /// <param name="menuCommand">The menu command context.</param>
        [MenuItem("GameObject/UI Widgets/ScrollView/ScrollViewWidgetHorizontalSticky", false, 22)]
        private static void InstantiateScrollViewWidgetHorizontalSticky(MenuCommand menuCommand)
        {
            InstantiateWidget(menuCommand, WidgetPrefabsSO.WidgetType.ScrollViewWidgetHorizontalSticky);
        }

        /// <summary>
        /// Instantiates a ScrollViewWidgetHorizontalGrid prefab.
        /// </summary>
        /// <param name="menuCommand">The menu command context.</param>
        [MenuItem("GameObject/UI Widgets/ScrollView/ScrollViewWidgetHorizontalGrid", false, 23)]
        private static void InstantiateScrollViewWidgetHorizontalGrid(MenuCommand menuCommand)
        {
            InstantiateWidget(menuCommand, WidgetPrefabsSO.WidgetType.ScrollViewWidgetHorizontalGrid);
        }

        /// <summary>
        /// Instantiates a ScrollViewWidgetVerticalGrid prefab.
        /// </summary>
        /// <param name="menuCommand">The menu command context.</param>
        [MenuItem("GameObject/UI Widgets/ScrollView/ScrollViewWidgetVerticalGrid", false, 24)]
        private static void InstantiateScrollViewWidgetVerticalGrid(MenuCommand menuCommand)
        {
            InstantiateWidget(menuCommand, WidgetPrefabsSO.WidgetType.ScrollViewWidgetVerticalGrid);
        }

        /// <summary>
        /// Instantiates a TabMenuWidget prefab.
        /// </summary>
        /// <param name="menuCommand">The menu command context.</param>
        [MenuItem("GameObject/UI Widgets/TabMenuWidget", false, 25)]
        private static void InstantiateTabMenuWidget(MenuCommand menuCommand)
        {
            InstantiateWidget(menuCommand, WidgetPrefabsSO.WidgetType.TabMenuWidget);
        }

        /// <summary>
        /// Instantiates a widget prefab based on the specified widget type.
        /// </summary>
        /// <param name="menuCommand">The menu command context.</param>
        /// <param name="widgetType">The type of widget to instantiate.</param>
        private static void InstantiateWidget(MenuCommand menuCommand, WidgetPrefabsSO.WidgetType widgetType)
        {
            var prefab = GetWidgetPrefabFromSo(widgetType);
            if (prefab != null)
            {
                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                GameObjectUtility.SetParentAndAlign(instance, menuCommand.context as GameObject);
                Undo.RegisterCreatedObjectUndo(instance, "Instantiate " + instance.name);
                Selection.activeObject = instance;
            }
            else
            {
                Debug.LogError("Prefab not found: " + widgetType);
            }
        }

        /// <summary>
        /// Retrieves the widget prefab from the ScriptableObject based on the specified widget type.
        /// </summary>
        /// <param name="widgetType">The type of widget to retrieve.</param>
        /// <returns>The widget prefab GameObject.</returns>
        private static GameObject GetWidgetPrefabFromSo(WidgetPrefabsSO.WidgetType widgetType)
        {
            var widgetPrefabsSo = AssetDatabase.LoadAssetAtPath<WidgetPrefabsSO>(WidgetPrefabsSoPath);

            if (widgetPrefabsSo != null)
            {
                return widgetPrefabsSo.GetWidgetPrefab(widgetType);
            }
            else
            {
                Debug.LogError("WidgetPrefabsSO not found: " + WidgetPrefabsSoPath);
                return null;
            }
        }
    }
}