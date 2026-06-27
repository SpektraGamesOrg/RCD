using Save;
using TMPro;
using UISystem.Runtime.Scripts;
using UnityEngine;

namespace UI
{
    /// <summary>
    /// Base for small, self-contained top-bar widgets that mirror a single saved value into a label.
    ///
    /// The widget owns its own display end to end: it repaints on enable and then stays current by
    /// listening to the relevant <see cref="SaveManager"/> event for as long as it is active, so no
    /// screen or controller ever has to push updates into it. Because the owning screen toggles its
    /// Content active on show/hide, the subscribe/refresh on <see cref="OnEnable"/> and teardown on
    /// <see cref="OnDisable"/> line up exactly with the widget becoming visible / hidden.
    /// </summary>
    public abstract class SaveValueWidget : UIWidget
    {
        [SerializeField] private TMP_Text amountText;

        protected virtual void OnEnable()
        {
            Subscribe();
            Refresh();
        }

        protected virtual void OnDisable()
        {
            Unsubscribe();
        }

        // A full save wipe is shared by every widget; the concrete value event is wired by the subclass.
        // Subscriptions are refreshed defensively (-= then +=) so re-enabling can never stack duplicates.
        private void Subscribe()
        {
            SaveManager.OnSaveReset -= HandleReset;
            SaveManager.OnSaveReset += HandleReset;
            SubscribeToValue();
        }

        private void Unsubscribe()
        {
            SaveManager.OnSaveReset -= HandleReset;
            UnsubscribeFromValue();
        }

        // Concrete widgets hook their own SaveManager value event here (always with the same defensive
        // -= / += pattern) and provide the formatted text for the current value.
        protected abstract void SubscribeToValue();
        protected abstract void UnsubscribeFromValue();
        protected abstract string GetDisplayText();

        // The saved value changed - re-read and repaint. Exposed to subclasses so they can use it as the
        // handler for their typed SaveManager event.
        protected void HandleValueChanged(int _) => Refresh();
        private void HandleReset() => Refresh();

        private void Refresh()
        {
            if (amountText)
                amountText.text = GetDisplayText();
        }
    }
}
