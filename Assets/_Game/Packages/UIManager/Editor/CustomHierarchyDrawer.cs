using UnityEditor;
using UnityEngine;

namespace UIManager.Editor
{
    [InitializeOnLoad]
    public class CustomHierarchyDrawer
    {
        private static GUIStyle style = null;
        private static GUIStyle windowTextStyle = null;

        static CustomHierarchyDrawer()
        {
            EditorApplication.hierarchyWindowItemByEntityIdOnGUI += HierarchyItemCB;
        }

        private static void HierarchyItemCB(EntityId entityID, Rect rect)
        {
            GameObject gameObject = EditorUtility.EntityIdToObject(entityID) as GameObject;

            if (gameObject == null || !gameObject.activeSelf)
                return;

            if (style == null)
            {
                style = new GUIStyle(GUI.skin.label);
                style.richText = true;
                style.fontStyle = FontStyle.Bold;
            }

            if (windowTextStyle == null)
            {
                windowTextStyle = new GUIStyle(GUI.skin.label);
                windowTextStyle.fontSize = 12;
                windowTextStyle.fontStyle = FontStyle.Bold;
                windowTextStyle.alignment = TextAnchor.MiddleCenter;
            }

            DrawHierarchyForScreensAndPopups(gameObject, rect);
        }

        private static void DrawHierarchyForScreensAndPopups(GameObject gameObject, Rect rect)
        {
            var windowBase = gameObject.GetComponent<UIViewBase>();
            if (windowBase == null || !windowBase.enabled || !windowBase.Content)
                return;

            float size = 15f;
            float extraX = 0f;
            var rectForButton = new Rect(
                rect.x + rect.width - size + extraX,
                rect.y,
                size,
                size);

            var defaultGUIColor = GUI.color;
            var onColor = Color.green;
            var offColor = Color.red;

            GUI.color = windowBase.ShowingOrShown ? onColor : offColor;

            if (GUI.Button(rectForButton, ""))
            {
                if (!Application.isPlaying)
                {
                    if (windowBase.ShowingOrShown)
                    {
                        windowBase.HideForEditor();
                    }
                    else
                    {
                        windowBase.ShowForEditor();
                    }
                }
                else
                {
                    if (windowBase.ViewType == UIViewBase.ViewTypeEnum.ScreenBase)
                    {
                        GameUIManager.Instance.SwitchScreen(windowBase as ScreenBase);
                    }
                    else if (windowBase.ViewType == UIViewBase.ViewTypeEnum.PopupBase)
                    {
                        if (windowBase.HidingOrHidden)
                            GameUIManager.Instance.ShowPopup(windowBase as PopupBase);
                        else
                            GameUIManager.Instance.HidePopup(windowBase as PopupBase);
                    }
                    else if (windowBase.ViewType == UIViewBase.ViewTypeEnum.OverlayBase)
                    {
                        if (windowBase.HidingOrHidden)
                            GameUIManager.Instance.ShowOverlayUI(windowBase as OverlayBase);
                        else
                            GameUIManager.Instance.HideOverlayUI(windowBase as OverlayBase);
                    }
                }
            }
            GUI.enabled = true;

            GUI.color = Color.yellow;
            var rectForText = rectForButton;
            if (windowBase.ViewType == UIViewBase.ViewTypeEnum.ScreenBase)
                GUI.Label(rectForText, "S", windowTextStyle);
            else if (windowBase.ViewType == UIViewBase.ViewTypeEnum.OverlayBase)
                GUI.Label(rectForText, "O", windowTextStyle);
            else if (windowBase.ViewType == UIViewBase.ViewTypeEnum.PopupBase)
                GUI.Label(rectForText, "P", windowTextStyle);
            else if (windowBase.ViewType == UIViewBase.ViewTypeEnum.TabBase || windowBase.ViewType == UIViewBase.ViewTypeEnum.TabbedScreenBase)
                GUI.Label(rectForText, "T", windowTextStyle);
            else
            {
                GUI.Label(rectForText, "C", windowTextStyle);
            }

            GUI.color = defaultGUIColor;
        }
    }
}