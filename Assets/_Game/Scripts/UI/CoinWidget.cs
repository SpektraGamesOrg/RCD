using Save;

namespace UI
{
    /// <summary>
    /// Top-bar gold counter. Mirrors <see cref="SaveManager.Gold"/> into its label, independently of any
    /// screen, by listening to <see cref="SaveManager.OnCoinsChanged"/> while active. Add it to the
    /// Widget_Coin prefab and wire its label.
    /// </summary>
    public class CoinWidget : SaveValueWidget
    {
        protected override void SubscribeToValue()
        {
            SaveManager.OnCoinsChanged -= HandleValueChanged;
            SaveManager.OnCoinsChanged += HandleValueChanged;
        }

        protected override void UnsubscribeFromValue()
        {
            SaveManager.OnCoinsChanged -= HandleValueChanged;
        }

        protected override string GetDisplayText() => SaveManager.Gold.ToString("N0");
    }
}
