using System;
using Save;
using TMPro;
using UIManager;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Vehicles;

namespace UI
{
    /// <summary>
    /// Garage / main menu screen. Browses the vehicle roster (left/right arrows), buys the displayed
    /// vehicle, jumps into customization, and starts the game.
    ///
    /// This screen is the VIEW only: all 3D spawning and prefab load/unload live in
    /// <see cref="GarageManager"/>. The screen asks it to switch (left/right) and listens to
    /// <see cref="GarageManager.DisplayedVehicleChanged"/> to keep the buttons and labels in sync.
    /// Vehicle data comes from <see cref="VehicleContainer"/>; ownership / currency from <see cref="SaveManager"/>.
    /// </summary>
    public class MainMenuScreen : ScreenBase
    {
        [Header("Garage")]
        [SerializeField] private GarageManager garageManager;

        [Header("Vehicle Navigation")]
        [SerializeField] private Button leftArrowButton;
        [SerializeField] private Button rightArrowButton;
        [SerializeField] private TMP_Text vehicleNameText;
        [SerializeField] private GameObject lockedBadge;

        [Header("Top Bar")]
        [SerializeField] private TMP_Text coinAmountText;
        [SerializeField] private Button settingsButton;

        [Header("Actions")]
        [SerializeField] private Button playButton;
        [SerializeField] private Button customizeButton;
        [SerializeField] private Button buyButton;
        [SerializeField] private TMP_Text buyPriceText;
        [SerializeField] private GameObject buyCoinIcon;

        [Header("Config")]
        [SerializeField] private string gameSceneName = "Game";

        /// <summary>Raised when the player taps Customize (no customization screen exists yet).</summary>
        public event Action CustomizeRequested;
        /// <summary>Raised when the player taps Settings (no settings screen exists yet).</summary>
        public event Action SettingsRequested;

        protected override void Awake()
        {
            base.Awake();

            if (leftArrowButton) leftArrowButton.onClick.AddListener(OnPreviousClicked);
            if (rightArrowButton) rightArrowButton.onClick.AddListener(OnNextClicked);
            if (playButton) playButton.onClick.AddListener(OnPlayClicked);
            if (customizeButton) customizeButton.onClick.AddListener(OnCustomizeClicked);
            if (buyButton) buyButton.onClick.AddListener(OnBuyClicked);
            if (settingsButton) settingsButton.onClick.AddListener(OnSettingsClicked);

            if (garageManager)
                garageManager.DisplayedVehicleChanged += OnDisplayedVehicleChanged;
        }

        private void OnDestroy()
        {
            if (leftArrowButton) leftArrowButton.onClick.RemoveListener(OnPreviousClicked);
            if (rightArrowButton) rightArrowButton.onClick.RemoveListener(OnNextClicked);
            if (playButton) playButton.onClick.RemoveListener(OnPlayClicked);
            if (customizeButton) customizeButton.onClick.RemoveListener(OnCustomizeClicked);
            if (buyButton) buyButton.onClick.RemoveListener(OnBuyClicked);
            if (settingsButton) settingsButton.onClick.RemoveListener(OnSettingsClicked);

            if (garageManager)
                garageManager.DisplayedVehicleChanged -= OnDisplayedVehicleChanged;
        }

        protected override void OnBeforeShowing(bool immediate, object uiData = null)
        {
            base.OnBeforeShowing(immediate, uiData);

            // Sync the UI to whatever the garage is currently showing when the screen opens.
            Refresh(CurrentVehicle);
        }

        // ---------------------------------------------------------------------
        // Button handlers
        // ---------------------------------------------------------------------

        private void OnNextClicked()
        {
            if (garageManager) garageManager.ShowNext();
        }

        private void OnPreviousClicked()
        {
            if (garageManager) garageManager.ShowPrevious();
        }

        private void OnDisplayedVehicleChanged(VehicleID id) => Refresh(id);

        private void OnBuyClicked()
        {
            VehicleID id = CurrentVehicle;
            if (id == VehicleID.None || SaveManager.IsOwned(id))
                return;

            int price = PriceOf(id);
            if (SaveManager.Coins < price)
            {
                Debug.LogError($"[MainMenu] Not enough coins to buy {id}. Need {price}, have {SaveManager.Coins}.");
                return;
            }

            SaveManager.Coins -= price;
            SaveManager.AddOwned(id);
            SaveManager.SelectVehicle(id);
            SaveManager.Save();

            Refresh(id);
        }

        private void OnPlayClicked()
        {
            SaveManager.Save();

            if (string.IsNullOrEmpty(gameSceneName))
            {
                Debug.LogError("[MainMenu] No game scene configured on the Play button.");
                return;
            }

            SceneManager.LoadScene(gameSceneName);
        }

        private void OnCustomizeClicked()
        {
            // No customization screen exists yet; surface the intent so it can be hooked up later.
            CustomizeRequested?.Invoke();
            Debug.Log($"[MainMenu] Customize requested for {CurrentVehicle}.");
        }

        private void OnSettingsClicked()
        {
            // No settings screen exists yet; surface the intent so it can be hooked up later.
            SettingsRequested?.Invoke();
            Debug.Log("[MainMenu] Settings requested.");
        }

        // ---------------------------------------------------------------------
        // View refresh
        // ---------------------------------------------------------------------

        private void Refresh(VehicleID id)
        {
            if (coinAmountText)
                coinAmountText.text = SaveManager.Coins.ToString("N0");

            bool hasVehicle = id != VehicleID.None;
            bool owned = hasVehicle && SaveManager.IsOwned(id);

            if (vehicleNameText)
                vehicleNameText.text = PrettyName(id);

            if (lockedBadge)
                lockedBadge.SetActive(hasVehicle && !owned);

            if (owned)
            {
                // Browsing an owned car selects it so the garage reflects the choice.
                SaveManager.SelectVehicle(id);

                if (buyButton) buyButton.gameObject.SetActive(false);
                if (playButton)
                {
                    playButton.gameObject.SetActive(true);
                    playButton.interactable = true;
                }
                if (customizeButton) customizeButton.interactable = true;
            }
            else
            {
                // Not owned: only Buy is available. Play and Customize are turned off.
                if (playButton) playButton.gameObject.SetActive(false);
                if (customizeButton) customizeButton.interactable = false;

                int price = PriceOf(id);
                if (buyButton)
                {
                    buyButton.gameObject.SetActive(hasVehicle);
                    buyButton.interactable = SaveManager.Coins >= price;
                }
                if (buyCoinIcon) buyCoinIcon.SetActive(true);
                if (buyPriceText) buyPriceText.text = price.ToString("N0");
            }
        }

        // ---------------------------------------------------------------------
        // Helpers
        // ---------------------------------------------------------------------

        private VehicleID CurrentVehicle => garageManager ? garageManager.CurrentVehicleId : VehicleID.None;

        private static int PriceOf(VehicleID id)
        {
            if (id == VehicleID.None || VehicleContainer.Instance == null)
                return 0;

            VehicleEntry entry = VehicleContainer.Instance.GetVehicle(id);
            return entry != null ? (int)entry.Price : 0;
        }

        // Turns "GTR_R35" into "GTR R35" for display. Cheap, allocation-light, only runs on swaps.
        private static string PrettyName(VehicleID id)
        {
            if (id == VehicleID.None)
                return "-";

            return id.ToString().Replace('_', ' ');
        }
    }
}
