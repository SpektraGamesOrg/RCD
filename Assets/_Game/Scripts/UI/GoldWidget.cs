using System;
using Save;
using UIManager;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// Top-bar gold counter. Mirrors <see cref="SaveManager.Gold"/> into its label, independently of any
    /// screen, by listening to <see cref="SaveManager.OnGoldsChanged"/> while active. Add it to the
    /// Widget_Coin prefab and wire its label.
    /// </summary>
    public class GoldWidget : SaveValueWidget
    {
        [SerializeField, SetRef(typeof(Button))]
        private Button goldButton;

        protected override void OnEnable()
        {
            base.OnEnable();

            goldButton.onClick.AddListener(OnClickGoldButton);
        }

        protected override void OnDisable()
        {
            base.OnDisable();

            if (goldButton)
                goldButton.onClick.RemoveListener(OnClickGoldButton);
        }

        protected override void SubscribeToValue()
        {
            SaveManager.OnGoldsChanged -= HandleValueChanged;
            SaveManager.OnGoldsChanged += HandleValueChanged;
        }

        protected override void UnsubscribeFromValue()
        {
            SaveManager.OnGoldsChanged -= HandleValueChanged;
        }

        protected override string GetDisplayText() => SaveManager.Gold.ToString("N0");

        private void OnClickGoldButton()
        {
            if (!GameUIManager.Instance)
                return;

            var activeScreen = GameUIManager.Instance.ActiveScreen;
            if (!activeScreen.screen)
                return;

            Type screenType = activeScreen.screen.GetType();

            if (screenType == typeof(WatchAdsToGetFreeGoldsScreen) ||
                screenType == typeof(LevelResultScreen))
            {
                return;
            }

            GameUIManager.Instance.SwitchScreen<WatchAdsToGetFreeGoldsScreen>();
        }
    }
}