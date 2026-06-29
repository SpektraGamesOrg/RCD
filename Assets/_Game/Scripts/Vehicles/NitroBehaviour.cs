using Core;
using Save;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Vehicles
{
    /// <summary>
    /// Per-vehicle nitro boost (GDD 5.1). A successful <see cref="TryActivate"/> consumes one saved nitro
    /// charge (<see cref="SaveManager.NitroCount"/>) and opens a <see cref="nitroDuration"/>-second boost
    /// window. While the window is open, the vehicle's engine torque is multiplied through RCC's own boost
    /// channel (<c>RCC_CarControllerV4.boostInput</c>; torque scales by <c>1 + boostInput</c>).
    ///
    /// Why it drives RCC this way:
    /// - RCC reads its NOS button in <c>Update()</c> via <c>Inputs()</c>: it sets <c>boostInput</c> from the
    ///   input manager and then zeroes it unless NOS is on, <c>NoS &gt;= 5</c> and throttle &gt;= 0.75. The
    ///   boost is consumed later in <c>FixedUpdate.Wheels()</c> (torque scales by <c>1 + boostInput</c>).
    /// - We therefore write the boost in OUR <c>Update()</c> with a high <see cref="DefaultExecutionOrder"/>
    ///   so it runs AFTER RCC's <c>Update()</c>/<c>Inputs()</c>. That overwrites the gated value, so the
    ///   boost lands in <c>Wheels()</c> for the full window regardless of throttle. We keep <c>useNOS</c> on
    ///   and <c>NoS</c> topped so RCC's own consumption never interferes. We do NOT edit third-party RCC.
    ///
    /// The controller reference is serialized and wired at edit time by
    /// <see cref="MainVehicleBehaviour.Validate"/> - there is no runtime component lookup.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(10000)] // After RCC_CarControllerV4 so our boostInput write survives Inputs().
    public class NitroBehaviour : VehicleBehaviourBase
    {
        [Tooltip("Controller whose boost channel is driven while nitro is active. " +
                 "Wired by MainVehicleBehaviour.Validate.")]
        [SerializeField]
        private RCC_CarControllerV4 controller = null;

        [Tooltip("How long a single nitro charge boosts the car (seconds). GDD 5.1 default is 5.")]
        [SerializeField, Min(0.1f)]
        private float nitroDuration = 5f;

        [Tooltip("RCC boostInput value applied while active (0 = none, 1 = double engine torque).")]
        [SerializeField, Range(0f, 1f)]
        private float boostAmount = 1f;

        // Seconds left in the current boost window. Runtime-only; surfaced for debugging via Odin.
        [ShowInInspector, ReadOnly]
        private float _timeRemaining;

        /// <summary>True while a boost window is open.</summary>
        public bool IsActive => _timeRemaining > 0f;

        /// <summary>Remaining boost time normalized to 0..1 (1 = just activated). For UI bars.</summary>
        public float NormalizedTimeRemaining =>
            nitroDuration > 0f ? Mathf.Clamp01(_timeRemaining / nitroDuration) : 0f;

        /// <summary>Whole seconds left in the current boost window (for the on-button countdown label).</summary>
        public int SecondsRemaining => Mathf.CeilToInt(Mathf.Max(0f, _timeRemaining));

        private void Awake()
        {
            if (!controller)
            {
                Debug.LogError($"[NitroBehaviour] {name} has no RCC controller assigned; nitro disabled. " +
                               "Re-run MainVehicleBehaviour.Validate to wire it.", this);
                enabled = false;
            }
        }

        private void Start()
        {
            if (!GameManager.Exists())
            {
                Debug.Log($"[NitroBehaviour] {name} disabled because we are not in the game scene.", this);
                enabled = false;
            }
        }

        /// <summary>
        /// Spends one saved nitro charge and opens a fresh boost window. Returns false (and changes nothing)
        /// when a boost is already running or no charge is available - the caller (HUD) handles the
        /// out-of-nitro / rewarded-ad path. Flushes the save so a charge is never lost on a crash.
        /// </summary>
        public bool TryActivate()
        {
            if (IsActive)
                return false;

            if (SaveManager.NitroCount <= 0)
                return false;

            SaveManager.AddNitro(-1);
            SaveManager.Save();

            _timeRemaining = nitroDuration;
            Debug.Log($"[NitroBehaviour] Nitro used on '{name}' for {nitroDuration:0.#}s. Remaining charges: {SaveManager.NitroCount}.");
            return true;
        }

        // Drives the boost while the window is open. Runs in Update() AFTER RCC's Update()/Inputs() (high
        // DefaultExecutionOrder), so writing boostInput here overwrites RCC's gated value and lands in the
        // next FixedUpdate.Wheels(). Pure field writes - no allocations, so there is zero GC while boosting.
        private void Update()
        {
            if (!IsActive)
                return;

            _timeRemaining -= Time.deltaTime;

            // Keep RCC's NOS state healthy and feed the boost. NoS is topped every frame so the built-in
            // nitrous consumption never starves the window; useNOS keeps the channel live.
            controller.useNOS = true;
            controller.NoS = 100f;
            controller.boostInput = boostAmount;

            // Closing the window: hand the boost channel back to RCC so torque returns to normal.
            if (_timeRemaining <= 0f)
            {
                _timeRemaining = 0f;
                controller.boostInput = 0f;
                Debug.Log($"[NitroBehaviour] Nitro boost ended on '{name}'.");
            }
        }

        // If the car is torn down / pooled away mid-boost, clear the window and the boost channel so a
        // reused controller does not start already boosting.
        private void OnDisable()
        {
            _timeRemaining = 0f;
            if (controller)
                controller.boostInput = 0f;
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
        /// Editor-only self-wiring: resolves the serialized controller from this GameObject's own
        /// components. GetComponent is permitted here because this is edit-time setup code (see CLAUDE.md),
        /// exactly like <see cref="VehicleKmTracker.EditorAutoWire"/>. Also invoked by
        /// <see cref="MainVehicleBehaviour.Validate"/> right after it adds the component. Returns true if the
        /// reference changed.
        /// </summary>
        public bool EditorAutoWire()
        {
            if (Application.isPlaying)
                return false;

            RCC_CarControllerV4 foundController = GetComponent<RCC_CarControllerV4>();
            if (controller == foundController)
                return false;

            controller = foundController;
            UnityEditor.EditorUtility.SetDirty(this);
            return true;
        }
#endif
    }
}
