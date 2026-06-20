using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UI;
using UIManager;

namespace Core
{
    /// <summary>
    /// Base for the post-load step of a scene. Once <see cref="CustomSceneManager"/> has streamed a scene in,
    /// it creates the matching loader and hands it the tail of the loading bar. The loader prepares the freshly
    /// loaded scene (e.g. loads the garage car), drives its slice of the bar, then switches to the scene's
    /// screen - so the loading screen is always replaced by a real screen, never an empty layer.
    ///
    /// Loaders are plain classes (NOT MonoBehaviours): they are created per load and find whatever scene
    /// objects they need after the scene is live. Each concrete scene gets its own loader sharing this base.
    /// </summary>
    public abstract class SceneLoaderBase
    {
        /// <summary>The scene this loader is responsible for.</summary>
        public abstract SceneType SceneType { get; }

        /// <summary>
        /// Prepares the freshly loaded scene and switches to its screen.
        /// <paramref name="progress"/> is a logical 0..1 reporter already scoped to this loader's slice of the
        /// bar (report <c>1f</c> when done). <paramref name="token"/> trips if the manager is destroyed mid-load.
        /// </summary>
        public abstract UniTask LoadAsync(IProgress<float> progress, CancellationToken token);

        /// <summary>
        /// Drives the loading bar to 100%, waits until it has visually filled, then holds the completed bar
        /// for a short beat - so the player always sees a full bar before the loader swaps in the scene's
        /// screen. No-op when no loading screen is available. Concrete loaders call this right before their
        /// screen switch.
        /// </summary>
        protected static async UniTask WaitForLoadingBarFilledAsync(CancellationToken token)
        {
            LoadingScreen loadingScreen = GameUIManager.Instance != null
                ? GameUIManager.Instance.GetScreen<LoadingScreen>()
                : null;

            if (loadingScreen != null)
                await loadingScreen.WaitUntilFilledAndHoldAsync(token);
        }
    }
}
