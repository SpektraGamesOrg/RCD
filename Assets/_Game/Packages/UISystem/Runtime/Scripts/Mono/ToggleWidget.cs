using System;
using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;

namespace UISystem.Runtime.Scripts.Mono
{
    public class ToggleWidget : UIWidget
    {
        [SerializeField] private Toggle toggle;
        [SerializeField] private ImageWidget background;
        [SerializeField] private ImageWidget handle;
        [SerializeField] private ImageWidget activeHandle;
        [SerializeField] private RectTransform handleTransform;

        [SerializeField] private RectTransform activeBackgroundRect;
        
        [SerializeField] private float animationDuration = 1f;
        
        private RectTransform _backgroundRect;

        public bool IsOn => toggle.isOn;
        public event Action<bool> OnValueChanged;
        
        private bool _initialized;

        private void Initialize()
        {
            if (_initialized)
                return;
            
            toggle = GetComponent<Toggle>();
            _backgroundRect = background.GetComponent<RectTransform>();
            toggle.onValueChanged.AddListener(OnToggleValueChanged);
            _initialized = true;
        }

        [Button]
        public void Set(bool isOn)
        {
            Initialize();
            toggle.SetIsOnWithoutNotify(isOn);
            SetUI(isOn);
            OnValueChanged?.Invoke(isOn);
        }
        
        private void OnToggleValueChanged(bool isOn)
        {
            SetUI(isOn);
            OnValueChanged?.Invoke(isOn);
        }

        private void SetUI(bool isOn)
        {
            activeHandle.Fade(isOn ? 1f : 0f, animationDuration);
            activeBackgroundRect.DOSizeDelta(
                new Vector2(isOn ? -1 * _backgroundRect.sizeDelta.x * 0.25f : -1 * _backgroundRect.sizeDelta.x * 0.75f,
                    0f), animationDuration);
            handleTransform.DOPivot(new Vector2(isOn ? 0 : 1, 0.5f), animationDuration);
        }
    }
}