using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UISystem.Runtime.Scripts.Mono.ScrollView;

namespace Packages.UISystem.Editor
{
    public static class ScrollRectToNestedConverter
    {
        private const string MenuPath = "CONTEXT/ScrollRect/Convert to ScrollRectNested";

        [MenuItem(MenuPath, true)]
        private static bool Validate(MenuCommand command)
        {
            return command.context is ScrollRect and not ScrollRectNested;
        }

        [MenuItem(MenuPath, false)]
        private static void Convert(MenuCommand command)
        {
            var source = (ScrollRect)command.context;
            var go = source.gameObject;

            var content = source.content;
            var horizontal = source.horizontal;
            var vertical = source.vertical;
            var movementType = source.movementType;
            var elasticity = source.elasticity;
            var inertia = source.inertia;
            var decelerationRate = source.decelerationRate;
            var scrollSensitivity = source.scrollSensitivity;
            var viewport = source.viewport;
            var horizontalScrollbar = source.horizontalScrollbar;
            var verticalScrollbar = source.verticalScrollbar;
            var horizontalScrollbarVisibility = source.horizontalScrollbarVisibility;
            var verticalScrollbarVisibility = source.verticalScrollbarVisibility;
            var horizontalScrollbarSpacing = source.horizontalScrollbarSpacing;
            var verticalScrollbarSpacing = source.verticalScrollbarSpacing;

            Undo.SetCurrentGroupName("Convert ScrollRect to ScrollRectNested");
            int undoGroup = Undo.GetCurrentGroup();

            Undo.DestroyObjectImmediate(source);

            var nested = Undo.AddComponent<ScrollRectNested>(go);

            nested.content = content;
            nested.horizontal = horizontal;
            nested.vertical = vertical;
            nested.movementType = movementType;
            nested.elasticity = elasticity;
            nested.inertia = inertia;
            nested.decelerationRate = decelerationRate;
            nested.scrollSensitivity = scrollSensitivity;
            nested.viewport = viewport;
            nested.horizontalScrollbar = horizontalScrollbar;
            nested.verticalScrollbar = verticalScrollbar;
            nested.horizontalScrollbarVisibility = horizontalScrollbarVisibility;
            nested.verticalScrollbarVisibility = verticalScrollbarVisibility;
            nested.horizontalScrollbarSpacing = horizontalScrollbarSpacing;
            nested.verticalScrollbarSpacing = verticalScrollbarSpacing;

            if (!go.GetComponent<ScrollViewWidget>())
            {
                Undo.AddComponent<ScrollViewWidget>(go);
            }

            Undo.CollapseUndoOperations(undoGroup);

            EditorUtility.SetDirty(go);
            Debug.Log($"[ScrollRectToNestedConverter] Converted ScrollRect to ScrollRectNested on '{go.name}'.", go);
        }
    }
}