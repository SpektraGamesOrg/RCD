using UnityEngine;

namespace UIManager
{
    public abstract class PopupBase : UIViewBase
    {
        public override ViewTypeEnum ViewType => ViewTypeEnum.PopupBase;

        public void Show(bool immediate = false, System.Object uiData = null)
        {
            GameUIManager.Instance.ShowPopup(this, immediate, uiData);
        }

        public void Hide(bool immediate = false)
        {
            GameUIManager.Instance.HidePopup(this, immediate);
        }
    }
}