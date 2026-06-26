using UISystem.Runtime.Scripts.Mono;

namespace UISystem.Runtime
{
    public class ConsumableTextWidget : TextWidget
    {
        public void SetText(int currentValue, int maxValue)
        {
            string formattedText = $"{currentValue}/{maxValue}";
            base.SetText(formattedText);
        }
    }
}