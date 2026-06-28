using Sirenix.OdinInspector;
using UnityEngine;

namespace UISystem.Runtime.Scripts
{
    public abstract class UIWidget : MonoBehaviour
    {
        public RectTransform RectTransform => _rectTransform ??= transform as RectTransform;
        private RectTransform _rectTransform;

        public bool IsDestroyed { get; protected set; }

        #region EVENT_FUNCTIONS

        protected virtual void Awake()
        {
        }

        protected virtual void OnDestroy()
        {
            IsDestroyed = true;
        }

        #endregion
    }
}