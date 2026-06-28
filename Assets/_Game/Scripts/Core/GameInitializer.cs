using System;
using System.Globalization;
using System.Threading;
using _Game.Scripts.Utils.VContainer;
using Cysharp.Threading.Tasks;
using Milestones;
using Save;
using SpektraGames.RuntimeUI.Runtime;
using SpektraGames.SpektraUtilities.Runtime;
using UI;
using UIManager;
using Unity.Services.Core;
using Unity.Services.Core.Environments;
using UnityEngine;

namespace Core
{
    [DefaultExecutionOrder(-1000)]
    public class GameInitializer : SingletonComponent<GameInitializer>
    {
        private const float InitializationProgressEnd = 0.3f;

        public static bool Initialized { get; private set; } = false;
        private static bool Initializing { get; set; } = false;

        protected override void Awake()
        {
            bool willDestroy = Exists();

            base.Awake();

            if (willDestroy)
                return;

            LoadingScreen loadingScreen = GameUIManager.Instance.GetScreen<LoadingScreen>();
            IProgress<float> bootstrapProgress = loadingScreen == null
                ? null
                : new RangedProgress(loadingScreen.Progress, 0f, InitializationProgressEnd);
            InitializeAsync(bootstrapProgress).Forget();
        }

        private void Start()
        {
        }

        private async UniTask InitializeAsync(IProgress<float> progress)
        {
#if DISABLE_SRDEBUGGER && !UNITY_SERVER
            Debug.developerConsoleEnabled = false;
            Debug.unityLogger.logEnabled = false;
            Debug.developerConsoleVisible = false;
#endif
            progress?.Report(0f);

            if (Initialized)
            {
                progress?.Report(1f); // Logical end of the bootstrap slice.
                return;
            }

            if (Initializing)
            {
                // Wait for the in-flight init to COMPLETE, not merely to be in-progress: the predicate
                // must be Initialized (Initializing is already true here, so it would return immediately
                // and read uninitialized state such as TutorialAPI). Mirrors WaitForInitialize() above.
                await UniTask.WaitUntil(() => Initialized);

                progress?.Report(1f); // Logical end of the bootstrap slice.
                return;
            }

            Initializing = true;

            // Auto-enable RCC's on-screen mobile controller on real Android / iOS device builds
            RCC_Settings rccSettings = RCC_Settings.Instance;
            if (rccSettings && (Application.isMobilePlatform || Application.isEditor))
                rccSettings.mobileControllerEnabled = true;

            // Save system. Run this synchronously (before the first await) so the player is guaranteed
            // to own a starter vehicle before the garage / main menu start reading the save data.
            SaveManager.Initialize();

            // Distance milestone service. Initialize right after the save system so it starts watching the
            // odometer before any gameplay or UI reads it. It grants nothing on startup: milestones already
            // reached in a previous session stay PENDING (claimable) - derived from the persisted distance
            // and claimed count - and are paid only when claimed.
            DistanceMilestoneManager.Initialize();

            // SR Debugger
#if !DISABLE_SRDEBUGGER
            if (!SRDebug.IsInitialized)
                SRDebug.Init();
#endif

            await UniTask.Yield();
            progress?.Report(0.2f); // Logical 0..1 within the bootstrap slice (scaled by RangedProgress).

            // Set time scale
            Time.timeScale = 1f;

            // Screen should never sleep
            Screen.sleepTimeout = SleepTimeout.NeverSleep;

            // To avoid the use of specific features used by different cultures like comma(',') character in floats
            Thread.CurrentThread.CurrentUICulture = new CultureInfo("en-US");
            Thread.CurrentThread.CurrentCulture = Thread.CurrentThread.CurrentUICulture;
            CultureInfo.DefaultThreadCurrentCulture = Thread.CurrentThread.CurrentUICulture;
            CultureInfo.DefaultThreadCurrentUICulture = Thread.CurrentThread.CurrentUICulture;

            // Core UI
            RuntimeUI.Init();
            progress?.Report(0.6f); // Logical 0..1 within the bootstrap slice (scaled by RangedProgress).

            // Unity services
            try
            {
                if (UnityServices.State != ServicesInitializationState.Initialized)
                {
#if DEV_GAME_ENVIRONMENT
                    string environment = "development";
#else
                        string environment = "production";
#endif
                    var options = new InitializationOptions().SetEnvironmentName(environment);
                    await UnityServices.InitializeAsync(options);
                }
            }
            catch (Exception e)
            {
                Debug.LogError(e.ToString());
            }

            MainLifetimeScope mainScope = ServiceLocator.GetScope<MainLifetimeScope>();

            // Completed
            Initializing = false;
            Initialized = true;
            progress?.Report(1f); // Logical end of the bootstrap slice.

            // Bootstrap is done: load the main menu scene asynchronously. The SceneManager drives its bar from
            // here (InitializationProgressEnd), streams the scene, then the MainMenu scene loader loads the
            // garage car and switches to the menu screen.
            await LoadMainMenuSceneAsync();
        }

        // Loads the main menu scene through the SceneManager singleton once bootstrap completes.
        private static async UniTask LoadMainMenuSceneAsync()
        {
            if (!CustomSceneManager.Exists())
            {
                Debug.LogError("[GameInitializer] No SceneManager in the scene; cannot load the main menu.");
                return;
            }

            // Hand the rest of the bar to the SceneManager, picking up where bootstrap left off.
            await CustomSceneManager.Instance.LoadSceneAsync(
                SceneType.MainMenu,
                "Loading menu...",
                InitializationProgressEnd);
        }
    }
}