using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UI;
using UIManager;
using UnityEngine;

namespace Core
{
    /// <summary>
    /// Post-load step for the Game scene. Waits for the selected vehicle to load and spawn, then replaces
    /// the loading screen with the persistent gameplay screen.
    /// </summary>
    public sealed class GameSceneLoader : SceneLoaderBase
    {
        public override SceneType SceneType => SceneType.Game;

        public override async UniTask LoadAsync(IProgress<float> progress, CancellationToken token)
        {
            progress?.Report(0f);

            GameManager gameManager = GameManager.Instance;
            GameplayScreen gameplayScreen = GameUIManager.Instance != null
                ? GameUIManager.Instance.GetScreen<GameplayScreen>()
                : null;

            if (gameManager == null)
            {
                Debug.LogError("[GameSceneLoader] No GameManager found in the Game scene.");
            }
            else
            {
                var spawnProgress = progress == null ? null : new RangedProgress(progress, 0f, 0.9f);
                if (await gameManager.SpawnCurrentVehicleAsync(spawnProgress, token) == null)
                    Debug.LogError("[GameSceneLoader] Gameplay vehicle could not be spawned.");
            }

            progress?.Report(1f);

            // Let the player see the bar reach 100% (it smooths), then hold a beat before swapping screens.
            await WaitForLoadingBarFilledAsync(token);

            if (gameplayScreen != null)
                await GameUIManager.Instance.SwitchScreenAsync(gameplayScreen).AttachExternalCancellation(token);
            else
                Debug.LogError("[GameSceneLoader] No GameplayScreen available to open.");
        }
    }
}
