using Ads;
using Gold;
using Sirenix.OdinInspector;
using TMPro;
using UIManager;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// Side popup shown when a gold is collected, offering a "CLAIM Nx" rewarded-ad bonus.
    /// Listens for <see cref="Gold.Gold.ClaimRequested"/>, shows itself with a timer bar that drains
    /// over <see cref="displayDuration"/>, and auto-dismisses when it empties (the player keeps the
    /// base reward they already received). Tapping CLAIM shows a rewarded ad via
    /// <see cref="RewardedAds"/> and, on success, grants the bonus through the request callback.
    /// </summary>
    public sealed class GoldClaimPopup : PopupBase
    {
        [Title("Gold Claim")]
        [Tooltip("Button that triggers the rewarded ad. Use the project EnhancedButton.")]
        [SerializeField] private EnhancedButton claimButton;

        [Tooltip("\"CLAIM 5X\" style label on the claim button.")]
        [SerializeField] private TMP_Text claimLabel;

        [Tooltip("Filled image (Image.type = Filled) used as the countdown bar. Drains 1 -> 0.")]
        [SerializeField] private Image timerBarFill;

        [Tooltip("Seconds the popup stays up before auto-dismissing.")]
        [SerializeField, Min(0.1f)] private float displayDuration = 5f;

        private float _timeLeft;
        private bool _claimed;
        private GoldClaimRequest _request;

        protected override void Awake()
        {
            base.Awake();
            if (claimButton != null)
                claimButton.onClick.AddListener(OnClaimClicked);
        }

        private void OnEnable()
        {
            Gold.Gold.ClaimRequested += OnClaimRequested;
        }

        private void OnDisable()
        {
            Gold.Gold.ClaimRequested -= OnClaimRequested;
        }

        private void OnClaimRequested(GoldClaimRequest request)
        {
            _request = request;
            _claimed = false;

            if (claimLabel != null)
                claimLabel.text = $"CLAIM {request.Multiplier}X";

            Show();
        }

        protected override void OnShowed(bool immediate, object uiData = null)
        {
            base.OnShowed(immediate, uiData);
            _timeLeft = displayDuration;
            if (claimButton != null) claimButton.interactable = true;
            UpdateTimerBar();
        }

        private void Update()
        {
            if (!ShowingOrShown || _claimed)
                return;

            _timeLeft -= Time.unscaledDeltaTime;
            UpdateTimerBar();

            if (_timeLeft <= 0f)
                Hide();
        }

        private void UpdateTimerBar()
        {
            if (timerBarFill != null)
                timerBarFill.fillAmount = Mathf.Clamp01(_timeLeft / displayDuration);
        }

        private void OnClaimClicked()
        {
            if (_claimed)
                return;

            _claimed = true;
            if (claimButton != null) claimButton.interactable = false;

            GoldClaimRequest request = _request;
            RewardedAds.Service.Show(
                onRewarded: () =>
                {
                    request.OnClaimed?.Invoke(request.Bonus);
                    Hide();
                },
                onFailed: () =>
                {
                    // Ad couldn't be shown; let the player try again until the timer runs out.
                    _claimed = false;
                    if (claimButton != null) claimButton.interactable = true;
                });
        }
    }
}
