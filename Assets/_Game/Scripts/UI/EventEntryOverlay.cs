using DG.Tweening;
using TMPro;
using UIManager;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// Display payload for <see cref="EventEntryOverlay"/>. Strings are pre-formatted by
    /// <see cref="Events.EventManager"/> so the overlay stays decoupled from the event/mode types.
    /// </summary>
    public sealed class EventEntryData
    {
        public readonly string Title;
        public readonly string ActionLabel; // "START" or "WATCH AD"
        public readonly int RewardAmount;
        public readonly Events.EventType EventType;

        public EventEntryData(string title, string actionLabel, int rewardAmount, Events.EventType eventType)
        {
            Title = title;
            ActionLabel = actionLabel;
            RewardAmount = rewardAmount;
            EventType = eventType;
        }
    }

    /// <summary>
    /// The small "approaching an event" pop-up (GDD "Entry"): shows the event title/level and a START (or
    /// WATCH AD) and CLOSE button. It stays up while the player is in the area; <see cref="Events.EventManager"/>
    /// hides it 3 seconds after the player leaves. The overlay is a thin view - the buttons forward straight to
    /// the manager, which owns all the flow.
    ///
    /// Transition: the overlay fades in while <see cref="slidePanel"/> slides from off the right edge
    /// (home + width) to its home position, and reverses on hide. Driven by the base
    /// <see cref="UIViewBase.hasTransitionAnimations"/> flow: the base invokes <see cref="PlayShowAnimation"/> /
    /// <see cref="PlayHideAnimation"/> (wired to the show/hide UnityEvents) and waits for <see cref="OnAnimationDone"/>.
    /// </summary>
    public sealed class EventEntryOverlay : OverlayBase
    {
        [SerializeField] private EnhancedButton startButton;
        [SerializeField] private EnhancedButton closeButton;

        [Tooltip("Label on the start button; text swaps to WATCH AD for Watch & Earn.")]
        [SerializeField] private TMP_Text startButtonLabel;
        [SerializeField] private TMP_Text rewardText;
        [SerializeField] private TMP_Text eventNameText;
        [SerializeField] private Image eventIconImage;

        [SerializeField]
        private SerializedDictionary<Events.EventType, Sprite> eventIcons = new();

        [Header("Transition Animation")]
        [Tooltip("Panel that slides horizontally during show/hide. Falls back to Content if unassigned.")]
        [SerializeField] private RectTransform slidePanel;

        [Tooltip("Duration (t seconds) of both the fade and the slide.")]
        [SerializeField, Min(0f)] private float transitionDuration = 0.3f;

        [SerializeField] private Ease showEase = Ease.OutCubic;
        [SerializeField] private Ease hideEase = Ease.InCubic;

        // The panel we actually animate; Content is a sensible fallback if slidePanel is not wired.
        private RectTransform SlideTarget => slidePanel ? slidePanel : content;

        // Authored (rest) position of the slide panel, captured before any tween can move it.
        private Vector2 _panelHomePosition;
        private bool _homeCaptured;

        // Single in-flight fade+slide sequence. Only ever one at a time - a new transition kills the old.
        private Tween _transitionTween;

        // Bumped on every show/hide so a stale tween's completion callback can be ignored.
        private int _transitionId;

        // True while the latest requested transition is a hide. Lets us tell a genuine animated hide apart
        // from the base's stale HideAsync continuation that lands here when a Show interrupted a Hide.
        private bool _lastOpWasHide;

        protected override void Awake()
        {
            base.Awake();
            CaptureHome();
            if (startButton) startButton.onClick.AddListener(OnStartClicked);
            if (closeButton) closeButton.onClick.AddListener(OnCloseClicked);
        }

        // Populate before the panel becomes visible so the fade-in never shows stale data. OnBeforeShowing
        // runs ahead of the show animation (and ahead of the immediate path), unlike OnShowed which the
        // animated flow only calls once the fade-in has finished.
        protected override void OnBeforeShowing(bool immediate, object uiData = null)
        {
            base.OnBeforeShowing(immediate, uiData);

            if (uiData is EventEntryData data)
            {
                startButtonLabel.text = data.ActionLabel;
                rewardText.text = data.RewardAmount.ToString();
                eventNameText.text = data.Title;
                eventIconImage.sprite = eventIcons[data.EventType];
            }

            if (startButton) startButton.interactable = true;
        }

        protected override void OnShowed(bool immediate, object uiData = null)
        {
            base.OnShowed(immediate, uiData);

            // Land on a clean shown state no matter how we got here (animation done, timeout fallback,
            // immediate show, or a Show that raced a Hide): full alpha, panel at home, no residual tween.
            _transitionTween?.Kill();
            _transitionTween = null;

            CaptureHome();
            content.gameObject.SetActive(true);
            canvasGroup.alpha = 1f;
            if (SlideTarget) SlideTarget.anchoredPosition = _panelHomePosition;
        }

        protected override void OnHidden(bool immediate = false)
        {
            base.OnHidden(immediate);

            // Stop any residual slide (e.g. an immediate Hide(true) cutting an in-flight animation short).
            _transitionTween?.Kill();
            _transitionTween = null;

            if (immediate)
            {
                // Base already cleared alpha and deactivated Content; just park the panel at home so a later
                // non-animated show appears in the right place.
                CaptureHome();
                if (SlideTarget) SlideTarget.anchoredPosition = _panelHomePosition;
                return;
            }

            // Animated-hide completion. If a Show interrupted this Hide, the base's stale HideAsync
            // continuation still reaches here after the overlay is already back on screen; in that race
            // _lastOpWasHide is false, so we must NOT tear the (now visible) overlay down.
            if (!_lastOpWasHide)
                return;

            canvasGroup.alpha = 0f;
            CaptureHome();
            if (SlideTarget) SlideTarget.anchoredPosition = _panelHomePosition;
            content.gameObject.SetActive(false);
        }

        // Wired to UIViewBase.playShowAnimation. Fades the overlay in and slides the panel from off the right
        // (home + width) to its home position over transitionDuration, then signals completion.
        public void PlayShowAnimation()
        {
            CaptureHome();
            RectTransform target = SlideTarget;
            if (!target)
            {
                // Nothing to animate - satisfy the base flow immediately.
                OnAnimationDone();
                return;
            }

            // Kill an interrupted hide WITHOUT completing it, so its callback can't fire for the wrong state.
            _transitionTween?.Kill();
            _lastOpWasHide = false;
            int id = ++_transitionId;

            Vector2 offScreen = _panelHomePosition + new Vector2(target.rect.width, 0f);

            // The base already forced alpha to 0; start the slide from off-left to match the fade.
            canvasGroup.alpha = 0f;
            target.anchoredPosition = offScreen;

            _transitionTween = DOTween.Sequence()
                .SetUpdate(true) // Unscaled: the overlay must animate even if gameplay time is paused/slowed.
                .Join(canvasGroup.DOFade(1f, transitionDuration).SetEase(showEase))
                .Join(target.DOAnchorPos(_panelHomePosition, transitionDuration).SetEase(showEase))
                .SetLink(gameObject)
                .OnComplete(() => { if (id == _transitionId) OnAnimationDone(); });
        }

        // Wired to UIViewBase.playHideAnimation. Fades the overlay out and slides the panel back off the right,
        // then signals completion. Fades from the current alpha so an interrupted show hides seamlessly.
        public void PlayHideAnimation()
        {
            CaptureHome();
            RectTransform target = SlideTarget;
            if (!target)
            {
                OnAnimationDone();
                return;
            }

            _transitionTween?.Kill();
            _lastOpWasHide = true;
            int id = ++_transitionId;

            Vector2 offScreen = _panelHomePosition + new Vector2(target.rect.width, 0f);

            _transitionTween = DOTween.Sequence()
                .SetUpdate(true)
                .Join(canvasGroup.DOFade(0f, transitionDuration).SetEase(hideEase))
                .Join(target.DOAnchorPos(offScreen, transitionDuration).SetEase(hideEase))
                .SetLink(gameObject)
                .OnComplete(() => { if (id == _transitionId) OnAnimationDone(); });
        }

        private void CaptureHome()
        {
            if (_homeCaptured)
                return;

            RectTransform target = SlideTarget;
            if (!target)
                return;

            _panelHomePosition = target.anchoredPosition;
            _homeCaptured = true;
        }

        private void OnStartClicked()
        {
            // Lock the button so a rapid double-tap can't start the flow twice; the manager owns the transition.
            if (startButton) startButton.interactable = false;
            if (Events.EventManager.Exists())
                Events.EventManager.Instance.OnEntryStartPressed();
        }

        private void OnCloseClicked()
        {
            if (Events.EventManager.Exists())
                Events.EventManager.Instance.OnEntryClosePressed();
            else
                Hide();
        }
    }
}
