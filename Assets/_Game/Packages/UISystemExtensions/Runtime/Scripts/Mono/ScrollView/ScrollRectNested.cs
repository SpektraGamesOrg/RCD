using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace UISystem.Runtime.Scripts.Mono.ScrollView
{
    /// <summary>
    /// A custom ScrollRect that routes drag events to parent ScrollRects if necessary.
    /// </summary>
    public class ScrollRectNested : ScrollRect
    {
        private bool routeToParent = false;

        /// <summary>Raised when the user begins dragging this scroll rect (after routing is decided).</summary>
        public event Action<PointerEventData> OnBeginDragEvent;

        /// <summary>
        /// Raised when the user releases a drag on this scroll rect. Carries the pointer data so
        /// consumers can measure the drag distance (e.g. press-to-release delta) to decide whether to
        /// advance to the next item or snap back, instead of polling velocity.
        /// </summary>
        public event Action<PointerEventData> OnEndDragEvent;

        /// <summary>
        /// Executes the specified action for all parent components of type T.
        /// </summary>
        /// <typeparam name="T">The type of the parent components to execute the action on.</typeparam>
        /// <param name="action">The action to execute.</param>
        private void DoForParents<T>(Action<T> action) where T : IEventSystemHandler
        {
            Transform parent = transform.parent;
            while (parent != null)
            {
                foreach (var component in parent.GetComponents<Component>())
                {
                    if (component is T)
                        action((T)(IEventSystemHandler)component);
                }

                parent = parent.parent;
            }
        }

        /// <summary>
        /// Routes the initialize potential drag event to parent components.
        /// </summary>
        /// <param name="eventData">Current event data.</param>
        public override void OnInitializePotentialDrag(PointerEventData eventData)
        {
            DoForParents<IInitializePotentialDragHandler>((parent) => { parent.OnInitializePotentialDrag(eventData); });
            base.OnInitializePotentialDrag(eventData);
        }

        /// <summary>
        /// Routes the drag event to parent components if necessary.
        /// </summary>
        /// <param name="eventData">Current event data.</param>
        public override void OnDrag(PointerEventData eventData)
        {
            if (routeToParent)
                DoForParents<IDragHandler>((parent) => { parent.OnDrag(eventData); });
            else
                base.OnDrag(eventData);
        }

        /// <summary>
        /// Routes the begin drag event to parent components if necessary.
        /// </summary>
        /// <param name="eventData">Current event data.</param>
        public override void OnBeginDrag(PointerEventData eventData)
        {
            if (!horizontal && Math.Abs(eventData.delta.x) > Math.Abs(eventData.delta.y))
                routeToParent = true;
            else if (!vertical && Math.Abs(eventData.delta.x) < Math.Abs(eventData.delta.y))
                routeToParent = true;
            else
                routeToParent = false;

            if (routeToParent)
                DoForParents<IBeginDragHandler>((parent) => { parent.OnBeginDrag(eventData); });
            else
                base.OnBeginDrag(eventData);

            OnBeginDragEvent?.Invoke(eventData);
        }

        /// <summary>
        /// Routes the end drag event to parent components if necessary.
        /// </summary>
        /// <param name="eventData">Current event data.</param>
        public override void OnEndDrag(PointerEventData eventData)
        {
            if (routeToParent)
                DoForParents<IEndDragHandler>((parent) => { parent.OnEndDrag(eventData); });
            else
                base.OnEndDrag(eventData);
            routeToParent = false;

            OnEndDragEvent?.Invoke(eventData);
        }
    }
}