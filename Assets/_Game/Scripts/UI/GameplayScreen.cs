using Core;
using Save;
using TMPro;
using UIManager;
using UnityEngine;
using UnityEngine.UI;
using Vehicles;

namespace UI
{
    /// <summary>
    /// In-game HUD. Shows the live coin total and hosts the gameplay action buttons (drift toggle,
    /// recovery, fix, pause). The action handlers drive the active vehicle resolved from
    /// <see cref="GameManager.SpawnedVehicle"/>.
    /// </summary>
    public class GameplayScreen : ScreenBase
    {
        /// <summary>The top-bar gold counter the collect VFX particles fly toward. Wired in the Inspector.</summary>
        public RectTransform GoldWidget => goldWidget;

        [SerializeField] private Button driftButton;
        [SerializeField] private Button recoveryButton;
        [SerializeField] private Button fixButton;
        [SerializeField] private Button pauseButton;

        [SerializeField]
        [Tooltip("The top-bar gold counter (Widget_Gold). Collect VFX particles fly toward this on screen " +
                 "when gold is picked up; ParticlesToCoin resolves it from here at runtime.")]
        private RectTransform goldWidget;

        [SerializeField] private Sprite driftOnSprite;
        [SerializeField] private Sprite driftOffSprite;

        [SerializeField]
        [Tooltip("RCC behavior preset applied while drift mode is ON. Must match a behaviorName under " +
                 "RCC Settings > Behavior Types (e.g. \"Drift\").")]
        private string driftBehaviorName = "Drift";

        [SerializeField]
        [Tooltip("RCC behavior preset applied while drift mode is OFF - the default. Must match a " +
                 "behaviorName under RCC Settings > Behavior Types (e.g. \"Balanced\").")]
        private string normalBehaviorName = "Balanced";

        [SerializeField]
        [Tooltip("Upward offset (metres) applied on recovery so the uprighted car clears the ground " +
                 "before it settles.")]
        private float recoveryLiftHeight = 1.5f;

        // Drift is a per-session HUD toggle that starts OFF (normal behavior). It maps directly onto an
        // RCC behavior preset, so there is no per-wheel state to track here.
        private bool _driftEnabled;

        protected override void Awake()
        {
            base.Awake();

            if (driftButton) driftButton.onClick.AddListener(OnDriftClicked);
            if (recoveryButton) recoveryButton.onClick.AddListener(OnRecoveryClicked);
            if (fixButton) fixButton.onClick.AddListener(OnFixClicked);
            if (pauseButton) pauseButton.onClick.AddListener(OnPauseClicked);
        }

        private void OnDestroy()
        {
            if (driftButton) driftButton.onClick.RemoveListener(OnDriftClicked);
            if (recoveryButton) recoveryButton.onClick.RemoveListener(OnRecoveryClicked);
            if (fixButton) fixButton.onClick.RemoveListener(OnFixClicked);
            if (pauseButton) pauseButton.onClick.RemoveListener(OnPauseClicked);
        }

        protected override void OnBeforeShowing(bool immediate, object uiData = null)
        {
            base.OnBeforeShowing(immediate, uiData);

            // Drift starts OFF every time the HUD opens: force the normal (Balanced) behavior and the OFF
            // icon. This also clears any Drift preset left selected from an earlier run in the same session.
            ResetDriftState();
        }

        protected override void OnHidden(bool immediate = false)
        {
            base.OnHidden(immediate);
        }

        // ---------------------------------------------------------------------
        // Button handlers
        // ---------------------------------------------------------------------

        private void OnDriftClicked() => SetDrift(!_driftEnabled);

        private void OnRecoveryClicked()
        {
            MainVehicleBehaviour vehicle = ActiveVehicle;
            if (!vehicle)
            {
                Debug.LogError("[Gameplay] Recovery requested but no active vehicle is spawned.");
                return;
            }

            Transform vehicleTransform = vehicle.transform;

            // Re-orient upright while preserving the current heading: drop pitch and roll, keep yaw.
            float yaw = vehicleTransform.eulerAngles.y;
            vehicleTransform.rotation = Quaternion.Euler(0f, yaw, 0f);

            // Lift slightly so the uprighted body clears the ground instead of settling inside it.
            vehicleTransform.position += Vector3.up * recoveryLiftHeight;

            // Kill momentum so the car drops and settles rather than carrying its spin straight back over.
            Rigidbody body = vehicle.Rigidbody;
            if (body)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }
        }

        private void OnFixClicked()
        {
            // TODO: Trigger the repair/fix-car flow on the active vehicle.
            Debug.LogError("[Gameplay] Fix car requested.");
        }

        private void OnPauseClicked()
        {
            Debug.LogError("OnPauseClicked");
        }

        // ---------------------------------------------------------------------
        // Drift
        // ---------------------------------------------------------------------

        // Drift is just an RCC behavior switch: ON -> the "Drift" preset, OFF -> the "Balanced" preset.
        // RCC.SetBehavior raises OnBehaviorChanged, so every spawned vehicle re-applies the FULL preset the
        // same way RCC does internally - driving assists (ABS/ESP/TCS), steering helper/limits/sensitivity,
        // counter-steering, anti-roll bars, angular drag, gear-shift delay AND the wheel friction curves -
        // not just a single grip value. In Play mode RCC_Settings.Instance is a runtime clone, so this only
        // affects in-memory state for the session and never edits the RCC_Settings asset on disk.
        private void SetDrift(bool enabled)
        {
            _driftEnabled = enabled;
            UpdateDriftButtonSprite();
            ApplyBehavior(enabled ? driftBehaviorName : normalBehaviorName);
        }

        private void ResetDriftState()
        {
            _driftEnabled = false;
            UpdateDriftButtonSprite();
            ApplyBehavior(normalBehaviorName);
        }

        private static void ApplyBehavior(string behaviorName)
        {
            int index = FindBehaviorIndex(behaviorName);
            if (index < 0)
            {
                Debug.LogError($"[Gameplay] RCC behavior '{behaviorName}' was not found. " +
                               "Check the names under RCC Settings > Behavior Types.");
                return;
            }

            // No active vehicle yet is fine: this sets the selected behavior on RCC_Settings, and a car
            // that spawns afterwards picks it up through its own CheckBehavior() on start.
            RCC.SetBehavior(index);
        }

        // Resolves a behavior preset index by its configured name (case-insensitive), or -1 if the settings
        // / behavior list is unavailable or the name does not match any preset.
        private static int FindBehaviorIndex(string behaviorName)
        {
            RCC_Settings settings = RCC_Settings.Instance;
            if (!settings || settings.behaviorTypes == null)
                return -1;

            for (int i = 0; i < settings.behaviorTypes.Length; i++)
            {
                RCC_Settings.BehaviorType behavior = settings.behaviorTypes[i];
                if (behavior != null &&
                    string.Equals(behavior.behaviorName, behaviorName, System.StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }

            return -1;
        }

        private void UpdateDriftButtonSprite()
        {
            if (!driftButton)
                return;

            Image targetImage = driftButton.image;
            if (!targetImage)
                return;

            Sprite sprite = _driftEnabled ? driftOnSprite : driftOffSprite;
            if (sprite)
                targetImage.sprite = sprite;
        }

        // ---------------------------------------------------------------------
        // Helpers
        // ---------------------------------------------------------------------

        // The active gameplay vehicle, or null if none is spawned yet. GameManager is the scene-local
        // owner of the spawned car (see GameManager.SpawnCurrentVehicleAsync). Resolved on demand on a
        // button tap - never per-frame - so there is no runtime lookup cost.
        private static MainVehicleBehaviour ActiveVehicle =>
            GameManager.Exists() ? GameManager.Instance.SpawnedVehicle : null;
    }
}