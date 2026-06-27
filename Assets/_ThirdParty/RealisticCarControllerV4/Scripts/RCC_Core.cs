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
using UnityEngine.Audio;

/// <summary>
/// Base class for all RCC components. Provides static access to global settings, ground materials,
/// the scene manager, and a reference to the parent vehicle controller. Also contains utility methods
/// for creating audio sources, wheel colliders, and applying behavior overrides.
/// </summary>
public class RCC_Core : MonoBehaviour {

    /// <summary>
    /// Resets cached static references when entering Play mode. Required for projects with domain reload disabled.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticReferences() {
        settings = null;
        groundMaterials = null;
    }

    /// <summary>
    /// Cached reference to the global RCC settings ScriptableObject.
    /// </summary>
    private static RCC_Settings settings;

    /// <summary>
    /// Global RCC settings instance. Loads from Resources if not yet cached.
    /// </summary>
    public static RCC_Settings Settings {

        get {

            if (settings == null)
                settings = RCC_Settings.Instance;

            return settings;

        }

    }

    /// <summary>
    /// Cached reference to the global ground materials ScriptableObject.
    /// </summary>
    private static RCC_GroundMaterials groundMaterials;

    /// <summary>
    /// Global ground materials instance defining surface friction properties. Loads from Resources if not yet cached.
    /// </summary>
    public static RCC_GroundMaterials GroundMaterials {

        get {

            if (groundMaterials == null)
                groundMaterials = RCC_GroundMaterials.Instance;

            return groundMaterials;

        }

    }

    /// <summary>
    /// Cached reference to the parent vehicle controller.
    /// </summary>
    private RCC_CarControllerV4 carController;

    /// <summary>
    /// The parent RCC_CarControllerV4 this component belongs to. Found by walking up the transform hierarchy.
    /// </summary>
    public RCC_CarControllerV4 CarController {

        get {

            if (carController == null)
                carController = GetComponentInParent<RCC_CarControllerV4>(true);

            return carController;

        }
        set {

            carController = value;

        }

    }

    /// <summary>
    /// Cached reference to the RCC scene manager singleton.
    /// </summary>
    private static RCC_SceneManager sceneManager;

    /// <summary>
    /// The RCC scene manager singleton. Manages active vehicles, camera, and recording state.
    /// </summary>
    public static RCC_SceneManager RCCSceneManager {

        get {

            if (sceneManager == null)
                sceneManager = RCC_SceneManager.Instance;

            return sceneManager;

        }

    }

    #region Create AudioSource

    /// <summary>
    /// Creates a new child GameObject containing an AudioSource with the specified settings, parented under an "All Audio Sources" container on the target GameObject. Routes output through the supplied mixer group. Spatial blend is 3D unless both distances are zero.
    /// </summary>
    /// <param name="audioMixer">Audio mixer group to route the source's output through.</param>
    /// <param name="go">GameObject under which the audio source will be parented.</param>
    /// <param name="audioName">Name assigned to the new audio source GameObject.</param>
    /// <param name="minDistance">Minimum 3D distance at which the source is heard at full volume.</param>
    /// <param name="maxDistance">Maximum 3D distance at which the source is audible.</param>
    /// <param name="volume">Playback volume (0-1).</param>
    /// <param name="audioClip">Audio clip assigned to the source.</param>
    /// <param name="loop">If true, the clip loops.</param>
    /// <param name="playNow">If true, the source begins playing immediately.</param>
    /// <param name="destroyAfterFinished">If true, the source GameObject is destroyed once playback ends.</param>
    /// <returns>The newly created AudioSource.</returns>
	public static AudioSource NewAudioSource(AudioMixerGroup audioMixer, GameObject go, string audioName, float minDistance, float maxDistance, float volume, AudioClip audioClip, bool loop, bool playNow, bool destroyAfterFinished) {

        GameObject audioSourceObject = new GameObject(audioName);

        if (go.transform.Find("All Audio Sources")) {

            audioSourceObject.transform.SetParent(go.transform.Find("All Audio Sources"));

        } else {

            GameObject allAudioSources = new GameObject("All Audio Sources");
            allAudioSources.transform.SetParent(go.transform, false);
            audioSourceObject.transform.SetParent(allAudioSources.transform, false);

        }

        audioSourceObject.transform.SetPositionAndRotation(go.transform.position, go.transform.rotation);

        audioSourceObject.AddComponent<AudioSource>();
        AudioSource source = audioSourceObject.GetComponent<AudioSource>();

        if (audioMixer)
            source.outputAudioMixerGroup = audioMixer;

        //audioSource.GetComponent<AudioSource>().priority =1;
        source.minDistance = minDistance;
        source.maxDistance = maxDistance;
        source.volume = volume;
        source.clip = audioClip;
        source.loop = loop;
        source.dopplerLevel = .5f;
        source.ignoreListenerPause = false;
        source.ignoreListenerVolume = false;

        if (minDistance == 0 && maxDistance == 0)
            source.spatialBlend = 0f;
        else
            source.spatialBlend = 1f;

        if (playNow) {

            source.playOnAwake = true;
            source.Play();

        } else {

            source.playOnAwake = false;

        }

        if (destroyAfterFinished) {

            if (audioClip)
                Destroy(audioSourceObject, audioClip.length);
            else
                Destroy(audioSourceObject);

        }

        return source;

    }

    /// <summary>
    /// Creates a new child GameObject containing an AudioSource with the specified settings, parented under an "All Audio Sources" container on the target GameObject. Does not assign an audio mixer group. Spatial blend is 3D unless both distances are zero.
    /// </summary>
    /// <param name="go">GameObject under which the audio source will be parented.</param>
    /// <param name="audioName">Name assigned to the new audio source GameObject.</param>
    /// <param name="minDistance">Minimum 3D distance at which the source is heard at full volume.</param>
    /// <param name="maxDistance">Maximum 3D distance at which the source is audible.</param>
    /// <param name="volume">Playback volume (0-1).</param>
    /// <param name="audioClip">Audio clip assigned to the source.</param>
    /// <param name="loop">If true, the clip loops.</param>
    /// <param name="playNow">If true, the source begins playing immediately.</param>
    /// <param name="destroyAfterFinished">If true, the source GameObject is destroyed once playback ends.</param>
    /// <returns>The newly created AudioSource.</returns>
    public static AudioSource NewAudioSource(GameObject go, string audioName, float minDistance, float maxDistance, float volume, AudioClip audioClip, bool loop, bool playNow, bool destroyAfterFinished) {

        GameObject audioSourceObject = new GameObject(audioName);

        if (go.transform.Find("All Audio Sources")) {

            audioSourceObject.transform.SetParent(go.transform.Find("All Audio Sources"));

        } else {

            GameObject allAudioSources = new GameObject("All Audio Sources");
            allAudioSources.transform.SetParent(go.transform, false);
            audioSourceObject.transform.SetParent(allAudioSources.transform, false);

        }

        audioSourceObject.transform.SetPositionAndRotation(go.transform.position, go.transform.rotation);

        audioSourceObject.AddComponent<AudioSource>();
        AudioSource source = audioSourceObject.GetComponent<AudioSource>();

        //audioSource.GetComponent<AudioSource>().priority =1;
        source.minDistance = minDistance;
        source.maxDistance = maxDistance;
        source.volume = volume;
        source.clip = audioClip;
        source.loop = loop;
        source.dopplerLevel = .5f;

        if (minDistance == 0 && maxDistance == 0)
            source.spatialBlend = 0f;
        else
            source.spatialBlend = 1f;

        if (playNow) {

            source.playOnAwake = true;
            source.Play();

        } else {

            source.playOnAwake = false;

        }

        if (destroyAfterFinished) {

            if (audioClip)
                Destroy(audioSourceObject, audioClip.length);
            else
                Destroy(audioSourceObject);

        }

        return source;

    }

    /// <summary>
    /// Creates a new child GameObject containing an AudioSource with the specified settings at a custom local position, parented under an "All Audio Sources" container on the target GameObject. Routes output through the supplied mixer group. Spatial blend is 3D unless both distances are zero.
    /// </summary>
    /// <param name="audioMixer">Audio mixer group to route the source's output through.</param>
    /// <param name="go">GameObject under which the audio source will be parented.</param>
    /// <param name="localPosition">Local position offset applied to the new audio source GameObject.</param>
    /// <param name="audioName">Name assigned to the new audio source GameObject.</param>
    /// <param name="minDistance">Minimum 3D distance at which the source is heard at full volume.</param>
    /// <param name="maxDistance">Maximum 3D distance at which the source is audible.</param>
    /// <param name="volume">Playback volume (0-1).</param>
    /// <param name="audioClip">Audio clip assigned to the source.</param>
    /// <param name="loop">If true, the clip loops.</param>
    /// <param name="playNow">If true, the source begins playing immediately.</param>
    /// <param name="destroyAfterFinished">If true, the source GameObject is destroyed once playback ends.</param>
    /// <returns>The newly created AudioSource.</returns>
    public static AudioSource NewAudioSource(AudioMixerGroup audioMixer, GameObject go, Vector3 localPosition, string audioName, float minDistance, float maxDistance, float volume, AudioClip audioClip, bool loop, bool playNow, bool destroyAfterFinished) {

        GameObject audioSourceObject = new GameObject(audioName);

        if (go.transform.Find("All Audio Sources")) {

            audioSourceObject.transform.SetParent(go.transform.Find("All Audio Sources"));

        } else {

            GameObject allAudioSources = new GameObject("All Audio Sources");
            allAudioSources.transform.SetParent(go.transform, false);
            audioSourceObject.transform.SetParent(allAudioSources.transform, false);

        }

        audioSourceObject.transform.SetPositionAndRotation(go.transform.position, go.transform.rotation);
        audioSourceObject.transform.localPosition = localPosition;

        audioSourceObject.AddComponent<AudioSource>();
        AudioSource source = audioSourceObject.GetComponent<AudioSource>();

        if (audioMixer)
            source.outputAudioMixerGroup = audioMixer;

        //audioSource.GetComponent<AudioSource>().priority =1;
        source.minDistance = minDistance;
        source.maxDistance = maxDistance;
        source.volume = volume;
        source.clip = audioClip;
        source.loop = loop;
        source.dopplerLevel = .5f;

        if (minDistance == 0 && maxDistance == 0)
            source.spatialBlend = 0f;
        else
            source.spatialBlend = 1f;

        if (playNow) {

            source.playOnAwake = true;
            source.Play();

        } else {

            source.playOnAwake = false;

        }

        if (destroyAfterFinished) {

            if (audioClip)
                Destroy(audioSourceObject, audioClip.length);
            else
                Destroy(audioSourceObject);

        }

        return source;

    }

    /// <summary>
    /// Creates a new child GameObject containing an AudioSource with the specified settings at a custom local position, parented under an "All Audio Sources" container on the target GameObject. Does not assign an audio mixer group. Spatial blend is 3D unless both distances are zero.
    /// </summary>
    /// <param name="go">GameObject under which the audio source will be parented.</param>
    /// <param name="localPosition">Local position offset applied to the new audio source GameObject.</param>
    /// <param name="audioName">Name assigned to the new audio source GameObject.</param>
    /// <param name="minDistance">Minimum 3D distance at which the source is heard at full volume.</param>
    /// <param name="maxDistance">Maximum 3D distance at which the source is audible.</param>
    /// <param name="volume">Playback volume (0-1).</param>
    /// <param name="audioClip">Audio clip assigned to the source.</param>
    /// <param name="loop">If true, the clip loops.</param>
    /// <param name="playNow">If true, the source begins playing immediately.</param>
    /// <param name="destroyAfterFinished">If true, the source GameObject is destroyed once playback ends.</param>
    /// <returns>The newly created AudioSource.</returns>
    public static AudioSource NewAudioSource(GameObject go, Vector3 localPosition, string audioName, float minDistance, float maxDistance, float volume, AudioClip audioClip, bool loop, bool playNow, bool destroyAfterFinished) {

        GameObject audioSourceObject = new GameObject(audioName);

        if (go.transform.Find("All Audio Sources")) {

            audioSourceObject.transform.SetParent(go.transform.Find("All Audio Sources"));

        } else {

            GameObject allAudioSources = new GameObject("All Audio Sources");
            allAudioSources.transform.SetParent(go.transform, false);
            audioSourceObject.transform.SetParent(allAudioSources.transform, false);

        }

        audioSourceObject.transform.SetPositionAndRotation(go.transform.position, go.transform.rotation);
        audioSourceObject.transform.localPosition = localPosition;

        audioSourceObject.AddComponent<AudioSource>();
        AudioSource source = audioSourceObject.GetComponent<AudioSource>();

        //audioSource.GetComponent<AudioSource>().priority =1;
        source.minDistance = minDistance;
        source.maxDistance = maxDistance;
        source.volume = volume;
        source.clip = audioClip;
        source.loop = loop;
        source.dopplerLevel = .5f;

        if (minDistance == 0 && maxDistance == 0)
            source.spatialBlend = 0f;
        else
            source.spatialBlend = 1f;

        if (playNow) {

            source.playOnAwake = true;
            source.Play();

        } else {

            source.playOnAwake = false;

        }

        if (destroyAfterFinished) {

            if (audioClip)
                Destroy(audioSourceObject, audioClip.length);
            else
                Destroy(audioSourceObject);

        }

        return source;

    }

    /// <summary>
    /// Adds an AudioHighPassFilter to the AudioSource's GameObject with the specified cutoff frequency and resonance. Used for turbo sound shaping.
    /// </summary>
    /// <param name="source">Audio source to attach the high-pass filter to.</param>
    /// <param name="freq">Cutoff frequency in Hz.</param>
    /// <param name="level">High-pass resonance Q value.</param>
    public static void NewHighPassFilter(AudioSource source, float freq, int level) {

        if (source == null)
            return;

        AudioHighPassFilter highFilter = source.gameObject.AddComponent<AudioHighPassFilter>();
        highFilter.cutoffFrequency = freq;
        highFilter.highpassResonanceQ = level;

    }

    /// <summary>
    /// Adds an AudioLowPassFilter to the AudioSource's GameObject with the specified cutoff frequency. Used for muffled engine-off sounds.
    /// </summary>
    /// <param name="source">Audio source to attach the low-pass filter to.</param>
    /// <param name="freq">Cutoff frequency in Hz.</param>
    public static void NewLowPassFilter(AudioSource source, float freq) {

        if (source == null)
            return;

        AudioLowPassFilter lowFilter = source.gameObject.AddComponent<AudioLowPassFilter>();
        lowFilter.cutoffFrequency = freq;
        //      lowFilter.highpassResonanceQ = level;

    }

    #endregion

    #region Create WheelColliders

    /// <summary>
    /// Programmatically creates a "Wheel Colliders" container under the vehicle and spawns a WheelCollider + RCC_WheelCollider pair for each assigned wheel-mesh transform (front-left, front-right, rear-left, rear-right, and any extra rear wheels). Sizes each collider from the wheel mesh bounds and applies the default suspension and friction values from RCC_InitialSettings. Logs an error and aborts if any wheel mesh is missing or fewer than four colliders end up created.
    /// </summary>
    /// <param name="carController">Vehicle controller whose wheel-mesh transforms will receive WheelColliders.</param>
    public void CreateWheelColliders(RCC_CarControllerV4 carController) {

        // Creating a list for all wheel models.
        List<Transform> allWheelModels = new List<Transform>();
        allWheelModels.Add(carController.FrontLeftWheelTransform); allWheelModels.Add(carController.FrontRightWheelTransform); allWheelModels.Add(carController.RearLeftWheelTransform); allWheelModels.Add(carController.RearRightWheelTransform);

        // If we have additional rear wheels, add them too.
        if (carController.ExtraRearWheelsTransform != null && carController.ExtraRearWheelsTransform.Length > 0 && carController.ExtraRearWheelsTransform[0] != null) {

            foreach (Transform t in carController.ExtraRearWheelsTransform)
                allWheelModels.Add(t);

        }

        // If we don't have any wheelmodels, throw an error.
        bool missingWheelFound = false;

        for (int i = 0; i < allWheelModels.Count; i++) {

            if (allWheelModels[i] == null) {

                missingWheelFound = true;
                break;

            }

        }

        if (missingWheelFound) {

#if UNITY_EDITOR
            Debug.LogError("You haven't choosen your Wheel Models. Please select all of your Wheel Models before creating Wheel Colliders. Script needs to know their sizes and positions, aye?");
#endif
            return;

        }

        // Holding default rotation.
        Quaternion currentRotation = carController.transform.rotation;

        // Resetting rotation.
        carController.transform.rotation = Quaternion.identity;

        // Creating a new gameobject called Wheel Colliders for all Wheel Colliders, and parenting it to this gameobject.
        GameObject WheelColliders = new GameObject("Wheel Colliders");
        WheelColliders.transform.SetParent(carController.transform, false);
        WheelColliders.transform.localRotation = Quaternion.identity;
        WheelColliders.transform.localPosition = Vector3.zero;
        WheelColliders.transform.localScale = Vector3.one;

        // Creating WheelColliders.
        foreach (Transform wheel in allWheelModels) {

            GameObject wheelcollider = new GameObject(wheel.transform.name);

            //  Assigning position, rotation, and radius of the wheelcollider by taking bounds of the wheel model.
            wheelcollider.transform.SetPositionAndRotation(RCC_GetBounds.GetBoundsCenter(wheel.transform), carController.transform.rotation);
            wheelcollider.transform.name = wheel.transform.name;
            wheelcollider.transform.SetParent(WheelColliders.transform);
            wheelcollider.transform.localScale = Vector3.one;
            wheelcollider.AddComponent<WheelCollider>();
            wheelcollider.AddComponent<RCC_WheelCollider>();

            Bounds biggestBound = new Bounds();
            Renderer[] renderers = wheel.GetComponentsInChildren<Renderer>();

            foreach (Renderer render in renderers) {
                if (render != GetComponent<Renderer>()) {
                    if (render.bounds.size.z > biggestBound.size.z)
                        biggestBound = render.bounds;
                }
            }

            //wheelcollider.transform.position += carController.transform.up * (wheelcollider.GetComponent<WheelCollider>().suspensionDistance * (carController.transform.localScale.y * (1f - wheelcollider.GetComponent<WheelCollider>().suspensionSpring.targetPosition)));
            wheelcollider.GetComponent<WheelCollider>().radius = RCC_GetBounds.MaxBoundsExtent(wheel.transform) / carController.transform.localScale.y;
            JointSpring spring = wheelcollider.GetComponent<WheelCollider>().suspensionSpring;

            spring.spring = RCC_InitialSettings.Instance.suspensionSpring;
            spring.damper = RCC_InitialSettings.Instance.suspensionDamping;
            spring.targetPosition = .5f;

            wheelcollider.GetComponent<WheelCollider>().suspensionSpring = spring;
            wheelcollider.GetComponent<WheelCollider>().suspensionDistance = RCC_InitialSettings.Instance.suspensionDistance;
            wheelcollider.GetComponent<WheelCollider>().forceAppPointDistance = RCC_InitialSettings.Instance.forceAppPoint;
            wheelcollider.GetComponent<WheelCollider>().mass = 40f;
            wheelcollider.GetComponent<WheelCollider>().wheelDampingRate = 1f;

            WheelFrictionCurve sidewaysFriction;
            WheelFrictionCurve forwardFriction;

            sidewaysFriction = wheelcollider.GetComponent<WheelCollider>().sidewaysFriction;
            forwardFriction = wheelcollider.GetComponent<WheelCollider>().forwardFriction;

            forwardFriction.extremumSlip = RCC_InitialSettings.Instance.forwardExtremumSlip;
            forwardFriction.extremumValue = RCC_InitialSettings.Instance.forwardExtremumValue;
            forwardFriction.asymptoteSlip = RCC_InitialSettings.Instance.forwardAsymptoteSlip;
            forwardFriction.asymptoteValue = RCC_InitialSettings.Instance.forwardAsymptoteValue;
            forwardFriction.stiffness = RCC_InitialSettings.Instance.forwardStiffness;

            sidewaysFriction.extremumSlip = RCC_InitialSettings.Instance.sidewaysExtremumSlip;
            sidewaysFriction.extremumValue = RCC_InitialSettings.Instance.sidewaysExtremumValue;
            sidewaysFriction.asymptoteSlip = RCC_InitialSettings.Instance.sidewaysAsymptoteSlip;
            sidewaysFriction.asymptoteValue = RCC_InitialSettings.Instance.sidewaysAsymptoteValue;
            sidewaysFriction.stiffness = RCC_InitialSettings.Instance.sidewaysStiffness;

            wheelcollider.GetComponent<WheelCollider>().sidewaysFriction = sidewaysFriction;
            wheelcollider.GetComponent<WheelCollider>().forwardFriction = forwardFriction;

        }

        RCC_WheelCollider[] allWheelColliders = GetComponentsInChildren<RCC_WheelCollider>();

        if (allWheelColliders.Length < 4) {

#if UNITY_EDITOR
            Debug.LogError("CreateWheelColliders failed: Expected at least 4 wheel colliders but found " + allWheelColliders.Length + ". Please assign all 4 wheel model transforms.", carController);
#endif
            return;

        }

        carController.FrontLeftWheelCollider = allWheelColliders[0];
        carController.FrontRightWheelCollider = allWheelColliders[1];
        carController.RearLeftWheelCollider = allWheelColliders[2];
        carController.RearRightWheelCollider = allWheelColliders[3];

        if (carController.ExtraRearWheelsTransform != null) {

            carController.ExtraRearWheelsCollider = new RCC_WheelCollider[carController.ExtraRearWheelsTransform.Length];

            for (int i = 0; i < carController.ExtraRearWheelsTransform.Length; i++)
                carController.ExtraRearWheelsCollider[i] = allWheelColliders[i + 4];

        }

        carController.transform.rotation = currentRotation;

    }

    #endregion

    #region Set Behavior

    /// <summary>
    /// Applies the currently selected behavior preset from RCC_Settings to the given vehicle. Overrides driving assists (ABS, ESP, TCS, steering helper, traction helper, angular drag helper, COM assister), steering type and limits, counter-steering, anti-roll bars, gear shifting delay, and clamps the vehicle's per-field values to the preset's min/max ranges. Does nothing if no behavior preset is selected.
    /// </summary>
    /// <param name="carController">Vehicle controller to apply the active behavior preset to.</param>
    public void SetBehavior(RCC_CarControllerV4 carController) {

        if (RCC_Settings.Instance.selectedBehaviorType == null)
            return;

        RCC_Settings.BehaviorType currentBehaviorType = RCC_Settings.Instance.selectedBehaviorType;
        
        //Debug.LogError("SetBehavior: " + currentBehaviorType.behaviorName);

        carController.steeringHelper = currentBehaviorType.steeringHelper;
        carController.tractionHelper = currentBehaviorType.tractionHelper;
        carController.angularDragHelper = currentBehaviorType.angularDragHelper;
        carController.useSteeringLimiter = currentBehaviorType.limitSteering;
        carController.useSteeringSensitivity = currentBehaviorType.steeringSensitivity;
        carController.steeringSensitivityFactor = Mathf.Clamp(carController.steeringSensitivityFactor, currentBehaviorType.steeringSensitivityMinimum, currentBehaviorType.steeringSensitivityMaximum);
        carController.steeringType = currentBehaviorType.steeringType;
        carController.COMAssister = currentBehaviorType.comAssister;

        if (carController.steeringType == RCC_CarControllerV4.SteeringType.Curve)
            carController.steerAngleCurve = currentBehaviorType.steeringCurve;

        carController.useCounterSteering = currentBehaviorType.counterSteering;
        carController.ABS = currentBehaviorType.ABS;
        carController.ESP = currentBehaviorType.ESP;
        carController.TCS = currentBehaviorType.TCS;

        carController.highspeedsteerAngle = Mathf.Clamp(carController.highspeedsteerAngle, currentBehaviorType.highSpeedSteerAngleMinimum, currentBehaviorType.highSpeedSteerAngleMaximum);
        carController.highspeedsteerAngleAtspeed = Mathf.Clamp(carController.highspeedsteerAngleAtspeed, currentBehaviorType.highSpeedSteerAngleAtspeedMinimum, currentBehaviorType.highSpeedSteerAngleAtspeedMaximum);
        carController.counterSteeringFactor = Mathf.Clamp(carController.counterSteeringFactor, currentBehaviorType.counterSteeringMinimum, currentBehaviorType.counterSteeringMaximum);
        carController.counterSteerInput = 0f;

        carController.steerHelperAngularVelStrength = Mathf.Clamp(carController.steerHelperAngularVelStrength, currentBehaviorType.steerHelperAngularVelStrengthMinimum, currentBehaviorType.steerHelperAngularVelStrengthMaximum);
        carController.steerHelperLinearVelStrength = Mathf.Clamp(carController.steerHelperLinearVelStrength, currentBehaviorType.steerHelperLinearVelStrengthMinimum, currentBehaviorType.steerHelperLinearVelStrengthMaximum);

        carController.tractionHelperStrength = Mathf.Clamp(carController.tractionHelperStrength, currentBehaviorType.tractionHelperStrengthMinimum, currentBehaviorType.tractionHelperStrengthMaximum);
        carController.antiRollFrontHorizontal = Mathf.Clamp(carController.antiRollFrontHorizontal, currentBehaviorType.antiRollFrontHorizontalMinimum, Mathf.Infinity);
        carController.antiRollRearHorizontal = Mathf.Clamp(carController.antiRollRearHorizontal, currentBehaviorType.antiRollRearHorizontalMinimum, Mathf.Infinity);

        carController.gearShiftingDelay = Mathf.Clamp(carController.gearShiftingDelay, 0f, currentBehaviorType.gearShiftingDelayMaximum);
        carController.Rigid.angularDamping = currentBehaviorType.angularDrag;

        carController.angularDragHelperStrength = Mathf.Clamp(carController.angularDragHelperStrength, currentBehaviorType.angularDragHelperMinimum, currentBehaviorType.angularDragHelperMaximum);

    }

    #endregion

}

