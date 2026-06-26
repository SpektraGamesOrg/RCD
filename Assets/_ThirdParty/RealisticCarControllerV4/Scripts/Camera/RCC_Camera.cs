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
using UnityEngine.EventSystems;
using UnityEngine.Rendering;

#if BCG_URP
using UnityEngine.Rendering.Universal;
#endif

/// <summary>
/// Main RCC camera controller. Supports 6 camera modes (TPS, FPS/Hood, Wheel, Fixed, Cinematic, Top)
/// with orbit control, collision detection, occlusion handling, and dynamic FOV adjustments.
/// Instead of using multiple cameras, it parents a single camera to the appropriate position for each mode.
/// </summary>
[DefaultExecutionOrder(-10)]
public class RCC_Camera : RCC_Core
{
    /// <summary>
    /// Stores references and cached data about the currently targeted player vehicle,
    /// including speed, velocity, hood camera, and wheel camera transforms.
    /// </summary>
    [System.Serializable]
    public class CameraTarget
    {
        /// <summary>
        /// The player vehicle this camera is currently tracking.
        /// </summary>
        [Tooltip("The player vehicle this camera is currently tracking.")]
        public RCC_CarControllerV4 playerVehicle;

        /// <summary>
        /// Gets the current speed of the player vehicle in km/h. Returns 0 if no vehicle is assigned.
        /// </summary>
        public float Speed
        {
            get
            {
                if (!playerVehicle)
                    return 0f;

                return playerVehicle.speed;
            }
        }

        /// <summary>
        /// Gets the velocity of the player vehicle in its local space. Returns Vector3.zero if no vehicle is assigned.
        /// </summary>
        public Vector3 Velocity
        {
            get
            {
                if (!playerVehicle)
                    return Vector3.zero;

                return playerVehicle.transform.InverseTransformDirection(playerVehicle.Rigid.linearVelocity);
            }
        }

        /// <summary>
        /// Gets the hood camera component attached to the player vehicle. Lazily cached on first access.
        /// </summary>
        public RCC_HoodCamera HoodCamera
        {
            get
            {
                if (!playerVehicle)
                    return null;

                if (!_hoodCamera)
                    _hoodCamera = playerVehicle.GetComponentInChildren<RCC_HoodCamera>();

                return _hoodCamera;
            }
        }
        private RCC_HoodCamera _hoodCamera;

        /// <summary>
        /// Gets the wheel camera component attached to the player vehicle. Lazily cached on first access.
        /// </summary>
        public RCC_WheelCamera WheelCamera
        {
            get
            {
                if (!playerVehicle)
                    return null;

                if (!_wheelCamera)
                    _wheelCamera = playerVehicle.GetComponentInChildren<RCC_WheelCamera>();

                return _wheelCamera;
            }
        }
        private RCC_WheelCamera _wheelCamera;
    }

    /// <summary>
    /// The camera's target vehicle data. Assign via <see cref="SetTarget"/> or directly.
    /// </summary>
    [Tooltip("The camera's target vehicle data. Assign via <see cref=\"SetTarget\"/> or directly.")]
    public CameraTarget cameraTarget = new CameraTarget();

    /// <summary>
    /// Whether the camera is currently rendering. When false, the camera GameObject is deactivated.
    /// </summary>
    [Tooltip("Whether the camera is currently rendering. When false, the camera GameObject is deactivated.")]
    public bool isRendering = true;

    /// <summary>
    /// Reference to the actual Unity Camera component used for rendering the viewport.
    /// </summary>
    [Tooltip("Reference to the actual Unity Camera component used for rendering the viewport.")]
    public Camera actualCamera;

    /// <summary>
    /// Pivot center of the camera. Used for applying collision shake offsets and camera movements.
    /// </summary>
    [Tooltip("Pivot center of the camera. Used for applying collision shake offsets and camera movements.")]
    public GameObject pivot;

    /// <summary>
    /// Available camera modes for the RCC camera system.
    /// </summary>
    public enum CameraMode
    {
        /// <summary>Third-person shooter style camera behind the vehicle.</summary>
        TPS,
        /// <summary>First-person / hood camera mounted on the vehicle.</summary>
        FPS,
        /// <summary>Low-angle camera mounted near the wheel of the vehicle.</summary>
        WHEEL,
        /// <summary>Stationary camera placed in the scene that tracks the vehicle.</summary>
        FIXED,
        /// <summary>Cinematic camera with animated movement around the vehicle.</summary>
        CINEMATIC,
        /// <summary>Top-down overhead camera looking at the vehicle from above.</summary>
        TOP
    }

    /// <summary>
    /// Currently active camera mode. Change via <see cref="ChangeCamera()"/> or direct assignment.
    /// </summary>
    [Tooltip("Currently active camera mode. Change via <see cref=\"ChangeCamera()\"/> or direct assignment.")]
    public CameraMode cameraMode = CameraMode.TPS;

    /// <summary>
    /// The camera mode from the previous frame, used to detect mode changes.
    /// </summary>
    private CameraMode lastCameraMode = CameraMode.TPS;

    /// <summary>
    /// Returns true if the target vehicle is currently in drift mode, enabling a wider camera angle.
    /// </summary>
    public bool DriftMode
    {
        get
        {
            if (cameraTarget == null)
                return false;

            if (cameraTarget.playerVehicle == null)
                return false;

            return cameraTarget.playerVehicle.driftMode;
        }
    }

    /// <summary>
    /// Shortcut to the scene-wide fixed camera singleton instance.
    /// </summary>
    private RCC_FixedCamera FixedCamera
    {
        get { return RCC_FixedCamera.Instance; }
    }

    /// <summary>
    /// Shortcut to the scene-wide cinematic camera singleton instance.
    /// </summary>
    private RCC_CinematicCamera CinematicCamera
    {
        get { return RCC_CinematicCamera.Instance; }
    }

    /// <summary>
    /// When enabled in TPS mode, the camera's X (pitch) rotation follows the vehicle's X rotation.
    /// </summary>
    [Tooltip("When enabled in TPS mode, the camera's X (pitch) rotation follows the vehicle's X rotation.")]
    public bool TPSLockX = true;

    /// <summary>
    /// When enabled in TPS mode, the camera's Y (yaw) rotation follows the vehicle's Y rotation.
    /// </summary>
    [Tooltip("When enabled in TPS mode, the camera's Y (yaw) rotation follows the vehicle's Y rotation.")]
    public bool TPSLockY = true;

    /// <summary>
    /// When enabled in TPS mode, the camera's Z (roll) rotation follows the vehicle's Z rotation.
    /// </summary>
    [Tooltip("When enabled in TPS mode, the camera's Z (roll) rotation follows the vehicle's Z rotation.")]
    public bool TPSLockZ = true;

    /// <summary>
    /// When enabled in TPS mode, the camera stops tracking vehicle rotation while the vehicle is airborne.
    /// </summary>
    [Tooltip("When enabled in TPS mode, the camera stops tracking vehicle rotation while the vehicle is airborne.")]
    public bool TPSFreeFall = true;

    /// <summary>
    /// When enabled, the TPS camera dynamically adjusts its distance and height based on vehicle acceleration.
    /// </summary>
    [Tooltip("When enabled, the TPS camera dynamically adjusts its distance and height based on vehicle acceleration.")]
    public bool TPSDynamic = false;

    /// <summary>
    /// Whether to enable top camera mode for cycling.
    /// </summary>
    [Tooltip("Whether to enable top camera mode for cycling.")]
    public bool useTopCameraMode = false;

    /// <summary>
    /// Whether to enable hood camera mode for cycling.
    /// </summary>
    [Tooltip("Whether to enable hood camera mode for cycling.")]
    public bool useHoodCameraMode = true;

    /// <summary>
    /// Whether orbit control can be used in TPS camera mode.
    /// </summary>
    [Tooltip("Whether orbit control can be used in TPS camera mode.")]
    public bool useOrbitInTPSCameraMode = true;

    /// <summary>
    /// Whether orbit control can be used in hood camera mode.
    /// </summary>
    [Tooltip("Whether orbit control can be used in hood camera mode.")]
    public bool useOrbitInHoodCameraMode = true;

    /// <summary>
    /// Whether to enable wheel camera mode for cycling.
    /// </summary>
    [Tooltip("Whether to enable wheel camera mode for cycling.")]
    public bool useWheelCameraMode = true;

    /// <summary>
    /// Whether to enable fixed camera mode for cycling.
    /// </summary>
    [Tooltip("Whether to enable fixed camera mode for cycling.")]
    public bool useFixedCameraMode = true;

    /// <summary>
    /// Whether to enable cinematic camera mode for cycling.
    /// </summary>
    [Tooltip("Whether to enable cinematic camera mode for cycling.")]
    public bool useCinematicCameraMode = true;

    /// <summary>
    /// In top camera mode, determines if the camera is orthographic or perspective.
    /// </summary>
    [Tooltip("In top camera mode, determines if the camera is orthographic or perspective.")]
    public bool useOrthoForTopCamera = false;

    /// <summary>
    /// Whether to use camera occlusion detection (to prevent camera passing through objects).
    /// </summary>
    [Tooltip("Whether to use camera occlusion detection (to prevent camera passing through objects).")]
    public bool useOcclusion = true;

    /// <summary>
    /// LayerMask for the camera occlusion detection.
    /// </summary>
    [Tooltip("LayerMask for the camera occlusion detection.")]
    public LayerMask occlusionLayerMask = -1;

    private bool occluded = false;

    /// <summary>
    /// If enabled, camera automatically changes its mode (TPS, Cinematic, etc.) after a certain time.
    /// </summary>
    [Tooltip("If enabled, camera automatically changes its mode (TPS, Cinematic, etc.) after a certain time.")]
    public bool useAutoChangeCamera = false;

    private float autoChangeCameraTimer = 0f;

    /// <summary>
    /// Angle of the camera in top view mode.
    /// </summary>
    [Tooltip("Angle of the camera in top view mode.")]
    public Vector3 topCameraAngle = new Vector3(45f, 45f, 0f);

    /// <summary>
    /// Vertical distance (height) from the vehicle in top camera mode.
    /// </summary>
    [Tooltip("Vertical distance (height) from the vehicle in top camera mode.")]
    [Min(0f)]
    public float topCameraDistance = 100f;

    /// <summary>
    /// Maximum additional distance offset in top camera mode, based on vehicle speed.
    /// </summary>
    [Tooltip("Maximum additional distance offset in top camera mode, based on vehicle speed.")]
    [Min(0f)]
    public float maximumZDistanceOffset = 10f;

    private float topCameraDistanceOffset = 0f;

    // Target position.
    private Vector3 targetPosition = Vector3.zero;

    // Used for resetting orbit values when direction of the vehicle has been changed.
    private int direction = 1;

    /// <summary>
    /// The distance of the camera from the vehicle in TPS mode.
    /// </summary>
    [Tooltip("The distance of the camera from the vehicle in TPS mode.")]
    [Range(0f, 20f)] public float TPSDistance = 6f;

    /// <summary>
    /// The height of the camera above the vehicle in TPS mode.
    /// </summary>
    [Tooltip("The height of the camera above the vehicle in TPS mode.")]
    [Range(0f, 10f)] public float TPSHeight = 2f;

    /// <summary>
    /// Rotation movement damping for TPS mode.
    /// </summary>
    [Tooltip("Rotation movement damping for TPS mode.")]
    [Range(0f, 1f)] public float TPSRotationDamping = .7f;

    /// <summary>
    /// Maximum tilt angle in TPS mode related with rigidbody local velocity.
    /// </summary>
    [Tooltip("Maximum tilt angle in TPS mode related with rigidbody local velocity.")]
    [Range(0f, 25f)] public float TPSTiltMaximum = 15f;

    /// <summary>
    /// Tilt angle multiplier in TPS mode.
    /// </summary>
    [Tooltip("Tilt angle multiplier in TPS mode.")]
    [Range(0f, 10f)] public float TPSTiltMultiplier = 1.5f;

    /// <summary>
    /// Yaw angle offset for TPS mode.
    /// </summary>
    [Tooltip("Yaw angle offset for TPS mode.")]
    [Range(-45f, 45f)] public float TPSYaw = 0f;

    /// <summary>
    /// Pitch angle offset for TPS mode.
    /// </summary>
    [Tooltip("Pitch angle offset for TPS mode.")]
    [Range(-45f, 45f)] public float TPSPitch = 10f;

    /// <summary>
    /// Automatically focuses distance and height based on vehicle bounds in TPS mode.
    /// </summary>
    [Tooltip("Automatically focuses distance and height based on vehicle bounds in TPS mode.")]
    public bool TPSAutoFocus = true;

    /// <summary>
    /// Automatically reverses the camera when vehicle is in reverse gear in TPS mode.
    /// </summary>
    [Tooltip("Automatically reverses the camera when vehicle is in reverse gear in TPS mode.")]
    public bool TPSAutoReverse = true;

    /// <summary>
    /// Enables collision shake/effect in TPS mode.
    /// </summary>
    [Tooltip("Enables collision shake/effect in TPS mode.")]
    public bool TPSCollision = true;

    /// <summary>
    /// Additional position offset in TPS mode.
    /// </summary>
    [Tooltip("Additional position offset in TPS mode.")]
    public Vector3 TPSOffset = new Vector3(0f, 0f, .2f);

    /// <summary>
    /// Initial rotation of the camera in TPS mode when the game starts.
    /// </summary>
    [Tooltip("Initial rotation of the camera in TPS mode when the game starts.")]
    public Vector3 TPSStartRotation = new Vector3(0f, 0f, 0f);

    private Quaternion TPSLastRotation;

    private float TPSTiltAngle = 0f;

    /// <summary>
    /// The target field of view that the camera smoothly interpolates toward each frame.
    /// </summary>
    internal float targetFieldOfView = 60f;

    /// <summary>
    /// Minimum field of view in TPS mode.
    /// </summary>
    [Tooltip("Minimum field of view in TPS mode.")]
    [Range(10f, 90f)] public float TPSMinimumFOV = 40f;

    /// <summary>
    /// Maximum field of view in TPS mode.
    /// </summary>
    [Tooltip("Maximum field of view in TPS mode.")]
    [Range(10f, 160f)] public float TPSMaximumFOV = 60f;

    /// <summary>
    /// Field of view used in hood camera (FPS) mode.
    /// </summary>
    [Tooltip("Field of view used in hood camera (FPS) mode.")]
    [Range(10f, 160f)] public float hoodCameraFOV = 60f;

    /// <summary>
    /// Field of view used in wheel camera mode.
    /// </summary>
    [Tooltip("Field of view used in wheel camera mode.")]
    [Range(10f, 160f)] public float wheelCameraFOV = 60f;

    /// <summary>
    /// Minimum orthographic size for top camera mode.
    /// </summary>
    [Tooltip("Minimum orthographic size for top camera mode.")]
    [Min(0f)]
    public float minimumOrtSize = 10f;

    /// <summary>
    /// Maximum orthographic size for top camera mode.
    /// </summary>
    [Tooltip("Maximum orthographic size for top camera mode.")]
    [Min(0f)]
    public float maximumOrtSize = 20f;

    /// <summary>
    /// Tracks the current index in the camera mode cycle, used by <see cref="ChangeCamera()"/>.
    /// </summary>
    internal int cameraSwitchCount = 0;

    private Vector3 accelerationVelocity = Vector3.zero;

    /// <summary>
    /// Current acceleration of the vehicle in local space.
    /// </summary>
    [Tooltip("Current acceleration of the vehicle in local space.")]
    public Vector3 acceleration = Vector3.zero;

    /// <summary>
    /// Last velocity in local space, used for calculating acceleration.
    /// </summary>
    [Tooltip("Last velocity in local space, used for calculating acceleration.")]
    public Vector3 lastVelocity = Vector3.zero;

    /// <summary>
    /// Smoothed acceleration value for camera usage.
    /// </summary>
    [Tooltip("Smoothed acceleration value for camera usage.")]
    public Vector3 acceleration_Smoothed = Vector3.zero;

    private Vector3 collisionDirection = Vector3.zero;
    private Vector3 collisionPos = Vector3.zero;
    private Quaternion collisionRot = Quaternion.identity;

    // Raw Orbit X and Y inputs.
    private float orbitX, orbitY = 0f;

    /// <summary>
    /// Minimum vertical orbit angle (degrees) for camera orbit control.
    /// </summary>
    [Tooltip("Minimum vertical orbit angle (degrees) for camera orbit control.")]
    [Range(-90f, 90f)]
    public float minOrbitY = -15f;

    /// <summary>
    /// Maximum vertical orbit angle (degrees) for camera orbit control.
    /// </summary>
    [Tooltip("Maximum vertical orbit angle (degrees) for camera orbit control.")]
    [Range(-90f, 90f)]
    public float maxOrbitY = 70f;

    /// <summary>
    /// Horizontal orbit rotation speed.
    /// </summary>
    [Tooltip("Horizontal orbit rotation speed.")]
    [Min(0f)]
    public float orbitXSpeed = 100f;

    /// <summary>
    /// Vertical orbit rotation speed.
    /// </summary>
    [Tooltip("Vertical orbit rotation speed.")]
    [Min(0f)]
    public float orbitYSpeed = 100f;

    /// <summary>
    /// Smoothing factor applied to orbit rotation transitions.
    /// </summary>
    [Tooltip("Smoothing factor applied to orbit rotation transitions.")]
    [Min(0f)]
    public float orbitSmooth = 40f;

    /// <summary>
    /// When enabled, orbit rotation is blended with TPS rotation damping for smoother transitions.
    /// </summary>
    [Tooltip("When enabled, orbit rotation is blended with TPS rotation damping for smoother transitions.")]
    public bool orbitWithRotationDamping = true;

    /// <summary>
    /// When enabled, the orbit rotation automatically resets to the default position when the vehicle is moving fast enough.
    /// </summary>
    [Tooltip("When enabled, the orbit rotation automatically resets to the default position when the vehicle is moving fast enough.")]
    public bool orbitReset = false;
    private float orbitResetTimer = 0f;
    private float oldOrbitX, oldOrbitY = 0f;

    /// <summary>
    /// True when the camera is looking back (rear view) in TPS mode.
    /// </summary>
    [Tooltip("True when the camera is looking back (rear view) in TPS mode.")]
    public bool lookBackNow = false;

    /// <summary>
    /// Singleton instance of the currently active RCC camera in the scene.
    /// </summary>
    public static RCC_Camera Instance { get; private set; }

    private void Awake()
    {
        // Register the singleton instance.
        Instance = this;

        // Getting Camera.
        if (!actualCamera)
            actualCamera = GetComponentInChildren<Camera>();

        //  If pivot of the camera is not found, create it.
        if (!pivot)
        {
            Transform pivotTransform = transform.Find("Pivot");
            pivot = pivotTransform ? pivotTransform.gameObject : null;

            if (!pivot)
                pivot = new GameObject("Pivot");

            pivot.transform.SetParent(transform);
            pivot.transform.localPosition = Vector3.zero;
            pivot.transform.localRotation = Quaternion.identity;

            actualCamera.transform.SetParent(pivot.transform, true);
        }

#if BCG_URP
        if (IsURP())
            CheckURP_PP();
#endif
    }

#if BCG_URP
    /// <summary>
    /// Checks if URP is used and ensures post-processing is enabled on the camera.
    /// </summary>
    public void CheckURP_PP()
    {
        GameObject actCamera = GetComponentInChildren<Camera>(true).gameObject;

        if (actCamera.TryGetComponent(out UniversalAdditionalCameraData cameraData))
        {
            if (!cameraData.renderPostProcessing)
            {
#if UNITY_EDITOR
                Debug.Log("Post processing of RCC_Camera has been enabled.");
#endif
                cameraData.renderPostProcessing = true;
            }
        }
        else
        {
#if UNITY_EDITOR
            Debug.Log("UniversalAdditionalCameraData wasn't found on RCC_Camera, adding it.");
#endif
            cameraData = actCamera.AddComponent<UniversalAdditionalCameraData>();

            if (!cameraData.renderPostProcessing)
            {
#if UNITY_EDITOR
                Debug.Log("Post processing of RCC_Camera has been enabled.");
#endif
                cameraData.renderPostProcessing = true;
            }
        }
    }
#endif

    private void OnEnable()
    {
        ResetCameraVariables();

        // Calling this event when BCG Camera spawned.
        RCC_Events.Event_OnBCGCameraSpawned(gameObject);

        // Listening player vehicle collisions for crashing effects.
        RCC_Events.OnRCCPlayerCollision += RCC_CarControllerV4_OnRCCPlayerCollision;

        // Listening input events for camera modes and look back.
        RCC_Events.OnChangeCamera += RCC_InputManager_OnChangeCamera;
        RCC_Events.OnLookBack += RCC_InputManager_OnLookBack;
    }

    /// <summary>
    /// Receives collision data from the player vehicle for collision effects.
    /// </summary>
    /// <param name="RCC">The vehicle controller that was involved in the collision.</param>
    /// <param name="collision">Unity collision data containing contact points and relative velocity.</param>
    private void RCC_CarControllerV4_OnRCCPlayerCollision(RCC_CarControllerV4 RCC, Collision collision)
    {
        Collision(collision);
    }

    /// <summary>
    /// Look back toggle triggered by input manager.
    /// </summary>
    /// <param name="state">True to enable look-back, false to disable.</param>
    private void RCC_InputManager_OnLookBack(bool state)
    {
        lookBackNow = state;
    }

    /// <summary>
    /// Cycle camera mode triggered by input manager.
    /// </summary>
    private void RCC_InputManager_OnChangeCamera()
    {
        ChangeCamera();
    }

    /// <summary>
    /// Assigns a target vehicle to the camera and optionally adjusts TPS camera distance/height.
    /// </summary>
    /// <param name="player">The vehicle controller to track with this camera.</param>
    public void SetTarget(RCC_CarControllerV4 player)
    {
        cameraTarget = new CameraTarget
        {
            playerVehicle = player
        };

        if (TPSAutoFocus)
            StartCoroutine(AutoFocus());

        SetupForNewMode();

        TPSLastRotation = player.transform.rotation;
    }

    /// <summary>
    /// Removes the current target vehicle from the camera.
    /// </summary>
    public void RemoveTarget()
    {
        transform.SetParent(null);
        cameraTarget.playerVehicle = null;
    }

    private void Update()
    {
        acceleration_Smoothed = Vector3.SmoothDamp(acceleration_Smoothed, acceleration, ref accelerationVelocity, .3f);

        // If it's not rendering, disable the camera.
        if (!isRendering)
        {
            if (actualCamera.gameObject.activeSelf)
                actualCamera.gameObject.SetActive(false);

            return;
        }
        else
        {
            if (!actualCamera.gameObject.activeSelf)
                actualCamera.gameObject.SetActive(true);
        }

        // Early out if we don't have the player vehicle.
        if (!cameraTarget.playerVehicle)
            return;

        // Lerping current field of view to target field of view.
        actualCamera.fieldOfView = Mathf.Lerp(actualCamera.fieldOfView, targetFieldOfView, Time.deltaTime * 5f);
    }

    private void LateUpdate()
    {
        // Early out if we don't have the player vehicle.
        if (!cameraTarget.playerVehicle)
            return;

        // Even if we have the player vehicle and it's disabled, return.
        if (!cameraTarget.playerVehicle.gameObject.activeSelf)
            return;

        if (Time.timeScale <= 0)
            return;

        // Run the corresponding method with chosen camera mode.
        switch (cameraMode)
        {
            case CameraMode.TPS:

                if (useOrbitInTPSCameraMode)
                    ORBIT();

                TPS();

                break;

            case CameraMode.FPS:

                if (useOrbitInHoodCameraMode)
                    ORBIT();

                FPS();

                break;

            case CameraMode.WHEEL:
                WHEEL();
                break;

            case CameraMode.FIXED:
                FIXED();
                break;

            case CameraMode.CINEMATIC:
                CINEMATIC();
                break;

            case CameraMode.TOP:
                TOP();
                break;
        }

        if (lastCameraMode != cameraMode)
            SetupForNewMode();

        lastCameraMode = cameraMode;

        if (useAutoChangeCamera)
            autoChangeCameraTimer += Time.deltaTime;

        if (useAutoChangeCamera && autoChangeCameraTimer >= 10)
        {
            autoChangeCameraTimer = 0f;
            ChangeCamera();
        }

        //  Checking if camera is occluded by some colliders.
        CheckIfOccluded();
    }

    private void FixedUpdate()
    {
        // Early out if we don't have the player vehicle.
        if (!cameraTarget.playerVehicle)
            return;

        // Even if we have the player vehicle and it's disabled, return.
        if (!cameraTarget.playerVehicle.gameObject.activeSelf)
            return;

        acceleration = (cameraTarget.playerVehicle.transform.InverseTransformDirection(cameraTarget.playerVehicle.Rigid.linearVelocity) - lastVelocity) /
                       Time.fixedDeltaTime;
        lastVelocity = cameraTarget.playerVehicle.transform.InverseTransformDirection(cameraTarget.playerVehicle.Rigid.linearVelocity);

        acceleration.x = 0f;
        acceleration.y = 0f;
        acceleration = Vector3.ClampMagnitude(acceleration, 10f);
    }

    /// <summary>
    /// Cycles the camera mode by incrementing cameraSwitchCount.
    /// </summary>
    public void ChangeCamera()
    {
        ChangeCamera(0);
    }

    /// <summary>
    /// Internal camera cycling with recursion depth guard to prevent infinite loops when all modes are disabled.
    /// </summary>
    /// <param name="depth">Current recursion depth.</param>
    private void ChangeCamera(int depth)
    {
        // Prevent infinite recursion if all optional camera modes are disabled.
        if (depth >= 6)
        {
            cameraMode = CameraMode.TPS;
            cameraSwitchCount = 0;
            return;
        }

        cameraSwitchCount++;

        if (cameraSwitchCount >= 6)
            cameraSwitchCount = 0;

        switch (cameraSwitchCount)
        {
            case 0:
                cameraMode = CameraMode.TPS;
                break;

            case 1:
                if (useHoodCameraMode && cameraTarget.HoodCamera)
                    cameraMode = CameraMode.FPS;
                else
                    ChangeCamera(depth + 1);
                break;

            case 2:
                if (useWheelCameraMode && cameraTarget.WheelCamera)
                    cameraMode = CameraMode.WHEEL;
                else
                    ChangeCamera(depth + 1);
                break;

            case 3:
                if (useFixedCameraMode && FixedCamera)
                    cameraMode = CameraMode.FIXED;
                else
                    ChangeCamera(depth + 1);
                break;

            case 4:
                if (useCinematicCameraMode && CinematicCamera)
                    cameraMode = CameraMode.CINEMATIC;
                else
                    ChangeCamera(depth + 1);
                break;

            case 5:
                if (useTopCameraMode)
                    cameraMode = CameraMode.TOP;
                else
                    ChangeCamera(depth + 1);
                break;
        }
    }

    /// <summary>
    /// Directly changes camera mode to the specified mode.
    /// </summary>
    /// <param name="mode">The camera mode to switch to.</param>
    public void ChangeCamera(CameraMode mode)
    {
        cameraMode = mode;
    }

    /// <summary>
    /// Hood (FPS) camera logic. If orbit is enabled, applies orbit rotation.
    /// </summary>
    private void FPS()
    {
        if (useOrbitInHoodCameraMode)
            transform.rotation = cameraTarget.playerVehicle.transform.rotation * Quaternion.Euler(orbitY, orbitX, 0f);
        else
            transform.rotation = cameraTarget.playerVehicle.transform.rotation;
    }

    /// <summary>
    /// Wheel camera logic. If occluded, switches to TPS.
    /// </summary>
    private void WHEEL()
    {
        if (useOcclusion && occluded)
            ChangeCamera(CameraMode.TPS);
    }

    /// <summary>
    /// TPS mode.
    /// </summary>
    private void TPS()
    {
        //  Setting rotation of the camera.
        transform.rotation = TPSLastRotation;

        // If TPS Auto Reverse is enabled and vehicle is moving backwards, reset X and Y orbits when vehicle direction is changed. Camera will look directly rear side of the vehicle.
        direction = cameraTarget.playerVehicle.direction;

        // Calculate the current rotation angles for TPS mode.
        if (!TPSAutoReverse)
            direction = 1;

        // Look back now?
        if (lookBackNow)
            direction *= -1;

        //  Vehicle direction.
        Vector3 playerVehicleDirection = cameraTarget.playerVehicle.transform.forward * direction;

        //  New direction for the drift mode.
        if (DriftMode)
        {
            Vector3 playerVelocityDirection = cameraTarget.playerVehicle.transform.InverseTransformDirection(cameraTarget.playerVehicle.Rigid.linearVelocity);

            playerVelocityDirection.y = 0f;
            playerVelocityDirection = cameraTarget.playerVehicle.transform.TransformDirection(playerVelocityDirection);

            playerVehicleDirection = playerVelocityDirection + (cameraTarget.playerVehicle.transform.forward * direction * .1f);
        }

        // Create the desired rotation based on the direction to the target
        Quaternion desiredRotation = Quaternion.LookRotation(playerVehicleDirection, Vector3.up);

        //  Desired vector.
        Vector3 desiredVector = desiredRotation.eulerAngles;

        //  Don't set this axis if it's not locked.
        if (!TPSLockX)
            desiredVector.x = transform.eulerAngles.x;

        //  Don't set this axis if it's not locked.
        if (!TPSLockY)
            desiredVector.y = transform.eulerAngles.y;

        //  Don't set this axis if it's not locked.
        if (!TPSLockZ)
            desiredVector.z = 0f;
        else
            desiredVector.z = cameraTarget.playerVehicle.transform.eulerAngles.z;

        //  Converting euler angles to quaternion.
        desiredRotation = Quaternion.Euler(desiredVector);

        //  Temp value for rotation damping.
        float rotDamp = TPSRotationDamping;

        //  Set rotation damping to 0 if free fall option is enabled and vehicle is not grounded.
        if (TPSFreeFall && Time.time >= 1f && !DriftMode)
        {
            if (!cameraTarget.playerVehicle.isGrounded)
                rotDamp = 0f;
        }

        // Smoothly rotate the object towards the desired rotation.
        transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, rotDamp * 10f * Time.deltaTime);

        //  Getting eulerangles of the camera.
        desiredVector = transform.rotation.eulerAngles;

        //  Applying orbit Y value.
        if (useOrbitInTPSCameraMode && orbitY != 0)
            desiredVector.x = orbitY;

        //  Applying orbit X value.
        if (useOrbitInTPSCameraMode && orbitX != 0)
            desiredVector.y = orbitX;

        //  Force z angle to 0.
        if (useOrbitInTPSCameraMode && (orbitX != 0 || orbitY != 0))
            desiredVector.z = 0f;

        //  Setting rotation of the camera.
        transform.rotation = Quaternion.Euler(desiredVector);

        //  Setting position of the camera.
        transform.position = cameraTarget.playerVehicle.transform.position;
        transform.position += cameraTarget.playerVehicle.transform.rotation * TPSOffset;
        transform.position -= transform.forward * TPSDistance;
        transform.position += Vector3.up * TPSHeight;

        float addTPSPitch = 0f;

        if (TPSDynamic && !DriftMode)
            transform.position -= cameraTarget.playerVehicle.transform.rotation * acceleration_Smoothed / 20f;

        //  Checks occlusion if it's enabled.
        if (useOcclusion)
            OccludeRay(cameraTarget.playerVehicle.transform.position);

        // Collision positions and rotations that affects pivot of the camera.
        if (Time.deltaTime != 0)
        {
            collisionPos = Vector3.Lerp(collisionPos, Vector3.zero, Time.deltaTime * 5f);
            collisionRot = Quaternion.Lerp(collisionRot, Quaternion.identity, Time.deltaTime * 5f);
        }

        // Lerping position and rotation of the pivot to collision.
        pivot.transform.localPosition = Vector3.Lerp(pivot.transform.localPosition, collisionPos, Time.deltaTime * 10f);
        pivot.transform.localRotation = Quaternion.Lerp(pivot.transform.localRotation, collisionRot, Time.deltaTime * 10f);

        //  Assigning last rotation of the camera.
        TPSLastRotation = transform.rotation;

        // Rotates camera by Z axis for tilt effect.
        TPSTiltAngle = TPSTiltMaximum *
                       (Mathf.Clamp(cameraTarget.playerVehicle.transform.InverseTransformDirection(cameraTarget.playerVehicle.Rigid.linearVelocity).x, -10f,
                           10f) * .04f);
        TPSTiltAngle *= TPSTiltMultiplier;

        //  Applying tilt angle rotation.
        transform.rotation *= Quaternion.Euler(TPSPitch + addTPSPitch, 0f, TPSYaw + TPSTiltAngle);

        // Lerping targetFieldOfView from TPSMinimumFOV to TPSMaximumFOV related to vehicle speed.
        targetFieldOfView = Mathf.Lerp(TPSMinimumFOV, TPSMaximumFOV, Mathf.Abs(cameraTarget.playerVehicle.speed) / 150f);
    }

    /// <summary>
    /// Specialized TPS mode for drifting. It tilts the camera more based on sideways velocity
    /// and slightly offsets behind the drift angle.
    /// </summary>
    private void TPSDrift()
    {
        // 1) Start from something close to the TPS logic
        transform.rotation = TPSLastRotation;

        // Just like in TPS, we consider forward direction, but let's incorporate a drift angle.
        int vehicleDirection = cameraTarget.playerVehicle.direction;

        // If we want to handle lookBackNow:
        if (lookBackNow)
            vehicleDirection *= -1;

        // Build the desired rotation looking at the vehicle's forward * direction
        Vector3 forwardDir = cameraTarget.playerVehicle.transform.forward * vehicleDirection;
        Quaternion desiredRotation = Quaternion.LookRotation(forwardDir, Vector3.up);

        // If you want to skip rotation damping while airborne, same as TPS:
        float rotDamp = (TPSFreeFall && !cameraTarget.playerVehicle.isGrounded) ? 0f : TPSRotationDamping;

        // Smooth rotation
        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            desiredRotation,
            rotDamp * 10f * Time.deltaTime
        );

        // Additional drift tilt. You can tweak this multiplier.
        // The sideways velocity in local space is cameraTarget.Velocity.x.
        float driftAngle = Mathf.Clamp(
            cameraTarget.Velocity.x, -10f, 10f
        );

        // Increase tilt multiplier for a more dramatic drift effect:
        float driftTiltMultiplier = TPSTiltMultiplier * 2.0f;
        float driftTiltMax = TPSTiltMaximum * 1.5f; // 1.5x the normal tilt, for example

        float driftTiltAngle = driftTiltMax * (driftAngle * 0.1f) * driftTiltMultiplier;

        // 2) Position the camera, slightly behind and above the normal TPS
        transform.position = cameraTarget.playerVehicle.transform.position;

        // Like TPS, we keep a base offset and distance, but can shift them for drift
        // For instance, push the camera a bit further away for a wide view:
        float driftDistance = TPSDistance + 2f;
        float driftHeight = TPSHeight + 1f;

        // Subtract forward for distance:
        transform.position -= transform.forward * driftDistance;
        transform.position += Vector3.up * driftHeight;

        // Optionally offset sideways behind drift direction
        // e.g. if drifting left (negative x), shift camera slightly right to keep car in frame:
        float sideOffset = Mathf.Lerp(-1f, 1f, (driftAngle + 10f) / 20f);
        transform.position += transform.right * sideOffset;

        // Let occlusion do its thing (if enabled)
        if (useOcclusion)
            OccludeRay(cameraTarget.playerVehicle.transform.position);

        // 3) Blend any collision effects
        collisionPos = Vector3.Lerp(collisionPos, Vector3.zero, Time.deltaTime * 5f);
        collisionRot = Quaternion.Lerp(collisionRot, Quaternion.identity, Time.deltaTime * 5f);
        pivot.transform.localPosition = Vector3.Lerp(pivot.transform.localPosition, collisionPos, Time.deltaTime * 10f);
        pivot.transform.localRotation = Quaternion.Lerp(pivot.transform.localRotation, collisionRot, Time.deltaTime * 10f);

        // 4) Final rotation application: apply pitch & drift tilt around Z:
        transform.rotation *= Quaternion.Euler(TPSPitch, 0f, TPSYaw + driftTiltAngle);

        // 5) Field of view could expand more during drift
        float speedFactor = Mathf.Abs(cameraTarget.playerVehicle.speed) / 150f;
        float extraFov = 10f; // Extra FOV for drift
        targetFieldOfView = Mathf.Lerp(TPSMinimumFOV, TPSMaximumFOV + extraFov, speedFactor);

        // 6) Store rotation for next frame
        TPSLastRotation = transform.rotation;
    }


    /// <summary>
    /// Fixed camera mode logic. Locks the camera to a fixed viewpoint.
    /// </summary>
    private void FIXED()
    {
        if (!FixedCamera)
            return;

        if (FixedCamera.transform.parent != null)
            FixedCamera.transform.SetParent(null);

        if (useOcclusion && occluded)
        {
            FixedCamera.ChangePosition();
            occluded = false;
        }
    }

    /// <summary>
    /// Top camera mode logic. Can be orthographic or perspective.
    /// </summary>
    private void TOP()
    {
        actualCamera.orthographic = useOrthoForTopCamera;

        topCameraDistanceOffset = Mathf.Lerp(0f, maximumZDistanceOffset, Mathf.Abs(cameraTarget.Speed) / 100f);
        targetFieldOfView = Mathf.Lerp(minimumOrtSize, maximumOrtSize, Mathf.Abs(cameraTarget.Speed) / 100f);
        actualCamera.orthographicSize = targetFieldOfView;

        targetPosition = cameraTarget.playerVehicle.transform.position;
        targetPosition += cameraTarget.playerVehicle.transform.rotation * Vector3.forward * topCameraDistanceOffset;

        transform.position = targetPosition;
        transform.rotation = Quaternion.Euler(topCameraAngle);

        pivot.transform.localPosition = new Vector3(0f, 0f, -topCameraDistance);
    }

    /// <summary>
    /// Handles orbit rotation inputs for TPS or FPS camera modes.
    /// </summary>
    private void ORBIT()
    {
        if (oldOrbitX != orbitX)
        {
            oldOrbitX = orbitX;
            orbitResetTimer = 2f;
        }

        if (oldOrbitY != orbitY)
        {
            oldOrbitY = orbitY;
            orbitResetTimer = 2f;
        }

        if (orbitResetTimer > 0)
            orbitResetTimer -= Time.deltaTime;

        Mathf.Clamp(orbitResetTimer, 0f, 2f);

        if (orbitReset && cameraTarget.Speed >= 25f && orbitResetTimer <= 0f)
        {
            orbitX = 0f;
            orbitY = 0f;
        }
    }

    /// <summary>
    /// Used by mobile UI drag panel to control orbit rotation with PointerEventData.
    /// </summary>
    /// <param name="pointerData">Pointer event data containing the drag delta from the UI system.</param>
    public void OnDrag(PointerEventData pointerData)
    {
        if (cameraMode == CameraMode.TPS)
        {
            if (orbitX == 0)
                orbitX = transform.eulerAngles.y;

            if (orbitY == 0)
                orbitY = transform.eulerAngles.x;
        }

        orbitX += pointerData.delta.x * orbitXSpeed / 1000f;
        orbitY -= pointerData.delta.y * orbitYSpeed / 1000f;

        orbitY = Mathf.Clamp(orbitY, minOrbitY, maxOrbitY);

        orbitResetTimer = 2f;
    }

    /// <summary>
    /// Used by mobile UI drag panel to control orbit rotation with float x, y.
    /// </summary>
    /// <param name="x">Horizontal drag delta value.</param>
    /// <param name="y">Vertical drag delta value.</param>
    public void OnDrag(float x, float y)
    {
        if (cameraMode == CameraMode.TPS)
        {
            if (orbitX == 0)
                orbitX = transform.eulerAngles.y;

            if (orbitY == 0)
                orbitY = transform.eulerAngles.x;
        }

        orbitX += x * orbitXSpeed / 10f;
        orbitY -= y * orbitYSpeed / 10f;

        orbitY = Mathf.Clamp(orbitY, minOrbitY, maxOrbitY);

        orbitResetTimer = 2f;
    }

    /// <summary>
    /// Cinematic camera mode logic. Positions the camera using RCC_CinematicCamera if available.
    /// </summary>
    private void CINEMATIC()
    {
        if (!CinematicCamera)
            return;

        if (CinematicCamera.transform.parent != null)
            CinematicCamera.transform.SetParent(null);

        targetFieldOfView = CinematicCamera.targetFOV;

        if (useOcclusion && occluded)
            ChangeCamera(CameraMode.TPS);
    }

    /// <summary>
    /// Handles collision effects on the camera (e.g., shake, FOV change) in TPS mode.
    /// </summary>
    /// <param name="collision">Unity collision data from the vehicle's collision event.</param>
    public void Collision(Collision collision)
    {
        if (!TPSCollision)
            return;

        if (!enabled || !isRendering)
            return;

        if (cameraMode != CameraMode.TPS)
            return;

        Vector3 colRelVel = collision.relativeVelocity;
        colRelVel *= 1f - Mathf.Abs(Vector3.Dot(transform.up, collision.GetContact(0).normal));

        float cos = Mathf.Abs(Vector3.Dot(collision.GetContact(0).normal, colRelVel.normalized));

        if (colRelVel.magnitude * cos >= 5f)
        {
            collisionDirection = transform.InverseTransformDirection(colRelVel) / (30f);

            collisionPos -= collisionDirection * 5f;
            collisionRot = Quaternion.Euler(new Vector3(-collisionDirection.z * 10f, -collisionDirection.y * 10f, -collisionDirection.x * 10f));
            targetFieldOfView = actualCamera.fieldOfView - Mathf.Clamp(collision.relativeVelocity.magnitude, 0f, 15f);
        }
    }

    /// <summary>
    /// Resets the camera variables and reinitializes the mode.
    /// </summary>
    private void SetupForNewMode()
    {
        ResetCameraVariables();

        if (FixedCamera)
            FixedCamera.canTrackNow = false;

        switch (cameraMode)
        {
            case CameraMode.TPS:
                transform.SetParent(null);
                targetFieldOfView = TPSMinimumFOV;
                break;

            case CameraMode.FPS:
                if (!cameraTarget.HoodCamera)
                    break;
                transform.SetParent(cameraTarget.HoodCamera.transform, false);
                transform.localPosition = Vector3.zero;
                transform.localRotation = Quaternion.identity;
                targetFieldOfView = hoodCameraFOV;
                cameraTarget.HoodCamera.FixShake();
                break;

            case CameraMode.WHEEL:
                if (!cameraTarget.WheelCamera)
                    break;
                transform.SetParent(cameraTarget.WheelCamera.transform, false);
                transform.localPosition = Vector3.zero;
                transform.localRotation = Quaternion.identity;
                targetFieldOfView = wheelCameraFOV;
                break;

            case CameraMode.FIXED:
                if (FixedCamera)
                {
                    transform.SetParent(FixedCamera.transform, false);
                    FixedCamera.canTrackNow = true;
                }
                transform.localPosition = Vector3.zero;
                transform.localRotation = Quaternion.identity;
                targetFieldOfView = 60;
                break;

            case CameraMode.CINEMATIC:
                if (CinematicCamera && CinematicCamera.pivot)
                    transform.SetParent(CinematicCamera.pivot.transform, false);
                transform.localPosition = Vector3.zero;
                transform.localRotation = Quaternion.identity;
                targetFieldOfView = 30f;
                break;

            case CameraMode.TOP:
                transform.SetParent(null);
                targetFieldOfView = minimumOrtSize;
                pivot.transform.localPosition = Vector3.zero;
                pivot.transform.localRotation = Quaternion.identity;
                targetPosition = cameraTarget.playerVehicle.transform.position;
                targetPosition += cameraTarget.playerVehicle.transform.rotation * Vector3.forward * topCameraDistanceOffset;
                transform.position = cameraTarget.playerVehicle.transform.position;
                break;
        }
    }

    /// <summary>
    /// Resets the camera variables to default, called when changing camera modes.
    /// </summary>
    public void ResetCameraVariables()
    {
        TPSTiltAngle = 0f;

        collisionPos = Vector3.zero;
        collisionRot = Quaternion.identity;
        collisionDirection = Vector3.zero;

        if (actualCamera)
        {
            actualCamera.transform.localPosition = Vector3.zero;
            actualCamera.transform.localRotation = Quaternion.identity;
        }

        if (pivot)
        {
            pivot.transform.localPosition = collisionPos;
            pivot.transform.localRotation = collisionRot;
        }

        orbitX = TPSStartRotation.y;
        orbitY = TPSStartRotation.x;

        if (TPSStartRotation != Vector3.zero)
            TPSStartRotation = Vector3.zero;

        if (actualCamera)
            actualCamera.orthographic = false;

        occluded = false;

        orbitResetTimer = 0f;
        orbitX = 0f;
        orbitY = 0f;
        direction = 1;
        lookBackNow = false;
        autoChangeCameraTimer = 0f;

        acceleration = Vector3.zero;
        lastVelocity = Vector3.zero;

        targetPosition = Vector3.zero;
    }

    /// <summary>
    /// Toggles the camera on/off by enabling or disabling its rendering.
    /// </summary>
    /// <param name="state">True to enable camera rendering, false to disable it.</param>
    public void ToggleCamera(bool state)
    {
        isRendering = state;
    }

    /// <summary>
    /// Adjusts camera position if an occlusion is detected by a raycast.
    /// </summary>
    /// <param name="targetFollow">The world-space position of the target (vehicle) to linecast from.</param>
    private void OccludeRay(Vector3 targetFollow)
    {
        RaycastHit wallHit = new RaycastHit();

        if (Physics.Linecast(targetFollow, transform.position, out wallHit, occlusionLayerMask))
        {
            if (!wallHit.collider.isTrigger && !wallHit.transform.IsChildOf(cameraTarget.playerVehicle.transform))
            {
                Vector3 occludedPosition = new Vector3(wallHit.point.x + wallHit.normal.x * .2f, wallHit.point.y + wallHit.normal.y * .2f,
                    wallHit.point.z + wallHit.normal.z * .2f);

                transform.position = occludedPosition;
            }
        }
    }

    /// <summary>
    /// Checks if the camera is currently occluded by a collider.
    /// </summary>
    private void CheckIfOccluded()
    {
        RaycastHit wallHit = new RaycastHit();

        if (Physics.Linecast(cameraTarget.playerVehicle.transform.position, transform.position, out wallHit, occlusionLayerMask))
        {
            if (!wallHit.collider.isTrigger && !wallHit.transform.IsChildOf(cameraTarget.playerVehicle.transform))
                occluded = true;
        }
    }

    /// <summary>
    /// Automatically adjusts TPSDistance and TPSHeight based on the target vehicle's bounds.
    /// </summary>
    public IEnumerator AutoFocus()
    {
        float timer = 3f;
        float bounds = RCC_GetBounds.MaxBoundsExtent(cameraTarget.playerVehicle.transform);

        while (timer > 0f)
        {
            timer -= Time.deltaTime;
            TPSDistance = Mathf.Lerp(TPSDistance, bounds * 2.7f, Time.deltaTime * 2f);
            TPSHeight = Mathf.Lerp(TPSHeight, bounds * .65f, Time.deltaTime * 2f);
            yield return null;
        }

        TPSDistance = bounds * 2.7f;
        TPSHeight = bounds * .65f;
    }

    /// <summary>
    /// Automatically adjusts TPSDistance and TPSHeight based on the specified transform's bounds.
    /// </summary>
    /// <param name="transformBounds">The transform whose bounds are used to calculate camera distance and height.</param>
    /// <returns>Coroutine enumerator that runs over 3 seconds.</returns>
    public IEnumerator AutoFocus(Transform transformBounds)
    {
        float timer = 3f;
        float bounds = RCC_GetBounds.MaxBoundsExtent(transformBounds);

        while (timer > 0f)
        {
            timer -= Time.deltaTime;
            TPSDistance = Mathf.Lerp(TPSDistance, bounds * 2.7f, Time.deltaTime * 2f);
            TPSHeight = Mathf.Lerp(TPSHeight, bounds * .65f, Time.deltaTime * 2f);
            yield return null;
        }

        TPSDistance = bounds * 2.7f;
        TPSHeight = bounds * .65f;
    }

    /// <summary>
    /// Automatically adjusts TPSDistance and TPSHeight based on the combined bounds of two transforms.
    /// </summary>
    /// <param name="transformBounds1">The first transform whose bounds contribute to the calculation.</param>
    /// <param name="transformBounds2">The second transform whose bounds contribute to the calculation.</param>
    /// <returns>Coroutine enumerator that runs over 3 seconds.</returns>
    public IEnumerator AutoFocus(Transform transformBounds1, Transform transformBounds2)
    {
        float timer = 3f;
        float bounds = (RCC_GetBounds.MaxBoundsExtent(transformBounds1) + RCC_GetBounds.MaxBoundsExtent(transformBounds2));

        while (timer > 0f)
        {
            timer -= Time.deltaTime;
            TPSDistance = Mathf.Lerp(TPSDistance, bounds * 2.7f, Time.deltaTime * 2f);
            TPSHeight = Mathf.Lerp(TPSHeight, bounds * .65f, Time.deltaTime * 2f);
            yield return null;
        }

        TPSDistance = bounds * 2.7f;
        TPSHeight = bounds * .65f;
    }

    /// <summary>
    /// Automatically adjusts TPSDistance and TPSHeight based on the combined bounds of three transforms.
    /// </summary>
    /// <param name="transformBounds1">The first transform whose bounds contribute to the calculation.</param>
    /// <param name="transformBounds2">The second transform whose bounds contribute to the calculation.</param>
    /// <param name="transformBounds3">The third transform whose bounds contribute to the calculation.</param>
    /// <returns>Coroutine enumerator that runs over 3 seconds.</returns>
    public IEnumerator AutoFocus(Transform transformBounds1, Transform transformBounds2, Transform transformBounds3)
    {
        float timer = 3f;
        float bounds = (RCC_GetBounds.MaxBoundsExtent(transformBounds1) + RCC_GetBounds.MaxBoundsExtent(transformBounds2) +
                        RCC_GetBounds.MaxBoundsExtent(transformBounds3));

        while (timer > 0f)
        {
            timer -= Time.deltaTime;
            TPSDistance = Mathf.Lerp(TPSDistance, bounds * 2.7f, Time.deltaTime * 2f);
            TPSHeight = Mathf.Lerp(TPSHeight, bounds * .65f, Time.deltaTime * 2f);
            yield return null;
        }

        TPSDistance = bounds * 2.7f;
        TPSHeight = bounds * .65f;
    }

    /// <summary>
    /// Checks if the current render pipeline is URP (Universal Render Pipeline).
    /// </summary>
    /// <returns>True if the active render pipeline is URP, false otherwise.</returns>
    private bool IsURP()
    {
        RenderPipelineAsset activePipeline = GraphicsSettings.currentRenderPipeline;

        if (activePipeline != null && activePipeline.GetType().ToString().Contains("Universal"))
            return true;

        return false;
    }

    private void OnDisable()
    {
        // Clear the singleton reference if it points to this instance.
        if (Instance == this)
            Instance = null;

        RCC_Events.OnRCCPlayerCollision -= RCC_CarControllerV4_OnRCCPlayerCollision;

        // Listening input events.
        RCC_Events.OnChangeCamera -= RCC_InputManager_OnChangeCamera;
        RCC_Events.OnLookBack -= RCC_InputManager_OnLookBack;
    }

    private void Reset()
    {
        //  If pivot of the camera is not found, create it.
        if (transform.Find("Pivot"))
            pivot = transform.Find("Pivot").gameObject;

        if (!pivot)
        {
            pivot = new GameObject("Pivot");

            pivot.transform.SetParent(transform);
            pivot.transform.localPosition = Vector3.zero;
            pivot.transform.localRotation = Quaternion.identity;
        }

        Camera foundCamera = GetComponentInChildren<Camera>();

        if (foundCamera)
            Destroy(foundCamera);

        GameObject newCamera = new GameObject("Camera");
        newCamera.transform.SetParent(pivot.transform);
        newCamera.transform.localPosition = Vector3.zero;
        newCamera.transform.localRotation = Quaternion.identity;
        newCamera.AddComponent<Camera>();
        newCamera.AddComponent<AudioListener>();
        newCamera.gameObject.tag = "MainCamera";
    }
}