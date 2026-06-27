//----------------------------------------------
//            Realistic Car Controller
//
// Copyright © 2014 - 2025 BoneCracker Games
// https://www.bonecrackergames.com
// Ekrem Bugra Ozdoganlar
//
//----------------------------------------------


using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

/// <summary>
/// The primary input management system for RCC, handling vehicle, camera, and optional inputs. 
/// This class translates raw input actions (from Unity's InputSystem) into RCC-friendly variables 
/// and events that other systems (e.g., RCC_CarControllerV4, RCC_Camera, RCC_MobileButtons) can access.
/// </summary>
public class RCC_InputManager : RCC_Singleton<RCC_InputManager> {

    /// <summary>
    /// An instance of RCC_Inputs, which aggregates all relevant input values (throttle, brake, etc.).
    /// </summary>
    [Tooltip("An instance of RCC_Inputs, which aggregates all relevant input values (throttle, brake, etc.).")]
    public RCC_Inputs inputs = new RCC_Inputs();

    /// <summary>
    /// The InputActions asset for RCC. It's assigned and enabled once in Awake().
    /// Note: Not static to prevent issues with multiple scenes and proper cleanup.
    /// </summary>
    private RCC_InputActions inputActions;

    /// <summary>
    /// Indicates whether gyroscopic (mobile) steering is in use.
    /// </summary>
    [Tooltip("Indicates whether gyroscopic (mobile) steering is in use.")]
    public bool gyroUsed = false;

    /// <summary>
    /// Flag to track if events have been subscribed to prevent double subscription.
    /// </summary>
    private bool eventsSubscribed = false;

    /// <summary>
    /// Lock object for thread-safe operations.
    /// </summary>
    private readonly object inputLock = new object();


    private void Awake() {

        // Check if another instance exists and handle it properly.
        if (Instance != this) {

            Destroy(gameObject);
            return;

        }

        // Hide this GameObject from the scene hierarchy for cleanliness.
        gameObject.hideFlags = HideFlags.HideInHierarchy;

        // Instantiate the inputs container once.
        inputs = new RCC_Inputs();

    }

    private void OnEnable() {

        // Initialize InputActions once during Awake.
        InitializeInputActions();

    }

    /// <summary>
    /// Properly cleanup when the InputManager is destroyed.
    /// </summary>
    private void OnDisable() {

        // Only clean up if this is the actual instance being destroyed.
        if (Instance != this)
            return;

        // Unsubscribe from all events.
        UnsubscribeFromEvents();

        if (inputActions != null) {

            inputActions.Disable();
            inputActions.Vehicle.Disable();
            inputActions.Camera.Disable();
            inputActions.Optional.Disable();

        }

    }

    protected override void OnDestroy() {

        base.OnDestroy();

        // Dispose of the input action asset to clean up unmanaged resources
        if (inputActions != null) {

            inputActions.Dispose();
            inputActions = null;

        }

    }

    private void Update() {

        // Collect current frame inputs from the InputSystem (or mobile, if enabled).
        GetInputs();

    }

    /// <summary>
    /// Initializes the InputActions and subscribes to all events once.
    /// Thread-safe implementation to prevent race conditions.
    /// </summary>
    private void InitializeInputActions() {

        lock (inputLock) {

            // Create and enable InputActions if not already done.
            if (inputActions == null) {

                try {

                    inputActions = new RCC_InputActions();
                    inputActions.Enable();
                    inputActions.Vehicle.Enable();
                    inputActions.Camera.Enable();
                    inputActions.Optional.Enable();

                } catch (System.Exception e) {

                    Debug.LogError("RCC: Failed to initialize InputActions: " + e.Message);
                    return;

                }

            }

            // Subscribe to events only once.
            if (!eventsSubscribed)
                SubscribeToEvents();

        }

    }

    /// <summary>
    /// Subscribes to all input action events.
    /// </summary>
    private void SubscribeToEvents() {

        if (inputActions == null || eventsSubscribed)
            return;

        // General vehicle events
        inputActions.Vehicle.StartStopEngine.performed += StartStopEngine_performed;
        inputActions.Vehicle.LowBeamLights.performed += LowBeamLights_performed;
        inputActions.Vehicle.HighBeamLights.performed += HighBeamLights_performed;
        inputActions.Camera.ChangeCamera.performed += ChangeCamera_performed;
        inputActions.Vehicle.IndicatorLeft.performed += IndicatorLeft_performed;
        inputActions.Vehicle.IndicatorRight.performed += IndicatorRight_performed;
        inputActions.Vehicle.IndicatorHazard.performed += IndicatorHazard_performed;
        inputActions.Vehicle.InteriorLights.performed += InteriorLights_performed;
        inputActions.Vehicle.GearShiftUp.performed += GearShiftUp_performed;
        inputActions.Vehicle.GearShiftDown.performed += GearShiftDown_performed;
        inputActions.Vehicle.NGear.performed += NGear_performed;
        inputActions.Vehicle.NGear.canceled += NGear_canceled;
        inputActions.Optional.SlowMotion.performed += SlowMotion_performed;
        inputActions.Optional.SlowMotion.canceled += SlowMotion_canceled;
        inputActions.Optional.Record.performed += Record_performed;
        inputActions.Optional.Replay.performed += Replay_performed;
        inputActions.Camera.LookBack.performed += LookBack_performed;
        inputActions.Camera.LookBack.canceled += LookBack_canceled;
        inputActions.Vehicle.TrailerDetach.performed += TrailerDetach_performed;

        eventsSubscribed = true;

    }

    /// <summary>
    /// Unsubscribes from all input action events.
    /// </summary>
    private void UnsubscribeFromEvents() {

        if (inputActions == null || !eventsSubscribed)
            return;

        // General vehicle events
        inputActions.Vehicle.StartStopEngine.performed -= StartStopEngine_performed;
        inputActions.Vehicle.LowBeamLights.performed -= LowBeamLights_performed;
        inputActions.Vehicle.HighBeamLights.performed -= HighBeamLights_performed;
        inputActions.Camera.ChangeCamera.performed -= ChangeCamera_performed;
        inputActions.Vehicle.IndicatorLeft.performed -= IndicatorLeft_performed;
        inputActions.Vehicle.IndicatorRight.performed -= IndicatorRight_performed;
        inputActions.Vehicle.IndicatorHazard.performed -= IndicatorHazard_performed;
        inputActions.Vehicle.InteriorLights.performed -= InteriorLights_performed;
        inputActions.Vehicle.GearShiftUp.performed -= GearShiftUp_performed;
        inputActions.Vehicle.GearShiftDown.performed -= GearShiftDown_performed;
        inputActions.Vehicle.NGear.performed -= NGear_performed;
        inputActions.Vehicle.NGear.canceled -= NGear_canceled;
        inputActions.Optional.SlowMotion.performed -= SlowMotion_performed;
        inputActions.Optional.SlowMotion.canceled -= SlowMotion_canceled;
        inputActions.Optional.Record.performed -= Record_performed;
        inputActions.Optional.Replay.performed -= Replay_performed;
        inputActions.Camera.LookBack.performed -= LookBack_performed;
        inputActions.Camera.LookBack.canceled -= LookBack_canceled;
        inputActions.Vehicle.TrailerDetach.performed -= TrailerDetach_performed;

        eventsSubscribed = false;

    }

    /// <summary>
    /// Updates all input values for the current frame.
    /// </summary>
    public void GetInputs() {

        // Ensure InputActions are initialized.
        if (inputActions == null || !inputActions.asset.enabled) {

            inputs = new RCC_Inputs();
            InitializeInputActions();
            return;

        }

        // PROJECT MODIFICATION (DRIVE01): decouple which input sources are read so the on-screen mobile
        // buttons and the keyboard can BOTH drive the car in the Editor.
        // - On device / standalone builds: behavior is unchanged from stock RCC -> mobile buttons feed input
        //   when mobileControllerEnabled is true, keyboard otherwise (the two are mutually exclusive).
        // - In the Editor: we force mobileControllerEnabled true (see GameInitializer) so the mobile buttons
        //   stay visible for HUD work, but we ALSO read the keyboard here and merge the mobile inputs on top,
        //   so WASD/arrows keep working while the buttons are clickable.
        bool readMobile = Settings.mobileControllerEnabled;
        bool readKeyboard = !readMobile;
#if UNITY_EDITOR
        readKeyboard = true;
#endif

        // Standard input from the InputActions asset (keyboard / gamepad).
        if (readKeyboard) {

            try {

                inputs.throttleInput = inputActions.Vehicle.Throttle.ReadValue<float>();
                inputs.brakeInput = inputActions.Vehicle.Brake.ReadValue<float>();
                inputs.steerInput = inputActions.Vehicle.Steering.ReadValue<float>();
                inputs.handbrakeInput = inputActions.Vehicle.Handbrake.ReadValue<float>();
                inputs.boostInput = inputActions.Vehicle.NOS.ReadValue<float>();
                inputs.clutchInput = inputActions.Vehicle.Clutch.ReadValue<float>();

                // Camera orbit/zoom inputs (e.g., right-stick or mouse movement).
                inputs.orbitX = inputActions.Camera.Orbit.ReadValue<Vector2>().x;
                inputs.orbitY = inputActions.Camera.Orbit.ReadValue<Vector2>().y;
                inputs.scroll = inputActions.Camera.Zoom.ReadValue<Vector2>();

            } catch (System.Exception e) {

                Debug.LogError("RCC: Error reading input values: " + e.Message);

            }

        }

        // Mobile (on-screen button) input from RCC_MobileButtons.
        if (readMobile && RCC_MobileButtons.mobileInputs != null) {

#if UNITY_EDITOR
            // Editor: merge on top of the keyboard values so neither source is lost. Take the stronger of the
            // two for the analog "pressed" axes, and additively combine steering (clamped to [-1, 1]).
            inputs.throttleInput = Mathf.Max(inputs.throttleInput, RCC_MobileButtons.mobileInputs.throttleInput);
            inputs.brakeInput = Mathf.Max(inputs.brakeInput, RCC_MobileButtons.mobileInputs.brakeInput);
            inputs.steerInput = Mathf.Clamp(inputs.steerInput + RCC_MobileButtons.mobileInputs.steerInput, -1f, 1f);
            inputs.handbrakeInput = Mathf.Max(inputs.handbrakeInput, RCC_MobileButtons.mobileInputs.handbrakeInput);
            inputs.boostInput = Mathf.Max(inputs.boostInput, RCC_MobileButtons.mobileInputs.boostInput);
#else
            // Device / standalone: mobile buttons fully define the input (stock RCC behavior).
            inputs.throttleInput = RCC_MobileButtons.mobileInputs.throttleInput;
            inputs.brakeInput = RCC_MobileButtons.mobileInputs.brakeInput;
            inputs.steerInput = RCC_MobileButtons.mobileInputs.steerInput;
            inputs.handbrakeInput = RCC_MobileButtons.mobileInputs.handbrakeInput;
            inputs.boostInput = RCC_MobileButtons.mobileInputs.boostInput;
#endif

        }

    }

    #region Input Action Callbacks

    private void StartStopEngine_performed(InputAction.CallbackContext obj) {

        RCC_Events.Event_OnStartStopEngine();

    }

    private void LowBeamLights_performed(InputAction.CallbackContext obj) {

        RCC_Events.Event_OnLowBeamHeadlights();

    }

    private void HighBeamLights_performed(InputAction.CallbackContext obj) {

        RCC_Events.Event_OnHighBeamHeadlights();

    }

    private void ChangeCamera_performed(InputAction.CallbackContext obj) {

        RCC_Events.Event_OnChangeCamera();

    }

    private void IndicatorLeft_performed(InputAction.CallbackContext obj) {

        RCC_Events.Event_OnIndicatorLeft();

    }

    private void IndicatorRight_performed(InputAction.CallbackContext obj) {

        RCC_Events.Event_OnIndicatorRight();

    }

    private void IndicatorHazard_performed(InputAction.CallbackContext obj) {

        RCC_Events.Event_OnIndicatorHazard();

    }

    private void InteriorLights_performed(InputAction.CallbackContext obj) {

        RCC_Events.Event_OnInteriorlights();

    }

    private void GearShiftUp_performed(InputAction.CallbackContext obj) {

        RCC_Events.Event_OnGearShiftUp();

    }

    private void GearShiftDown_performed(InputAction.CallbackContext obj) {

        RCC_Events.Event_OnGearShiftDown();

    }

    private void NGear_performed(InputAction.CallbackContext obj) {

        RCC_Events.Event_OnNGear(true);

    }

    private void NGear_canceled(InputAction.CallbackContext obj) {

        RCC_Events.Event_OnNGear(false);

    }

    private void SlowMotion_performed(InputAction.CallbackContext obj) {

        RCC_Events.Event_OnSlowMotion(true);

    }

    private void SlowMotion_canceled(InputAction.CallbackContext obj) {

        RCC_Events.Event_OnSlowMotion(false);

    }

    private void Record_performed(InputAction.CallbackContext obj) {

        RCC_Events.Event_OnRecord();

    }

    private void Replay_performed(InputAction.CallbackContext obj) {

        RCC_Events.Event_OnReplay();

    }

    private void LookBack_performed(InputAction.CallbackContext obj) {

        RCC_Events.Event_OnLookBack(true);

    }

    private void LookBack_canceled(InputAction.CallbackContext obj) {

        RCC_Events.Event_OnLookBack(false);

    }

    private void TrailerDetach_performed(InputAction.CallbackContext obj) {

        RCC_Events.Event_OnTrailerDetach();

    }

    #endregion

    /// <summary>
    /// Disables or re-enables input actions when the application is paused or resumed.
    /// </summary>
    /// <param name="pauseStatus">True when the application is paused, false when resumed.</param>
    private void OnApplicationPause(bool pauseStatus) {

        if (pauseStatus && inputActions != null) {

            inputActions.Disable();

        } else if (!pauseStatus && inputActions != null) {

            inputActions.Enable();

        }

    }

    /// <summary>
    /// Disables or re-enables input actions when the application gains or loses window focus.
    /// </summary>
    /// <param name="hasFocus">True when the application has focus, false when it loses focus.</param>
    private void OnApplicationFocus(bool hasFocus) {

        if (!hasFocus && inputActions != null) {

            inputActions.Disable();

        } else if (hasFocus && inputActions != null) {

            inputActions.Enable();

        }

    }

}