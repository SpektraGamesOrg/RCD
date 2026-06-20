using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UI;
using UIManager;
using UnityEngine;
using Vehicles;

namespace Core
{
    /// <summary>
    /// Post-load step for the MainMenu scene: binds the in-scene <see cref="GarageManager"/> to the persistent
    /// <see cref="MainMenuScreen"/>, waits for the first showroom car to finish loading, then switches to the
    /// menu. The garage lives in the MainMenu scene while the screen is a DontDestroyOnLoad view owned by
    /// <see cref="GameUIManager"/>, so the link can't be set in the inspector - it is wired here at runtime.
    /// </summary>
    public sealed class MainMenuSceneLoader : SceneLoaderBase
    {
        public override SceneType SceneType => SceneType.MainMenu;

        public override async UniTask LoadAsync(IProgress<float> progress, CancellationToken token)
        {
            progress?.Report(0f);

            GarageManager garage = GarageManager.Instance;
            MainMenuScreen menu = GameUIManager.Instance != null
                ? GameUIManager.Instance.GetScreen<MainMenuScreen>()
                : null;

            if (garage == null)
                Debug.LogError("[MainMenuSceneLoader] No GarageManager found in the MainMenu scene.");
            else
                menu?.Bind(garage); // Wire the cross-scene link the inspector can't.

            progress?.Report(0.1f);

            // Hold the loading bar until the garage's first showroom car has finished loading/spawning.
            if (garage != null)
                await garage.WaitUntilReadyAsync().AttachExternalCancellation(token);

            progress?.Report(1f);

            // Let the player see the bar reach 100% (it smooths), then hold a beat before swapping screens.
            await WaitForLoadingBarFilledAsync(token);

            // Always replace the loading screen with a real screen (never an empty layer).
            if (menu != null)
                GameUIManager.Instance.SwitchScreen(menu);
            else
                Debug.LogError("[MainMenuSceneLoader] No MainMenuScreen available to open.");
        }
    }
}
