using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Sirenix.OdinInspector;
using SpektraGames.SpektraUtilities.Runtime;

namespace UISystem.Runtime.Scripts.Mono
{
    public class GroupButtonWidget : MonoBehaviour
    {
        [Title("Selection Options")]
        [Tooltip("Initially selected button when the group is activated.")]
        [SerializeField]
        private ButtonWidget initialSelected;

        [Title("Behaviour")]
        [Tooltip("If enabled, clicking the selected button again will deselect it.")]
        public bool allowDeselectOnReselect = true;

        public bool disableReselectCallback = false;
        

        private List<ButtonWidget> _buttons = new();

        private ButtonWidget _lastSelected;

        public Action<ButtonWidget, bool> OnButtonSelected;
        public ButtonWidget previouslySelected;

        public ButtonWidget LastSelected => _lastSelected;

        public int Count => _buttons.Count;

        private void Awake()
        {
            _buttons = GetComponentsInChildren<ButtonWidget>(includeInactive: true).ToList();

            foreach (var button in _buttons)
            {
                var b = button;
                button.OnClick += () => OnButtonClicked(b);
            }
        }

        public int GetButtonIndex(ButtonWidget button)
        {
            return _buttons.IndexOf(button);
        }

        public void UnregisterClickListeners()
        {
            foreach (var button in _buttons)
            {
                button.OnClick.ForceRemoveAllListeners();
                button.OnClick = null;
            }
        }

        public void DestroyButtons()
        {
            if (_buttons == null || _buttons.Count == 0)
                _buttons = GetComponentsInChildren<ButtonWidget>(includeInactive: true).ToList();

            foreach (var button in _buttons)
            {
                button.OnClick.ForceRemoveAllListeners();
                button.OnClick = null;

                Destroy(button.gameObject);
            }

            _buttons.Clear();
        }

        public void ResetButtonsWith(List<ButtonWidget> buttons)
        {
            _buttons.Clear();
            _buttons.AddRange(buttons);

            foreach (var button in _buttons)
            {
                button.OnClick.ForceRemoveAllListeners();
                button.OnClick = null;
                var b = button;
                button.OnClick += () => OnButtonClicked(b);
            }
        }
        
        public void ResetButtons(bool removeLastSelected = false)
        {
            _buttons.Clear();

            _buttons = GetComponentsInChildren<ButtonWidget>(includeInactive: true).ToList();

            if (removeLastSelected && _buttons.Count <= 0)
                _lastSelected = null;

            foreach (var button in _buttons)
            {
                button.OnClick.ForceRemoveAllListeners();
                button.OnClick = null;
                var b = button;
                button.OnClick += () => OnButtonClicked(b);
            }
        }

        public void SelectFirst(bool isFromClick = false)
        {
            var firstButton = _buttons.FirstOrDefault();
            if (firstButton)
            {
                OnButtonClicked(firstButton, isFromClick);
            }
            else
            {
                Debug.LogError("No buttons found in the group to select.");
            }
        }

        public void SelectCustom(ButtonWidget button, bool isFromClickForce = false)
        {
            var a = _buttons.FirstOrDefault(b => b == button);
            if (a)
            {
                OnButtonClicked(a, isFromClickForce);
            }
            else
            {
                Debug.LogError("Button not found in the group to select.");
            }
        }
        
        public void SelectCustom(int index, bool isFromClickForce = false)
        {
            if (_buttons.HaveIndex(index))
            {
                OnButtonClicked(_buttons[index], isFromClickForce);
            }
            else
            {
                Debug.LogError("Button not found in the group to select: "  + index);
            }
        }
        
        public List<ButtonWidget> GetButtons()
        {
            if (_buttons == null || _buttons.Count == 0)
            {
                _buttons = GetComponentsInChildren<ButtonWidget>(includeInactive: true).ToList();
            }
            
            return _buttons;
        }
        
        private void OnDestroy()
        {
            foreach (var button in _buttons)
            {
                button.OnClick.ForceRemoveAllListeners();
                button.OnClick = null;
            }

            _buttons.Clear();
            _lastSelected = null;
        }

        private void Start()
        {
            //DeselectAll();
            if (initialSelected)
            {
                OnButtonClicked(initialSelected, false);
            }
        }

        private void OnButtonClicked(ButtonWidget clicked, bool isFromClick = true)
        {
            if (_lastSelected == clicked)
            {
                if (allowDeselectOnReselect)
                {
                    _lastSelected.Deselect();
                    _lastSelected = null;
                }
                else
                {
                    clicked.Select();
                }

                if (!disableReselectCallback)
                {
                    OnButtonSelected?.Invoke(clicked, isFromClick);
                }

                return;
            }


            if (_lastSelected && _lastSelected != clicked)
            {
                _lastSelected.Deselect();
            }

            clicked.Select();
            previouslySelected = _lastSelected;
            _lastSelected = clicked;
            OnButtonSelected?.Invoke(clicked, isFromClick);
        }


        public void DeselectAll()
        {
            if (!_lastSelected)
                return;

            _lastSelected.Deselect();
            _lastSelected = null;
        }
    }
}