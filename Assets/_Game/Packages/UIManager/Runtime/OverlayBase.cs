using Sirenix.OdinInspector;
using UnityEngine;

namespace UIManager
{
    public abstract class OverlayBase : UIViewBase
    {
        public override ViewTypeEnum ViewType => ViewTypeEnum.OverlayBase;

        [SerializeField, BoxGroup("Overlay Base")]
        private bool alwaysShow = false;
        public bool AlwaysShow => alwaysShow;

        [SerializeField, BoxGroup("Overlay Base")]
        private bool hideFromActiveUis = false;
        public bool HideFromActiveUis => hideFromActiveUis;

        public void Show(bool immediate = false, System.Object uiData = null)
        {
            GameUIManager.Instance.ShowOverlayUI(this, immediate, uiData);
        }

        public void Hide(bool immediate = false)
        {
            GameUIManager.Instance.HideOverlayUI(this, immediate);
        }
    }
}