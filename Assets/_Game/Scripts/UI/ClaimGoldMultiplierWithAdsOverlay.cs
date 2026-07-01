using _Game.Scripts.Utils.VContainer;
using Ads;
using Cysharp.Threading.Tasks;
using Gold;
using Sirenix.OdinInspector;
using SpektraGames.RuntimeUI.Runtime;
using TMPro;
using UIManager;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// Generic "watch a rewarded ad to multiply a reward" overlay. It is reward-agnostic: it presents the
    /// multiplier (e.g. "x3"), runs a shared close-time countdown visualised by <see cref="loadingBar"/>, and
    /// on CLAIM shows a rewarded ad - then reports the outcome back through the <see cref="GoldMultiplierAdOffer"/>
    /// it was shown with. Every reward-specific decision (the multiplier, how long the offer lasts, what each
    /// outcome grants) is supplied by the caller in that offer.
    ///
    /// Two callers share this single instance:
    ///  - the gold pickup flow: this overlay self-subscribes to <see cref="Gold.Gold.ClaimRequested"/>
    ///    (it replaces the old GoldClaimOverlay) and offers the pickup's "CLAIM Nx" bonus;
    ///  - the milestone flow: <see cref="MilestoneCompletedOverlay"/> calls <see cref="TryShowOffer"/>.
    /// Because there is only one instance, both paths funnel through <see cref="TryShowOffer"/>, which is a
    /// no-op while the overlay is already in use (<see cref="IsBusy"/>), so the two flows never conflict.
    /// </summary>
    public sealed class ClaimGoldMultiplierWithAdsOverlay : OverlayBase
    {
        [Title("Claim Gold Multiplier")]
        [Tooltip("\"x3\" style multiplier label.")]
        [SerializeField] private TMP_Text rewardMultiplierText;

        [Tooltip("Countdown bar for the offer's close time. Drains 1 -> 0 over CloseSeconds.")]
        [SerializeField] private LoadingBar loadingBar;

        [Tooltip("Button that triggers the rewarded ad.")]
        [SerializeField] private Button claimButton;

        [Tooltip("Fallback close-time (seconds) used only when an offer is shown without one (CloseSeconds <= 0).")]
        [SerializeField, Min(0.1f)] private float fallbackCloseSeconds = 5f;

        // The offer currently being presented; null while hidden.
        private GoldMultiplierAdOffer _offer;
        private float _timeLeft;
        private float _closeSeconds;

        // True once CLAIM has been pressed: freezes the countdown/loading bar while the ad is in flight and
        // blocks a second press or an expiry from racing the ad outcome.
        private bool _claiming;

        // Set when a close is requested while we are still animating IN (Showing). The close is deferred to
        // OnShowed so a Hide is never issued mid-show animation (the shared UIViewBase has its re-entry guards
        // disabled and does not handle an overlapping Show/Hide on the same view).
        private bool _closePending;

        /// <summary>
        /// True whenever the overlay is not fully hidden (showing, shown, or mid-hide animation) - i.e. it is
        /// presenting an offer and unavailable to another caller. Accounts for the in-between Showing/Hiding
        /// states so a caller never double-shows while an animation is still running.
        /// </summary>
        [ShowInInspector, ReadOnly] public bool IsBusy => VisibilityState != UIVisibilityState.Hidden;

        protected override void Awake()
        {
            base.Awake();
            if (claimButton)
                claimButton.onClick.AddListener(OnClaimClicked);
        }

        private void OnEnable()
        {
            // Take over the old GoldClaimOverlay's role: surface the "CLAIM Nx" bonus when a gold is collected.
            Gold.Gold.ClaimRequested += OnGoldClaimRequested;
        }

        private void OnDisable()
        {
            Gold.Gold.ClaimRequested -= OnGoldClaimRequested;
        }

        // Gold pickup entry: the base reward was already granted on pickup, so the ad only grants the bonus.
        // Expiry / ad-failure simply close the offer (the player keeps the base).
        private void OnGoldClaimRequested(GoldClaimRequest request)
        {
            var offer = new GoldMultiplierAdOffer(
                multiplier: request.Multiplier,
                closeSeconds: request.PopupSeconds,
                onRewarded: () => request.OnClaimed?.Invoke(request.Bonus));
            TryShowOffer(offer);
        }

        /// <summary>
        /// Shows the overlay for <paramref name="offer"/> unless it is already in use. Returns true when the
        /// offer was accepted (the overlay is now showing it) or false when it was busy and the caller should
        /// skip the upsell. The single entry point for every caller, so the shared instance never double-shows.
        /// </summary>
        public bool TryShowOffer(GoldMultiplierAdOffer offer)
        {
            if (offer == null || IsBusy)
                return false;

            Show(uiData: offer);
            return true;
        }

        /// <summary>
        /// Asks the overlay to close <paramref name="offer"/> in an animation-safe way - and ONLY if that is
        /// the offer currently up (so a coordinating view never closes another flow's offer, e.g. a gold
        /// pickup's). If we are still animating IN (Showing), the close is deferred to OnShowed so a Hide is
        /// never issued mid-show. No-op if a different offer is up or we are already hiding/hidden.
        /// </summary>
        public void RequestClose(GoldMultiplierAdOffer offer)
        {
            if (!ReferenceEquals(_offer, offer))
                return;

            if (VisibilityState == UIVisibilityState.Showing)
                _closePending = true;
            else if (ShowingOrShown)
                Hide();
        }

        protected override void OnBeforeShowing(bool immediate, object uiData = null)
        {
            base.OnBeforeShowing(immediate, uiData);

            // Capture the offer BEFORE any show animation so a coordinating view (e.g. the milestone pop-up)
            // can still identify and close exactly this offer via RequestClose while we are mid-show.
            _offer = uiData as GoldMultiplierAdOffer;
            _claiming = false;
            _closePending = false;
        }

        protected override void OnShowed(bool immediate, object uiData = null)
        {
            base.OnShowed(immediate, uiData);

            // Normally set in OnBeforeShowing; re-resolve defensively for the animation-timeout fallback path
            // (ShowAsync re-enters with canCallOnShowed:false, skipping OnBeforeShowing).
            if (_offer == null)
                _offer = uiData as GoldMultiplierAdOffer;

            // A close arrived while we were animating in: honour it now that the show has completed, instead
            // of having issued a Hide mid-show.
            if (_closePending)
            {
                _closePending = false;
                HideSelf();
                return;
            }

            _claiming = false;

            _closeSeconds = _offer != null && _offer.CloseSeconds > 0f ? _offer.CloseSeconds : fallbackCloseSeconds;
            _timeLeft = _closeSeconds;

            int multiplier = _offer?.Multiplier ?? 1;
            if (rewardMultiplierText)
                rewardMultiplierText.text = $"<size=60>x</size>{multiplier}";

            if (claimButton)
                claimButton.interactable = true;

            // The bar starts full and drains to empty over the countdown (a "time remaining" read).
            if (loadingBar)
                loadingBar.SetProgress(1f, instant: true);
        }

        private void Update()
        {
            // Only tick while fully shown and not waiting on an ad. Cheap early-out every other frame.
            if (_claiming || VisibilityState != UIVisibilityState.Showed)
                return;

            _timeLeft -= Time.unscaledDeltaTime;

            if (loadingBar && _closeSeconds > 0f)
                loadingBar.SetProgress(_timeLeft / _closeSeconds);

            if (_timeLeft <= 0f)
                Expire();
        }

        // Countdown elapsed with no claim: dismiss, then report expiry to the caller.
        private void Expire()
        {
            GoldMultiplierAdOffer offer = _offer;
            HideSelf();
            offer?.OnExpired?.Invoke();
        }

        private void OnClaimClicked()
        {
            if (_claiming || VisibilityState != UIVisibilityState.Showed)
                return;

            _claiming = true;
            if (claimButton)
                claimButton.interactable = false;

            ShowRewardedAdAsync().Forget();
        }

        // Plays the rewarded ad for the current offer through the shared MaxAdService. The offer is captured
        // up front so the right flow still resolves even though OnHidden clears _offer while the ad is in
        // flight. A successful watch grants the bonus (OnRewarded); any failure surfaces a toast and falls
        // back to the offer's OnAdFailed path (the player keeps whatever base reward was already granted).
        private async UniTaskVoid ShowRewardedAdAsync()
        {
            GoldMultiplierAdOffer offer = _offer;

            // Tell the caller the ad is starting so it can freeze any coordinating timer of its own.
            offer?.OnClaimInitiated?.Invoke();

            bool isSuccess = await ServiceLocator.GetService<MaxAdService>().ShowRewardedAdAsync("gold_multiplier");
            if (!isSuccess)
            {
                RuntimeUI.ShowToast("Rewarded ad was not completed");
                HideSelf();
                offer?.OnAdFailed?.Invoke();
                return;
            }

            HideSelf();
            offer?.OnRewarded?.Invoke();
        }

        // Dismiss guard: only hide while actually on screen, so a coordinating view closing us and our own
        // expiry / ad-outcome can't replay the hide. Offer/claim state is cleared in OnHidden so an external
        // Hide() is handled too.
        private void HideSelf()
        {
            _claiming = false;
            if (ShowingOrShown)
                Hide();
        }

        protected override void OnHidden(bool immediate = false)
        {
            base.OnHidden(immediate);
            _offer = null;
            _claiming = false;
            _closePending = false;
        }
    }
}
