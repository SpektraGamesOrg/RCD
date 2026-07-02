using System;
using _Game.Scripts.Utils.VContainer;
using Ads;
using Clutch;
using Core;
using Cysharp.Threading.Tasks;
using Save;
using SpektraGames.RuntimeUI.Runtime;
using TMPro;
using UIManager;
using UnityEngine;
using UnityEngine.UI;
using Utils;
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
        private GarageManager _garageManager = null;

        // Guards the rewarded-ad flow so a second tap can't start a second ad while one is in flight.
        private bool _processingAd;

        [Header("Vehicle Navigation")]
        [SerializeField] private Button leftArrowButton;
        [SerializeField] private Button rightArrowButton;
        [SerializeField] private TMP_Text vehicleNameText;
        [SerializeField] private GameObject lockedBadge;

        [Header("Top Bar")]
        // Gold and driven-distance are no longer shown here: each lives in its own self-updating widget
        // (CoinWidget / DriveDistanceWidget) on the Widget_Coin / Widget_DriveDistance prefabs.
        [SerializeField] private Button settingsButton;

        [Header("Actions")]
        [SerializeField] private Button playButton;
        [SerializeField] private GameObject buyArea;
        [SerializeField] private Button watchAdsBuyButton;
        [SerializeField] private TMP_Text watchAdsButtonText;
        [SerializeField] private GameObject unlockStatusArea;
        [SerializeField] private TMP_Text unlockStatusText;
        [SerializeField] private TMP_Text unlockStatusDistanceText;
        [SerializeField] private LoadingBar unlockStatusLoadingBar;
        [SerializeField] private Button claimNowButton;
        [SerializeField] private Button buyButton;
        [SerializeField] private TMP_Text buyPriceText;

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
            if (buyButton) buyButton.onClick.AddListener(OnBuyClicked);
            if (watchAdsBuyButton) watchAdsBuyButton.onClick.AddListener(OnWatchAdsClicked);
            if (claimNowButton) claimNowButton.onClick.AddListener(OnClaimNowClicked);
            if (settingsButton) settingsButton.onClick.AddListener(OnSettingsClicked);

            // NOTE: garageManager is bound at runtime via Bind(), not in the inspector. This screen is a
            // persistent (DontDestroyOnLoad) view owned by GameUIManager, while the garage lives in the
            // MainMenu scene, so the cross-scene reference can't be serialized.
            if (_garageManager)
                _garageManager.DisplayedVehicleChanged += OnDisplayedVehicleChanged;
        }

        /// <summary>
        /// Wires the in-scene <see cref="GarageManager"/> to this persistent screen at runtime, since the
        /// cross-scene reference can't be set in the inspector. Called by the MainMenu scene controller
        /// before the screen is opened. Safe to call repeatedly; re-binding swaps the subscription.
        /// </summary>
        public void Bind(GarageManager manager)
        {
            if (_garageManager == manager)
            {
                Refresh(CurrentVehicle);
                return;
            }

            if (_garageManager)
                _garageManager.DisplayedVehicleChanged -= OnDisplayedVehicleChanged;

            _garageManager = manager;

            if (_garageManager)
                _garageManager.DisplayedVehicleChanged += OnDisplayedVehicleChanged;

            Refresh(CurrentVehicle);
        }

        private void OnDestroy()
        {
            if (leftArrowButton) leftArrowButton.onClick.RemoveListener(OnPreviousClicked);
            if (rightArrowButton) rightArrowButton.onClick.RemoveListener(OnNextClicked);
            if (playButton) playButton.onClick.RemoveListener(OnPlayClicked);
            if (buyButton) buyButton.onClick.RemoveListener(OnBuyClicked);
            if (watchAdsBuyButton) watchAdsBuyButton.onClick.RemoveListener(OnWatchAdsClicked);
            if (claimNowButton) claimNowButton.onClick.RemoveListener(OnClaimNowClicked);
            if (settingsButton) settingsButton.onClick.RemoveListener(OnSettingsClicked);

            UnsubscribeFromSaveEvents();

            if (_garageManager)
                _garageManager.DisplayedVehicleChanged -= OnDisplayedVehicleChanged;
        }

        protected override void OnBeforeShowing(bool immediate, object uiData = null)
        {
            base.OnBeforeShowing(immediate, uiData);

            // Listen for save changes while visible so the screen stays in sync no matter who edits the
            // data (gameplay rewards, debug tools, etc.) instead of those callers reaching in to refresh it.
            SubscribeToSaveEvents();

            // Sync the UI to whatever the garage is currently showing when the screen opens.
            Refresh(CurrentVehicle);
        }

        protected override void OnHidden(bool immediate = false)
        {
            base.OnHidden(immediate);
            UnsubscribeFromSaveEvents();
        }

        // ---------------------------------------------------------------------
        // Button handlers
        // ---------------------------------------------------------------------

        private void OnNextClicked()
        {
            if (_garageManager) _garageManager.ShowNext();
        }

        private void OnPreviousClicked()
        {
            if (_garageManager) _garageManager.ShowPrevious();
        }

        private void OnDisplayedVehicleChanged(VehicleID id) => Refresh(id);

        private void OnBuyClicked()
        {
            VehicleID id = CurrentVehicle;
            VehicleEntry entry = GetEntry(id);
            ResolvedVehicleConfig config = Resolve(entry);
            if (entry == null || SaveManager.IsOwned(id) || !Has(config.ObtainType, VehicleObtainType.ByGold))
                return;

            int price = config.GoldValue;
            if (SaveManager.Gold < price)
            {
                Debug.LogError($"[MainMenu] Not enough golds to buy {id}. Need {price}, have {SaveManager.Gold}.");
                RuntimeUI.ShowToast("Not enough gold to buy");
                return;
            }

            SaveManager.Gold -= price;
            UnlockVehicle(id);

            Refresh(CurrentVehicle);
        }

        private void OnWatchAdsClicked()
        {
            OnWatchAdsClickedAsync(CurrentVehicle).Forget();
        }

        // Plays a rewarded ad for an unlock-by-ads vehicle; on a successful watch it counts one ad toward
        // this car's target and unlocks it once the target is reached. The vehicle id is captured up front
        // so the right car is credited even if the player browses away while the ad is on screen.
        private async UniTaskVoid OnWatchAdsClickedAsync(VehicleID id)
        {
            if (_processingAd)
                return;

            VehicleEntry entry = GetEntry(id);
            ResolvedVehicleConfig config = Resolve(entry);
            if (entry == null || SaveManager.IsOwned(id) || !Has(config.ObtainType, VehicleObtainType.ByWatchAds))
                return;

            _processingAd = true;
            if (watchAdsBuyButton) watchAdsBuyButton.interactable = false;
            try
            {
                if (!await ShowRewardedAdAsync())
                    return;

                int target = config.AdsValue;
                int watched = SaveManager.GetVehicleWatchAdCount(id) + 1;
                SaveManager.SetVehicleWatchAdCount(id, watched);

                if (watched >= target)
                    UnlockVehicle(id);
                else
                    SaveManager.Save();
            }
            finally
            {
                _processingAd = false;
                // Always resync to whatever car is on screen now (the player may have browsed during the ad).
                Refresh(CurrentVehicle);
            }
        }

        private void OnClaimNowClicked()
        {
            VehicleID id = CurrentVehicle;
            VehicleEntry entry = GetEntry(id);
            ResolvedVehicleConfig config = Resolve(entry);
            if (entry == null || SaveManager.IsOwned(id) || !Has(config.ObtainType, VehicleObtainType.DistanceMilestoneKm))
                return;

            if (SaveManager.DistanceDrivenKm < config.DistanceKm)
            {
                Debug.LogError($"[MainMenu] Cannot claim {id}: distance milestone not reached.");
                return;
            }

            UnlockVehicle(id);
            Refresh(CurrentVehicle);
        }

        private void OnPlayClicked()
        {
            VehicleID id = CurrentVehicle;
            if (id == VehicleID.None || !SaveManager.SelectVehicle(id))
            {
                Debug.LogError($"[MainMenu] Cannot start gameplay with vehicle '{id}'.");
                return;
            }

            SaveManager.Save();

            if (!CustomSceneManager.Exists())
            {
                Debug.LogError("[MainMenu] No SceneManager available to load the game scene.");
                return;
            }

            CustomSceneManager.Instance.LoadGameScene();
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
            bool hasVehicle = id != VehicleID.None;
            bool owned = hasVehicle && SaveManager.IsOwned(id);

            if (vehicleNameText)
                vehicleNameText.text = PrettyName(id);

            if (lockedBadge)
                lockedBadge.SetActive(hasVehicle && !owned);

            if (owned)
            {
                // Browsing an owned car selects it so the garage reflects the choice. The buy area (and
                // every obtain control inside it) is hidden; only Play/Drive is shown.
                SaveManager.SelectVehicle(id);

                if (buyArea) buyArea.SetActive(false);
                if (playButton)
                {
                    playButton.gameObject.SetActive(true);
                    playButton.interactable = true;
                }
                return;
            }

            // Not owned: hide Play/Drive and show the buy area. Free cars never reach here - they are
            // auto-granted in SaveManager.EnsureStarterVehicle - so only Gold / WatchAds / Distance apply.
            if (playButton) playButton.gameObject.SetActive(false);
            if (buyArea) buyArea.SetActive(hasVehicle);

            if (hasVehicle)
                ConfigureBuyArea(id);
        }

        // Turns each obtain control inside the buy area on/off based on the vehicle's VehicleObtainType
        // flags (a car can offer several at once). buyArea must be the parent of buyButton,
        // watchAdsBuyButton, unlockStatusArea and claimNowButton so hiding it hides them all.
        private void ConfigureBuyArea(VehicleID id)
        {
            VehicleEntry entry = GetEntry(id);
            ResolvedVehicleConfig config = Resolve(entry);
            VehicleObtainType obtain = config.ObtainType;

            // --- Obtain by gold: show the price and gate the button on the player's balance. ---
            bool byGold = Has(obtain, VehicleObtainType.ByGold);
            if (buyButton)
            {
                buyButton.gameObject.SetActive(byGold);
            }
            if (byGold && buyPriceText) buyPriceText.text = "BUY " + config.GoldValue.ToString("N0");

            // --- Obtain by watching ads: show "WATCH AD watched/target" for this specific car. ---
            bool byAds = Has(obtain, VehicleObtainType.ByWatchAds);
            if (watchAdsBuyButton)
            {
                watchAdsBuyButton.gameObject.SetActive(byAds);
                watchAdsBuyButton.interactable = byAds && !_processingAd;
            }
            if (byAds && watchAdsButtonText)
            {
                int adsTarget = config.AdsValue;
                int watched = Mathf.Min(SaveManager.GetVehicleWatchAdCount(id), adsTarget);
                watchAdsButtonText.text = $"WATCH AD {watched}/{adsTarget}";
            }

            // --- Obtain by distance milestone: global km progress, with Claim Now once the target is hit. ---
            bool byDistance = Has(obtain, VehicleObtainType.DistanceMilestoneKm);
            if (unlockStatusArea) unlockStatusArea.SetActive(byDistance);

            bool milestoneReached = false;
            if (byDistance)
            {
                int kmTarget = config.DistanceKm;
                int driven = SaveManager.DistanceDrivenKm;
                float progress01 = kmTarget > 0 ? Mathf.Clamp01((float)driven / kmTarget) : 1f;
                int percent = Mathf.Clamp(Mathf.FloorToInt(progress01 * 100f), 0, 100);
                milestoneReached = driven >= kmTarget;

                if (unlockStatusText)
                    unlockStatusText.text = $"UNLOCK STATUS: <color=#FFC31A>{percent}%</color>";
                if (unlockStatusDistanceText)
                    unlockStatusDistanceText.text = $"{DistanceFormat.Km(Mathf.Min(driven, kmTarget))} / {DistanceFormat.KmTarget(kmTarget)} KM";
                if (unlockStatusLoadingBar)
                    unlockStatusLoadingBar.SetProgress(progress01);
            }

            // Claim Now is an extra button that only appears once the distance milestone is reached.
            if (claimNowButton) claimNowButton.gameObject.SetActive(byDistance && milestoneReached);
        }

        // ---------------------------------------------------------------------
        // Save events
        // ---------------------------------------------------------------------

        // Subscriptions are refreshed defensively (-= then +=) so re-showing the screen can never stack
        // duplicate handlers. Driven from SaveManager so any system that mutates the save (gameplay,
        // debug menu, etc.) keeps this screen correct without depending on a reference to it.
        private void SubscribeToSaveEvents()
        {
            SaveManager.OnGoldsChanged -= HandleSaveValueChanged;
            SaveManager.OnGoldsChanged += HandleSaveValueChanged;
            SaveManager.OnDistanceDrivenChanged -= HandleSaveValueChanged;
            SaveManager.OnDistanceDrivenChanged += HandleSaveValueChanged;
            SaveManager.OnSaveReset -= HandleSaveReset;
            SaveManager.OnSaveReset += HandleSaveReset;

            // Prices come from the Clutch config (with cache/SO fallback). If Clutch resolves AFTER this
            // screen is already shown, re-run the refresh so displayed prices switch from the fallback to
            // the resolved values instead of staying on the VehicleContainer defaults.
            if (ServiceLocator.TryGetService(out IClutchConfigService clutchConfig))
            {
                clutchConfig.OnConfigUpdated -= HandleClutchConfigUpdated;
                clutchConfig.OnConfigUpdated += HandleClutchConfigUpdated;
            }
        }

        private void UnsubscribeFromSaveEvents()
        {
            SaveManager.OnGoldsChanged -= HandleSaveValueChanged;
            SaveManager.OnDistanceDrivenChanged -= HandleSaveValueChanged;
            SaveManager.OnSaveReset -= HandleSaveReset;

            if (ServiceLocator.TryGetService(out IClutchConfigService clutchConfig))
                clutchConfig.OnConfigUpdated -= HandleClutchConfigUpdated;
        }

        // Gold and distance both feed the buy area (affordability, milestone progress), so any change
        // re-runs the same full refresh for the car currently on screen.
        private void HandleSaveValueChanged(int _) => Refresh(CurrentVehicle);
        private void HandleSaveReset() => Refresh(CurrentVehicle);

        // Clutch config resolved (or refreshed): re-read prices so the buy area reflects the resolved
        // Clutch/cache values rather than the VehicleContainer fallback shown before resolution completed.
        private void HandleClutchConfigUpdated() => Refresh(CurrentVehicle);

        // ---------------------------------------------------------------------
        // Helpers
        // ---------------------------------------------------------------------

        private VehicleID CurrentVehicle => _garageManager ? _garageManager.CurrentVehicleId : VehicleID.None;

        // Static catalog entry for a vehicle, or null if it has no entry / the container is missing.
        private static VehicleEntry GetEntry(VehicleID id)
        {
            if (id == VehicleID.None || !VehicleContainer.Instance)
                return null;

            return VehicleContainer.Instance.GetVehicle(id);
        }

        // Effective obtain config for an entry, resolving the Clutch "VehicleConfig" flag (keyed by the
        // VehicleID enum name) over the entry's serialized values. When Clutch has an entry for the vehicle,
        // its declared paths and per-path values are authoritative; otherwise the serialized type/amount are
        // used (the single serialized amount applies to whichever path(s) the serialized type enables).
        // Callers should resolve ONCE and read the path(s) and per-path values they need, so the obtain
        // controls and targets stay consistent within a single refresh.
        private static ResolvedVehicleConfig Resolve(VehicleEntry entry)
        {
            if (entry == null || entry.ID == VehicleID.None)
                return ResolvedVehicleConfig.None;

            // Clutch is the single source: the service resolves the cached Clutch value, then the
            // ClutchConfig SO fallback. The VehicleContainer no longer carries obtain data.
            if (ServiceLocator.TryGetService(out IClutchConfigService clutchConfig))
                return clutchConfig.GetVehicleConfig(entry.ID);

            return ResolvedVehicleConfig.None;
        }

        // Allocation-free flag test (Enum.HasFlag boxes); VehicleObtainType is a [Flags] enum.
        private static bool Has(VehicleObtainType obtain, VehicleObtainType flag) => (obtain & flag) != 0;

        // Grants the vehicle, selects it, and flushes the save. Shared by every unlock path
        // (gold, ads, distance) so ownership is recorded the same way everywhere.
        private static void UnlockVehicle(VehicleID id)
        {
            SaveManager.AddOwned(id);
            SaveManager.SelectVehicle(id);
            SaveManager.Save();
        }

        // Plays a rewarded ad through the shared MaxAdService and reports whether it was watched to completion.
        // On any failure it surfaces the shared toast, so callers can simply branch on the returned bool.
        private static async UniTask<bool> ShowRewardedAdAsync()
        {
            bool isSuccess = await ServiceLocator.GetService<MaxAdService>().ShowRewardedAdAsync("vehicle_unlock");
            if (!isSuccess)
                RuntimeUI.ShowToast("Rewarded ad was not completed");

            return isSuccess;
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