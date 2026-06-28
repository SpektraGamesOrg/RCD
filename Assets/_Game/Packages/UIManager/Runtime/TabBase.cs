using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using Sirenix.OdinInspector;
using UnityEngine;

namespace UIManager
{
    /// <summary>
    /// Abstract base class for tabs that can be displayed within a TabbedScreenBase.
    /// Tabs cannot be shown/hidden directly - they must be managed by their parent screen.
    /// </summary>
    public abstract class TabBase : UIViewBase
    {
        public override ViewTypeEnum ViewType => ViewTypeEnum.TabBase;

        [SerializeField, ReadOnly, BoxGroup("Tab Base")]
        private TabbedScreenBase parentScreen;

        /// <summary>
        /// The parent screen that manages this tab.
        /// </summary>
        public TabbedScreenBase ParentScreen => parentScreen;

        /// <summary>
        /// Whether this tab has a valid parent screen assigned.
        /// </summary>
        public bool HasParentScreen => parentScreen != null;

        protected override void Awake()
        {
            base.Awake();

            if (!HasParentScreen)
            {
                Debug.LogError($"TabBase '{gameObject.name}' does not have a parent screen assigned! " +
                               "Tabs must be connected to a TabbedScreenBase.", gameObject);
            }
        }

        protected override void OnHovered()
        {

            base.OnHovered();
        }

        /// <summary>
        /// Sets the parent screen reference. Called by TabbedScreenBase during initialization.
        /// </summary>
        internal void SetParentScreen(TabbedScreenBase screen)
        {
            parentScreen = screen;
        }

        /// <summary>
        /// Shows this tab. Can only be called internally by the parent screen.
        /// </summary>
        internal async UniTask ShowTabAsync(bool immediate = false, object uiData = null)
        {
            if (!HasParentScreen)
            {
                Debug.LogError($"Cannot show tab '{gameObject.name}' - no parent screen assigned!", gameObject);
                return;
            }

            await ShowAsync(immediate, uiData);
        }

        /// <summary>
        /// Hides this tab. Can only be called internally by the parent screen.
        /// </summary>
        internal async UniTask HideTabAsync(bool immediate = false)
        {
            if (!HasParentScreen)
            {
                Debug.LogError($"Cannot hide tab '{gameObject.name}' - no parent screen assigned!", gameObject);
                return;
            }

            await HideAsync(immediate);
        }

        /// <summary>
        /// Shows this tab (fire and forget). Can only be called internally by the parent screen.
        /// </summary>
        internal void ShowTab(bool immediate = false, object uiData = null)
        {
            ShowTabAsync(immediate, uiData).Forget();
        }

        /// <summary>
        /// Hides this tab (fire and forget). Can only be called internally by the parent screen.
        /// </summary>
        internal void HideTab(bool immediate = false)
        {
            HideTabAsync(immediate).Forget();
        }

#if UNITY_EDITOR
        [Button("Validate Parent Screen")]
        [BoxGroup("Tab Base")]
        private void ValidateParentScreen()
        {
            if (parentScreen == null)
            {
                // Try to find parent screen in hierarchy
                parentScreen = GetComponentInParent<TabbedScreenBase>();

                if (parentScreen != null)
                {
                    Debug.Log($"Found parent screen: {parentScreen.gameObject.name}", parentScreen.gameObject);
                    UnityEditor.EditorUtility.SetDirty(this);
                }
                else
                {
                    Debug.LogWarning($"No TabbedScreenBase found in parent hierarchy for tab '{gameObject.name}'", gameObject);
                }
            }
            else
            {
                Debug.Log($"Parent screen already assigned: {parentScreen.gameObject.name}", parentScreen.gameObject);
            }
        }
#endif
    }
}