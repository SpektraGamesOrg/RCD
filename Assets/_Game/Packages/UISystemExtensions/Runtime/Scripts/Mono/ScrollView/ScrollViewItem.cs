using System;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using Sirenix.OdinInspector;

namespace UISystem.Runtime.Scripts.Mono.ScrollView
{
    [RequireComponent(typeof(CanvasGroup))]
    public class ScrollViewItem : MonoBehaviour
    {
        [SerializeField, BoxGroup("ScrollViewItem")]
        private int customSiblingIndex;
        [SerializeField, BoxGroup("ScrollViewItem")]
        private float horizontalOffset = 50f;
        [SerializeField, BoxGroup("ScrollViewItem")]
        private float verticalOffset = 50f;
        [SerializeField, BoxGroup("ScrollViewItem")]
        private bool canUpdateVisual = true;
        [SerializeField, BoxGroup("ScrollViewItem")]
        private float fadeDuration = 0.25f;

        public int CustomSiblingIndex => customSiblingIndex;

        private Canvas _canvas;
        private CanvasGroup _canvasGroup;
        private GraphicRaycaster _graphicRaycaster;
        private RectTransform _scrollRectTransform;
        private RectTransform _itemRectTransform;
        private ScrollRect _scrollRect;

        private readonly Vector3[] _itemWorldCorners = new Vector3[4];
        private readonly Vector3[] _viewportWorldCorners = new Vector3[4];

        private bool _isVisible = false;
        [ReadOnly, ShowInInspector] public bool IsVisible => _isVisible;

        // Aktif tween referansı
        private Tween _fadeTween;

        private bool _isVisibleSetOnce = false;
        private bool _lastIsVisible = false;
        
        public Action<bool> OnVisibleChanged;

        protected virtual void Start()
        {
            _canvas = GetComponent<Canvas>();
            _canvasGroup = GetComponent<CanvasGroup>();
            _graphicRaycaster = GetComponent<GraphicRaycaster>();
            _itemRectTransform = GetComponent<RectTransform>();

            UpdateParentScrollRect();
            ApplyCustomWidthFromLayoutElement();

            // İlk durumda görünmesin
            _canvasGroup.alpha = 0f;
            _isVisible = false;
            SetVisual(false, instant: true);
            OnVisibleChanged?.Invoke(false);
        }

        public void UpdateParentScrollRect()
        {
            _scrollRect = GetComponentInParent<ScrollRect>();
            if (_scrollRect == null) return;

            _scrollRectTransform = _scrollRect.GetComponent<RectTransform>();
            _scrollRectTransform.GetWorldCorners(_viewportWorldCorners);
        }

        protected virtual void Update()
        {
            CheckVisibility();
        }

        protected virtual void OnDisable()
        {
            _lastIsVisible = false;
            _isVisibleSetOnce = true;
            OnVisibleChanged?.Invoke(false);
        }

        private void ApplyCustomWidthFromLayoutElement()
        {
            var layoutElement = GetComponent<LayoutElement>();
            if (layoutElement == null) return;

            var rectTransform = GetComponent<RectTransform>();
            rectTransform.sizeDelta = new Vector2(layoutElement.preferredWidth, rectTransform.sizeDelta.y);
        }

        public void ApplySiblingIndex()
        {
            transform.SetSiblingIndex(customSiblingIndex);
        }

        public void SetCustomSiblingIndex(int index)
        {
            customSiblingIndex = index;
        }

        private void SetVisual(bool value, bool instant = false)
        {
            if (!canUpdateVisual)
                return;

            // Canvas ve Raycaster aktif/pasif olsun
            if (_canvas)
            {
                if (_graphicRaycaster) _graphicRaycaster.enabled = value;
                _canvas.enabled = value;
            }

            // Tween varsa öldür
            _fadeTween?.Kill();

            if (instant)
            {
                _canvasGroup.alpha = value ? 1 : 0;

                if (!_isVisibleSetOnce || value != _lastIsVisible)
                {
                    _lastIsVisible = value;
                    _isVisibleSetOnce = true;
                }

                return;
            }

            float targetAlpha = value ? 1f : 0f;

            // Yeni fade tween oluştur
            _fadeTween = _canvasGroup
                .DOFade(targetAlpha, fadeDuration)
                .SetUpdate(true); // timescale etkilenmesin
        }

        private void CheckVisibility()
        {
            if (!_scrollRectTransform)
                return;

            _scrollRectTransform.GetWorldCorners(_viewportWorldCorners);

            var viewportMinX = _viewportWorldCorners[0].x - horizontalOffset;
            var viewportMaxX = _viewportWorldCorners[2].x + horizontalOffset;
            var viewportMinY = _viewportWorldCorners[0].y - verticalOffset;
            var viewportMaxY = _viewportWorldCorners[2].y + verticalOffset;

            _itemRectTransform.GetWorldCorners(_itemWorldCorners);

            bool isOutside =
                _itemWorldCorners[2].x < viewportMinX ||
                _itemWorldCorners[0].x > viewportMaxX ||
                _itemWorldCorners[2].y < viewportMinY ||
                _itemWorldCorners[0].y > viewportMaxY;

            switch (isOutside)
            {
                case true when _isVisible:
                    SetVisual(false);
                    _isVisible = false;
                    break;

                case false when !_isVisible:
                    SetVisual(true);
                    _isVisible = true;
                    break;
            }

            if (!_isVisibleSetOnce || _isVisible != _lastIsVisible)
            {
                _lastIsVisible = _isVisible;
                _isVisibleSetOnce = true;
                OnVisibleChanged?.Invoke(_isVisible);
            }
        }
    }
}