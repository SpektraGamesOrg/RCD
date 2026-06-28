using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.InputSystem;

namespace UIManager
{
    /// <summary>
    /// Abstract base class for screens that contain switchable tabs.
    /// Manages tab lifecycle and provides methods for switching between tabs.
    /// </summary>
    public abstract class TabbedScreenBase : ScreenBase
    {
        public override ViewTypeEnum ViewType => ViewTypeEnum.TabbedScreenBase;

        [SerializeField, BoxGroup("Tabbed Screen")]
        private TabBase[] tabs = Array.Empty<TabBase>();

        [SerializeField, BoxGroup("Tabbed Screen")]
        private TabButton[] tabButtons = Array.Empty<TabButton>();

        [SerializeField, BoxGroup("Tabbed Screen")]
        protected TabBase defaultTab;

        /// <summary>
        /// All tabs managed by this screen.
        /// </summary>
        public IReadOnlyList<TabBase> Tabs => tabs;

        /// <summary>
        /// All tab buttons in this screen.
        /// </summary>
        public IReadOnlyList<TabButton> TabButtons => tabButtons;

        /// <summary>
        /// The currently active tab.
        /// </summary>
        [ShowInInspector, ReadOnly, BoxGroup("Tabbed Screen")]
        public TabBase ActiveTab { get; private set; }

        /// <summary>
        /// Event fired when the active tab changes.
        /// </summary>
        public event Action<TabBase, TabBase> OnTabChanged;

        protected override void Awake()
        {
            base.Awake();
            InitializeTabs();
        }

        /// <summary>
        /// Initializes all tabs by setting their parent screen reference.
        /// </summary>
        private void InitializeTabs()
        {
            for (int i = 0; i < tabs.Length; i++)
            {
                if (tabs[i] != null)
                {
                    tabs[i].SetParentScreen(this);
                }
                else
                {
                    Debug.LogError($"Null tab at index {i} in TabbedScreenBase '{gameObject.name}'", gameObject);
                }
            }

            // Validate default tab
            if (defaultTab == null && tabs.Length > 0)
            {
                defaultTab = tabs[0];
                Debug.Log($"No default tab set for '{gameObject.name}', using first tab.", gameObject);
            }

            // Validate default tab is in tabs array
            if (defaultTab != null && Array.IndexOf(tabs, defaultTab) < 0)
            {
                Debug.LogError($"Default tab '{defaultTab.gameObject.name}' is not in the tabs array!", gameObject);
            }
        }

        protected override void OnBeforeShowing(bool immediate, object uiData = null)
        {
            base.OnBeforeShowing(immediate, uiData);

            // Hide all tabs initially
            for (int i = 0; i < tabs.Length; i++)
            {
                if (tabs[i] != null && tabs[i].ShowingOrShown)
                {
                    tabs[i].HideTab(true);
                }
            }
        }

        protected override void OnHovered()
        {
            base.OnHovered();

            for (int i = 0; i < tabs.Length; i++)
            {
                if (tabs[i] != null && tabs[i].ShowingOrShown)
                {
                    tabs[i].InvokeOnHovered();
                }
            }
        }

        protected override void OnUnHovered()
        {
            base.OnUnHovered();

            for (int i = 0; i < tabs.Length; i++)
            {
                if (tabs[i] != null && tabs[i].ShowingOrShown)
                {
                    tabs[i].InvokeOnUnHovered();
                }
            }
        }

        protected override void OnShowed(bool immediate, object uiData = null)
        {
            base.OnShowed(immediate, uiData);

            // Show the default tab when screen is shown
            if (defaultTab != null)
            {
                SwitchTab(defaultTab, immediate, uiData);
            }
        }

        protected override void OnBeforeHiding(bool immediate = false)
        {
            base.OnBeforeHiding(immediate);

            // Hide active tab when screen is hiding
            if (ActiveTab != null && ActiveTab.ShowingOrShown)
            {
                ActiveTab.HideTab(immediate);
            }
        }

        protected override void OnHidden(bool immediate = false)
        {
            base.OnHidden(immediate);
            ActiveTab = null;
            UpdateTabButtonStates();
        }

        /// <summary>
        /// Switches to the specified tab.
        /// </summary>
        public void SwitchTab(TabBase tab, bool immediate = false, object uiData = null)
        {
            SwitchTabAsync(tab, immediate, uiData).Forget();
        }

        /// <summary>
        /// Switches to the specified tab asynchronously.
        /// </summary>
        public async UniTask SwitchTabAsync(TabBase tab, bool immediate = false, object uiData = null)
        {
            if (tab == null)
            {
                Debug.LogError("Cannot switch to null tab!", gameObject);
                return;
            }

            // Validate tab belongs to this screen
            if (Array.IndexOf(tabs, tab) < 0)
            {
                Debug.LogError($"Tab '{tab.gameObject.name}' does not belong to screen '{gameObject.name}'!", gameObject);
                return;
            }

            // Don't switch if already on this tab
            if (ActiveTab == tab && tab.ShowingOrShown)
            {
                return;
            }

            TabBase previousTab = ActiveTab;

            // Hide current tab
            if (ActiveTab != null && ActiveTab.ShowingOrShown)
            {
                ActiveTab.HideTab(immediate);
            }

            // Show new tab
            ActiveTab = tab;
            await tab.ShowTabAsync(immediate, uiData);

            // Update button states
            UpdateTabButtonStates();

            // Fire event
            OnTabChanged?.Invoke(previousTab, ActiveTab);
        }

        /// <summary>
        /// Switches to a tab by type.
        /// </summary>
        public void SwitchTab<T>(bool immediate = false, object uiData = null) where T : TabBase
        {
            TabBase tab = GetTab<T>();
            if (tab != null)
            {
                SwitchTab(tab, immediate, uiData);
            }
        }

        /// <summary>
        /// Switches to a tab by type asynchronously.
        /// </summary>
        public async UniTask SwitchTabAsync<T>(bool immediate = false, object uiData = null) where T : TabBase
        {
            TabBase tab = GetTab<T>();
            if (tab != null)
            {
                await SwitchTabAsync(tab, immediate, uiData);
            }
        }

        /// <summary>
        /// Gets a tab by type.
        /// </summary>
        public T GetTab<T>() where T : TabBase
        {
            Type targetType = typeof(T);

            for (int i = 0; i < tabs.Length; i++)
            {
                if (tabs[i] != null && tabs[i].GetType() == targetType)
                {
                    return tabs[i] as T;
                }
            }

            Debug.LogError($"Tab of type '{targetType.Name}' not found in screen '{gameObject.name}'!", gameObject);
            return null;
        }

        /// <summary>
        /// Gets the index of the currently active tab.
        /// Returns -1 if no tab is active.
        /// </summary>
        public int GetActiveTabIndex()
        {
            if (ActiveTab == null)
                return -1;

            return Array.IndexOf(tabs, ActiveTab);
        }

        /// <summary>
        /// Opens the next tab in the list. Wraps around to the first tab if at the end.
        /// </summary>
        public void OpenNextTab(bool immediate = false, object uiData = null)
        {
            OpenNextTabAsync(immediate, uiData).Forget();
        }

        /// <summary>
        /// Opens the next tab in the list asynchronously. Wraps around to the first tab if at the end.
        /// </summary>
        public async UniTask OpenNextTabAsync(bool immediate = false, object uiData = null)
        {
            if (tabs.Length == 0)
            {
                Debug.LogWarning($"No tabs available in '{gameObject.name}'", gameObject);
                return;
            }

            int currentIndex = GetActiveTabIndex();
            int nextIndex = (currentIndex + 1) % tabs.Length;

            await SwitchTabAsync(tabs[nextIndex], immediate, uiData);
        }

        /// <summary>
        /// Opens the previous tab in the list. Wraps around to the last tab if at the beginning.
        /// </summary>
        public void OpenPreviousTab(bool immediate = false, object uiData = null)
        {
            OpenPreviousTabAsync(immediate, uiData).Forget();
        }

        /// <summary>
        /// Opens the previous tab in the list asynchronously. Wraps around to the last tab if at the beginning.
        /// </summary>
        public async UniTask OpenPreviousTabAsync(bool immediate = false, object uiData = null)
        {
            if (tabs.Length == 0)
            {
                Debug.LogWarning($"No tabs available in '{gameObject.name}'", gameObject);
                return;
            }

            int currentIndex = GetActiveTabIndex();
            int previousIndex = currentIndex <= 0 ? tabs.Length - 1 : currentIndex - 1;

            await SwitchTabAsync(tabs[previousIndex], immediate, uiData);
        }

        /// <summary>
        /// Opens a tab by its index in the tabs array.
        /// </summary>
        public void OpenTabByIndex(int index, bool immediate = false, object uiData = null)
        {
            OpenTabByIndexAsync(index, immediate, uiData).Forget();
        }

        /// <summary>
        /// Opens a tab by its index in the tabs array asynchronously.
        /// </summary>
        public async UniTask OpenTabByIndexAsync(int index, bool immediate = false, object uiData = null)
        {
            if (index < 0 || index >= tabs.Length)
            {
                Debug.LogError($"Tab index {index} is out of range (0-{tabs.Length - 1}) in '{gameObject.name}'", gameObject);
                return;
            }

            await SwitchTabAsync(tabs[index], immediate, uiData);
        }

        /// <summary>
        /// Updates all tab button visual states based on the active tab.
        /// </summary>
        private void UpdateTabButtonStates()
        {
            for (int i = 0; i < tabButtons.Length; i++)
            {
                if (tabButtons[i] != null)
                {
                    bool isActive = tabButtons[i].TargetTab == ActiveTab;
                    tabButtons[i].SetActiveState(isActive);
                }
            }
        }

#if UNITY_EDITOR
        [Button("Auto-Find Tabs")]
        [BoxGroup("Tabbed Screen")]
        private void AutoFindTabs()
        {
            tabs = GetComponentsInChildren<TabBase>(true);

            for (int i = 0; i < tabs.Length; i++)
            {
                tabs[i].SetParentScreen(this);
                UnityEditor.EditorUtility.SetDirty(tabs[i]);
            }

            if (tabs.Length > 0 && defaultTab == null)
            {
                defaultTab = tabs[0];
            }

            Debug.Log($"Found {tabs.Length} tabs in '{gameObject.name}'", gameObject);
            UnityEditor.EditorUtility.SetDirty(this);
        }

        [Button("Auto-Find Tab Buttons")]
        [BoxGroup("Tabbed Screen")]
        private void AutoFindTabButtons()
        {
            tabButtons = GetComponentsInChildren<TabButton>(true);

            Debug.Log($"Found {tabButtons.Length} tab buttons in '{gameObject.name}'", gameObject);
            UnityEditor.EditorUtility.SetDirty(this);
        }

        [Button("Validate Setup")]
        [BoxGroup("Tabbed Screen")]
        private void ValidateSetup()
        {
            bool isValid = true;

            if (tabs.Length == 0)
            {
                Debug.LogWarning($"No tabs assigned to '{gameObject.name}'", gameObject);
                isValid = false;
            }

            if (defaultTab == null)
            {
                Debug.LogWarning($"No default tab set for '{gameObject.name}'", gameObject);
                isValid = false;
            }

            for (int i = 0; i < tabs.Length; i++)
            {
                if (tabs[i] == null)
                {
                    Debug.LogError($"Null tab at index {i}", gameObject);
                    isValid = false;
                }
                else if (tabs[i].ParentScreen != this)
                {
                    Debug.LogWarning($"Tab '{tabs[i].gameObject.name}' has incorrect parent screen reference", tabs[i].gameObject);
                }
            }

            for (int i = 0; i < tabButtons.Length; i++)
            {
                if (tabButtons[i] == null)
                {
                    Debug.LogError($"Null tab button at index {i}", gameObject);
                    isValid = false;
                }
                else
                {
                    if (tabButtons[i].ParentScreen != this)
                    {
                        Debug.LogWarning($"Tab button '{tabButtons[i].gameObject.name}' has incorrect parent screen reference", tabButtons[i].gameObject);
                    }
                    if (tabButtons[i].TargetTab == null)
                    {
                        Debug.LogWarning($"Tab button '{tabButtons[i].gameObject.name}' has no target tab", tabButtons[i].gameObject);
                    }
                }
            }

            if (isValid)
            {
                Debug.Log($"Tabbed screen '{gameObject.name}' setup is valid!", gameObject);
            }
        }
#endif
    }
}