using Save;

namespace UI
{
    /// <summary>
    /// Top-bar driven-distance counter. Mirrors <see cref="SaveManager.DistanceDrivenKm"/> into its label,
    /// independently of any screen, by listening to <see cref="SaveManager.OnDistanceDrivenChanged"/> while
    /// active. Add it to the Widget_DriveDistance prefab and wire its label.
    /// </summary>
    public class DriveDistanceWidget : SaveValueWidget
    {
        protected override void SubscribeToValue()
        {
            SaveManager.OnDistanceDrivenChanged -= HandleValueChanged;
            SaveManager.OnDistanceDrivenChanged += HandleValueChanged;
        }

        protected override void UnsubscribeFromValue()
        {
            SaveManager.OnDistanceDrivenChanged -= HandleValueChanged;
        }

        protected override string GetDisplayText() => $"{SaveManager.DistanceDrivenKm:N0} KM";
    }
}