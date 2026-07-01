using TMPro;
using UIManager;
using UnityEngine;
using Utils;

namespace UI
{
    /// <summary>
    /// Result payload for <see cref="LevelResultOverlay"/>, built by <see cref="Events.EventManager"/>.
    /// </summary>
    public sealed class LevelResultData
    {
        public readonly bool Win;
        public readonly int BaseReward; // full reward on win, 1/3 on fail (already computed by the manager)
        public readonly int BonusReward;
        public readonly int AdMultiplier; // 3X upsell offered on a win; 1 (no upsell) on a fail

        public LevelResultData(bool win, int baseReward, int bonusReward, int adMultiplier)
        {
            Win = win;
            BaseReward = baseReward;
            BonusReward = bonusReward;
            AdMultiplier = adMultiplier;
        }
    }

    /// <summary>
    /// The level end screen (GDD "Completion"). Shows WIN/FAIL + the reward, and on a win offers the shared
    /// "watch ad to multiply" upsell (<see cref="ClaimGoldMultiplierWithAdsOverlay"/>) exactly like
    /// <see cref="MilestoneCompletedOverlay"/>. Every path (CLAIM button, auto-close timer, ad success/failure)
    /// funnels through one guarded <see cref="Resolve"/> so the reward is reported exactly once, after which
    /// <see cref="Events.EventManager"/> grants it, advances the level and tears the event down.
    /// </summary>
    public sealed class LevelResultOverlay : OverlayBase
    {
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text rewardText;
        [SerializeField] private EnhancedButton claimButton;

        [Tooltip("WIN title text.")]
        [SerializeField] private string winTitle = "COMPLETE!";
        [Tooltip("FAIL title text.")]
        [SerializeField] private string failTitle = "FAILED";

        [Tooltip("Seconds the result stays up before the base reward auto-claims (shared with the 3X upsell).")]
        [SerializeField, Min(0.1f)] private float popupCloseSeconds = 6f;

        private bool _win;
        private int _baseReward;
        private int _bonusReward;
        private int _adMultiplier;

        private bool _resolved;
        private bool _awaitingAd;
        private float _timeLeft;

        // The upsell offer we opened this round, so we only ever close our own (never a gold pickup's).
        private GoldMultiplierAdOffer _offer;

        private ClaimGoldMultiplierWithAdsOverlay _multiplierOverlay;
        private ClaimGoldMultiplierWithAdsOverlay MultiplierOverlay => _multiplierOverlay
            ? _multiplierOverlay
            : (_multiplierOverlay = GameUIManagerInstance.GetOverlayUI<ClaimGoldMultiplierWithAdsOverlay>());

        protected override void Awake()
        {
            base.Awake();
            if (claimButton)
                claimButton.onClick.AddListener(OnClaimClicked);
        }

        protected override void OnShowed(bool immediate, object uiData = null)
        {
            base.OnShowed(immediate, uiData);

            _resolved = false;
            _awaitingAd = false;
            _timeLeft = popupCloseSeconds;

            var data = uiData as LevelResultData;
            _win = data?.Win ?? false;
            _baseReward = data?.BaseReward ?? 0;
            _bonusReward = data?.BonusReward ?? 0;
            _adMultiplier = data?.AdMultiplier ?? 1;

            if (titleText) titleText.text = _win ? winTitle : failTitle;
            if (rewardText) rewardText.text = GoldFormat.Abbreviate(_baseReward);
            if (claimButton) claimButton.interactable = true;

            // Offer the 3X upsell only on a win with a real multiplier.
            if (_win && _adMultiplier > 1)
                TryShowMultiplierUpsell();
        }

        private void TryShowMultiplierUpsell()
        {
            _offer = null;

            ClaimGoldMultiplierWithAdsOverlay overlay = MultiplierOverlay;
            if (!overlay || overlay.IsBusy)
                return;

            var offer = new GoldMultiplierAdOffer(
                multiplier: _adMultiplier,
                closeSeconds: popupCloseSeconds,
                onRewarded: () => Resolve((_baseReward + _bonusReward) * _adMultiplier),
                onAdFailed: () => Resolve(_baseReward + _bonusReward),
                onExpired: null, // our own countdown auto-claims the base at the same time
                onClaimInitiated: OnUpsellClaimInitiated);

            if (overlay.TryShowOffer(offer))
                _offer = offer;
        }

        // The ad is in flight: stop the auto-claim countdown and lock our claim button - the ad outcome decides.
        private void OnUpsellClaimInitiated()
        {
            _awaitingAd = true;
            if (claimButton)
                claimButton.interactable = false;
        }

        private void Update()
        {
            if (_resolved || _awaitingAd || VisibilityState != UIVisibilityState.Showed)
                return;

            _timeLeft -= Time.unscaledDeltaTime;
            if (_timeLeft <= 0f)
                Resolve(_baseReward + _bonusReward); // auto-claim the base so a reward is never lost
        }

        private void OnClaimClicked() => Resolve(_baseReward + _bonusReward);

        // Single guarded resolve+close path. Closes the upsell we own, hides self, then reports the final reward
        // to the manager (which grants gold, advances the level and restores the world).
        private void Resolve(int finalReward)
        {
            if (_resolved)
                return;

            _resolved = true;

            ClaimGoldMultiplierWithAdsOverlay overlay = _multiplierOverlay;
            if (overlay && _offer != null)
                overlay.RequestClose(_offer);
            _offer = null;

            bool win = _win;
            if (ShowingOrShown)
                Hide();

            if (Events.EventManager.Exists())
                Events.EventManager.Instance.OnResultResolved(win, finalReward);
        }
    }
}