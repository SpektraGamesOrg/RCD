using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using SpektraGames.ResourceObject.Runtime;
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
    /// <see cref="GameUIManager"/> for the duration of the load and drives its progress bar. The bar is split
    /// into phases (see <see cref="sceneStreamProgressEnd"/>): the caller's bootstrap owns <c>[0, progressStart]</c>,
    /// scene streaming owns <c>[progressStart, sceneStreamProgressEnd]</c>, and the scene's
    /// <see cref="SceneLoaderBase"/> owns <c>[sceneStreamProgressEnd, 1]</c>.
    ///
    /// Every async load first hops through a lightweight buffer scene (<c>EmptySceneForLoading</c>)
    /// and waits a few frames there, so the current (heavy) scene is fully torn down before the target
    /// scene streams in. This keeps peak memory low on low-end mobile devices.
    ///
    /// Once the scene is live the matching <see cref="SceneLoaderBase"/> takes over: it prepares the scene
    /// (e.g. loads the garage car) and switches to the scene's screen. The loading screen is therefore always
    /// replaced by a real screen - the screen layer is never left empty.
    ///
    /// This object MUST be marked "Dont Destroy On Load" (Singleton foldout in the Inspector) so it
    /// survives the scene swap and can finish the load. It lives under the MainMenu "Singletons" root next to
    /// <see cref="GameInitializer"/>. The persistent <see cref="GameUIManager"/> is DontDestroyOnLoad too, so
    /// the loading screen it owns stays alive across the transition.
    ///
    /// Usage:
    /// <code>
    /// SceneManager.Instance.LoadGameScene();      // main menu -> gameplay
    /// SceneManager.Instance.LoadMainMenuScene();  // gameplay  -> main menu
    /// await SceneManager.Instance.LoadSceneAsync(SceneType.Game, "Loading track...");
    /// </code>
    /// </summary>
    [DefaultExecutionOrder(-900)]
    public class CustomSceneManager : SingletonComponent<CustomSceneManager>
    {
        // -----------------------------------------------------------------------------
        // Scene-state flags. Set by LoadSceneAsync so gameplay / menu code can ask "which scene am I in?"
        // without scanning the scene graph. The two "*LoadingOrLoaded" flags are mutually exclusive, as are
        // the two "*ActiveNow" flags. All default to false (the Starter boot scene is neither MainMenu nor
        // Game), so they stay false until the first scene load begins.
        // -----------------------------------------------------------------------------

        /// <summary>
        /// True from the moment a <see cref="SceneType.Game"/> load begins and for as long as the Game scene
        /// stays the current scene. Flips back to false the instant a load to a different scene begins.
        /// Mutually exclusive with <see cref="IsMainMenuSceneLoadingOrLoaded"/>.
        /// </summary>
        public static bool IsGameSceneLoadingOrLoaded { get; private set; }

        /// <summary>
        /// True only while the Game scene is fully streamed in, prepared and live (its loader has finished and
        /// its screen is up) - i.e. real gameplay is running. False during every load transition. Mutually
        /// exclusive with <see cref="IsMainMenuSceneActiveNow"/>.
        /// </summary>
        public static bool IsGameSceneActiveNow { get; private set; }

        /// <summary>
        /// True from the moment a <see cref="SceneType.MainMenu"/> load begins and for as long as the MainMenu
        /// scene stays the current scene. Flips back to false the instant a load to a different scene begins.
        /// Mutually exclusive with <see cref="IsGameSceneLoadingOrLoaded"/>.
        /// </summary>
        public static bool IsMainMenuSceneLoadingOrLoaded { get; private set; }

        /// <summary>
        /// True only while the MainMenu scene is fully streamed in, prepared and live (its loader has finished
        /// and its screen is up). False during every load transition. Mutually exclusive with
        /// <see cref="IsGameSceneActiveNow"/>.
        /// </summary>
        public static bool IsMainMenuSceneActiveNow { get; private set; }

        // Resets the static scene-state flags on entering play mode. The project has "Enter Play Mode Options"
        // enabled, so domain reload may be skipped and static state would otherwise leak across play sessions.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSceneStateFlags()
        {
            IsGameSceneLoadingOrLoaded = false;
            IsGameSceneActiveNow = false;
            IsMainMenuSceneLoadingOrLoaded = false;
            IsMainMenuSceneActiveNow = false;
        }


        [Header("Scene Names (must match Build Settings)")]
        [Tooltip("Scene name for SceneType.MainMenu.")]
        [SerializeField] private string mainMenuSceneName = "MainMenu";
        [Tooltip("Scene name for SceneType.Game.")]
        [SerializeField] private string gameSceneName = "Game";

        [Header("Loading Buffer Scene")]
        [Tooltip(
            "Empty scene loaded first (Single mode) so the current scene is unloaded before the target scene streams in. Must match Build Settings. Leave empty to skip the buffer hop.")]
        [SerializeField] private string loadingBufferSceneName = "EmptySceneForLoading";
        [Tooltip("Frames to wait on the empty buffer scene before loading the target scene, so the previous scene's teardown / asset unloading can settle.")]
        [SerializeField, Min(0)] private int bufferFrameDelay = 3;

        [Header("Loading Screen")]
        [SerializeField] private string gameLoadingDescription = "Loading...";
        [SerializeField] private string menuLoadingDescription = "Loading...";

        [Tooltip("Minimum time (seconds, unscaled) the loading screen stays up so it never just flashes.")]
        [SerializeField, Min(0f)] private float minLoadingDuration = 0.4f;

        [Header("Loading Progress Split")]
        [Tooltip("Point on the 0..1 bar where scene streaming ends. The scene's SceneLoaderBase (vehicles + " +
                 "screen switch) then drives the bar from here to 1. The caller's bootstrap, if any, owns " +
                 "[0, progressStart] before streaming begins.")]
        [SerializeField, Range(0f, 1f)] private float sceneStreamProgressEnd = 0.7f;

        // Fraction of the scene-streaming slice (progressStart -> sceneStreamProgressEnd) reserved for the
        // empty buffer scene load. The buffer scene is tiny so this only nudges the bar before the real load.
        private const float BufferLoadProgressWeight = 0.15f;

        private bool _isLoading;

        /// <summary>True while a scene load is in progress. Re-entrant load requests are ignored.</summary>
        public bool IsLoading => _isLoading;

        // -----------------------------------------------------------------------------
        // Public API (button-friendly fire-and-forget wrappers)
        // -----------------------------------------------------------------------------

        /// <summary>Loads the gameplay scene (main menu -&gt; game).</summary>
        public void LoadGameScene() => LoadSceneAsync(SceneType.Game, gameLoadingDescription).Forget();

        /// <summary>Loads the main menu scene (game -&gt; main menu).</summary>
        public void LoadMainMenuScene() => LoadSceneAsync(SceneType.MainMenu, menuLoadingDescription).Forget();

        /// <summary>Loads a scene by <see cref="SceneType"/>. Fire-and-forget wrapper for UI buttons.</summary>
        public void LoadScene(SceneType sceneType) => LoadSceneAsync(sceneType).Forget();

        // -----------------------------------------------------------------------------
        // Core async load
        // -----------------------------------------------------------------------------

        /// <summary>
        /// Loads <paramref name="sceneType"/> asynchronously, keeping the loading screen visible for the
        /// duration of the load. <paramref name="progressStart"/> is where this load picks the bar up (the
        /// caller's bootstrap owns everything before it). Scene streaming fills up to
        /// <see cref="sceneStreamProgressEnd"/>, then the scene's <see cref="SceneLoaderBase"/> drives the
        /// bar to 1 and switches to the scene's screen. Awaitable; ignores re-entrant calls while a load is
        /// already running.
        /// </summary>
        public async UniTask LoadSceneAsync(
            SceneType sceneType,
            string description = null,
            float progressStart = 0f)
        {
            if (_isLoading)
            {
                Debug.LogError($"[SceneManager] Ignored LoadScene({sceneType}) - a scene load is already in progress.");
                return;
            }

            string sceneName = ResolveSceneName(sceneType);
            if (string.IsNullOrEmpty(sceneName))
            {
                Debug.LogError($"[SceneManager] No scene name configured for SceneType.{sceneType}.");
                return;
            }

            _isLoading = true;
            progressStart = Mathf.Clamp01(progressStart);

            // A load just began: mark the target scene as "loading or loaded" and clear the scene we are
            // leaving. Neither scene counts as "active now" during the transition - the target only becomes
            // active once its loader has finished (see end of the try block below).
            IsGameSceneLoadingOrLoaded = sceneType == SceneType.Game;
            IsMainMenuSceneLoadingOrLoaded = sceneType == SceneType.MainMenu;
            IsGameSceneActiveNow = false;
            IsMainMenuSceneActiveNow = false;

            // Where scene streaming ends (and the loader takes over). Never below progressStart.
            float sceneStreamEnd = Mathf.Clamp(sceneStreamProgressEnd, progressStart, 1f);

            // This object is DontDestroyOnLoad, so the token only trips on real destruction (e.g. app quit),
            // not on the scene swap - the continuation that switches to the scene's screen is therefore safe.
            CancellationToken token = this.GetCancellationTokenOnDestroy();
            LoadingScreen loadingScreen = ShowLoadingScreen(sceneType, description, progressStart);

            await UniTask.NextFrame();
            await UniTask.NextFrame();

            try
            {
                float startTime = Time.unscaledTime;

                // Step 1: hop through the lightweight buffer scene first (Single mode) so the current
                // heavy scene is fully unloaded before the target scene streams in - keeps peak memory
                // low on mobile. Hold there for a few frames so teardown / unloading can settle.
                float targetProgressStart = progressStart;
                if (!string.IsNullOrEmpty(loadingBufferSceneName) && loadingBufferSceneName != sceneName)
                {
                    targetProgressStart = Mathf.Lerp(progressStart, sceneStreamEnd, BufferLoadProgressWeight);
                    await LoadSceneOperationAsync(loadingBufferSceneName, loadingScreen, progressStart, targetProgressStart, token);

                    if (bufferFrameDelay > 0)
                        await UniTask.DelayFrame(bufferFrameDelay, cancellationToken: token);
                }

                // Free the gameplay-only minimap texture before the shared memory cleanup when returning to
                // the main menu, so it is fully reclaimed by UnloadUnusedAssets / GC below.
                if (sceneType == SceneType.MainMenu)
                    Minimap.MinimapManager.UnloadMapTexture();

                // Clean memory
                await UniTask.NextFrame();
                ResourceObjectCleaner.UnloadAll();
                System.GC.Collect();
                System.GC.WaitForPendingFinalizers();
                System.GC.Collect();
                await Resources.UnloadUnusedAssets();
                System.GC.Collect();

                // Step 2: stream the target scene over the rest of the scene-streaming slice.
                await LoadSceneOperationAsync(sceneName, loadingScreen, targetProgressStart, sceneStreamEnd, token);

                if (loadingScreen != null)
                    loadingScreen.SetProgress(sceneStreamEnd);

                // Keep the loading screen up for at least the minimum duration so a fast load never just blinks,
                // before the loader takes over the tail of the bar and switches to the scene's screen.
                float elapsed = Time.unscaledTime - startTime;
                if (elapsed < minLoadingDuration)
                {
                    await UniTask.Delay(
                        TimeSpan.FromSeconds(minLoadingDuration - elapsed),
                        DelayType.UnscaledDeltaTime,
                        cancellationToken: token);
                }

                // Step 3: hand the tail of the bar [sceneStreamEnd -> 1] to the scene's loader. It prepares the
                // freshly streamed scene and switches to the scene's screen (so the loading screen is replaced
                // by a real screen, never an empty layer).
                SceneLoaderBase loader = ResolveLoader(sceneType);
                IProgress<float> loaderProgress = loadingScreen == null
                    ? null
                    : new RangedProgress(loadingScreen.Progress, sceneStreamEnd, 1f);

                if (loader != null)
                    await loader.LoadAsync(loaderProgress, token);
                else if (loadingScreen != null)
                    loadingScreen.SetProgress(1f);

                // The scene is fully streamed in, prepared and its screen is up: it is now the active, live
                // scene. (Reached only on success - a cancellation/failure above leaves "active now" false.)
                IsGameSceneActiveNow = sceneType == SceneType.Game;
                IsMainMenuSceneActiveNow = sceneType == SceneType.MainMenu;
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
                _isLoading = false;
            }
        }

        // Loads a single scene (Single mode) to completion, mapping its 0..1 load progress onto the
        // [from, to] slice of the loading bar so multiple loads can share one continuous bar.
        private static async UniTask LoadSceneOperationAsync(
            string sceneName,
            LoadingScreen loadingScreen,
            float from,
            float to,
            CancellationToken token)
        {
            IProgress<float> progress = loadingScreen == null
                ? null
                : new RangedProgress(loadingScreen.Progress, from, to);

            AsyncOperation operation = UnitySceneManager.LoadSceneAsync(sceneName);
            await operation.ToUniTask(progress: progress, cancellationToken: token);
        }

        // Maps a SceneType to its configured scene name (must match Build Settings).
        // Returns null for an unmapped value so the caller can fail loudly.
        private string ResolveSceneName(SceneType sceneType)
        {
            switch (sceneType)
            {
                case SceneType.MainMenu: return mainMenuSceneName;
                case SceneType.Game: return gameSceneName;
                default: return null;
            }
        }

        // Creates the post-load loader for a scene. Loaders are plain (non-MonoBehaviour) classes created per
        // load; returns null for an unmapped scene so the caller just finishes the bar.
        private static SceneLoaderBase ResolveLoader(SceneType sceneType)
        {
            switch (sceneType)
            {
                case SceneType.MainMenu: return new MainMenuSceneLoader();
                case SceneType.Game: return new GameSceneLoader();
                default: return null;
            }
        }

        // -----------------------------------------------------------------------------
        // Loading screen helpers
        // -----------------------------------------------------------------------------

        // Switches to the shared loading screen. Returns null (and falls back to a screenless load)
        // when no GameUIManager / LoadingScreen is available.
        private static LoadingScreen ShowLoadingScreen(SceneType sceneType, string description, float progressStart)
        {
            if (GameUIManager.Instance == null)
            {
                Debug.LogError("[SceneManager] No GameUIManager available; loading without a loading screen.");
                return null;
            }

            LoadingScreen loadingScreen = GameUIManager.Instance.GetScreen<LoadingScreen>();
            if (loadingScreen == null)
                return null; // GetScreen already logged the error.

            if (GameUIManager.Instance.ActiveScreen.screen == loadingScreen)
            {
                // Already showing (e.g. GameUIManager auto-opened Screen_Loading at boot). Re-switching to
                // it would race Hide vs Show on the same view, so continue from the requested range instead.
                loadingScreen.SetProgress(progressStart, instant: Mathf.Approximately(progressStart, 0f));
                if (!string.IsNullOrEmpty(description))
                    loadingScreen.SetDescription(description);
                loadingScreen.SetBackground(sceneType);
            }
            else
            {
                var loadingArgs = new LoadingScreen.LoadingArgs
                {
                    Description = description,
                    InitialProgress = progressStart,
                    SceneType = sceneType,
                };
                GameUIManager.Instance.SwitchScreen(loadingScreen, false, loadingArgs);
            }

            return loadingScreen;
        }
    }
}