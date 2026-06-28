using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using Sirenix.OdinInspector;
using SpektraGames.SpektraUtilities.Runtime;
using UnityEngine;
using UnityEngine.InputSystem;

namespace UIManager
{
    [DefaultExecutionOrder(-100000)]
    public class GameUIManager : SingletonComponent<GameUIManager>
    {
        [SerializeField] private ScreenBase autoOpenScreen = null;

        [SerializeField, ReadOnly]
        private List<ScreenBase> allScreens = new();
        [SerializeField, ReadOnly]
        private List<PopupBase> allPopups = new();
        [SerializeField, ReadOnly]
        private List<OverlayBase> allOverlays = new();
        [SerializeField, ReadOnly]
        private List<TabBase> allTabs = new();

        [ShowInInspector, ReadOnly]
        public (ScreenBase screen, object uiData) ActiveScreen { get; set; } = default;
        [ShowInInspector, ReadOnly]
        public List<(ScreenBase screen, object uiData)> ScreenHistory { get; private set; } = new();

        [ShowInInspector, ReadOnly]
        public List<UIViewBase> CurrentActiveUIs { get; private set; } = new();

        [ShowInInspector]
        public UIViewBase CurrentActiveView
        {
            get
            {
                for (var i = CurrentActiveUIs.Count - 1; i >= 0; i--)
                {
                    if (CurrentActiveUIs[i])
                    {
                        if (CurrentActiveUIs[i] is TabBase)
                            continue;

                        if (CurrentActiveUIs[i] is OverlayBase overlayBase && overlayBase.HideFromActiveUis)
                            continue;

                        return CurrentActiveUIs[i];
                    }
                }

                return null;
            }
        }

        public static Action OnOnyUIViewShowed;
        public static Action OnOnyUIViewHide;

        private void Start()
        {
            ForceCloseAllScreens();
            CloseAllPopups();
            CloseAllOverlays();
            OpenOverlaysThatMustBeAlwaysOpened();

            if (autoOpenScreen)
                SwitchScreen(autoOpenScreen, true, null);
        }

        private void OnEnable()
        {
        }

        private void OnDisable()
        {
        }

        public T GetScreen<T>() where T : ScreenBase
        {
            Type tType = typeof(T);

            for (var i = 0; i < allScreens.Count; i++)
            {
                if (allScreens[i] && allScreens[i].GetType() == tType)
                {
                    return allScreens[i] as T;
                }
            }

            Debug.LogError("UI not found for type: " + tType.Name);

            return null;
        }

        public T GetPopup<T>() where T : PopupBase
        {
            Type tType = typeof(T);

            for (var i = 0; i < allPopups.Count; i++)
            {
                if (allPopups[i] && allPopups[i].GetType() == tType)
                {
                    return allPopups[i] as T;
                }
            }

            Debug.LogError("UI not found for type: " + tType.Name);

            return null;
        }

        public T GetOverlayUI<T>() where T : OverlayBase
        {
            Type tType = typeof(T);

            for (var i = 0; i < allOverlays.Count; i++)
            {
                if (allOverlays[i] && allOverlays[i].GetType() == tType)
                {
                    return allOverlays[i] as T;
                }
            }

            Debug.LogError("UI not found for type: " + tType.Name);

            return null;
        }

        public T GetTab<T>() where T : TabBase
        {
            Type tType = typeof(T);

            for (var i = 0; i < allTabs.Count; i++)
            {
                if (allTabs[i] && allTabs[i].GetType() == tType)
                {
                    return allTabs[i] as T;
                }
            }

            Debug.LogError("Tab not found for type: " + tType.Name);

            return null;
        }

        public void RebaseBackHistory()
        {
            ScreenHistory.Clear();
            ScreenHistory.Add(ActiveScreen);
        }

        public bool Back(bool immediate = false, System.Object uiData = null)
        {
            BackAsync().Forget();
            return true;
        }

        public async UniTask<bool> BackAsync(bool immediate = false, System.Object uiData = null)
        {
            if (CurrentActiveView == null)
            {
                return false;
            }

            if (!CurrentActiveView.CanBackNow)
            {
                return false;
            }

            // if (UIHelper.AnyLoadingActive || UIHelper.AnyUIBlockActive)
            // {
            //     return false;
            // }

            // Wait for transition
            if (ActiveScreen.screen.VisibilityState == UIViewBase.UIVisibilityState.Hiding ||
                ActiveScreen.screen.VisibilityState == UIViewBase.UIVisibilityState.Showing ||
                CurrentActiveView.VisibilityState == UIViewBase.UIVisibilityState.Hiding ||
                CurrentActiveView.VisibilityState == UIViewBase.UIVisibilityState.Showing)
            {
                return false;
            }

            if (CurrentActiveView is ScreenBase screenBase)
            {
                if (ActiveScreen.screen != null && ActiveScreen.screen.HaveCustomBackAction)
                {
                    try
                    {
                        ActiveScreen.screen.OnCustomBackAction();
                    }
                    catch (Exception e)
                    {
                        e.LogException();
                        return false;
                    }

                    return true;
                }

                if (ScreenHistory.Count <= 1)
                {
                    return false;
                }

                (ScreenBase screen, object uiData) toScreen = ScreenHistory[^2];

                // Remove last two elements from history
                ScreenHistory.RemoveRange(ScreenHistory.Count - 2, 2);

                await SwitchScreenAsyncInternal(toScreen.screen, immediate, uiData ?? toScreen.uiData);
                return true;
            }
            else
            {
                await CurrentActiveView.HideAsync(immediate);
                return true;
            }
        }

        public async UniTask SwitchScreenAsync(ScreenBase toScreen, bool immediate = false, System.Object uiData = null)
        {
            await SwitchScreenAsyncInternal(toScreen, immediate, uiData);
        }

        public void SwitchScreen(ScreenBase toScreen, bool immediate = false, System.Object uiData = null)
        {
            SwitchScreenAsyncInternal(toScreen, immediate, uiData).Forget();
        }

        public async UniTask SwitchScreenAsync<T>(bool immediate = false, System.Object uiData = null) where T : ScreenBase
        {
            ScreenBase toScreen = GetScreen<T>();
            await SwitchScreenAsyncInternal(toScreen, immediate, uiData);
        }

        public void SwitchScreen<T>(bool immediate = false, System.Object uiData = null) where T : ScreenBase
        {
            ScreenBase toScreen = GetScreen<T>();
            SwitchScreenAsyncInternal(toScreen, immediate, uiData).Forget();
        }

        private async UniTask SwitchScreenAsyncInternal(ScreenBase toScreen, bool immediate = false, System.Object uiData = null)
        {
            ScreenBase fromScreen = ActiveScreen.screen;

            if (fromScreen)
            {
                fromScreen.HideAsync().Forget();
            }

            await toScreen.ShowAsync(immediate, uiData);

            ActiveScreen = (screen: toScreen, uiData: uiData);

            ScreenHistory.Add(ActiveScreen);

            if (toScreen.RebaseBackHistoryWhenOpened)
            {
                RebaseBackHistory();
            }
        }

        public void ShowPopup(PopupBase popup, bool immediate = false, System.Object uiData = null)
        {
            popup.ShowAsync(immediate, uiData).Forget();
        }

        public void HidePopup(PopupBase popup, bool immediate = false)
        {
            popup.HideAsync(immediate).Forget();
        }

        public void ShowOverlayUI(OverlayBase overlayUI, bool immediate = false, System.Object uiData = null)
        {
            overlayUI.ShowAsync(immediate, uiData).Forget();
        }

        public void HideOverlayUI(OverlayBase overlayUI, bool immediate = false)
        {
            overlayUI.HideAsync(immediate).Forget();
        }

        public void ForceCloseAllScreens()
        {
            for (var i = 0; i < allScreens.Count; i++)
            {
                if (allScreens[i])
                {
                    bool wasOpened = allScreens[i].ShowingOrShown;

                    allScreens[i].HideAsync(true, false).Forget();

                    // Special status
                    if (wasOpened)
                    {
                        OnHiddenAnyUIView(allScreens[i]);
                    }
                }
            }
        }

        public void CloseAllPopups(bool immediate = false)
        {
            for (var i = 0; i < allPopups.Count; i++)
            {
                if (allPopups[i])
                {
                    if (allPopups[i].ShowingOrShown)
                    {
                        allPopups[i].Hide(immediate);
                    }
                }
            }
        }

        public void CloseAllOverlays(bool immediate = false)
        {
            for (var i = 0; i < allOverlays.Count; i++)
            {
                if (allOverlays[i])
                {
                    if (allOverlays[i].ShowingOrShown && !allOverlays[i].AlwaysShow)
                    {
                        allOverlays[i].Hide(immediate);
                    }
                }
            }
        }

        public void OpenOverlaysThatMustBeAlwaysOpened()
        {
            for (var i = 0; i < allOverlays.Count; i++)
            {
                if (allOverlays[i] && allOverlays[i].AlwaysShow)
                {
                    allOverlays[i].Show(true);
                }
            }
        }

        protected internal void OnBeforeShowingAnyUIView(UIViewBase view)
        {
            if (!CurrentActiveUIs.Contains(view))
            {
                if (view is OverlayBase overlayBase && overlayBase.HideFromActiveUis)
                {
                    return;
                }

                UIViewBase previousActiveView = CurrentActiveView;

                CurrentActiveUIs.Add(view);
                OnOnyUIViewShowed?.Invoke();

                UIViewBase newActiveView = CurrentActiveView;

                if (previousActiveView && previousActiveView != newActiveView)
                {
                    previousActiveView.InvokeOnUnHovered();
                }
            }
        }

        protected internal void OnHiddenAnyUIView(UIViewBase view)
        {
            if (CurrentActiveUIs.Contains(view))
            {
                UIViewBase previousActiveView = CurrentActiveView;

                CurrentActiveUIs.Remove(view);
                OnOnyUIViewHide?.Invoke();

                UIViewBase newActiveView = CurrentActiveView;

                if (previousActiveView && newActiveView && previousActiveView != newActiveView)
                {
                    newActiveView.InvokeOnHovered();
                }
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (Application.isPlaying)
                return;

            // RefreshReferences();
        }

        [Button]
        public void RefreshReferences()
        {
            if (Application.isPlaying)
                return;

            allScreens = GetComponentsInChildren<ScreenBase>(true).ToList();
            allPopups = GetComponentsInChildren<PopupBase>(true).ToList();
            allOverlays = GetComponentsInChildren<OverlayBase>(true).ToList();

            for (var i = 0; i < allScreens.Count; i++)
            {
                for (var j = 0; j < allScreens.Count; j++)
                {
                    if (i != j && allScreens[j].GetType() == allScreens[i].GetType())
                    {
                        Debug.LogError($"The screen {allScreens[i].name} used in multiple same type!", allScreens[i].gameObject);
                    }
                }

                string nameShouldBe = "Screen_" + allScreens[i].GetType().Name.Replace("Screen", "");
                if (allScreens[i].gameObject.name != nameShouldBe)
                {
                    allScreens[i].gameObject.name = nameShouldBe;
                    UnityEditor.EditorUtility.SetDirty(allScreens[i].gameObject);
                }
            }

            for (var i = 0; i < allPopups.Count; i++)
            {
                for (var j = 0; j < allPopups.Count; j++)
                {
                    if (i != j && allPopups[j].GetType() == allPopups[i].GetType())
                    {
                        Debug.LogError($"The popup {allPopups[i].name} used in multiple same type!", allPopups[i].gameObject);
                    }
                }

                string nameShouldBe = "Popup_" + allPopups[i].GetType().Name.Replace("Screen", "");
                if (allPopups[i].gameObject.name != nameShouldBe)
                {
                    allPopups[i].gameObject.name = nameShouldBe;
                    UnityEditor.EditorUtility.SetDirty(allPopups[i].gameObject);
                }
            }

            for (var i = 0; i < allOverlays.Count; i++)
            {
                for (var j = 0; j < allOverlays.Count; j++)
                {
                    if (i != j && allOverlays[j].GetType() == allOverlays[i].GetType())
                    {
                        Debug.LogError($"The overlay ui {allOverlays[i].name} used in multiple same type!", allOverlays[i].gameObject);
                    }
                }

                string nameShouldBe = "Overlay_" + allOverlays[i].GetType().Name.Replace("Screen", "");
                if (allOverlays[i].gameObject.name != nameShouldBe)
                {
                    allOverlays[i].gameObject.name = nameShouldBe;
                    UnityEditor.EditorUtility.SetDirty(allOverlays[i].gameObject);
                }
            }

            // Tabs
            allTabs = GetComponentsInChildren<TabBase>().ToList();

            for (var i = 0; i < allTabs.Count; i++)
            {
                for (var j = 0; j < allTabs.Count; j++)
                {
                    if (i != j && allTabs[j].GetType() == allTabs[i].GetType())
                    {
                        Debug.LogError($"The tab {allTabs[i].name} used in multiple same type!", allTabs[i].gameObject);
                    }
                }

                string nameShouldBe = "Tab_" + allTabs[i].GetType().Name.Replace("Tab", "");
                if (allTabs[i].gameObject.name != nameShouldBe)
                {
                    allTabs[i].gameObject.name = nameShouldBe;
                    UnityEditor.EditorUtility.SetDirty(allTabs[i].gameObject);
                }
            }
            
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif
    }
}