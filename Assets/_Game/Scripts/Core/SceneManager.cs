using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using SpektraGames.SpektraUtilities.Runtime;
using UI;
using UIManager;
using UnityEngine;
using UnitySceneManager = UnityEngine.SceneManagement.SceneManager;

namespace Core
{
    /// <summary>
    /// Singleton that switches scenes (main menu &lt;-&gt; gameplay) asynchronously.
    ///
    /// Shows the shared <see cref="LoadingScreen"/> (<c>Screen_Loading</c>) through
    /// <see cref="GameUIManager"/> for the duration of the load, drives its progress bar from the
    /// <see cref="AsyncOperation"/>, then closes the screen layer once the new scene is live.
    ///
    /// This object MUST be marked "Dont Destroy On Load" (Singleton foldout in the Inspector) so it
    /// survives the scene swap and can finish the load / hide the loading screen. It lives under the
    /// MainMenu "Singletons" root next to <see cref="GameInitializer"/>. The persistent
    /// <see cref="GameUIManager"/> is DontDestroyOnLoad too, so the loading screen it owns stays alive
    /// across the transition.
    ///
    /// Usage:
    /// <code>
    /// SceneManager.Instance.LoadGameScene();      // main menu -> gameplay
    /// SceneManager.Instance.LoadMainMenuScene();  // gameplay  -> main menu
    /// await SceneManager.Instance.LoadSceneAsync("Game", "Loading track...");
    /// </code>
    /// The manager only drives the loading screen; the caller / destination scene opens the next
    /// screen (e.g. the menu) after the load, since the screen layer is intentionally left empty.
    /// </summary>
    [DefaultExecutionOrder(-900)]
    public class SceneManager : SingletonComponent<SceneManager>
    {
        [Header("Scene Names (must match Build Settings)")]
        [SerializeField] private string mainMenuSceneName = "MainMenu";
        [SerializeField] private string gameSceneName = "Game";

        [Header("Loading Screen")]
        [SerializeField] private string gameLoadingDescription = "Loading...";
        [SerializeField] private string menuLoadingDescription = "Loading...";

        [Tooltip("Minimum time (seconds, unscaled) the loading screen stays up so it never just flashes.")]
        [SerializeField, Min(0f)] private float minLoadingDuration = 0.4f;

        private bool isLoading;

        /// <summary>True while a scene load is in progress. Re-entrant load requests are ignored.</summary>
        public bool IsLoading => isLoading;

        // -----------------------------------------------------------------------------
        // Public API (button-friendly fire-and-forget wrappers)
        // -----------------------------------------------------------------------------

        /// <summary>Loads the gameplay scene (main menu -&gt; game).</summary>
        public void LoadGameScene() => LoadSceneAsync(gameSceneName, gameLoadingDescription).Forget();

        /// <summary>Loads the main menu scene (game -&gt; main menu).</summary>
        public void LoadMainMenuScene() => LoadSceneAsync(mainMenuSceneName, menuLoadingDescription).Forget();

        /// <summary>Loads an arbitrary scene by name. Fire-and-forget wrapper for UI buttons.</summary>
        public void LoadScene(string sceneName) => LoadSceneAsync(sceneName).Forget();

        // -----------------------------------------------------------------------------
        // Core async load
        // -----------------------------------------------------------------------------

        /// <summary>
        /// Loads <paramref name="sceneName"/> asynchronously, keeping the loading screen visible for the
        /// duration of the load. Awaitable; ignores re-entrant calls while a load is already running.
        /// </summary>
        public async UniTask LoadSceneAsync(string sceneName, string description = null)
        {
            if (isLoading)
            {
                Debug.LogError($"[SceneManager] Ignored LoadScene('{sceneName}') - a scene load is already in progress.");
                return;
            }

            if (string.IsNullOrEmpty(sceneName))
            {
                Debug.LogError("[SceneManager] Cannot load a scene with a null or empty name.");
                return;
            }

            isLoading = true;

            // This object is DontDestroyOnLoad, so the token only trips on real destruction (e.g. app quit),
            // not on the scene swap - the continuation that hides the loading screen is therefore safe.
            CancellationToken token = this.GetCancellationTokenOnDestroy();
            LoadingScreen loadingScreen = ShowLoadingScreen(description);

            try
            {
                float startTime = Time.unscaledTime;

                AsyncOperation operation = UnitySceneManager.LoadSceneAsync(sceneName);
                await operation.ToUniTask(
                    progress: loadingScreen != null ? loadingScreen.Progress : null,
                    cancellationToken: token);

                // Snap the bar to full and hold the screen briefly so it never just blinks.
                if (loadingScreen != null)
                    loadingScreen.SetProgress(1f);

                float elapsed = Time.unscaledTime - startTime;
                if (elapsed < minLoadingDuration)
                {
                    await UniTask.Delay(
                        TimeSpan.FromSeconds(minLoadingDuration - elapsed),
                        DelayType.UnscaledDeltaTime,
                        cancellationToken: token);
                }
            }
            catch (OperationCanceledException)
            {
                // The manager was destroyed mid-load (e.g. application quit). Nothing else to do.
                return;
            }
            catch (Exception e)
            {
                Debug.LogError($"[SceneManager] Failed to load scene '{sceneName}': {e}");
            }
            finally
            {
                HideLoadingScreen();
                isLoading = false;
            }
        }

        // -----------------------------------------------------------------------------
        // Loading screen helpers
        // -----------------------------------------------------------------------------

        // Switches to the shared loading screen. Returns null (and falls back to a screenless load)
        // when no GameUIManager / LoadingScreen is available.
        private static LoadingScreen ShowLoadingScreen(string description)
        {
            if (GameUIManager.Instance == null)
            {
                Debug.LogError("[SceneManager] No GameUIManager available; loading without a loading screen.");
                return null;
            }

            LoadingScreen loadingScreen = GameUIManager.Instance.GetScreen<LoadingScreen>();
            if (loadingScreen == null)
                return null; // GetScreen already logged the error.

            // Passing the description as uiData makes LoadingScreen.OnBeforeShowing apply it and reset the bar to 0.
            GameUIManager.Instance.SwitchScreen(loadingScreen, false, description);
            return loadingScreen;
        }

        // Clears the screen layer so the loading screen is dismissed with nothing left showing.
        // The destination scene / caller opens whatever screen should come next.
        private static void HideLoadingScreen()
        {
            if (GameUIManager.Instance == null)
                return;

            GameUIManager.Instance.ForceCloseAllScreens();
        }
    }
}
