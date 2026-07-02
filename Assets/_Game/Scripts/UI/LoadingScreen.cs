using System;
using System.Threading;
using _Game.Scripts.Utils.VContainer;
using Ads;
using Core;
using Cysharp.Threading.Tasks;
using TMPro;
using UIManager;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// Full-screen loading view (<c>Screen_Loading</c>). Shows a big background, a modular
    /// <see cref="LoadingBar"/> and a description line beneath the bar.
    ///
    /// The bar is fully controllable from the outside. Either talk to this screen
    /// (<see cref="SetProgress"/>, <see cref="SetDescription"/>) or grab the underlying
    /// <see cref="LoadingBar"/> / its <see cref="IProgress{Single}"/> and hand it to an async loader:
    /// <code>
    /// var loading = GameUIManager.Instance.GetScreen&lt;LoadingScreen&gt;();
    /// GameUIManager.Instance.SwitchScreen(loading);
    /// loading.SetDescription("Loading track...");
    /// await SceneManager.LoadSceneAsync("Game").ToUniTask(progress: loading.Progress);
    /// </code>
    ///
    /// Optionally pass a <see cref="string"/> (or <see cref="LoadingArgs"/>) as the screen's
    /// uiData and the description / starting progress are applied automatically when it opens.
    /// </summary>
    public class LoadingScreen : ScreenBase
    {
        [Header("Loading")]
        [SerializeField] private LoadingBar loadingBar;
        [SerializeField] private Image loadingBackground;
        [SerializeField] private TMP_Text descriptionText;

        [Tooltip("Seconds (unscaled) the full bar is held on screen after it reaches 100%, before the " +
                 "loading screen is dismissed.")]
        [SerializeField, Min(0f)] private float filledHoldDuration = 1f;
        
        [SerializeField] private SerializedDictionary<SceneType, Sprite> loadingBackgrounds = new();

        private const string DefaultDescription = "Loading...";

        /// <summary>The modular progress bar driving this screen. Safe to drive directly.</summary>
        public LoadingBar Bar => loadingBar;

        /// <summary>
        /// The bar exposed as an <see cref="IProgress{Single}"/>. Hand this to any async loader so
        /// it can report 0..1 progress straight to the bar.
        /// </summary>
        public IProgress<float> Progress => loadingBar;

        /// <summary>Optional payload for opening the screen with a preset description / progress / background.</summary>
        public class LoadingArgs
        {
            public string Description;
            public float InitialProgress;
            public SceneType SceneType;
        }

        protected override void OnBeforeShowing(bool immediate, object uiData = null)
        {
            base.OnBeforeShowing(immediate, uiData);

            // Always start from a clean bar each time the screen is opened.
            if (loadingBar)
                loadingBar.ResetBar();

            switch (uiData)
            {
                case LoadingArgs args:
                    SetDescription(string.IsNullOrEmpty(args.Description) ? DefaultDescription : args.Description);
                    if (loadingBar)
                        loadingBar.SetProgress(args.InitialProgress);
                    SetBackground(args.SceneType);
                    break;

                case string description:
                    SetDescription(description);
                    break;

                default:
                    break;
            }

            // Loading-screen MREC (gated by ad_loading_mrec_enabled inside the table). MREC is a display
            // unit, not cooldown-gated. Kept loaded and re-shown; hidden again in OnHidden.
            if (ServiceLocator.TryGetService(out IAdService adService))
                adService.ShowBanner(BannerPlacement.LoadingMrec);
        }

        protected override void OnHidden(bool immediate = false)
        {
            base.OnHidden(immediate);

            // Hide the loading MREC when the loading screen is dismissed (keeps it loaded for cheap re-show).
            if (ServiceLocator.TryGetService(out IAdService adService))
                adService.HideBanner(BannerPlacement.LoadingMrec);
        }

        /// <summary>
        /// Drives the bar to 100%, waits until it has visually filled, then holds the completed bar on screen
        /// for <see cref="filledHoldDuration"/> seconds - so the player always sees a full bar for a beat
        /// before the loading screen is replaced. Safe no-op when there is no bar.
        /// </summary>
        public async UniTask WaitUntilFilledAndHoldAsync(CancellationToken token = default)
        {
            if (loadingBar != null)
                await loadingBar.WaitUntilFilledAsync(token);

            if (filledHoldDuration > 0f)
                await UniTask.Delay(
                    TimeSpan.FromSeconds(filledHoldDuration),
                    DelayType.UnscaledDeltaTime,
                    cancellationToken: token);
        }

        /// <summary>Sets the progress shown on the bar. <paramref name="value01"/> is clamped to 0..1.</summary>
        public void SetProgress(float value01, bool instant = false)
        {
            if (loadingBar)
                loadingBar.SetProgress(value01, instant);
        }

        /// <summary>Replaces the description line shown under the bar.</summary>
        public void SetDescription(string text)
        {
            if (descriptionText)
                descriptionText.text = text;
        }

        /// <summary>
        /// Swaps the background image to the sprite mapped for <paramref name="sceneType"/>. Leaves the
        /// current background untouched when the scene has no entry in <see cref="loadingBackgrounds"/>.
        /// </summary>
        public void SetBackground(SceneType sceneType)
        {
            if (loadingBackground == null || loadingBackgrounds == null)
                return;

            if (loadingBackgrounds.TryGetValue(sceneType, out Sprite sprite) && sprite != null)
                loadingBackground.sprite = sprite;
        }
    }
}
