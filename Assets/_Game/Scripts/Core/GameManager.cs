using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Save;
using Sirenix.OdinInspector;
using SpektraGames.ResourceObject.Runtime;
using SpektraGames.SpektraUtilities.Runtime;
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

        [Tooltip("Gameplay virtual camera that follows and looks at the spawned vehicle.")]
        [SerializeField] private CinemachineVirtualCamera vehicleCameraCinemachine;

        [Tooltip("The active gameplay vehicle instance. Assigned after the async spawn completes.")]
        [ShowInInspector, ReadOnly] private MainVehicleBehaviour _spawnedVehicle = null;

        public Transform VehicleSpawnPoint => vehicleSpawnPoint;
        public CinemachineVirtualCamera VehicleCameraCinemachine => vehicleCameraCinemachine;
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
                BindCamera(_spawnedVehicle.transform);
                progress?.Report(1f);
                return _spawnedVehicle;
            }

            if (vehicleSpawnPoint == null)
            {
                Debug.LogError("[GameManager] No vehicle spawn point assigned.");
                return null;
            }

            if (vehicleCameraCinemachine == null)
            {
                Debug.LogError("[GameManager] No Cinemachine vehicle camera assigned.");
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

            BindCamera(_spawnedVehicle.transform);
            progress?.Report(1f);
            return _spawnedVehicle;
        }

        protected override void OnDestroy()
        {
            if (vehicleCameraCinemachine != null && _spawnedVehicle != null)
            {
                Transform vehicleTransform = _spawnedVehicle.transform;
                if (vehicleCameraCinemachine.Follow == vehicleTransform)
                    vehicleCameraCinemachine.Follow = null;
                if (vehicleCameraCinemachine.LookAt == vehicleTransform)
                    vehicleCameraCinemachine.LookAt = null;
            }

            base.OnDestroy();
        }

        private void BindCamera(Transform target)
        {
            vehicleCameraCinemachine.Follow = target;
            vehicleCameraCinemachine.LookAt = target;
        }
    }
}