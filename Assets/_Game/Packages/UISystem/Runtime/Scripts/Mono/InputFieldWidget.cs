using System;
using Cysharp.Threading.Tasks;
using TMPro;
using UISystem.Runtime.Scripts;
using UnityEngine;

namespace Packages.UISystem.Runtime.Scripts.Mono
{
    public class InputFieldWidget : MonoBehaviour
    {
        public TMP_InputField inputField;
        public int minLength = 3;
        public int maxLength = 15;

        public Action<bool, string, string> OnInputFieldEndEdit;
        public Action OnInputFieldSelect;
        
        private string _previousText = string.Empty;
        
        private void Awake()
        {
            inputField.characterLimit = maxLength;
            // inputField.onValueChanged.AddListener(OnValueChanged);
            inputField.onEndEdit.AddListener(ValidateInput);
            inputField.onSelect.AddListener(OnSelect);
            inputField.characterLimit = maxLength;
        }

        private void OnSelect(string text)
        {
            OnInputFieldSelect?.Invoke();
        }

        public void SetText(string text)
        {
            inputField.text = text;
            _previousText = text;
        }
        
        public void EnableInputField()
        {
            inputField.ActivateInputField();
        }

        public void DisableInputField()
        {
            inputField.DeactivateInputField();
            inputField.OnSubmitAsync().Forget();
        }
        
        void OnDestroy()
        {
            // inputField.onValueChanged.RemoveListener(OnValueChanged);
            inputField.onEndEdit.RemoveListener(ValidateInput);
        }

        void OnValueChanged(string text)
        {
            if (text.Length > maxLength)
            {
                inputField.text = text.Substring(0, maxLength);
            }
        }

        void ValidateInput(string text)
        {
            if (text.Length > maxLength)
            {
                inputField.text = text.Substring(0, maxLength);
            }

            var success = text.Length >= minLength && text.Length <= maxLength;
            var isSame = string.Equals(text, _previousText, StringComparison.Ordinal);
            
            if (!success)
            {
                inputField.text = _previousText; // Revert to previous text if validation fails
            }
            else
            {
                _previousText = text; // Update previous text only on successful validation
            }

            if (isSame)
            {
                OnInputFieldEndEdit?.Invoke(false, inputField.text, "");
            }
            else
            {
                OnInputFieldEndEdit?.Invoke(success, inputField.text, "Username must be between " + minLength + " and " + maxLength + " characters.");
            }
        }
    }
}