using System;
using System.Globalization;
using System.Threading;
using Cysharp.Threading.Tasks;
using Save;
using SpektraGames.RuntimeUI.Runtime;
using SpektraGames.SpektraUtilities.Runtime;
using Unity.Services.Core;
using Unity.Services.Core.Environments;
using UnityEngine;

namespace Core
{
    [DefaultExecutionOrder(-1000)]
    public class GameInitializer : SingletonComponent<GameInitializer>
    {
        public static bool Initialized { get; private set; } = false;
        private static bool Initializing { get; set; } = false;
        
        protected override void Awake()
        {
            bool willDestroy = Exists();

            base.Awake();

            if (willDestroy)
                return;

            InitializeAsync().Forget();
        }

        private void Start()
        {
        }
        
        private async UniTask InitializeAsync(IProgress<float> progress = null)
        {
#if DISABLE_SRDEBUGGER && !UNITY_SERVER
            Debug.developerConsoleEnabled = false;
            Debug.unityLogger.logEnabled = false;
            Debug.developerConsoleVisible = false;
#endif
            progress?.Report(0f);
            
            if (Initialized)
            {
                progress?.Report(1f);
                return;
            }
            
            if (Initializing)
            {
                // Wait for the in-flight init to COMPLETE, not merely to be in-progress: the predicate
                // must be Initialized (Initializing is already true here, so it would return immediately
                // and read uninitialized state such as TutorialAPI). Mirrors WaitForInitialize() above.
                await UniTask.WaitUntil(() => Initialized);

                progress?.Report(1f);
                return;
            }
            
            Initializing = true;

            // Save system. Run this synchronously (before the first await) so the player is guaranteed
            // to own a starter vehicle before the garage / main menu start reading the save data.
            SaveManager.Initialize();

            // SR Debugger
#if !DISABLE_SRDEBUGGER
            if (!SRDebug.IsInitialized)
                SRDebug.Init();
#endif

            await UniTask.Yield();
            
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
            
            // Disable unity debug runtime ui
            try
            {
                //DebugManager.instance.enableRuntimeUI = false;
                Debug.developerConsoleVisible = false;
                Debug.developerConsoleEnabled = false;
            }
            catch
            {
                // ignored
            }
            
            // Completed
            Initializing = false;
            Initialized = true;
        }
    }
}