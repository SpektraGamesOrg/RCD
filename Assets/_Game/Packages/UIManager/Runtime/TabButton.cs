using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;

namespace UIManager
{
    /// <summary>
    /// Button component that switches between tabs in a TabbedScreenBase.
    /// Should be placed within the screen's hierarchy and configured with references
    /// to both the parent screen and target tab.
    /// </summary>
    public class TabButton : MonoBehaviour
    {
        [SerializeField, Required, BoxGroup("Tab Button")]
        private TabbedScreenBase parentScreen;

        [SerializeField, Required, BoxGroup("Tab Button")]
        private TabBase targetTab;

        [SerializeField, Required, BoxGroup("Tab Button")]
        private EnhancedButton button;

        [SerializeField, BoxGroup("Visual State")]
        private GameObject activeVisual;

        [SerializeField, BoxGroup("Visual State")]
        private GameObject inactiveVisual;

        [SerializeField, BoxGroup("Settings")]
        private bool switchImmediate = false;

        /// <summary>
        /// The parent screen this button belongs to.
        /// </summary>
        public TabbedScreenBase ParentScreen => parentScreen;

        /// <summary>
        /// The tab this button will switch to when clicked.
        /// </summary>
        public TabBase TargetTab => targetTab;

        /// <summary>
        /// Whether this button's target tab is currently active.
        /// </summary>
        [ShowInInspector, ReadOnly, BoxGroup("Tab Button")]
        public bool IsActive { get; private set; }

        private void OnEnable()
        {
            if (button != null)
            {
                button.onClick.AddListener(OnClick);
            }
        }

        private void OnDisable()
        {
            if (button != null)
            {
                button.onClick.RemoveListener(OnClick);
            }
        }

        private void Start()
        {
            ValidateReferences();
        }

        /// <summary>
        /// Validates that all required references are set.
        /// </summary>
        private void ValidateReferences()
        {
            if (parentScreen == null)
            {
                Debug.LogError($"TabButton '{gameObject.name}' has no parent screen assigned!", gameObject);
            }

            if (targetTab == null)
            {
                Debug.LogError($"TabButton '{gameObject.name}' has no target tab assigned!", gameObject);
            }

            if (button == null)
            {
                Debug.LogError($"TabButton '{gameObject.name}' has no button assigned!", gameObject);
            }

            // Validate target tab belongs to parent screen
            if (parentScreen != null && targetTab != null)
            {
                bool found = false;
                foreach (var tab in parentScreen.Tabs)
                {
                    if (tab == targetTab)
                    {
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    Debug.LogError($"TabButton '{gameObject.name}' target tab does not belong to parent screen!", gameObject);
                }
            }
        }

        /// <summary>
        /// Called when the button is clicked.
        /// </summary>
        private void OnClick()
        {
            if (parentScreen == null || targetTab == null)
            {
                Debug.LogError($"TabButton '{gameObject.name}' cannot switch tab - missing references!", gameObject);
                return;
            }

            parentScreen.SwitchTab(targetTab, switchImmediate);
        }

        /// <summary>
        /// Sets the visual state of this button.
        /// Called by TabbedScreenBase when the active tab changes.
        /// </summary>
        public void SetActiveState(bool isActive)
        {
            IsActive = isActive;

            if (activeVisual != null)
            {
                activeVisual.SetActive(isActive);
            }

            if (inactiveVisual != null)
            {
                inactiveVisual.SetActive(!isActive);
            }

            // Optionally disable button interactability when active
            // button.interactable = !isActive;
        }

#if UNITY_EDITOR
        [Button("Auto-Find References")]
        [BoxGroup("Tab Button")]
        private void AutoFindReferences()
        {
            // Find button if not set
            if (button == null)
            {
                button = GetComponent<EnhancedButton>();
                if (button == null)
                {
                    button = GetComponentInChildren<EnhancedButton>();
                }
            }

            // Find parent screen if not set
            if (parentScreen == null)
            {
                parentScreen = GetComponentInParent<TabbedScreenBase>();
            }

            if (button != null)
            {
                Debug.Log($"Found button: {button.gameObject.name}", button.gameObject);
            }
            else
            {
                Debug.LogWarning("No Button component found", gameObject);
            }

            if (parentScreen != null)
            {
                Debug.Log($"Found parent screen: {parentScreen.gameObject.name}", parentScreen.gameObject);
            }
            else
            {
                Debug.LogWarning("No TabbedScreenBase found in parent hierarchy", gameObject);
            }

            UnityEditor.EditorUtility.SetDirty(this);
        }

        private void Reset()
        {
            button = GetComponent<EnhancedButton>();
            if (button == null)
            {
                button = GetComponentInChildren<EnhancedButton>();
            }

            parentScreen = GetComponentInParent<TabbedScreenBase>();
        }
#endif
    }
}