using System;
using System.Globalization;
using System.Threading;
using _Game.Scripts.Utils.VContainer;
using Ads;
using Analytics;
using Analytics.AppsFlyer;
using Clutch;
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

            MainLifetimeScope mainScope = ServiceLocator.GetScope<MainLifetimeScope>();

            // Firebase
            FirebaseAnalyticsService firebaseService = null;
            if (mainScope.TryGetServiceFromScope(out firebaseService))
            {
                var loadFirebaseAsync = firebaseService.InitializeAsync();
                float timeOut = 5f;
                float counter = timeOut;
                while (loadFirebaseAsync.Status != UniTaskStatus.Succeeded)
                {
                    await UniTask.NextFrame();
                    counter -= Time.unscaledDeltaTime;

                    if (counter <= 0)
                    {
                        Debug.LogError($"Firebase check dependencies could not complete in {timeOut} seconds");
                        break;
                    }
                }

                if (firebaseService.isFirebaseInitialized)
                {
                    firebaseService.SetUserId(PlayerPrefs.GetString("UserIDSaved", ""));
                }
                else
                {
                    Debug.LogError($"Firebase not initialized within {timeOut}s");
                }
            }
            else
            {
                Debug.LogError("firebaseService not found in main scope");
            }

            ServiceLocator.TryGetService(out IMMPService mmpService);

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

            // Clutch remote config. Resolves feature flags (vehicle prices, ad config) from Clutch with a
            // PlayerPrefs cache and a fallback SO, so a value is always available even offline. The service
            // owns its own timeout and resolves to cache/fallback within budget, so this await never blocks
            // boot on a hung request and the config is fully resolved before the main menu reads it.
            try
            {
                if (ServiceLocator.TryGetService(out IClutchConfigService clutchConfigService))
                {
                    // InitializeAsync owns its own 5s timeout and always resolves to cache/fallback
                    // within budget, so IsReady is true (cache authoritative) before this returns.
                    await clutchConfigService.InitializeAsync();

                    if (!clutchConfigService.IsReady)
                        Debug.LogError("[GameInitializer] Clutch config did not resolve; consumers will use cache/fallback.");

                    // Phase-two free grant: the synchronous boot grant (SaveManager.Initialize) used the
                    // serialized obtain types; now that Clutch has resolved, grant any car the remote config
                    // promotes to Free, so product can flip a car free without an SO change.
                    SaveManager.GrantClutchFreeVehicles(clutchConfigService);
                }
                else
                {
                    Debug.LogError("[GameInitializer] IClutchConfigService not found in scope.");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[GameInitializer] Clutch config init failed: {e.Message}");
            }

            // Ad config provider: parse the typed AdConfig from the now-resolved Clutch flag BEFORE the ad
            // service (and its gating/banner consumers) initialize, so Current is populated for the first read.
            if (ServiceLocator.TryGetService(out IAdConfigProvider adConfigProvider))
                adConfigProvider.Initialize();
            else
                Debug.LogError("[GameInitializer] IAdConfigProvider not found in scope.");

            // Applovin Max
            var adService = ServiceLocator.GetService<MaxAdService>();
            adService.Initialize(true);
            await UniTask.WaitUntil(() => adService.IsInitialized).TimeoutWithoutException(TimeSpan.FromSeconds(5));
            if (!adService.IsInitialized)
                Debug.LogError("AdInitTimeout");

            // App Open lifecycle owner (cold start + warm resume). Created programmatically as a
            // DontDestroyOnLoad service so no scene wiring is required; it is inert until a real App Open
            // ad unit id is configured (CTR-6257). Cold-start show is attempted right after SDK init.
            if (!AppOpenAdController.Exists())
            {
                var appOpenGo = new GameObject("[AppOpenAdController]");
                appOpenGo.AddComponent<AppOpenAdController>();
            }
            AppOpenAdController.Instance.TryShowColdStart();

            // User id
            SetUserIdForServices();

            // Completed
            Initializing = false;
            Initialized = true;
            progress?.Report(1f); // Logical end of the bootstrap slice.

            // Bootstrap is done: load the main menu scene asynchronously. The SceneManager drives its bar from
            // here (InitializationProgressEnd), streams the scene, then the MainMenu scene loader loads the
            // garage car and switches to the menu screen.
            await LoadMainMenuSceneAsync();
        }

        private void SetUserIdForServices()
        {
            string userId = SaveManager.UserId;

            try
            {
                UnityServices.ExternalUserId = userId;
            }
            catch (Exception e)
            {
                Debug.LogError(e.ToString());
            }

            try
            {
                ServiceLocator.TryGetService(out FirebaseAnalyticsService firebaseService);
                firebaseService.SetUserId(userId);
            }
            catch (Exception e)
            {
                Debug.LogError(e.ToString());
            }

            try
            {
                var adService = ServiceLocator.GetService<MaxAdService>();
                adService.SetUserId(userId);
            }
            catch (Exception e)
            {
                Debug.LogError(e.ToString());
            }
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