using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UIManager;
using UnityEngine;

namespace UI
{
    /// <summary>
    /// The 3-2-1 countdown shown just before a Commercial Break interstitial (RCD "Ad Placements":
    /// "3s countdown then inter"). Mirrors <see cref="EventCountdownOverlay"/> but with NO "GO!" flash —
    /// when the count reaches zero the caller opens the interstitial instead.
    ///
    /// Driven by <see cref="Ads.CommercialBreakController"/>, which awaits <see cref="PlayAsync"/> and then
    /// shows the ad. Uses unscaled time so the countdown is unaffected by any time-scale changes. Lives as a
    /// child overlay under GameUIManager (like the other overlays) and is resolved via GetOverlayUI.
    /// </summary>
    public sealed class CommercialBreakCountdownOverlay : OverlayBase
    {
        [SerializeField] private TMP_Text countdownText;

        /// <summary>
        /// Runs a <paramref name="fromSeconds"/>..1 countdown, holding each whole second, then hides. Awaitable;
        /// cancellation (e.g. scene tear-down) stops it mid-count. The caller opens the ad once this returns.
        /// </summary>
        public async UniTask PlayAsync(int fromSeconds, CancellationToken token = default)
        {
            Show(immediate: true);

            try
            {
                for (int n = fromSeconds; n >= 1; n--)
                {
                    if (countdownText)
                        countdownText.text = n.ToString();

                    await UniTask.Delay(TimeSpan.FromSeconds(1f), DelayType.UnscaledDeltaTime, cancellationToken: token);
                }
            }
            finally
            {
                // Always hide, even if cancelled mid-count, so the overlay never lingers over gameplay.
                Hide(immediate: true);
            }
        }
    }
}
