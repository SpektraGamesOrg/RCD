using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.InputSystem;

namespace UIManager
{
    public abstract class ScreenBase : UIViewBase
    {
        public override ViewTypeEnum ViewType => ViewTypeEnum.ScreenBase;

        [SerializeField, BoxGroup("Screen Base")]
        private bool rebaseBackHistoryWhenOpened = false;
        public bool RebaseBackHistoryWhenOpened => rebaseBackHistoryWhenOpened;

        public void Open(bool immediate = false, System.Object uiData = null)
        {
            GameUIManager.Instance.SwitchScreen(this, immediate, uiData);
        }

        public async UniTaskVoid OpenAsync(bool immediate = false, System.Object uiData = null)
        {
            await GameUIManager.Instance.SwitchScreenAsync(this, immediate, uiData);
        }

        protected override void OnBeforeShowing(bool immediate, object uiData = null)
        {
            base.OnBeforeShowing(immediate, uiData);
        }

        protected override void OnShowed(bool immediate, object uiData = null)
        {
            base.OnShowed(immediate, uiData);
        }

        protected override void OnBeforeHiding(bool immediate = false)
        {
            base.OnBeforeHiding(immediate);
        }

        protected override void OnHidden(bool immediate = false)
        {
            base.OnHidden(immediate);
        }
    }
}