//----------------------------------------------
//            Realistic Car Controller
//
// Copyright © 2014 - 2025 BoneCracker Games
// https://www.bonecrackergames.com
// Ekrem Bugra Ozdoganlar
//
//----------------------------------------------


using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;
using TMPro;

#if PHOTON_UNITY_NETWORKING
using Photon.Pun;
#endif

/// <summary>
/// Manages the main RCC dashboard canvas, updating text labels, indicator images, and panel visibility
/// based on the active player vehicle's state. Supports multiple display modes (Full, Customization,
/// TopButtonsOnly, Off) and optional Photon networking integration.
/// </summary>
[RequireComponent(typeof(RCC_DashboardInputs))]
public class RCC_UI_DashboardDisplay : RCC_Core {

    /// <summary>
    /// Cached reference to the <see cref="RCC_DashboardInputs"/> component that provides vehicle data.
    /// </summary>
    private RCC_DashboardInputs inputs;

    /// <summary>
    /// Lazy-loaded accessor for the <see cref="RCC_DashboardInputs"/> component on this GameObject.
    /// </summary>
    private RCC_DashboardInputs Inputs {

        get {

            if (inputs == null)
                inputs = GetComponent<RCC_DashboardInputs>();

            return inputs;

        }

    }

    /// <summary>
    /// Current display mode controlling which UI panels are visible.
    /// </summary>
    [Tooltip("Current display mode controlling which UI panels are visible.")]
    public DisplayType displayType = DisplayType.Full;

    /// <summary>
    /// Defines the available dashboard display modes.
    /// </summary>
    public enum DisplayType { Full, Customization, TopButtonsOnly, Off }

    /// <summary>
    /// Reference to the vehicle whose data is displayed. Auto-assigned if <see cref="findPlayerVehicleAuto"/> is true.
    /// </summary>
    [Tooltip("Reference to the vehicle whose data is displayed. Auto-assigned if <see cref=\"findPlayerVehicleAuto\"/> is true.")]
    public RCC_CarControllerV4 vehicle;

    /// <summary>
    /// When true, automatically assigns the active player vehicle from the scene manager each frame.
    /// </summary>
    [Tooltip("When true, automatically assigns the active player vehicle from the scene manager each frame.")]
    public bool findPlayerVehicleAuto = true;

#if PHOTON_UNITY_NETWORKING && RCC_PHOTON
    /// <summary>
    /// When true, enables Photon networking UI elements (Photon spawn button and car selection dropdown).
    /// </summary>
    [Tooltip("When true, enables Photon networking UI elements (Photon spawn button and car selection dropdown).")]
    public bool usePhotonWithThisCanvas = false;
#endif

    /// <summary>
    /// Panel containing the vehicle control buttons (throttle, brake, steering, etc.).
    /// </summary>
    [Tooltip("Panel containing the vehicle control buttons (throttle, brake, steering, etc.).")]
    [Header("Panels")]
    public GameObject controllerButtons;

    /// <summary>
    /// Panel containing the dashboard gauges (RPM, speed, fuel, etc.).
    /// </summary>
    [Tooltip("Panel containing the dashboard gauges (RPM, speed, fuel, etc.).")]
    public GameObject gauges;

    /// <summary>
    /// Panel containing the vehicle customization menu.
    /// </summary>
    [Tooltip("Panel containing the vehicle customization menu.")]
    public GameObject customizationMenu;

    /// <summary>
    /// Panel containing the options/settings menu.
    /// </summary>
    [Tooltip("Panel containing the options/settings menu.")]
    public GameObject optionsMenu;

    /// <summary>
    /// Local spawn button for spawning vehicles without networking.
    /// </summary>
    [Tooltip("Local spawn button for spawning vehicles without networking.")]
    [Header("Buttons")]
    public GameObject spawn;

    /// <summary>
    /// Photon-specific spawn button for spawning networked vehicles.
    /// </summary>
    [Tooltip("Photon-specific spawn button for spawning networked vehicles.")]
    public GameObject spawn_Photon;

    /// <summary>
    /// Text label displaying the current engine RPM.
    /// </summary>
    [Tooltip("Text label displaying the current engine RPM.")]
    [Header("Texts")]
    public TextMeshProUGUI RPMLabel;

    /// <summary>
    /// Text label displaying the current speed in KMH or MPH.
    /// </summary>
    [Tooltip("Text label displaying the current speed in KMH or MPH.")]
    public TextMeshProUGUI KMHLabel;

    /// <summary>
    /// Text label displaying the current gear (number, "R" for reverse, or "N" for neutral).
    /// </summary>
    [Tooltip("Text label displaying the current gear (number, \"R\" for reverse, or \"N\" for neutral).")]
    public TextMeshProUGUI GearLabel;

    /// <summary>
    /// Text label showing "Recording" or "Playing" status during replay recording/playback.
    /// </summary>
    [Tooltip("Text label showing \"Recording\" or \"Playing\" status during replay recording/playback.")]
    public TextMeshProUGUI recordingLabel;

    /// <summary>
    /// Indicator image for Anti-lock Braking System (ABS) status.
    /// </summary>
    [Tooltip("Indicator image for Anti-lock Braking System (ABS) status.")]
    [Header("Images")]
    public Image ABS;

    /// <summary>
    /// Indicator image for Electronic Stability Program (ESP) status.
    /// </summary>
    [Tooltip("Indicator image for Electronic Stability Program (ESP) status.")]
    public Image ESP;

    /// <summary>
    /// Indicator image for parking/handbrake status.
    /// </summary>
    [Tooltip("Indicator image for parking/handbrake status.")]
    public Image Park;

    /// <summary>
    /// Indicator image for headlight status (low or high beam).
    /// </summary>
    [Tooltip("Indicator image for headlight status (low or high beam).")]
    public Image Headlights;

    /// <summary>
    /// Indicator image for the left turn signal.
    /// </summary>
    [Tooltip("Indicator image for the left turn signal.")]
    public Image leftIndicator;

    /// <summary>
    /// Indicator image for the right turn signal.
    /// </summary>
    [Tooltip("Indicator image for the right turn signal.")]
    public Image rightIndicator;

    /// <summary>
    /// Warning indicator image that turns red when engine heat reaches critical level.
    /// </summary>
    [Tooltip("Warning indicator image that turns red when engine heat reaches critical level.")]
    public Image heatIndicator;

    /// <summary>
    /// Warning indicator image that turns red when fuel level is critically low.
    /// </summary>
    [Tooltip("Warning indicator image that turns red when fuel level is critically low.")]
    public Image fuelIndicator;

    /// <summary>
    /// Warning indicator image that turns red when engine RPM is near the redline.
    /// </summary>
    [Tooltip("Warning indicator image that turns red when engine RPM is near the redline.")]
    public Image rpmIndicator;

    /// <summary>
    /// Color used for active/engaged indicator images (e.g., ABS on, ESP on).
    /// </summary>
    [Tooltip("Color used for active/engaged indicator images (e.g., ABS on, ESP on).")]
    [Header("Colors")]
    public Color color_On = Color.yellow;

    /// <summary>
    /// Color used for inactive/disengaged indicator images.
    /// </summary>
    [Tooltip("Color used for inactive/disengaged indicator images.")]
    public Color color_Off = Color.white;

    /// <summary>
    /// Dropdown for selecting the mobile controller type. Disabled when mobile controllers are not enabled.
    /// </summary>
    [Tooltip("Dropdown for selecting the mobile controller type. Disabled when mobile controllers are not enabled.")]
    [Header("Dropdowns")]
    public Dropdown mobileControllersDropdown;

    /// <summary>
    /// Dropdown for selecting which vehicle to spawn (local mode).
    /// </summary>
    [Tooltip("Dropdown for selecting which vehicle to spawn (local mode).")]
    public Dropdown carSelectionDropdown;

    /// <summary>
    /// Dropdown for selecting which vehicle to spawn (Photon networking mode).
    /// </summary>
    [Tooltip("Dropdown for selecting which vehicle to spawn (Photon networking mode).")]
    public Dropdown carSelectionDropdown_Photon;

    private void Awake() {

#if PHOTON_UNITY_NETWORKING && RCC_PHOTON

        if (usePhotonWithThisCanvas) {

            if (carSelectionDropdown)
                carSelectionDropdown.gameObject.SetActive(false);

            if (carSelectionDropdown_Photon)
                carSelectionDropdown_Photon.gameObject.SetActive(true);

            if (spawn)
                spawn.SetActive(false);

            if (spawn_Photon)
                spawn_Photon.SetActive(true);

            StartCoroutine(EnableOptionsMenuOnPhotonConnect());

        } else {

            if (carSelectionDropdown)
                carSelectionDropdown.gameObject.SetActive(true);

            if (carSelectionDropdown_Photon)
                carSelectionDropdown_Photon.gameObject.SetActive(false);

            if (spawn)
                spawn.SetActive(true);

            if (spawn_Photon)
                spawn_Photon.SetActive(false);

        }

#else

        if (carSelectionDropdown)
            carSelectionDropdown.gameObject.SetActive(true);

#endif

    }
#if PHOTON_UNITY_NETWORKING && RCC_PHOTON

    private IEnumerator EnableOptionsMenuOnPhotonConnect() {

        if (optionsMenu)
            optionsMenu.SetActive(false);

        yield return new WaitUntil(() => PhotonNetwork.InRoom);

        if (optionsMenu)
            optionsMenu.SetActive(true);

    }

#endif

    private void Update() {

        if (mobileControllersDropdown)
            mobileControllersDropdown.interactable = Settings.mobileControllerEnabled;

        //  Enabling / disabling corresponding elements related to choosen display type.
        switch (displayType) {

            case DisplayType.Full:

                if (controllerButtons && !controllerButtons.activeSelf)
                    controllerButtons.SetActive(true);

                if (gauges && !gauges.activeSelf)
                    gauges.SetActive(true);

                if (customizationMenu && customizationMenu.activeSelf)
                    customizationMenu.SetActive(false);

                break;

            case DisplayType.Customization:

                if (controllerButtons && controllerButtons.activeSelf)
                    controllerButtons.SetActive(false);

                if (gauges && gauges.activeSelf)
                    gauges.SetActive(false);

                if (customizationMenu && !customizationMenu.activeSelf)
                    customizationMenu.SetActive(true);

                break;

            case DisplayType.TopButtonsOnly:

                if (controllerButtons && controllerButtons.activeSelf)
                    controllerButtons.SetActive(false);

                if (gauges && gauges.activeSelf)
                    gauges.SetActive(false);

                if (customizationMenu && customizationMenu.activeSelf)
                    customizationMenu.SetActive(false);

                break;

            case DisplayType.Off:

                if (controllerButtons && controllerButtons.activeSelf)
                    controllerButtons.SetActive(false);

                if (gauges && gauges.activeSelf)
                    gauges.SetActive(false);

                if (customizationMenu && customizationMenu.activeSelf)
                    customizationMenu.SetActive(false);

                break;

        }

    }

    private void LateUpdate() {

        //  If inputs are not enabled yet, disable it and return.
        if (!Inputs.enabled)
            return;

        if (findPlayerVehicleAuto && RCCSceneManager.activePlayerVehicle)
            vehicle = RCCSceneManager.activePlayerVehicle;

        if (!vehicle)
            return;

        if (RPMLabel)
            RPMLabel.text = Inputs.RPM.ToString("0");

        if (KMHLabel) {

            if (Settings.units == RCC_Settings.Units.KMH)
                KMHLabel.text = Inputs.KMH.ToString("0");
            else
                KMHLabel.text = (Inputs.KMH * 0.62f).ToString("0");

        }

        if (GearLabel) {

            if (!Inputs.NGear && !Inputs.changingGear)
                GearLabel.text = Inputs.direction == 1 ? (Inputs.Gear + 1).ToString("0") : "R";
            else
                GearLabel.text = "N";

        }

        if (recordingLabel) {

            switch (RCCSceneManager.recordMode) {

                case RCC_SceneManager.RecordMode.Neutral:

                    if (recordingLabel.gameObject.activeSelf)
                        recordingLabel.gameObject.SetActive(false);

                    recordingLabel.text = "";

                    break;

                case RCC_SceneManager.RecordMode.Play:

                    if (!recordingLabel.gameObject.activeSelf)
                        recordingLabel.gameObject.SetActive(true);

                    recordingLabel.text = "Playing";
                    recordingLabel.color = Color.green;

                    break;

                case RCC_SceneManager.RecordMode.Record:

                    if (!recordingLabel.gameObject.activeSelf)
                        recordingLabel.gameObject.SetActive(true);

                    recordingLabel.text = "Recording";
                    recordingLabel.color = Color.red;

                    break;

            }

        }

        if (ABS)
            ABS.color = Inputs.ABS == true ? color_On : color_Off;

        if (ESP)
            ESP.color = Inputs.ESP == true ? color_On : color_Off;

        if (Park)
            Park.color = Inputs.Park == true ? Color.red : color_Off;

        if (Headlights)
            Headlights.color = Inputs.Headlights == true ? Color.green : color_Off;

        if (heatIndicator)
            heatIndicator.color = vehicle.engineHeat >= 100f ? Color.red : new Color(.1f, 0f, 0f);

        if (fuelIndicator)
            fuelIndicator.color = vehicle.fuelTank < 10f ? Color.red : new Color(.1f, 0f, 0f);

        if (rpmIndicator)
            rpmIndicator.color = vehicle.engineRPM >= vehicle.maxEngineRPM - 500f ? Color.red : new Color(.1f, 0f, 0f);

        if (leftIndicator && rightIndicator) {

            switch (Inputs.indicators) {

                case RCC_CarControllerV4.IndicatorsOn.Left:
                    leftIndicator.color = new Color(1f, .5f, 0f);
                    rightIndicator.color = new Color(.5f, .25f, 0f);
                    break;
                case RCC_CarControllerV4.IndicatorsOn.Right:
                    leftIndicator.color = new Color(.5f, .25f, 0f);
                    rightIndicator.color = new Color(1f, .5f, 0f);
                    break;
                case RCC_CarControllerV4.IndicatorsOn.All:
                    leftIndicator.color = new Color(1f, .5f, 0f);
                    rightIndicator.color = new Color(1f, .5f, 0f);
                    break;
                case RCC_CarControllerV4.IndicatorsOn.Off:
                    leftIndicator.color = new Color(.5f, .25f, 0f);
                    rightIndicator.color = new Color(.5f, .25f, 0f);
                    break;

            }

        }

    }

    /// <summary>
    /// Sets the current display mode of the dashboard, controlling which UI panels are visible.
    /// </summary>
    /// <param name="_displayType">The display mode to switch to.</param>
    public void SetDisplayType(DisplayType _displayType) {

        displayType = _displayType;

    }

}
