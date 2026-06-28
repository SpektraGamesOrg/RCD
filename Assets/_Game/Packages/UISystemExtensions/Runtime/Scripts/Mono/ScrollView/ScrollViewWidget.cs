using System;
using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

namespace UISystem.Runtime.Scripts.Mono.ScrollView
{
    /// <summary>
    /// A widget for managing scroll view functionality, including scrolling to specific positions and items.
    /// </summary>
    public class ScrollViewWidget : UIWidget
    {
        /// <summary>
        /// Event triggered while scrolling.
        /// </summary>
        public Action OnScrolling;

        /// <summary>
        /// Event triggered when a scroll is requested.
        /// </summary>
        public Action OnScrollRequested;

        [SerializeField] private Vector2 startPosition;
        [SerializeField] private bool setPosOnAwake;
        [SerializeField] private bool dragEnabledOnAwake = true;
        [SerializeField] private float scrollOffset = 0.5f;
        [SerializeField] private float scrollDuration = 0.5f;

        private bool _isSnapping;
        private ScrollRectNested _scrollRect;

        private Tween _lastScrollAction;

        /// <summary>
        /// Gets the ScrollRectNested component.
        /// </summary>
        public ScrollRectNested ScrollRect => _scrollRect ??= GetComponent<ScrollRectNested>();

        /// <summary>
        /// Initializes the component.
        /// </summary>
        protected override void Awake()
        {
            base.Awake();
            if (setPosOnAwake)
                ScrollToCustomPos(startPosition, true);
        }

        /// <summary>
        /// Disables the scroll functionality.
        /// </summary>
        public void CloseScroll()
        {
            ScrollRect.enabled = false;
            this.enabled = false;
        }

        /// <summary>
        /// Sets the duration for scrolling.
        /// </summary>
        /// <param name="value">The new scroll duration.</param>
        public void SetScrollDuration(float value) => scrollDuration = value;

        /// <summary>
        /// Scrolls to a specific item.
        /// </summary>
        /// <param name="item">The item to scroll to.</param>
        /// <param name="onComplete">Action to invoke when the scroll is complete.</param>
        /// <param name="completeInstant">If true, completes the scroll instantly.</param>
        [Button]
        public void ScrollToItem(ScrollViewItem item, Action onComplete = null, bool completeInstant = false)
        {
            if (!Application.isPlaying)
                return;

            Vector2 targetPosition = CalculateTargetPosition(item, scrollOffset);

            ScrollToCustomPos(targetPosition, completeInstant, onComplete);
        }

        /// <summary>
        /// Scrolls to a custom position.
        /// </summary>
        /// <param name="target">The target position to scroll to.</param>
        /// <param name="completeInstant">If true, completes the scroll instantly.</param>
        /// <param name="onComplete">Action to invoke when the scroll is complete.</param>
        [Button]
        public void ScrollToCustomPos(Vector2 target, bool completeInstant = false, Action onComplete = null)
        {
            if (!Application.isPlaying)
                return;

            if (!ScrollRect.horizontal)
                target.x = ScrollRect.normalizedPosition.x;

            if (!ScrollRect.vertical)
                target.y = ScrollRect.normalizedPosition.y;

            // Debug.LogError("target: " + target +
            //                ", normalizedPosition: " + ScrollRect.normalizedPosition +
            //                ", content.sizeDelta.x: " + ScrollRect.content.sizeDelta.x +
            //                ", content.sizeDelta.y: " + ScrollRect.content.sizeDelta.y
            //     , ScrollRect);

            if (_lastScrollAction != null)
            {
                _lastScrollAction.Kill();
                _lastScrollAction = null;
            }

            if (completeInstant)
            {
                ScrollRect.normalizedPosition = target;
                onComplete?.Invoke();
            }
            else
            {
                _isSnapping = true;
                OnScrollRequested?.Invoke();
                _lastScrollAction = DOTween.To(() => ScrollRect.normalizedPosition, x => ScrollRect.normalizedPosition = x, target, scrollDuration)
                    .OnUpdate(() => OnScrolling?.Invoke())
                    .OnComplete(() =>
                    {
                        _isSnapping = false;
                        _lastScrollAction = null;
                        onComplete?.Invoke();
                    });
            }
        }

        /// <summary>
        /// Calculates the target position for scrolling to an item.
        /// </summary>
        /// <param name="item">The item to scroll to.</param>
        /// <param name="offset">The offset for the scroll position.</param>
        /// <returns>The target position as a Vector2.</returns>
        private Vector2 CalculateTargetPosition(ScrollViewItem item, float offset)
        {
            Vector2 itemLocalPosition = (Vector2)ScrollRect.content.InverseTransformPoint(item.transform.position);
            Vector2 contentPivotOffset = new Vector2(ScrollRect.content.pivot.x * ScrollRect.content.rect.width,
                ScrollRect.content.pivot.y * ScrollRect.content.rect.height);
            Vector2 itemPositionRelativeToPivot = itemLocalPosition + contentPivotOffset;
            Vector2 viewportSize = ScrollRect.viewport.rect.size;

            float targetX = (itemPositionRelativeToPivot.x - viewportSize.x * offset) /
                            (ScrollRect.content.rect.width - viewportSize.x);
            float targetY = (itemPositionRelativeToPivot.y - viewportSize.y * offset) /
                            (ScrollRect.content.rect.height - viewportSize.y);

            return new Vector2(Mathf.Clamp01(targetX), Mathf.Clamp01(targetY));
        }

        /// <summary>
        /// Calculates the visible area of an item within the scroll view.
        /// </summary>
        /// <param name="item">The item to calculate the visible area for.</param>
        /// <returns>The visible area as a float.</returns>
        protected float CalculateVisibleArea(ScrollViewItem item)
        {
            RectTransform itemRectTransform = item.GetComponent<RectTransform>();
            RectTransform viewportRectTransform = ScrollRect.viewport;

            Vector3[] itemWorldCorners = new Vector3[4];
            itemRectTransform.GetWorldCorners(itemWorldCorners);

            Vector3[] viewportWorldCorners = new Vector3[4];
            viewportRectTransform.GetWorldCorners(viewportWorldCorners);

            Rect itemRect = new Rect(itemWorldCorners[0], itemWorldCorners[2] - itemWorldCorners[0]);
            Rect viewportRect = new Rect(viewportWorldCorners[0], viewportWorldCorners[2] - viewportWorldCorners[0]);

            Rect intersection = Rect.MinMaxRect(
                Mathf.Max(itemRect.xMin, viewportRect.xMin),
                Mathf.Max(itemRect.yMin, viewportRect.yMin),
                Mathf.Min(itemRect.xMax, viewportRect.xMax),
                Mathf.Min(itemRect.yMax, viewportRect.yMax)
            );

            return intersection.width <= 0 || intersection.height <= 0 ? 0f : intersection.width * intersection.height;
        }
    }
}