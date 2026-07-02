using System.Threading;
using _Game.Scripts.Utils.VContainer;
using Ads;
using Clutch;
using Core;
using Cysharp.Threading.Tasks;
using Gold;
using Save;
using SpektraGames.RuntimeUI.Runtime;
using TMPro;
using UIManager;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace UI
{
    public class WatchAdsToGetFreeGoldsScreen : ScreenBase
    {
        [SerializeField, SetRef("Text_Reward")]
        private TMP_Text rewardText;
        [SerializeField, SetRef("Button_Claim")]
        private Button claimButton;
        [SerializeField, SetRef("Button_Skip")]
        private Button skipButton;

        private int _goldToGive;

        private void Start()
        {
            claimButton.onClick.AddListener(OnClickClaimButton);
            skipButton.onClick.AddListener(OnClickSkipButton);
        }

        private void OnDestroy()
        {
            if (claimButton)
                claimButton.onClick.RemoveListener(OnClickClaimButton);
            if (skipButton)
                skipButton.onClick.RemoveListener(OnClickSkipButton);
        }

        protected override void OnBeforeShowing(bool immediate, object uiData = null)
        {
            _goldToGive = ClutchConfigResolver.Get<FreeGoldConfig>(ClutchFlagKeys.FreeGoldConfig).WatchAdsRewardGold;

            rewardText.SetText(_goldToGive.ToString("N0"));
        }

        private async void OnClickClaimButton()
        {
            CancellationToken token = this.GetCancellationTokenOnDestroy();

            bool isSuccess = await ServiceLocator.GetService<MaxAdService>()
                .ShowRewardedAdAsync()
                .AttachExternalCancellation(token);

            if (isSuccess)
            {
                SaveManager.AddGolds(_goldToGive);
                SaveManager.Save();
                RuntimeUI.ShowToast($"+{_goldToGive} G");
            }
            else
            {
                RuntimeUI.ShowToast("Rewarded ad was not completed");
            }
        }

        private void OnClickSkipButton()
        {
            if (GameManager.Exists())
            {
                GameUIManager.Instance.SwitchScreen<GameplayScreen>();
            }
            else
            {
                GameUIManager.Instance.SwitchScreen<MainMenuScreen>();
            }
        }
    }
}