using System;
using System.Collections;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;

namespace UI.Extensions
{
    [RequireComponent(typeof(ScrollRect))]
    public class StickyScrollView : MonoBehaviour
    {
        [Header("SELECT this, if you have issues with the element flickering outside the viewport on the first frame")]
        [SerializeField] private bool AddMaskToScrollView;
        [SerializeField, ShowIf("AddMaskToScrollView")]
        private bool ShowMaskGraphic = true;

        [Header("SELECT THIS, if you use LAYOUT GROUP on Content's GameObject")]
        [SerializeField] private bool AddOneFrameDelayForCanvasRebuild;

        [SerializeField] private float placeholderCustomHeight = -1f;

        private Transform _content;
        private RectTransform _viewport;
        private RectTransform _placeholder;
        private RectTransform _stickyElement;
        private ScrollRect _scrollRect;

        private Coroutine _firstSetPositionDelay;
        private Action _detachCallback;
        private Action _attachCallback;

        // Fitters (geçici kapatıp açmak için)
        private ContentSizeFitter _csf;
        private AspectRatioFitter _arf;
        private bool _csfWasEnabled, _arfWasEnabled;

        private Vector2 _lastScrollPos;

        // Orijinal RT state'i saklamak için
        private struct SavedRT
        {
            public Vector2 anchorMin, anchorMax, anchoredPos, sizeDelta, pivot;
            public Vector3 localScale;
            public Quaternion localRot;

            public SavedRT(RectTransform rt)
            {
                anchorMin = rt.anchorMin;
                anchorMax = rt.anchorMax;
                anchoredPos = rt.anchoredPosition;
                sizeDelta = rt.sizeDelta;
                pivot = rt.pivot;
                localScale = rt.localScale;
                localRot = rt.localRotation;
            }

            public void Restore(RectTransform rt)
            {
                rt.anchorMin = anchorMin;
                rt.anchorMax = anchorMax;
                rt.pivot = pivot;
                rt.sizeDelta = sizeDelta;
                rt.anchoredPosition = anchoredPos;
                rt.localScale = localScale;
                rt.localRotation = localRot;
            }
        }

        private SavedRT _savedRT;

        public RectTransform GetPlaceHolder() => _placeholder;

        private void Awake()
        {
            _scrollRect = GetComponent<ScrollRect>();
            _scrollRect.onValueChanged.AddListener(OnScroll);
            _content = _scrollRect.content;
            _viewport = _scrollRect.viewport;

            var placeholder = new GameObject("Placeholder");
            placeholder.transform.SetParent(_viewport);
            _placeholder = placeholder.AddComponent<RectTransform>();
            if (placeholderCustomHeight > 0f)
            {
                _placeholder.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, placeholderCustomHeight);
            }
            _placeholder.gameObject.SetActive(false);

            if (AddMaskToScrollView)
            {
                var mask = gameObject.GetComponent<Mask>();
                if (mask == null) mask = gameObject.AddComponent<Mask>();
                mask.showMaskGraphic = ShowMaskGraphic;
            }
        }

        [Button]
        public void AttachStickyElement(int siblingIndex, Action attachCallback = null, Action detachCallback = null)
        {
            DetachStickyElement();

            _attachCallback = attachCallback;
            _detachCallback = detachCallback;

            _stickyElement = _content.GetChild(siblingIndex).GetComponent<RectTransform>();
            _savedRT = new SavedRT(_stickyElement);

            // Layout'tan çıkar (yükseklik animasyonu için)
            var le = _stickyElement.GetComponent<LayoutElement>();
            if (le == null) le = _stickyElement.gameObject.AddComponent<LayoutElement>();
            le.ignoreLayout = true;

            // Fitters varsa geçici kapat
            _csf = _stickyElement.GetComponent<ContentSizeFitter>();
            _arf = _stickyElement.GetComponent<AspectRatioFitter>();
            if (_csf)
            {
                _csfWasEnabled = _csf.enabled;
                _csf.enabled = false;
            }
            if (_arf)
            {
                _arfWasEnabled = _arf.enabled;
                _arf.enabled = false;
            }

            // Placeholder'ı content altında aynı sıraya koy
            _placeholder.gameObject.SetActive(true);
            _placeholder.transform.SetParent(_content);
            _placeholder.transform.SetSiblingIndex(siblingIndex);

            // Sticky'yi viewport'a taşı (aynı uzayda çalışacağız)
            _stickyElement.SetParent(_viewport, worldPositionStays: true);

            // --- STICKY ANCHOR/PIVOT PROFİLİ (yatay stretch + orta pivot) ---
            // Anchors: Min(0,0.5) Max(1,0.5)  => width stretch
            // Pivot:   (0.5,0.5)             => merkez pivot
            var worldPos = _stickyElement.position;
            _stickyElement.anchorMin = new Vector2(0f, 0.5f);
            _stickyElement.anchorMax = new Vector2(1f, 0.5f);
            _stickyElement.pivot = new Vector2(0.5f, 0.5f);

            // Yalnızca HEIGHT'i placeholder'dan kopyala; width stretch'te otomatik
            var phHeight = _placeholder.rect.height;
            if (phHeight <= 0f) phHeight = _stickyElement.rect.height; // emniyet
            // Debug.LogError(
            //     "_placeholder.rect.height: " + _placeholder.rect.height + ", _stickyElement.rect.height: " + _stickyElement.rect.height, _stickyElement);
            _stickyElement.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, phHeight);

            // Stretch olduğumuz için X ofseti sıfırla (tam genişlik)
            var anchored = _stickyElement.anchoredPosition;
            anchored.x = 0f;
            _stickyElement.anchoredPosition = anchored;

            // World konumu koru (ilk hizalama için)
            _stickyElement.position = worldPos;

            if (_firstSetPositionDelay != null)
                StopCoroutine(_firstSetPositionDelay);

            if (AddOneFrameDelayForCanvasRebuild)
            {
                _firstSetPositionDelay = StartCoroutine(FirstSetPositionDelay());
            }
            else
            {
                OnScroll(Vector2.zero);
                _attachCallback?.Invoke();
                _attachCallback = null;
            }
        }

        [Button]
        public void DetachStickyElement()
        {
            if (_stickyElement == null) return;

            var siblingIndex = _placeholder.transform.GetSiblingIndex();

            // Placeholder'ı gizle
            _placeholder.transform.SetParent(_viewport);
            _placeholder.gameObject.SetActive(false);

            // Sticky'yi content'e geri tak
            _stickyElement.SetParent(_content, worldPositionStays: true);
            _stickyElement.SetSiblingIndex(siblingIndex);

            // Layout etkisini geri aç
            var le = _stickyElement.GetComponent<LayoutElement>();
            if (le != null) le.ignoreLayout = false;

            // Fitters'ları eski haline getir
            if (_csf) _csf.enabled = _csfWasEnabled;
            if (_arf) _arf.enabled = _arfWasEnabled;

            // Orijinal RT state'ini tamamen geri yükle
            _savedRT.Restore(_stickyElement);

            _detachCallback?.Invoke();
            _stickyElement = null;
        }

        public void SnapToStickedElement()
        {
            if (_placeholder.parent == _scrollRect.content)
            {
                Canvas.ForceUpdateCanvases();
                _scrollRect.content.anchoredPosition =
                    (Vector2)_scrollRect.transform.InverseTransformPoint(_scrollRect.content.position)
                    - (Vector2)_scrollRect.transform.InverseTransformPoint(_placeholder.position);
            }
        }

        // İlk frame gecikmesi gerekiyorsa
        private IEnumerator FirstSetPositionDelay()
        {
            yield return new WaitForEndOfFrame();
            OnScroll(Vector2.zero);
            _attachCallback?.Invoke();
            _attachCallback = null;
        }

        // ---- VİEWPORT GÖRÜNÜRLÜK/YÖN DURUMLARI ----

        // Placeholder viewport içinde dikey olarak herhangi bir kısmı görünür mü?
        private bool IsPlaceholderVerticallyVisible()
        {
            Vector3[] world = new Vector3[4];
            _placeholder.GetWorldCorners(world);
            var vrect = _viewport.rect;

            for (int i = 0; i < 4; i++)
            {
                Vector2 lp = _viewport.InverseTransformPoint(world[i]);
                if (lp.y >= vrect.yMin && lp.y <= vrect.yMax)
                    return true;
            }
            return false;
        }

        // Placeholder tamamen viewport'un üstünde mi?
        private bool IsPlaceholderAboveViewport()
        {
            Vector3[] world = new Vector3[4];
            _placeholder.GetWorldCorners(world);

            float maxY = float.NegativeInfinity;
            for (int i = 0; i < 4; i++)
            {
                Vector2 lp = _viewport.InverseTransformPoint(world[i]);
                if (lp.y > maxY) maxY = lp.y;
            }
            return maxY > _viewport.rect.yMax;
        }

        // Placeholder tamamen viewport'un altında mı?
        private bool IsPlaceholderBelowViewport()
        {
            Vector3[] world = new Vector3[4];
            _placeholder.GetWorldCorners(world);

            float minY = float.PositiveInfinity;
            for (int i = 0; i < 4; i++)
            {
                Vector2 lp = _viewport.InverseTransformPoint(world[i]);
                if (lp.y < minY) minY = lp.y;
            }
            return minY < _viewport.rect.yMin;
        }

        // ---- ANA MANTIK: GÖRÜNÜRKEN 'CHILD GİBİ', DEĞİLSE TEPE/ALTA YAPIŞ ----
        public void OnScroll(Vector2 _)
        {
            if (_stickyElement == null || _placeholder == null) return;

            var vpRect = _viewport.rect;

            // Yalnızca Y ile ilgileniyoruz (X stretch)
            float elemH = _stickyElement.rect.height;
            float py = _stickyElement.pivot.y; // 0.5

            // Üst/Alt hedef Y (viewport local space)
            float topLocalY = vpRect.yMax - elemH * (1f - py); // h/2 - h*0.5 = (h - elemH)/2
            float botLocalY = vpRect.yMin + elemH * py; // -h/2 + h*0.5 = -(h - elemH)/2

            // Placeholder'ın viewport yerel merkez Y'si
            float phLocalY = _viewport.InverseTransformPoint(_placeholder.position).y;

            float desiredLocalY;
            if (IsPlaceholderVerticallyVisible())
            {
                // Görünür: child gibi hizalan (Y'yi birebir eşitle)
                desiredLocalY = Mathf.Clamp(phLocalY, botLocalY, topLocalY);
            }
            else
            {
                // Görünmez: üst/alta yapış
                if (IsPlaceholderAboveViewport())
                    desiredLocalY = topLocalY;
                else if (IsPlaceholderBelowViewport())
                    desiredLocalY = botLocalY;
                else
                    desiredLocalY = Mathf.Clamp(phLocalY, botLocalY, topLocalY); // emniyet
            }

            // anchoredPosition: ebeveyn pivot merkezli -> viewport.rect.center.y çoğu zaman 0
            var anchored = _stickyElement.anchoredPosition;
            anchored.x = 0f; // stretch olduğumuz için X ofseti 0
            anchored.y = desiredLocalY - vpRect.center.y;
            _stickyElement.anchoredPosition = anchored;
        }
    }
}