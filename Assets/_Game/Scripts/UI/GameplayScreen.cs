using Save;
using TMPro;
using UIManager;
using UnityEngine;

namespace UI
{
    /// <summary>
    /// In-game HUD. Currently shows the live coin total, updating instantly when coins change
    /// (e.g. when a gold pickup is collected) via <see cref="SaveManager.OnCoinsChanged"/>.
    /// </summary>
    public class GameplayScreen : ScreenBase
    {
        [SerializeField] private TMP_Text coinAmountText;

        protected override void OnBeforeShowing(bool immediate, object uiData = null)
        {
            base.OnBeforeShowing(immediate, uiData);
            SaveManager.OnCoinsChanged += OnCoinsChanged;
            RefreshCoins(SaveManager.Coins);
        }

        protected override void OnHidden(bool immediate = false)
        {
            base.OnHidden(immediate);
            SaveManager.OnCoinsChanged -= OnCoinsChanged;
        }

        private void OnCoinsChanged(int newTotal) => RefreshCoins(newTotal);

        private void RefreshCoins(int total)
        {
            if (coinAmountText != null)
                coinAmountText.text = total.ToString("N0");
        }
    }
}
