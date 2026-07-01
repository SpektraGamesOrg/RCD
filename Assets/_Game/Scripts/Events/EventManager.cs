using System;
using System.Threading;
using _Game.Scripts.Utils.VContainer;
using Ads;
using Clutch;
using Core;
using Cysharp.Threading.Tasks;
using Save;
using Sirenix.OdinInspector;
using SpektraGames.RuntimeUI.Runtime;
using SpektraGames.SpektraUtilities.Runtime;
using UI;
using UIManager;
using UnityEngine;
using Vehicles;

namespace Events
{
    /// <summary>
    /// Scene-local orchestrator for the three in-game events (GDD "3. In-Game Events"). Mirrors
    /// <see cref="GameManager"/>: a <see cref="SingletonComponent{T}"/> that lives on the Game scene and does not
    /// survive a scene change. It owns the whole flow - entry pop-up, fade, traffic isolation, level spawn,
    /// teleport, 3-2-1 countdown, the live run, the result screen + 3X upsell, level progression and restore.
    ///
    /// Design choices (per the confirmed answers):
    ///  - Levels are global per mode: any area plays the player's CURRENT level; a win advances the whole map and
    ///    wraps back to level 1 after the last (endless loop).
    ///  - Traffic (Gley) is disabled and cleared for the duration of a Jump/Time-Trial run, then restored.
    ///  - The world stays live (timeScale = 1, Time Trial needs a real clock); only the free-roam HUD is hidden.
    ///  - Watch &amp; Earn is a simple drive-in area: watch a rewarded ad, get gold, no level/teleport.
    ///
    /// Nothing here scans the scene: the vehicle comes from <see cref="GameManager.SpawnedVehicle"/>, UI from
    /// <see cref="GameUIManager"/>, ads from the VContainer service locator. Per-frame work happens only during a
    /// live Time Trial run (a single timer text write).
    /// </summary>
    public sealed class EventManager : SingletonComponent<EventManager>
    {
        // Starting = the launch sequence (fade/spawn/teleport/countdown) BEFORE the live run. It is a distinct
        // phase so teleporting the car out of the EventArea trigger during launch does not get treated as
        // "left the entry prompt" (which would auto-close the prompt and wipe the active level mid-launch).
        private enum EventPhase
        {
            Idle,
            EntryPrompt,
            Starting,
            Running,
            Result
        }

        [Title("Timing")]
        [SerializeField, Min(0f)] private float fadeDuration = 0.35f;
        [SerializeField, Min(1)] private int countdownSeconds = 3;
        [Tooltip("GDD: the entry pop-up disappears 3 seconds after the player leaves the area.")]
        [SerializeField, Min(0f)] private float entryLeaveHideDelay = 3f;

        // Reward values (Watch & Earn gold, win 3X multiplier, fail-reward divider) are remote-tuned via the
        // Clutch "EventsConfig" flag - see EventsConfig / the Config accessor below.

        [Title("Vehicle Restore")]
        [Tooltip("Upward offset applied when the car respawns at its pre-event position (recovery convention).")]
        [SerializeField, Min(0f)] private float respawnLiftHeight = 1.5f;

        [SerializeField] private string watchAndEarnPlacement = "watch_and_earn";

        // ---- runtime state (never serialized) ----
        [ShowInInspector, ReadOnly] private EventPhase _phase = EventPhase.Idle;
        private EventArea _activeArea;
        private EventType _activeType;
        private LevelData _activeLevel;
        private int _activeLevelNumber;
        private int _rewardAmount;

        private bool _isolated; // true while a run has the world isolated (HUD hidden, traffic off)
        private bool _poseCaptured; // guards the restore teleport so we never snap to (0,0,0)
        private Vector3 _preEventPos;
        private Quaternion _preEventRot;

        private float _timeRemaining; // Time Trial clock
        private UniTaskCompletionSource<bool> _outcome; // completed by finish / obstacle / timeout

        private LevelRuntime _level;
        private Transform _poolContainer;
        private Transform _spawnRoot; // active, at world origin/identity/unit-scale: live level pieces spawn under it
        private CancellationTokenSource _leaveHideCts;

        // Lazily resolved DDOL overlays (cached; never resolved per-frame).
        private EventEntryOverlay _entryOverlay;
        private EventCountdownOverlay _countdownOverlay;
        private EventHudOverlay _hudOverlay;
        private LevelResultOverlay _resultOverlay;
        private EventFadeOverlay _fadeOverlay;

        /// <summary>True while a run has the world isolated - used by the HUD pop-ups to stay out of the way.</summary>
        public static bool IsEventBlockingHud => Exists() && Instance._isolated;

        private EventEntryOverlay EntryOverlay => Resolve(ref _entryOverlay);
        private EventCountdownOverlay CountdownOverlay => Resolve(ref _countdownOverlay);
        private EventHudOverlay HudOverlay => Resolve(ref _hudOverlay);
        private LevelResultOverlay ResultOverlay => Resolve(ref _resultOverlay);
        private EventFadeOverlay Fade => Resolve(ref _fadeOverlay);

        // Remote-tuned reward values (Clutch "EventsConfig" flag -> prefs cache -> SO fallback -> schema
        // defaults). The config service memoizes the parse, so resolving on demand is cheap.
        private static EventsConfig Config => ClutchConfigResolver.Get<EventsConfig>(ClutchFlagKeys.EventsConfig);

        protected override void Awake()
        {
            base.Awake();

            // Dedicated container for pooled (idle) level pieces so they never clutter the scene root.
            var poolGo = new GameObject("EventLevelPool");
            poolGo.transform.SetParent(transform, false);
            poolGo.SetActive(false);
            _poolContainer = poolGo.transform;

            // Active root for LIVE level pieces, pinned to world origin / identity / unit scale so pieces spawn at
            // their exact authored world transform regardless of where this manager sits.
            var spawnGo = new GameObject("EventLevelRoot");
            spawnGo.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            spawnGo.transform.localScale = Vector3.one;
            _spawnRoot = spawnGo.transform;

            _level = new LevelRuntime(_poolContainer);
        }

        // -----------------------------------------------------------------
        // Area entry / exit (called by EventArea triggers)
        // -----------------------------------------------------------------

        public void NotifyAreaEntered(EventArea area)
        {
            if (!area)
                return;

            // A launch/run (or Watch & Earn ad) is in progress: ignore area triggers until it finishes.
            if (_phase == EventPhase.Starting || _phase == EventPhase.Running || _phase == EventPhase.Result)
                return;

            // Re-entered the same area whose prompt is already up: just cancel the pending auto-hide.
            if (_phase == EventPhase.EntryPrompt && area == _activeArea)
            {
                CancelLeaveHide();
                return;
            }

            // Idle, OR a DIFFERENT area entered while a prompt is up (possibly during another area's 3s
            // leave-grace): the latest-entered area always wins. Switch the prompt to this area.
            ShowEntryPrompt(area);
        }

        public void NotifyAreaExited(EventArea area)
        {
            // Only the area whose prompt is currently up starts the auto-hide; leaving a different (older)
            // area is ignored, so the latest area's prompt stays while the player remains inside it.
            if (_phase == EventPhase.EntryPrompt && area == _activeArea)
                StartLeaveHide();
        }

        // Resolves the current level for the area's mode and shows (or refreshes) the entry pop-up for it.
        private void ShowEntryPrompt(EventArea area)
        {
            CancelLeaveHide();

            EventType type = area.EventType;
            string title;
            string action;
            LevelData level = null;
            int levelNumber = 0;

            if (type == EventType.WatchAndEarn)
            {
                title = "WATCH &\nEARN";
                action = "WATCH AD";
                _rewardAmount = Config.WatchAndEarnGold;
            }
            else
            {
                levelNumber = GetCurrentLevelNumber(type);
                level = EventLevelContainer.Instance
                    ? EventLevelContainer.Instance.GetLevel(type, levelNumber)
                    : null;

                if (!level)
                {
                    Debug.LogError($"[EventManager] No level found for {type} #{levelNumber}.");
                    CloseEntryPrompt(); // drop any prompt currently up rather than leave a stale one
                    return;
                }

                title = (type == EventType.JumpChallenge) ? "JUMP\nCHALLANGE" : "TIME TRAIL";
                action = "START";
                _rewardAmount = level.WinRewardGold;
            }

            _activeArea = area;
            _activeType = type;
            _activeLevel = level;
            _activeLevelNumber = levelNumber;
            _phase = EventPhase.EntryPrompt;

            EntryOverlay?.Show(uiData: new EventEntryData(title, action, _rewardAmount, type));
        }

        // -----------------------------------------------------------------
        // Entry pop-up buttons (called by EventEntryOverlay)
        // -----------------------------------------------------------------

        public void OnEntryStartPressed()
        {
            if (_phase != EventPhase.EntryPrompt)
                return;

            CancelLeaveHide();

            if (_activeType == EventType.WatchAndEarn)
                WatchAndEarnFlow().Forget();
            else
                RunEventFlow().Forget();
        }

        public void OnEntryClosePressed()
        {
            if (_phase != EventPhase.EntryPrompt)
                return;

            CloseEntryPrompt();
        }

        // -----------------------------------------------------------------
        // Watch & Earn
        // -----------------------------------------------------------------

        private async UniTaskVoid WatchAndEarnFlow()
        {
            CancellationToken token = this.GetCancellationTokenOnDestroy();

            HideEntryOverlay();
            _phase = EventPhase.Running; // mark busy (no world isolation for Watch & Earn)

            try
            {
                bool isSuccess = await ServiceLocator.GetService<MaxAdService>()
                    .ShowRewardedAdAsync(watchAndEarnPlacement)
                    .AttachExternalCancellation(token);

                if (isSuccess)
                {
                    int reward = Config.WatchAndEarnGold;
                    SaveManager.AddGolds(reward);
                    SaveManager.Save();
                    RuntimeUI.ShowToast($"+{reward}G");
                }
                else
                {
                    RuntimeUI.ShowToast("Rewarded ad was not completed");
                }
            }
            catch (OperationCanceledException)
            {
                // scene tear-down
            }
            catch (Exception e)
            {
                Debug.LogError($"[EventManager] Watch & Earn error: {e}");
            }
            finally
            {
                ResetToIdle();
            }
        }

        // -----------------------------------------------------------------
        // Jump Challenge / Time Trial run
        // -----------------------------------------------------------------

        private async UniTaskVoid RunEventFlow()
        {
            CancellationToken token = this.GetCancellationTokenOnDestroy();

            MainVehicleBehaviour vehicle = GameManager.Exists() ? GameManager.Instance.SpawnedVehicle : null;
            if (!vehicle || !_activeLevel || !_activeArea)
            {
                Debug.LogError("[EventManager] Cannot start event: missing vehicle, level or area.");
                CloseEntryPrompt();
                return;
            }

            try
            {
                HideEntryOverlay();
                _isolated = true;
                // Enter the launch phase up front: while teleporting the car out of the area, its OnTriggerExit
                // must NOT be read as leaving the entry prompt (that would auto-close it and null the level).
                _phase = EventPhase.Starting;

                // 1) Fade out, then clear traffic. The free-roam HUD stays visible during the run (the player can
                // still use nitro etc.); only the milestone COMPLETED pop-up is suppressed so it can't overlap.
                await FadeToBlack(token);
                if (GameManager.Exists())
                    GameManager.Instance.SetTrafficActive(false);
                if (GameUIManager.Instance)
                    GameUIManager.Instance.GetOverlayUI<MilestoneCompletedOverlay>()?.ForceClose();

                // Disable ALL event areas (every mode) so none can be triggered while this event runs.
                EventArea.SetAllActive(false);

                // 2) Capture the pre-event pose and spawn the level at its authored WORLD location.
                _preEventPos = vehicle.transform.position;
                _preEventRot = vehicle.transform.rotation;
                _poseCaptured = true;
                _level.Spawn(_activeLevel, _spawnRoot);

                // 3) Place the car at the start marker's world pose (levels must author a Start piece).
                Vector3 startPos;
                Quaternion startRot;
                if (_level.StartMarker)
                {
                    Transform s = _level.StartMarker.transform;
                    startPos = s.position;
                    startRot = s.rotation;
                }
                else
                {
                    Debug.LogError($"[EventManager] Level '{_activeLevel.name}' has no Start marker; placing the car at the layout origin.");
                    startPos = _spawnRoot.position;
                    startRot = _spawnRoot.rotation;
                }

                PlaceVehicle(vehicle, startPos, startRot);
                SetVehicleControl(vehicle, false);

                // 4) Show the in-run HUD and reveal the level.
                bool isTimeTrial = _activeType == EventType.TimeTrial;
                HudOverlay?.Show(uiData: new EventHudData(isTimeTrial, isTimeTrial ? string.Empty : "REACH THE FINISH!"));
                await FadeToClear(token);

                // 5) 3-2-1 countdown (car locked).
                if (CountdownOverlay)
                    await CountdownOverlay.PlayAsync(countdownSeconds, token);

                // 6) GO: arm the level, hand control back, and start the clock.
                _outcome = new UniTaskCompletionSource<bool>();
                ArmLevel(true);
                SetVehicleControl(vehicle, true);
                _phase = EventPhase.Running;

                if (isTimeTrial)
                {
                    _timeRemaining = _activeLevel.TimeLimitSeconds;
                    HudOverlay?.SetTimer(_timeRemaining);
                }

                // 7) Wait for the outcome (finish = win, obstacle = fail in Jump, timeout = fail in Time Trial).
                bool win = await _outcome.Task.AttachExternalCancellation(token);

                // 8) End the run -> show the result screen. Reward/restore continue in OnResultResolved.
                EndRun(vehicle, win);
            }
            catch (OperationCanceledException)
            {
                // scene tear-down; OnDestroy cleans up.
            }
            catch (Exception e)
            {
                Debug.LogError($"[EventManager] Event flow error: {e}");
                AbortToFreeRoam();
            }
        }

        private void EndRun(MainVehicleBehaviour vehicle, bool win)
        {
            _phase = EventPhase.Result;

            ArmLevel(false);
            StopVehicle(vehicle); // GDD: the car is forced to stop at the finish
            SetVehicleControl(vehicle, false);
            HudOverlay?.Hide();

            // Defensive: if the active level was lost somehow, don't NRE - just restore free-roam.
            if (!_activeLevel)
            {
                Debug.LogError("[EventManager] EndRun with no active level; returning to free-roam.");
                AbortToFreeRoam();
                return;
            }

            EventsConfig config = Config;
            int baseReward = win
                ? _activeLevel.WinRewardGold
                : _activeLevel.WinRewardGold / Mathf.Max(1, config.FailRewardDivider);
            int bonusReward = win ? _activeLevel.BonusRewardGold : 0;

            int multiplier = win ? config.WinAdMultiplier : 1;
            ResultOverlay?.Show(uiData: new LevelResultData(win, baseReward, bonusReward, multiplier));
        }

        /// <summary>Called by <see cref="LevelResultOverlay"/> once the reward is decided (base, or base * ad).</summary>
        public void OnResultResolved(bool win, int reward)
        {
            if (_phase != EventPhase.Result)
                return;

            if (reward > 0)
            {
                SaveManager.AddGolds(reward);
                SaveManager.Save();
            }

            if (win)
                AdvanceLevel(_activeType);

            RestoreSequence().Forget();
        }

        private async UniTaskVoid RestoreSequence()
        {
            CancellationToken token = this.GetCancellationTokenOnDestroy();

            try
            {
                await FadeToBlack(token);

                _level.Despawn();

                MainVehicleBehaviour vehicle = GameManager.Exists() ? GameManager.Instance.SpawnedVehicle : null;
                if (vehicle && _poseCaptured)
                {
                    PlaceVehicle(vehicle, _preEventPos + Vector3.up * respawnLiftHeight, _preEventRot);
                    SetVehicleControl(vehicle, true);
                }

                if (GameManager.Exists())
                    GameManager.Instance.SetTrafficActive(true);

                // Re-enable the areas while still in the Result phase, so the OnTriggerEnter fired when the car
                // respawns inside its origin area is ignored (no instant re-prompt after finishing).
                EventArea.SetAllActive(true);

                _isolated = false;

                await FadeToClear(token);
            }
            catch (OperationCanceledException)
            {
                // scene tear-down
            }
            catch (Exception e)
            {
                // A restore step failed partway (reward is already granted): force the world fully back to
                // free-roam so we never strand the player in the isolated state (traffic off, HUD hidden).
                Debug.LogError($"[EventManager] Restore error: {e}");
                AbortToFreeRoam();
            }
            finally
            {
                ResetToIdle();
            }
        }

        // -----------------------------------------------------------------
        // Run outcome sources
        // -----------------------------------------------------------------

        private void Update()
        {
            // Only the live Time Trial clock needs per-frame work.
            if (_phase != EventPhase.Running || _activeType != EventType.TimeTrial)
                return;

            _timeRemaining -= Time.deltaTime;
            HudOverlay?.SetTimer(_timeRemaining);

            if (_timeRemaining <= 0f)
            {
                _timeRemaining = 0f;
                _outcome?.TrySetResult(false); // out of time = fail
            }
        }

        private void OnFinishReached(LevelObject finish)
        {
            if (_phase == EventPhase.Running)
                _outcome?.TrySetResult(true);
        }

        private void OnObstacleHit(LevelObject obstacle)
        {
            // Obstacles only fail a Jump Challenge (GDD 3.1).
            if (_phase == EventPhase.Running && _activeType == EventType.JumpChallenge)
                _outcome?.TrySetResult(false);
        }

        private void ArmLevel(bool arm)
        {
            // Every Finish piece is armed - crossing ANY of them wins (levels can have multi-part finish gates).
            System.Collections.Generic.IReadOnlyList<LevelObject> finishes = _level.Finishes;
            for (int i = 0; i < finishes.Count; i++)
            {
                LevelObject f = finishes[i];
                if (!f)
                    continue;

                if (arm)
                {
                    f.VehicleEntered += OnFinishReached;
                    f.Arm();
                }
                else
                {
                    f.VehicleEntered -= OnFinishReached;
                    f.Disarm();
                }
            }

            System.Collections.Generic.IReadOnlyList<LevelObject> obstacles = _level.Obstacles;
            for (int i = 0; i < obstacles.Count; i++)
            {
                LevelObject o = obstacles[i];
                if (!o)
                    continue;

                if (arm)
                {
                    o.VehicleEntered += OnObstacleHit;
                    o.Arm();
                }
                else
                {
                    o.VehicleEntered -= OnObstacleHit;
                    o.Disarm();
                }
            }
        }

        // -----------------------------------------------------------------
        // Helpers
        // -----------------------------------------------------------------

        private void CloseEntryPrompt()
        {
            HideEntryOverlay();
            ResetToIdle();
        }

        private void HideEntryOverlay()
        {
            EventEntryOverlay overlay = EntryOverlay;
            if (overlay && overlay.ShowingOrShown)
                overlay.Hide();
        }

        private void ResetToIdle()
        {
            _phase = EventPhase.Idle;
            // Idle is never isolated: clearing this here guarantees the static HUD-block flag is released even if
            // a restore path was cut short (e.g. cancelled by scene tear-down before RestoreSequence finished).
            _isolated = false;
            _activeArea = null;
            _activeLevel = null;
            _activeLevelNumber = 0;
            _poseCaptured = false;
            _outcome = null;
            CancelLeaveHide();
        }

        // Best-effort return to free-roam after an unexpected error mid-run.
        private void AbortToFreeRoam()
        {
            ArmLevel(false);
            if (_level != null)
                _level.Despawn();

            MainVehicleBehaviour vehicle = GameManager.Exists() ? GameManager.Instance.SpawnedVehicle : null;
            if (vehicle && _poseCaptured)
            {
                PlaceVehicle(vehicle, _preEventPos + Vector3.up * respawnLiftHeight, _preEventRot);
                SetVehicleControl(vehicle, true);
            }

            if (GameManager.Exists())
                GameManager.Instance.SetTrafficActive(true);

            EventArea.SetAllActive(true);
            _isolated = false;

            EventFadeOverlay fade = Fade;
            if (fade)
                fade.ToClearAsync(0.2f).Forget();

            ResetToIdle();
        }

        private void StartLeaveHide()
        {
            CancelLeaveHide();
            _leaveHideCts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
            // Capture the area this timer belongs to: if the player switches to another area before it elapses,
            // this timer must NOT close the newer area's prompt.
            LeaveHideAsync(_activeArea, _leaveHideCts.Token).Forget();
        }

        private async UniTaskVoid LeaveHideAsync(EventArea area, CancellationToken token)
        {
            try
            {
                await UniTask.Delay(TimeSpan.FromSeconds(entryLeaveHideDelay), DelayType.UnscaledDeltaTime,
                    cancellationToken: token);

                if (_phase == EventPhase.EntryPrompt && _activeArea == area)
                    CloseEntryPrompt();
            }
            catch (OperationCanceledException)
            {
                // re-entered the area (or scene tear-down) before the delay elapsed
            }
        }

        private void CancelLeaveHide()
        {
            if (_leaveHideCts == null)
                return;

            _leaveHideCts.Cancel();
            _leaveHideCts.Dispose();
            _leaveHideCts = null;
        }

        private UniTask FadeToBlack(CancellationToken token)
        {
            EventFadeOverlay fade = Fade;
            return fade ? fade.ToBlackAsync(fadeDuration, token) : UniTask.CompletedTask;
        }

        private UniTask FadeToClear(CancellationToken token)
        {
            EventFadeOverlay fade = Fade;
            return fade ? fade.ToClearAsync(fadeDuration, token) : UniTask.CompletedTask;
        }

        private static void PlaceVehicle(MainVehicleBehaviour vehicle, Vector3 position, Quaternion rotation)
        {
            Transform t = vehicle.transform;
            t.SetPositionAndRotation(position, rotation);

            Rigidbody body = vehicle.Rigidbody;
            if (body)
            {
                body.position = position;
                body.rotation = rotation;
            }

            Physics.SyncTransforms();

            if (body)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }
        }

        private static void StopVehicle(MainVehicleBehaviour vehicle)
        {
            Rigidbody body = vehicle.Rigidbody;
            if (!body)
                return;

            // body.linearVelocity = Vector3.zero;
            // body.angularVelocity = Vector3.zero;
        }

        private static void SetVehicleControl(MainVehicleBehaviour vehicle, bool canControl)
        {
            RCC_CarControllerV4 controller = vehicle.VehicleController;
            if (!controller)
                return;

            controller.SetCanControl(canControl);
            if (canControl)
                controller.SetExternalControl(false);
        }

        private static int GetCurrentLevelNumber(EventType type)
        {
            return type == EventType.TimeTrial ? SaveManager.TimeTrialLevel : SaveManager.JumpChallengeLevel;
        }

        private static void AdvanceLevel(EventType type)
        {
            int count = EventLevelContainer.Instance ? EventLevelContainer.Instance.GetLevelCount(type) : 0;
            int current = GetCurrentLevelNumber(type);
            int next = count > 0 ? (current % count) + 1 : current + 1; // 1-based; wraps back to 1 after the last

            if (type == EventType.TimeTrial)
                SaveManager.TimeTrialLevel = next;
            else
                SaveManager.JumpChallengeLevel = next;

            SaveManager.Save();
        }

        private static string ModeTitle(EventType type)
        {
            return type == EventType.TimeTrial ? "TIME TRIAL" : "JUMP CHALLENGE";
        }

        private static T Resolve<T>(ref T cached) where T : OverlayBase
        {
            if (cached)
                return cached;

            if (!GameUIManager.Instance)
                return null;

            cached = GameUIManager.Instance.GetOverlayUI<T>();
            return cached;
        }

        protected override void OnDestroy()
        {
            CancelLeaveHide();

            if (_level != null && _level.IsSpawned)
            {
                ArmLevel(false);
                _level.Despawn();
            }

            // The live-piece root is a scene-root object we created; destroy it so it never lingers.
            if (_spawnRoot)
                Destroy(_spawnRoot.gameObject);

            // These are DontDestroyOnLoad views: hide any that are up so none lingers over the menu/loading screen.
            if (GameUIManager.Instance)
            {
                GameUIManager.Instance.GetOverlayUI<EventEntryOverlay>()?.Hide(true);
                GameUIManager.Instance.GetOverlayUI<EventCountdownOverlay>()?.Hide(true);
                GameUIManager.Instance.GetOverlayUI<EventHudOverlay>()?.Hide(true);
                GameUIManager.Instance.GetOverlayUI<LevelResultOverlay>()?.Hide(true);
                GameUIManager.Instance.GetOverlayUI<EventFadeOverlay>()?.Hide(true);
            }

            base.OnDestroy();
        }
    }
}