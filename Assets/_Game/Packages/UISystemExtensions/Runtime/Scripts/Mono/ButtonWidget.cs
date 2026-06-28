using System;
using System.Collections.Generic;
using System.Threading;
using BrunoMikoski.AnimationSequencer;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Sirenix.OdinInspector;
using UI.NewUI.BaseUI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace UISystem.Runtime.Scripts.Mono
{
    [Flags]
    public enum ChangeStyle
    {
        None = 0,
        Sprite = 1 << 0,
        Color = 1 << 1,
    }

    [Serializable]
    public class GraphicSwapTarget
    {
        [VerticalGroup("Root")]
        [LabelText("Target"), PropertyOrder(0)]
        public Graphic graphic;

        [VerticalGroup("Root"), EnumToggleButtons, LabelText("Change Style"), PropertyOrder(1)]
        public ChangeStyle changeStyle;

        [HorizontalGroup("Root/Bottom", Width = 280)]
        [VerticalGroup("Root/Bottom/Color"), Title("Normal Color"), LabelWidth(80), HideLabel, PropertyOrder(2)]
        public Color normalColor = Color.white;

        [VerticalGroup("Root/Bottom/Color"), Title("Selected Color"), LabelWidth(80), HideLabel, PropertyOrder(3)]
        public Color selectedColor = Color.white;

        [VerticalGroup("Root/Bottom/Sprite"), Title("Normal Sprite"), LabelWidth(90), HideLabel]
        [ShowIf("@graphic is UnityEngine.UI.Image"), PreviewField(40), PropertyOrder(4)]
        public Sprite normalSprite;

        [VerticalGroup("Root/Bottom/Sprite"), Title("Selected Sprite"), LabelWidth(90), HideLabel]
        [ShowIf("@graphic is UnityEngine.UI.Image"), PreviewField(40), PropertyOrder(5)]
        public Sprite selectedSprite;

        [VerticalGroup("Root/Bottom/Animation"), Title("Animate Color Change"), HideLabel]
        public bool animateColor = false;

        [ShowIf("animateColor")]
        [VerticalGroup("Root/Bottom/Animation"), LabelText("Duration"), SuffixLabel("seconds")]
        public float colorAnimDuration = 0.2f;

        [VerticalGroup("Root/Bottom/Animation"), Title("Animate Sprite Change"), HideLabel]
        public bool animateSprite = false;

        [ShowIf("animateSprite")]
        [VerticalGroup("Root/Bottom/Animation"), LabelText("Duration"), SuffixLabel("seconds")]
        public float spriteAnimDuration = 0.2f;
    }

    [Serializable]
    public class DisabledGraphicSwapTarget
    {
        [LabelText("Target")]
        public Graphic graphic;

        // ENABLE (normal) state değerleri
        [ShowIf("@graphic is UnityEngine.UI.Image")]
        [LabelText("Normal Sprite"), PreviewField(40)]
        public Sprite normalSprite;

        [LabelText("Normal Color")]
        public Color normalColor = Color.white;

        // DISABLED state değerleri
        [ShowIf("@graphic is UnityEngine.UI.Image")]
        [LabelText("Disabled Sprite"), PreviewField(40)]
        public Sprite disabledSprite;

        [LabelText("Disabled Color")]
        public Color disabledColor = Color.gray;

        // ANİMASYON seçenekleri
        [LabelText("Animate Color")]
        public bool animateColor = false;

        [ShowIf("animateColor")]
        [LabelText("Color Anim. Duration"), SuffixLabel("sn"), LabelWidth(60)]
        public float colorAnimDuration = 0.2f;

        [LabelText("Animate Sprite")]
        public bool animateSprite = false;

        [ShowIf("animateSprite")]
        [LabelText("Sprite Anim. Duration"), SuffixLabel("sn"), LabelWidth(60)]
        public float spriteAnimDuration = 0.2f;
    }


    /// <summary>
    /// A widget for managing button functionality, including click events and interaction states.
    /// </summary>
    public class ButtonWidget : UIWidget, IPointerDownHandler, IPointerUpHandler
    {
        /// <summary>
        /// Event triggered when the button is clicked.
        /// </summary>
        public Action OnClick;

        /// <summary>
        /// Event triggered while the button is being clicked.
        /// </summary>
        public Action OnClicking;

        /// <summary>
        /// Event triggered when the button is pressed down.
        /// </summary>
        public Action OnClickDown;

        /// <summary>
        /// Event triggered when the button is released.
        /// </summary>
        public Action OnClickUp;

        [field: SerializeField]
        public TextWidget ButtonText { get; private set; }

        private CancellationTokenSource _spriteAnimCts;

        private Button _button;

        public Button Button => _button ??= GetComponent<Button>();

        private bool _isHolding;

        [TableList]
        public List<GraphicSwapTarget> swapTargets = new();

        [Title("Disabled State Options")]
        [PropertyOrder(100)]
        public List<DisabledGraphicSwapTarget> disabledSwapTargets = new();

        [Space, Title("Scriptable Button Anchor")]
        [ToggleLeft]
        [SerializeField] private bool hasScriptableButtonAnchor;

        [Space, Title("Animation")]
        [ToggleLeft]
        public bool useSelectAnimation;

        [ShowIf("useSelectAnimation")]
        [LabelText("Select Animation Sequencer")]
        public AnimationSequencerController selectAnimation;

        [ShowIf("useSelectAnimation")]
        [LabelText("Deselect Animation Sequencer")]
        public AnimationSequencerController deselectAnimation;

        [Space, Title("Click Animation")]
        [ToggleLeft]
        public bool useClickAnimation = true;

        [ShowIf("useClickAnimation")]
        [LabelText("Click Animation Target")]
        [Tooltip("Optional. If null, tweens this ButtonWidget's transform.")]
        [SerializeField]
        private Transform clickAnimationTarget;

        [ShowIf("useClickAnimation")]
        [LabelText("Click Scale")]
        public float clickAnimationScale = 0.95f;

        [ShowIf("useClickAnimation")]
        [LabelText("Click Duration"), SuffixLabel("seconds")]
        public float clickAnimationDuration = 0.15f;

        private Tween _clickAnimationTween;
        private Vector3 _clickAnimationInitialScale;
        private bool _clickAnimationInitialScaleCaptured;
        private bool _isClickAnimationPlaying;

        /// <summary>
        /// Initializes the component.
        /// </summary>
        protected override void Awake()
        {
            base.Awake();
            Button.onClick.AddListener(OnClickHandler);
        }

        private void OnEnable()
        {
        }

        private void OnDisable()
        {
        }

        private void Update()
        {
            if (_isHolding)
                OnClicking?.Invoke();
        }

        protected override void OnDestroy()
        {

            base.OnDestroy();
        }

        private void OnClickHandler()
        {
            // Drop re-clicks (audio + OnClick) while the scale tween is in flight.
            if (useClickAnimation && _isClickAnimationPlaying)
                return;
            
            PlayClickAnimation(() => OnClick?.Invoke());
        }

        private void PlayClickAnimation(Action onAnimationComplete = null)
        {
            // Idempotent callback: fires exactly once via OnComplete OR OnKill,
            // guaranteeing the click is never silently dropped.
            bool invoked = false;
            void InvokeOnce()
            {
                if (invoked) return;
                invoked = true;
                onAnimationComplete?.Invoke();
            }

            if (!useClickAnimation || !Button.interactable)
            {
                InvokeOnce();
                return;
            }

            var target = clickAnimationTarget != null ? clickAnimationTarget : transform;

            if (!_clickAnimationInitialScaleCaptured)
            {
                _clickAnimationInitialScale = target.localScale;
                _clickAnimationInitialScaleCaptured = true;
            }

            _isClickAnimationPlaying = true;

            var halfDuration = clickAnimationDuration * 0.5f;

            _clickAnimationTween?.Kill();
            _clickAnimationTween = DOTween.Sequence()
                .SetUpdate(true) // Independent of Time.timeScale so UI clicks still fire when paused.
                .Append(target.DOScale(_clickAnimationInitialScale * clickAnimationScale, halfDuration)
                    .SetEase(Ease.OutQuad))
                .Append(target.DOScale(_clickAnimationInitialScale, halfDuration)
                    .SetEase(Ease.OutQuad))
                .OnComplete(() =>
                {
                    _isClickAnimationPlaying = false;
                    InvokeOnce();
                })
                .OnKill(() =>
                {
                    _isClickAnimationPlaying = false;
                    if (target != null)
                        target.localScale = _clickAnimationInitialScale;
                    // Fire callback only if this widget is still alive — if the gameObject
                    // was destroyed mid-tween, the caller's closure is unsafe to invoke.
                    if (this != null)
                        InvokeOnce();
                })
                .SetLink(gameObject);
        }

        private CancellationTokenSource _disabledAnimCts;

        public void SetButtonWidgetInteraction(bool value)
        {
            if (Button.TryGetComponent(out CanvasGroup cg))
            {
                cg.interactable = value;
                cg.blocksRaycasts = value;
            }

            Button.interactable = value;
            Button.targetGraphic.raycastTarget = value;

            // Tüm animasyonları başlatmadan önce eski token'ı iptal et
            _disabledAnimCts?.Cancel();
            _disabledAnimCts?.Dispose();
            _disabledAnimCts = new CancellationTokenSource();

            foreach (var t in disabledSwapTargets)
            {
                if (t.graphic == null) continue;

                if (value) // ENABLE olunca NORMAL değerlerine döndür
                {
                    // COLOR
                    if (t.animateColor)
                        t.graphic.DOColor(t.normalColor, t.colorAnimDuration);
                    else
                        t.graphic.color = t.normalColor;

                    // SPRITE
                    if (t.graphic is UnityEngine.UI.Image img && t.normalSprite)
                    {
                        if (t.animateSprite)
                            AnimateSpriteChangeAsync(img, t.normalSprite, t.spriteAnimDuration, _disabledAnimCts.Token)
                                .Forget();
                        else
                            img.sprite = t.normalSprite;
                    }
                }
                else // DISABLE olunca DISABLED değerlerine geç
                {
                    // COLOR
                    if (t.animateColor)
                        t.graphic.DOColor(t.disabledColor, t.colorAnimDuration);
                    else
                        t.graphic.color = t.disabledColor;

                    // SPRITE
                    if (t.graphic is UnityEngine.UI.Image img && t.disabledSprite)
                    {
                        if (t.animateSprite)
                            AnimateSpriteChangeAsync(img, t.disabledSprite, t.spriteAnimDuration,
                                _disabledAnimCts.Token).Forget();
                        else
                            img.sprite = t.disabledSprite;
                    }
                }
            }
        }

        public void SetCustomSprite(Sprite sprite)
        {
            Button.image.sprite = sprite;
        }

        [ContextMenu("ClickButton")]
        public void ClickButton()
        {
            Button.onClick.Invoke();
        }

        // Fires OnClick synchronously without the click scale tween.
        // Use for programmatic refreshes (e.g. screen-open initial state) where the
        // animation delay would stall the data update by clickAnimationDuration.
        public void InvokeClickImmediate()
        {
            OnClick?.Invoke();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            OnClickDown?.Invoke();
            _isHolding = true;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            OnClickUp?.Invoke();
            _isHolding = false;
        }

        public void Select()
        {
            _spriteAnimCts?.Cancel();
            _spriteAnimCts?.Dispose();
            _spriteAnimCts = new CancellationTokenSource();

            foreach (var t in swapTargets)
            {
                // Color
                if ((t.changeStyle & ChangeStyle.Color) != 0)
                {
                    if (t.animateColor)
                        t.graphic.DOColor(t.selectedColor, t.colorAnimDuration);
                    else
                        t.graphic.color = t.selectedColor;
                }

                // Sprite
                if ((t.changeStyle & ChangeStyle.Sprite) != 0 && t.graphic is UnityEngine.UI.Image img &&
                    t.selectedSprite)
                {
                    if (t.animateSprite)
                        AnimateSpriteChangeAsync(img, t.selectedSprite, t.spriteAnimDuration, _spriteAnimCts.Token)
                            .Forget();
                    else
                        img.sprite = t.selectedSprite;
                }
            }

            if (gameObject.TryGetComponent<ISelectableUIWidget>(out var selectableWidget))
                selectableWidget.Select();

            if (useSelectAnimation && selectAnimation)
                selectAnimation.Play();
        }

        public void Deselect()
        {
            _spriteAnimCts?.Cancel();
            _spriteAnimCts?.Dispose();
            _spriteAnimCts = new CancellationTokenSource();

            foreach (var t in swapTargets)
            {
                // Color
                if ((t.changeStyle & ChangeStyle.Color) != 0)
                {
                    if (t.animateColor)
                        t.graphic.DOColor(t.normalColor, t.colorAnimDuration);
                    else
                        t.graphic.color = t.normalColor;
                }

                // Sprite
                if ((t.changeStyle & ChangeStyle.Sprite) != 0 && t.graphic is UnityEngine.UI.Image img &&
                    t.normalSprite)
                {
                    if (t.animateSprite)
                        AnimateSpriteChangeAsync(img, t.normalSprite, t.spriteAnimDuration, _spriteAnimCts.Token)
                            .Forget();
                    else
                        img.sprite = t.normalSprite;
                }
            }

            if (gameObject.TryGetComponent<ISelectableUIWidget>(out var selectableWidget))
                selectableWidget.Deselect();

            if (useSelectAnimation && deselectAnimation)
                deselectAnimation.Play();
        }

        private static async UniTask AnimateSpriteChangeAsync(UnityEngine.UI.Image img, Sprite newSprite,
            float duration, CancellationToken token)
        {
            try
            {
                if (!img)
                    return;

                img.CrossFadeAlpha(0f, duration * 0.5f, false);
                await UniTask.Delay(TimeSpan.FromSeconds(duration * 0.5f), cancellationToken: token)
                    .SuppressCancellationThrow();

                img.sprite = newSprite;

                img.CrossFadeAlpha(1f, duration * 0.5f, false);
                await UniTask.Delay(TimeSpan.FromSeconds(duration * 0.5f), cancellationToken: token)
                    .SuppressCancellationThrow();

                img.canvasRenderer.SetAlpha(1f);
            }
            catch (OperationCanceledException)
            {
            }
        }
        
#if UNITY_EDITOR
        [Button("Select"), HideInEditorMode, HorizontalGroup("EditorActions")]
        private void Editor_Select()
        {
            Select();
        }
        
        [Button("Deselect"), HideInEditorMode, HorizontalGroup("EditorActions")]
        private void Editor_Deselect()
        {
            Deselect();
        }
#endif
    }
}