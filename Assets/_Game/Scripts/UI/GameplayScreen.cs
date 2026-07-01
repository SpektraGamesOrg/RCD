using _Game.Scripts.Utils.VContainer;
using Ads;
using Core;
using Cysharp.Threading.Tasks;
using Save;
using SpektraGames.RuntimeUI.Runtime;
using TMPro;
using UIManager;
using UnityEngine;
using UnityEngine.UI;
using Vehicles;
using Vehicles.CarDeformationSystem;

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
        [SerializeField] private Button nitroButton;

        [SerializeField]
        [Tooltip("Shows the remaining free nitro count. Always visible (shows \"0\" when out of charges).")]
        private TMP_Text nitroCountText;

        [SerializeField]
        [Tooltip("The NOS_icon image whose tint reflects nitro state: white when usable / boosting, and the " +
                 "inactive (limit-reached) color when the count is 0. Stays its default color during a boost.")]
        private Image nitroIconImage;

        [SerializeField]
        [Tooltip("NOS_icon tint when out of charges and not boosting (limit reached / inactive).")]
        private Color nitroInactiveColor = new Color(0x7B / 255f, 0x7B / 255f, 0x7B / 255f, 1f); // #7B7B7B

        [SerializeField]
        [Tooltip("Filled image (NosActiveState) shown only while boosting; drains 1 -> 0 over the boost " +
                 "duration to show time remaining. Disabled when idle.")]
        private Image nitroActiveFill;

        [SerializeField]
        [Tooltip("Text_NosDuration: whole-second countdown shown while boosting (5 -> 4 -> 3 -> 2 -> 1, " +
                 "never 0). Lives under NosActiveState so it is only visible during the boost.")]
        private TMP_Text nitroDurationText;

        [SerializeField]
        [Tooltip("Child rewarded-ad icon on the nitro button. Enabled when the free nitro count hits 0; " +
                 "tapping the button in that state shows a rewarded ad that grants more nitros.")]
        private GameObject nitroRewardedIconObject;

        [SerializeField]
        [Tooltip("How many nitros a successfully watched rewarded ad grants. GDD 5.1 default is 2.")]
        private int nitroAdRewardAmount = 2;

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

        // Tracks whether the nitro button is currently showing the active-boost countdown, so the idle
        // count/icon visual is restored exactly once when the boost window ends.
        private bool _nitroBoostActive;

        protected override void Awake()
        {
            base.Awake();

            if (driftButton) driftButton.onClick.AddListener(OnDriftClicked);
            if (recoveryButton) recoveryButton.onClick.AddListener(OnRecoveryClicked);
            if (fixButton) fixButton.onClick.AddListener(OnFixClicked);
            if (pauseButton) pauseButton.onClick.AddListener(OnPauseClicked);
            if (nitroButton) nitroButton.onClick.AddListener(OnNitroClicked);

            SaveManager.OnNitroChanged += OnNitroCountChanged;
        }

        private void OnDestroy()
        {
            if (driftButton) driftButton.onClick.RemoveListener(OnDriftClicked);
            if (recoveryButton) recoveryButton.onClick.RemoveListener(OnRecoveryClicked);
            if (fixButton) fixButton.onClick.RemoveListener(OnFixClicked);
            if (pauseButton) pauseButton.onClick.RemoveListener(OnPauseClicked);
            if (nitroButton) nitroButton.onClick.RemoveListener(OnNitroClicked);

            SaveManager.OnNitroChanged -= OnNitroCountChanged;
        }

        protected override void OnBeforeShowing(bool immediate, object uiData = null)
        {
            base.OnBeforeShowing(immediate, uiData);

            // Drift starts OFF every time the HUD opens: force the normal (Balanced) behavior and the OFF
            // icon. This also clears any Drift preset left selected from an earlier run in the same session.
            ResetDriftState();

            // Show the current free-nitro count / rewarded-ad affordance from the moment the HUD appears.
            RefreshNitroVisual();
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
            ActiveVehicle.GetComponent<VehicleDamageReceiver>().ResetAllDamage();
        }

        private void OnPauseClicked()
        {
            Debug.LogError("OnPauseClicked");
        }

        // When the player has free nitros, spend one and boost. When the count is 0, the button instead
        // offers a rewarded ad (its child icon is showing) that grants more nitros on success. Taps are
        // ignored while a boost is already running.
        private void OnNitroClicked()
        {
            NitroBehaviour nitro = ActiveNitro;

            // No re-trigger while a boost window is open.
            if (nitro && nitro.IsActive)
                return;

            if (SaveManager.NitroCount > 0)
            {
                if (!nitro)
                {
                    Debug.LogError("[Gameplay] Nitro requested but no active vehicle / NitroBehaviour.");
                    return;
                }

                nitro.TryActivate();
                return;
            }

            ShowNitroRewardedAd().Forget();
        }

        // ---------------------------------------------------------------------
        // Nitro
        // ---------------------------------------------------------------------

        // Plays a rewarded ad through the shared MaxAdService; on a completed watch it grants nitros.
        // Mirrors the project's ClaimGoldMultiplierWithAdsOverlay flow (placement string + toast on failure).
        private async UniTaskVoid ShowNitroRewardedAd()
        {
            // Block re-taps while the ad is in flight.
            if (nitroButton) nitroButton.interactable = false;

            bool isSuccess = await ServiceLocator.GetService<MaxAdService>().ShowRewardedAdAsync("nitro_refill");

            if (nitroButton) nitroButton.interactable = true;

            if (!isSuccess)
            {
                RuntimeUI.ShowToast("Rewarded ad was not completed");
                return;
            }

            SaveManager.AddNitro(nitroAdRewardAmount);
            SaveManager.Save();
            // RefreshNitroVisual runs via OnNitroChanged; the icon/count update as the count goes positive.
        }

        private void OnNitroCountChanged(int newCount) => RefreshNitroVisual();

        // Idle visual: count is always shown (including "0"), the NOS icon is white when usable or the
        // inactive (limit) color at 0, the active-boost fill is hidden, and the rewarded-ad icon shows at 0.
        // The active-boost fill is driven in Update while a boost window is open.
        private void RefreshNitroVisual()
        {
            _nitroBoostActive = false;

            // Re-enable the button once no boost is running (it is disabled during the active window, and
            // briefly while a rewarded ad is in flight).
            if (nitroButton && !nitroButton.interactable)
                nitroButton.interactable = true;

            int count = SaveManager.NitroCount;
            bool outOfNitro = count <= 0;

            // Rewarded-ad icon shows only when out of charges.
            if (nitroRewardedIconObject && nitroRewardedIconObject.activeSelf != outOfNitro)
                nitroRewardedIconObject.SetActive(outOfNitro);

            // Count is always written (never disabled) - shows "0" when out of charges.
            if (nitroCountText)
                nitroCountText.text = count.ToString();

            // Icon stays its default color while usable/boosting; only the out-of-charges (limit) state dims
            // it. The active boost is conveyed by the drain fill, not by recolouring the icon.
            if (nitroIconImage)
                nitroIconImage.color = outOfNitro ? nitroInactiveColor : Color.white;

            // The active-boost drain fill is only visible while boosting.
            if (nitroActiveFill && nitroActiveFill.gameObject.activeSelf)
            {
                nitroActiveFill.fillAmount = 1f;
                nitroActiveFill.gameObject.SetActive(false);
            }
        }

        // Drives the active-boost visual: shows the NosActiveState fill and drains it 1 -> 0 over the boost,
        // locks the button, and hides the rewarded icon. The NOS icon keeps its default color (the fill is
        // the boost indicator). Touches UI only while the HUD is shown, so there is no per-frame work once
        // the boost ends.
        private void Update()
        {
            if (!ShowingOrShown)
                return;

            NitroBehaviour nitro = ActiveNitro;
            bool active = nitro && nitro.IsActive;

            if (!active)
            {
                // Restore the idle count/icon/fill/interactable the first frame after a boost ends.
                if (_nitroBoostActive)
                    RefreshNitroVisual();
                return;
            }

            // Entering the boost: block re-taps, show the drain fill, and hide the rewarded icon.
            if (!_nitroBoostActive)
            {
                _nitroBoostActive = true;
                if (nitroButton) nitroButton.interactable = false;
                if (nitroActiveFill && !nitroActiveFill.gameObject.activeSelf)
                    nitroActiveFill.gameObject.SetActive(true);
                if (nitroRewardedIconObject && nitroRewardedIconObject.activeSelf)
                    nitroRewardedIconObject.SetActive(false);
            }

            // Drain the fill from 1 (just activated) to 0 (window ends) over the boost duration.
            if (nitroActiveFill)
                nitroActiveFill.fillAmount = nitro.NormalizedTimeRemaining;

            // Whole-second countdown: 5 -> 4 -> 3 -> 2 -> 1 (never 0 while active).
            if (nitroDurationText)
                nitroDurationText.text = nitro.SecondsRemaining.ToString();
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

        // The active vehicle's nitro component, or null if no car is spawned. Resolved through the spawned
        // vehicle's serialized reference - no runtime scene lookup.
        private static NitroBehaviour ActiveNitro
        {
            get
            {
                MainVehicleBehaviour vehicle = ActiveVehicle;
                return vehicle ? vehicle.NitroBehaviour : null;
            }
        }
    }
}