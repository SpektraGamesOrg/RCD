using UnityEngine;

namespace UISystem.Editor
{
    [CreateAssetMenu(fileName = "WidgetPrefabs", menuName = "UI/WidgetPrefabs", order = 0)]
    public class WidgetPrefabsSO : ScriptableObject
    {
        public enum WidgetType
        {
            ImageWidget,
            TextWidget,
            ExpandableImageTextWidget,
            ExpandableContainerTextWidget,
            ConsumableTextWidget,
            ProgressionWidget,
            ButtonWidget,
            ToggleWidget,
            TimerTextWidget,
            TabMenuWidget,
            ScrollViewWidgetHorizontal,
            ScrollViewWidgetVertical,
            ScrollViewWidgetVerticalSticky,
            ScrollViewWidgetHorizontalSticky,
            ScrollViewWidgetHorizontalGrid,
            ScrollViewWidgetVerticalGrid,
        }
        
        [SerializeField] private SerializedDictionary<WidgetType, GameObject> widgetPrefabs;
        
        public GameObject GetWidgetPrefab(WidgetType widgetType)
        {
            return widgetPrefabs[widgetType];
        }
    }
}