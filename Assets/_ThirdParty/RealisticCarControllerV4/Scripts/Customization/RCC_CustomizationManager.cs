//----------------------------------------------
//            Realistic Car Controller
//
// Copyright © 2014 - 2025 BoneCracker Games
// https://www.bonecrackergames.com
// Ekrem Bugra Ozdoganlar
//
//----------------------------------------------


using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// Scene-wide customization manager singleton. Required in the scene for the customization system to function.
/// Provides high-level methods to paint, change wheels, upgrade stats, and swap spoilers/sirens on the active player vehicle. UI elements are optional.
/// </summary>
public class RCC_CustomizationManager : RCC_Singleton<RCC_CustomizationManager> {

    /// <summary>
    /// Current player vehicle's customizer component. Automatically updated when the active player vehicle changes.
    /// </summary>
    [Tooltip("Current player vehicle's customizer component. Automatically updated when the active player vehicle changes.")]
    public RCC_Customizer vehicle;

    /// <summary>
    /// Constructor. Attempts to find the active player vehicle's customizer on creation.
    /// </summary>
    public RCC_CustomizationManager() {

        if (RCCSceneManager.activePlayerVehicle)
            vehicle = RCCSceneManager.activePlayerVehicle.Customizer;

    }

    private void Awake()
    {
        m_Instance = this;
    }

    private void Start() {

        if (RCCSceneManager.activePlayerVehicle)
            vehicle = RCCSceneManager.activePlayerVehicle.Customizer;

    }

    private void OnEnable() {

        RCC_Events.OnVehicleChanged += RCC_SceneManager_OnVehicleChanged;

    }

    /// <summary>
    /// Called when the active player vehicle changes. Updates the customizer reference to the new vehicle.
    /// </summary>
    private void RCC_SceneManager_OnVehicleChanged() {

        if (!RCCSceneManager.activePlayerVehicle)
            return;

        vehicle = RCCSceneManager.activePlayerVehicle.Customizer;

    }

    /// <summary>
    /// Sets the new target as currentApplier.
    /// </summary>
    /// <param name="newTarget"></param>
    public void SetTarget(RCC_Customizer newTarget) {

        vehicle = newTarget;

    }

    /// <summary>
    /// Paints the target vehicle.
    /// </summary>
    /// <param name="color"></param>
    public void Paint(Color color) {

        // If no any player vehicle, return.
        if (!vehicle)
            return;

        if (!vehicle.PaintManager)
            return;

        vehicle.PaintManager.Paint(color);

        if (vehicle.SpoilerManager && vehicle.SpoilerManager.paintSpoilers)
            vehicle.SpoilerManager.Paint(color);

    }

    /// <summary>
    /// Changes the wheels of the target vehicle.
    /// </summary>
    /// <param name="wheelIndex"></param>
    public void ChangeWheels(int wheelIndex) {

        // If no any player vehicle, return.
        if (!vehicle)
            return;

        if (!vehicle.WheelManager)
            return;

        vehicle.WheelManager.UpdateWheel(wheelIndex);

    }

    /// <summary>
    /// Upgrades speed of the target vehicle.
    /// </summary>
    public void UpgradeSpeed() {

        // If no any player vehicle, return.
        if (!vehicle)
            return;

        if (!vehicle.UpgradeManager)
            return;

        vehicle.UpgradeManager.UpgradeEngine();

    }

    /// <summary>
    /// Upgrades handling of the target vehicle.
    /// </summary>
    public void UpgradeHandling() {

        // If no any player vehicle, return.
        if (!vehicle)
            return;

        if (!vehicle.UpgradeManager)
            return;

        vehicle.UpgradeManager.UpgradeHandling();

    }

    /// <summary>
    /// Upgrades brakes of the target vehicle.
    /// </summary>
    public void UpgradeBrake() {

        // If no any player vehicle, return.
        if (!vehicle)
            return;

        if (!vehicle.UpgradeManager)
            return;

        vehicle.UpgradeManager.UpgradeBrake();

    }

    /// <summary>
    /// Changes the spoiler of the target vehicle.
    /// </summary>
    /// <param name="index"></param>
    public void Spoiler(int index) {

        // If no any player vehicle, return.
        if (!vehicle)
            return;

        if (!vehicle.SpoilerManager)
            return;

        vehicle.SpoilerManager.Upgrade(index);

    }

    /// <summary>
    /// Changes the siren of the target vehicle.
    /// </summary>
    /// <param name="index"></param>
    public void Siren(int index) {

        // If no any player vehicle, return.
        if (!vehicle)
            return;

        if (!vehicle.SirenManager)
            return;

        vehicle.SirenManager.Upgrade(index);

    }

    private void OnDisable() {

        RCC_Events.OnVehicleChanged -= RCC_SceneManager_OnVehicleChanged;

    }

}
