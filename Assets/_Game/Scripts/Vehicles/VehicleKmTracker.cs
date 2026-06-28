using Core;
using Milestones;
using Save;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Vehicles
{
    /// <summary>
    /// Per-vehicle odometer. Integrates the vehicle's speed every physics step into a runtime
    /// accumulator and pushes the whole kilometres into <see cref="SaveManager.DistanceDrivenKm"/>
    /// only at real persistence boundaries (despawn, app pause, app quit) - never from the physics loop.
    ///
    /// Why it is built this way:
    /// - Velocity-based (∫|v|·dt), never position-delta, so respawns/teleports add no bogus distance.
    /// - <see cref="FixedUpdate"/> only adds a float; it touches neither PlayerPrefs nor the heap, so
    ///   there is zero per-frame I/O and zero GC while driving.
    /// - The saved int (whole km) is written at most once per boundary via
    ///   <see cref="SaveManager.AddDistanceDriven"/>; the sub-km remainder stays in the runtime
    ///   accumulator for the next stretch.
    /// - Only counts while the player actually controls the car, so the parked garage model and any
    ///   control-disabled state (countdown / pause / finish) never inflate the saved distance.
    ///
    /// The body and controller references are serialized and wired at edit time by
    /// <see cref="MainVehicleBehaviour.Validate"/> - there is no runtime component lookup.
    /// </summary>
    [DisallowMultipleComponent]
    public class VehicleKmTracker : VehicleBehaviourBase
    {
        [Tooltip("Body whose speed is integrated. Wired by MainVehicleBehaviour.Validate.")]
        [SerializeField]
        private Rigidbody body = null;

        [Tooltip("Controller used to tell whether the player is actively driving this car. " +
                 "Wired by MainVehicleBehaviour.Validate.")]
        [SerializeField]
        private RCC_CarControllerV4 controller = null;

        // Distance driven but not yet pushed to the saved int (km). Runtime-only; the sub-km remainder
        // is kept here between commits so nothing is lost while driving. Surfaced for debugging via Odin.
        [ShowInInspector, ReadOnly]
        private float _pendingKm;

        // Distance integrated over this instance's lifetime (km). Debug aid only.
        [ShowInInspector, ReadOnly]
        private float _sessionKm;

        private const float MetersToKilometers = 0.001f;

        private void Awake()
        {
            if (!body)
            {
                Debug.LogError($"[VehicleKmTracker] {name} has no Rigidbody assigned; odometer disabled. " +
                               "Re-run MainVehicleBehaviour.Validate to wire it.", this);
                enabled = false;
            }
        }

        private void Start()
        {
            if (!GameManager.Exists())
            {
                Debug.Log($"[VehicleKmTracker] {name} Component disabled because of we are not in game scene", this);
                enabled = false;
            }
        }

        private void FixedUpdate()
        {
            // Only the car the player is actively driving feeds the odometer. This keeps the parked
            // garage model and any control-disabled state from inflating the distance.
            if (controller && (!controller.canControl || controller.externalController))
                return;

            // Pure float accumulation - NO PlayerPrefs, NO allocations in the physics loop.
            // Distance this step = speed (m/s) * step (s); magnitude is the only sqrt.
            float stepKm = body.linearVelocity.magnitude * Time.fixedDeltaTime * MetersToKilometers;
            if (stepKm <= 0f)
                return;

            _sessionKm += stepKm;
            _pendingKm += stepKm;

            // Feed the live (uncommitted) distance to the milestone service so the HUD bar can move smoothly
            // while driving. This is a plain float assignment - no allocations, no PlayerPrefs - and is
            // display-only; milestone rewards still key off the committed whole-km value at commit time.
            DistanceMilestoneManager.ReportSessionDistanceKm(_pendingKm);
        }

        // Stage progress whenever the car is torn down (run end / scene change / pooled away).
        private void OnDisable()
        {
            CommitDistance();
        }

        // Mobile's real "the app may die now" moment: stage and force a disk flush so a
        // backgrounded-then-killed app keeps progress. Save() is ordering-safe vs SaveHelper.
        private void OnApplicationPause(bool paused)
        {
            if (paused && CommitDistance())
                SaveManager.Save();
        }

        private void OnApplicationQuit()
        {
            if (CommitDistance())
                SaveManager.Save();
        }

        /// <summary>
        /// Pushes the accumulated whole kilometres into <see cref="SaveManager.DistanceDrivenKm"/> and
        /// keeps the sub-km remainder for later. A no-op (returns false) until at least 1 km has built
        /// up, so it is cheap to call. Public so gameplay flows (e.g. a run-complete screen) can persist
        /// on demand "when needed". Note: this only stages the value; a disk flush still comes from the
        /// auto-save helper on pause/quit (or from the explicit Save() in the lifecycle hooks above).
        /// </summary>
        public bool CommitDistance()
        {
            if (_pendingKm < 1f)
                return false;

            int wholeKm = (int)_pendingKm;
            _pendingKm -= wholeKm;
            SaveManager.AddDistanceDriven(wholeKm);
            return true;
        }

#if UNITY_EDITOR
        // Runs once when the component is first added, and again on any inspector/load validation.
        private void Reset()
        {
            EditorAutoWire();
        }

        protected override void OnValidate()
        {
            base.OnValidate();
            EditorAutoWire();
        }

        /// <summary>
        /// Editor-only self-wiring: resolves the serialized body/controller from this GameObject's own
        /// components. GetComponent is permitted here because this is edit-time setup code (see
        /// CLAUDE.md), exactly like <see cref="MainVehicleBehaviour.Validate"/> wires its own refs. Self-
        /// wiring lets the prefab heal no matter how the component was added. Returns true if a reference
        /// changed. Also invoked by <see cref="MainVehicleBehaviour.Validate"/> right after it adds the
        /// component, so the body is wired immediately rather than waiting for the next validation tick.
        /// </summary>
        public bool EditorAutoWire()
        {
            if (Application.isPlaying)
                return false;

            bool changed = false;

            Rigidbody foundBody = GetComponent<Rigidbody>();
            if (body != foundBody)
            {
                body = foundBody;
                changed = true;
            }

            RCC_CarControllerV4 foundController = GetComponent<RCC_CarControllerV4>();
            if (controller != foundController)
            {
                controller = foundController;
                changed = true;
            }

            if (changed)
                UnityEditor.EditorUtility.SetDirty(this);

            return changed;
        }
#endif
    }
}