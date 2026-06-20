using System;
using TMPro;
using UIManager;
using UnityEngine;

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
        [SerializeField] private TMP_Text descriptionText;

        private const string DefaultDescription = "Loading...";

        /// <summary>The modular progress bar driving this screen. Safe to drive directly.</summary>
        public LoadingBar Bar => loadingBar;

        /// <summary>
        /// The bar exposed as an <see cref="IProgress{Single}"/>. Hand this to any async loader so
        /// it can report 0..1 progress straight to the bar.
        /// </summary>
        public IProgress<float> Progress => loadingBar;

        /// <summary>Optional payload for opening the screen with a preset description / progress.</summary>
        public class LoadingArgs
        {
            public string Description;
            public float InitialProgress;
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
                        loadingBar.SetProgress(args.InitialProgress, true);
                    break;

                case string description:
                    SetDescription(description);
                    break;

                default:
                    break;
            }
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
    }
}