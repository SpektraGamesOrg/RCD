using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Gley.TrafficSystem;
using Save;
using Sirenix.OdinInspector;
using SpektraGames.ResourceObject.Runtime;
using SpektraGames.SpektraUtilities.Runtime;
using UI;
using UIManager;
using Unity.Cinemachine;
using UnityEngine;
using Vehicles;

namespace Core
{
    /// <summary>
    /// Scene-local owner of gameplay references and the active player vehicle. This singleton belongs to
    /// the Game scene and deliberately does not survive scene changes.
    /// </summary>
    public sealed class GameManager : SingletonComponent<GameManager>
    {
        [Header("Vehicle")]
        [Tooltip("World-space pose used when the selected vehicle is spawned for gameplay.")]
        [SerializeField] private Transform vehicleSpawnPoint;

        [SerializeField] private RCC_Camera rccCamera;
        [SerializeField] private TrafficComponent gleyTrafficComponent;

        [Tooltip("The active gameplay vehicle instance. Assigned after the async spawn completes.")]
        [ShowInInspector, ReadOnly] private MainVehicleBehaviour _spawnedVehicle = null;

        public Transform VehicleSpawnPoint => vehicleSpawnPoint;
        public RCC_Camera RccCamera => rccCamera;
        public MainVehicleBehaviour SpawnedVehicle => _spawnedVehicle;

        /// <summary>
        /// Loads the saved selected vehicle, spawns it at <see cref="vehicleSpawnPoint"/>, and binds the
        /// gameplay camera. Returns the existing instance when called more than once.
        /// </summary>
        public async UniTask<MainVehicleBehaviour> SpawnCurrentVehicleAsync(
            IProgress<float> progress = null,
            CancellationToken token = default)
        {
            progress?.Report(0f);

            if (_spawnedVehicle != null)
            {
                BindCamera(_spawnedVehicle.VehicleController);
                progress?.Report(1f);
                return _spawnedVehicle;
            }

            if (vehicleSpawnPoint == null)
            {
                Debug.LogError("[GameManager] No vehicle spawn point assigned.");
                return null;
            }

            VehicleContainer container = VehicleContainer.Instance;
            if (container == null || container.Vehicles.Count == 0)
            {
                Debug.LogError("[GameManager] No vehicles registered in the VehicleContainer.");
                return null;
            }

            VehicleID selectedVehicle = SaveManager.SelectedVehicle;
            if (selectedVehicle == VehicleID.None || !container.Contains(selectedVehicle))
            {
                VehicleID fallbackVehicle = container.Vehicles[0].ID;
                Debug.LogError(
                    $"[GameManager] Selected vehicle '{selectedVehicle}' is unavailable. Falling back to '{fallbackVehicle}'.");
                selectedVehicle = fallbackVehicle;
            }

            ResourceObject<MainVehicleBehaviour> resource = container.GetPrefab(selectedVehicle);
            if (resource == null || !resource.IsValid)
            {
                Debug.LogError($"[GameManager] No valid prefab assigned for {selectedVehicle}.");
                return null;
            }

            var loadProgress = progress == null ? null : new RangedProgress(progress, 0f, 0.9f);
            MainVehicleBehaviour prefab = await resource
                .LoadAsync(loadProgress)
                .AttachExternalCancellation(token);

            progress?.Report(0.9f);

            if (prefab == null)
            {
                Debug.LogError($"[GameManager] Failed to load the prefab for {selectedVehicle}.");
                return null;
            }

            token.ThrowIfCancellationRequested();

            _spawnedVehicle = Instantiate(
                prefab,
                vehicleSpawnPoint.position,
                vehicleSpawnPoint.rotation);
            RCC_SceneManager.Instance.RegisterPlayer(_spawnedVehicle.VehicleController);
            _spawnedVehicle.VehicleController.SetCanControl(true);
            _spawnedVehicle.VehicleController.SetExternalControl(false);
            _spawnedVehicle.VehicleController.StartEngine(true);

            BindCamera(_spawnedVehicle.VehicleController);

            // Traffic
            // Gley uses this transform's position as the line-of-sight "eye" that decides where traffic may spawn.
            // Pass the elevated chase camera (what the player actually sees), not the ground-level car origin: the
            // car origin sits inside the vehicle's own colliders, which would make every nearby spawn point read as
            // "occluded" and defeat the minDistanceToAdd spawn guard.
            gleyTrafficComponent.player = _spawnedVehicle.transform; // cosmetic only; the real reference is the Initialize argument below
            Transform trafficLosEye = rccCamera && rccCamera.actualCamera
                ? rccCamera.actualCamera.transform
                : _spawnedVehicle.transform; // fallback if the camera has not resolved its Camera yet
            API.Initialize(trafficLosEye, gleyTrafficComponent.nrOfVehicles, gleyTrafficComponent.vehiclePool, gleyTrafficComponent.Options);

            progress?.Report(1f);
            return _spawnedVehicle;
        }

        /// <summary>
        /// Enables or disables Gley traffic for the duration of an in-game event (see Events.EventManager).
        /// Disabling stops new spawns AND removes every currently spawned traffic vehicle so the map is clear
        /// (the event design wants no traffic during Jump Challenge / Time Trial); enabling restores the normal
        /// density and lets traffic repopulate.
        /// </summary>
        public void SetTrafficActive(bool active)
        {
            if (!gleyTrafficComponent)
                return;

            if (active)
            {
                API.SetTrafficDensity(gleyTrafficComponent.nrOfVehicles);
                return;
            }

            API.SetTrafficDensity(0);

            VehicleComponent[] vehicles = API.GetAllVehicles();
            if (vehicles == null)
                return;

            for (int i = 0; i < vehicles.Length; i++)
            {
                VehicleComponent vc = vehicles[i];
                if (vc)
                    API.RemoveVehicle(vc.gameObject);
            }
        }

        protected override void OnDestroy()
        {
            // The milestone HUD overlay and its completion pop-ups are DontDestroyOnLoad views shown for
            // gameplay (see GameSceneLoader). Close them as the Game scene tears down so none of them lingers
            // over the loading screen or main menu. Close the pop-ups first (they would otherwise try to
            // restore the HUD), then hide the HUD bar itself.
            if (GameUIManager.Instance)
            {
                GameUIManager.Instance.GetOverlayUI<MilestoneCompletedOverlay>()?.ForceClose();
                GameUIManager.Instance.GetOverlayUI<ClaimGoldMultiplierWithAdsOverlay>()?.Hide(immediate: true);
                GameUIManager.Instance.GetOverlayUI<DriveDistanceMilestoneOverlay>()?.Hide(immediate: true);
            }

            base.OnDestroy();
        }

        private void BindCamera(RCC_CarControllerV4 target)
        {
            // rccCamera.SetTarget(target); // Not needed to cal lthis because of RCC Scene Manager
        }
    }
}