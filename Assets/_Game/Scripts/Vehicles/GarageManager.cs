using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Save;
using SpektraGames.ResourceObject.Runtime;
using SpektraGames.RuntimeUI.Runtime;
using SpektraGames.SpektraUtilities.Runtime;
using UnityEngine;

namespace Vehicles
{
    /// <summary>
    /// Drives the garage showroom: keeps track of which vehicle is being browsed, spawns it as a
    /// static display at <see cref="spawnPoint"/>, and manages a small async load/unload window so
    /// only a handful of prefabs are ever in memory at once.
    ///
    /// The UI (e.g. <c>MainMenuScreen</c>) only calls <see cref="ShowNext"/> / <see cref="ShowPrevious"/>
    /// and listens to <see cref="DisplayedVehicleChanged"/>; all spawning and memory work lives here.
    ///
    /// Memory window: the current vehicle plus <see cref="preloadRadius"/> on each side are kept loaded
    /// (so neighbours are ready instantly); anything outside the window is dropped and reclaimed async.
    /// If the player browses faster than a prefab can load, <see cref="RuntimeUI.ShowLoading"/> covers the
    /// gap and the older, superseded request quietly bails so only the final selection is ever spawned.
    /// </summary>
    public class GarageManager : SingletonComponent<GarageManager>
    {
        [Tooltip("Empty transform where the showroom car is parented. Its pose defines how the car is framed.")]
        [SerializeField] private Transform spawnPoint;

        [Tooltip("How many vehicles to keep loaded on each side of the current one (2 = current ± 2).")]
        [SerializeField, Min(0)] private int preloadRadius = 2;

        [Tooltip("How the spawned showroom car is dropped onto the floor so its 4 tyres rest on the ground.")]
        [SerializeField] private VehicleGroundingSettings groundingSettings = new();

        /// <summary>Raised whenever the browsed vehicle changes (fires immediately on a switch, before the 3D car finishes loading).</summary>
        public event Action<VehicleID> DisplayedVehicleChanged;

        // Completes once the initial showroom car has finished loading/spawning (or the roster is empty / the
        // first display bailed). The MainMenu scene controller awaits this so the loading screen can stay up
        // until the garage is actually ready before the menu is revealed.
        private readonly UniTaskCompletionSource _readySource = new();

        /// <summary>True once the garage has finished its first display.</summary>
        public bool IsReady { get; private set; }

        /// <summary>Awaitable that completes when the first showroom car is ready. Returns immediately if already ready.</summary>
        public UniTask WaitUntilReadyAsync() => _readySource.Task;

        // The browsed index into the container roster.
        private int _index = -1;

        // Bumped on every switch. A spawn request only acts if its captured value still matches, so a
        // burst of fast clicks collapses to a single spawn (the latest one) instead of spawning each car.
        private int _requestId;

        // The currently spawned showroom instance and which vehicle it represents.
        private GameObject _currentInstance;
        private VehicleID _spawnedId = VehicleID.None;

        // Vehicles with a LoadAsync in flight, so we never start a second load for the same one.
        private readonly HashSet<VehicleID> _loading = new();

        // Reused each window update to avoid per-switch allocations.
        private readonly HashSet<int> _keepIndices = new();

        // Coalesces a burst of unloads into a single Resources.UnloadUnusedAssets pass.
        private bool _unloadScheduled;

        private static VehicleContainer Container => VehicleContainer.Instance;
        private IReadOnlyList<VehicleEntry> Entries => Container != null ? Container.Vehicles : null;
        private int Count => Entries?.Count ?? 0;

        /// <summary>The vehicle currently being browsed, or <see cref="VehicleID.None"/> when the roster is empty.</summary>
        public VehicleID CurrentVehicleId =>
            Entries != null && _index >= 0 && _index < Entries.Count ? Entries[_index].ID : VehicleID.None;

        private void Start()
        {
            if (Count == 0)
            {
                Debug.LogError("[GarageManager] No vehicles registered in the VehicleContainer.");
                MarkReady();
                return;
            }

            // Start on the saved selection, then fall back to the first owned vehicle, then roster index 0.
            _index = FindStartIndex();
            RaiseChanged();
            DisplayCurrentAsync().Forget();
        }

        // Returns the roster index to start on: saved selection → first owned vehicle → 0.
        private int FindStartIndex()
        {
            int selected = IndexOf(SaveManager.SelectedVehicle);
            if (selected >= 0)
                return selected;

            IReadOnlyList<VehicleEntry> entries = Entries;
            if (entries != null)
            {
                for (int i = 0; i < entries.Count; i++)
                {
                    if (SaveManager.IsOwned(entries[i].ID))
                        return i;
                }
            }

            return 0;
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();

            if (_currentInstance != null)
                Destroy(_currentInstance);

            // Never leave the loading overlay up if we are torn down mid-load (e.g. scene change).
            RuntimeUI.HideLoading();
        }

        // ---------------------------------------------------------------------
        // Browsing
        // ---------------------------------------------------------------------

        public void ShowNext() => Step(1);
        public void ShowPrevious() => Step(-1);

        private void Step(int direction)
        {
            int count = Count;
            if (count == 0)
                return;

            _index = (_index + direction + count) % count;
            RaiseChanged();
            DisplayCurrentAsync().Forget();
        }

        private void RaiseChanged() => DisplayedVehicleChanged?.Invoke(CurrentVehicleId);

        // Marks the garage ready exactly once so awaiters (the MainMenu scene controller) wake up.
        private void MarkReady()
        {
            if (IsReady)
                return;

            IsReady = true;
            _readySource.TrySetResult();
        }

        // ---------------------------------------------------------------------
        // Display + load/unload
        // ---------------------------------------------------------------------

        private async UniTaskVoid DisplayCurrentAsync()
        {
            int requestId = ++_requestId;
            try
            {
                VehicleID id = CurrentVehicleId;
                if (id == VehicleID.None)
                    return;

                // Keep the memory window in sync first so neighbours start preloading right away.
                UpdateWindow();

                ResourceObject<MainVehicleBehaviour> resource = CurrentResource();
                if (resource == null || !resource.IsValid)
                {
                    Debug.LogError($"[GarageManager] No valid prefab assigned for {id}.");
                    return;
                }

                // If the target is not ready, cover the gap with the loading overlay and wait for it.
                if (!resource.IsLoaded)
                {
                    RuntimeUI.ShowLoading();
                    await LoadAsync(id, resource);

                    // A newer switch came in while we were loading; it owns the overlay and the spawn now.
                    if (requestId != _requestId)
                    {
                        // We loaded a car the player has already browsed past; drop it if it left the window.
                        if (!IsInWindow(id))
                        {
                            resource.Unload();
                            ScheduleUnloadUnused();
                        }
                        return;
                    }
                }

                if (resource.Asset == null)
                {
                    Debug.LogError($"[GarageManager] Failed to load prefab for {id}.");
                    return;
                }

                Spawn(resource.Asset, id);
            }
            finally
            {
                // The latest request always clears the overlay, no matter how it exits (success, invalid
                // prefab, load failure, or a thrown spawn). Superseded requests leave it to the newer one,
                // so the overlay can never be orphaned and never hidden out from under a pending load.
                if (requestId == _requestId)
                {
                    RuntimeUI.HideLoading();

                    // The first settled display means the garage is ready; let the scene controller reveal the menu.
                    MarkReady();
                }
            }
        }

        private void UpdateWindow()
        {
            int count = Count;
            if (count == 0)
                return;

            // Build the set of indices to keep loaded: current ± preloadRadius (wrapped).
            _keepIndices.Clear();
            for (int offset = -preloadRadius; offset <= preloadRadius; offset++)
            {
                int i = ((_index + offset) % count + count) % count;
                _keepIndices.Add(i);
            }

            // Preload anything in the window that isn't loaded yet (in the background).
            foreach (int i in _keepIndices)
            {
                VehicleEntry entry = Entries[i];
                ResourceObject<MainVehicleBehaviour> resource = entry.MainBehaviour;
                if (resource != null && resource.IsValid && !resource.IsLoaded && !_loading.Contains(entry.ID))
                    PreloadAsync(entry.ID, resource).Forget();
            }

            // Drop anything loaded that fell outside the window.
            bool anyDropped = false;
            for (int i = 0; i < count; i++)
            {
                if (_keepIndices.Contains(i))
                    continue;

                ResourceObject<MainVehicleBehaviour> resource = Entries[i].MainBehaviour;
                if (resource != null && resource.IsLoaded)
                {
                    resource.Unload(); // Drops the reference; the memory is reclaimed by the pass below.
                    anyDropped = true;
                }
            }

            if (anyDropped)
                ScheduleUnloadUnused();
        }

        // Background preload that cleans up after itself: if the window has moved on by the time the
        // load finishes (the player browsed past it), the just-loaded prefab is dropped again so memory
        // stays within the window even when the player spams faster than prefabs load.
        private async UniTaskVoid PreloadAsync(VehicleID id, ResourceObject<MainVehicleBehaviour> resource)
        {
            await LoadAsync(id, resource);

            if (!IsInWindow(id))
            {
                resource.Unload();
                ScheduleUnloadUnused();
            }
        }

        private async UniTask LoadAsync(VehicleID id, ResourceObject<MainVehicleBehaviour> resource)
        {
            if (resource.IsLoaded)
                return;

            // Another caller is already loading this one; just wait for it to finish.
            if (_loading.Contains(id))
            {
                await UniTask.WaitUntil(() => resource.IsLoaded || !_loading.Contains(id));
                return;
            }

            _loading.Add(id);
            try
            {
                await resource.LoadAsync();
            }
            finally
            {
                _loading.Remove(id);
            }
        }

        private void ScheduleUnloadUnused()
        {
            if (_unloadScheduled)
                return;

            _unloadScheduled = true;
            UnloadUnusedAsync().Forget();
        }

        private async UniTaskVoid UnloadUnusedAsync()
        {
            // Wait a frame so a burst of rapid switches collapses into a single reclaim pass.
            await UniTask.Yield();
            _unloadScheduled = false;
            await Resources.UnloadUnusedAssets().ToUniTask();
        }

        // ---------------------------------------------------------------------
        // Spawning
        // ---------------------------------------------------------------------

        private void Spawn(MainVehicleBehaviour prefab, VehicleID id)
        {
            if (spawnPoint == null)
            {
                Debug.LogError("[GarageManager] No spawn point assigned.");
                return;
            }

            if (_spawnedId == id && _currentInstance != null)
                return; // Already showing this exact car.

            if (_currentInstance != null)
            {
                // Destroy() is deferred to end of frame, so the old car's colliders would still be in the
                // physics scene when the new car runs its grounding raycast below — and the rays would land on
                // the OUTGOING car's body instead of the floor, spawning the new car high. Deactivating removes
                // its colliders from physics immediately (this frame) so the raycast only sees the floor.
                _currentInstance.SetActive(false);
                Destroy(_currentInstance);
                _currentInstance = null;
            }

            MainVehicleBehaviour instance = Instantiate(prefab, spawnPoint);

            // Track the instance immediately, BEFORE any further setup. If something below throws, the
            // car is still owned by us and will be destroyed on the next swap instead of leaking.
            _currentInstance = instance.gameObject;
            _spawnedId = id;

            Transform t = instance.transform;
            t.localPosition = Vector3.zero;
            t.localRotation = Quaternion.identity;
            t.localScale = Vector3.one;

            // Drop the car onto the floor from its 4 wheels so it never spawns buried or floating, no matter
            // how each prefab's pivot sits relative to its wheels. The car stays a live physics vehicle.
            VehicleGroundAligner.Align(t, instance.VehicleController, groundingSettings);

            // Re-seat the rigidbody on the final, grounded pose so physics agrees with the transform.
            instance.Rigidbody.position = t.position;
            instance.Rigidbody.rotation = t.rotation;
            Physics.SyncTransforms();
        }

        // The roster prefabs are full driving vehicles; freeze them so they sit still in the showroom.
        private static void MakeStaticDisplay(GameObject instance)
        {
            if (instance.TryGetComponent(out Rigidbody body))
            {
                body.isKinematic = true;
                body.useGravity = false;
            }

            // Stop the driving logic so the car is a calm prop. Disabling an NWH controller that has not
            // finished initialising can throw from its OnDisable, so guard it (kinematic already keeps it still).
            MainVehicleBehaviour behaviour = instance.GetComponent<MainVehicleBehaviour>();
            if (behaviour != null && behaviour.VehicleController != null)
            {
                try
                {
                    behaviour.VehicleController.enabled = false;
                }
                catch (Exception e)
                {
                    Debug.LogError($"[GarageManager] Could not disable VehicleController on {instance.name}: {e.Message}");
                }
            }
        }

        // ---------------------------------------------------------------------
        // Helpers
        // ---------------------------------------------------------------------

        private ResourceObject<MainVehicleBehaviour> CurrentResource()
        {
            return Entries != null && _index >= 0 && _index < Entries.Count ? Entries[_index].MainBehaviour : null;
        }

        private int IndexOf(VehicleID id)
        {
            IReadOnlyList<VehicleEntry> entries = Entries;
            if (entries == null)
                return -1;

            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].ID == id)
                    return i;
            }

            return -1;
        }

        // True when the vehicle sits within the current keep-loaded window (current ± preloadRadius).
        private bool IsInWindow(VehicleID id)
        {
            int count = Count;
            if (count == 0)
                return false;

            for (int offset = -preloadRadius; offset <= preloadRadius; offset++)
            {
                int i = ((_index + offset) % count + count) % count;
                if (Entries[i].ID == id)
                    return true;
            }

            return false;
        }
    }
}