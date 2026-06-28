using System;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace UIManager
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(0)]
    public class EnhancedButton : Button, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        public enum PublicState
        {
            Normal,
            Highlighted,
            Pressed,
            Selected,
            Disabled
        }

        [ShowInInspector]
        public PublicState State
        {
            get
            {
                var state = (PublicState)(int)currentSelectionState;

                if (state == PublicState.Disabled)
                {
                    return interactable ? PublicState.Normal : PublicState.Disabled;
                }

                return state;
            }
        }

        private PublicState _lastState;

        public event Action<PublicState> StateChanged;
        public Action OnPointerEnterAction;
        public Action OnPointerExitAction;
        public Action OnPointerDownAction;
        public Action OnPointerUpAction;

        protected override void Awake()
        {
            base.Awake();
            _lastState = State;
        }

        [ContextMenu("Log Current State")]
        private void LogCurrentState()
        {
            Debug.LogError(State.ToString());
        }

        [ContextMenu("Set Selected GameObject")]
        private void SetSelectedGameObject()
        {
            EventSystem.current.SetSelectedGameObject(gameObject);
        }

        protected override void DoStateTransition(SelectionState state, bool instant)
        {
            base.DoStateTransition(state, instant);
            RaiseIfChanged();
        }

        private void RaiseIfChanged()
        {
            var s = State;
            if (s == _lastState) return;
            _lastState = s;
            StateChanged?.Invoke(s);
        }

        // These are the main transitions in Selectable:
        public override void OnPointerEnter(PointerEventData eventData)
        {
            base.OnPointerEnter(eventData);
            RaiseIfChanged();
            OnPointerEnterAction?.Invoke();
        }

        public override void OnPointerExit(PointerEventData eventData)
        {
            base.OnPointerExit(eventData);
            RaiseIfChanged();
            OnPointerExitAction?.Invoke();
        }

        public override void OnPointerDown(PointerEventData eventData)
        {
            base.OnPointerDown(eventData);
            RaiseIfChanged();
            OnPointerDownAction?.Invoke();
        }

        public override void OnPointerUp(PointerEventData eventData)
        {
            base.OnPointerUp(eventData);
            RaiseIfChanged();
            OnPointerUpAction?.Invoke();
        }

        public override void OnSelect(BaseEventData eventData)
        {
            base.OnSelect(eventData);
            RaiseIfChanged();
        }

        public override void OnDeselect(BaseEventData eventData)
        {
            base.OnDeselect(eventData);
            RaiseIfChanged();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            _lastState = State;
            RaiseIfChanged();
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            // Optional: treat disabled as a state
            // (note: OnDisable means the component is inactive, not just non-interactable)
        }

        // Called when properties change in editor/animation, and also when interactable changes internally
        protected override void OnDidApplyAnimationProperties()
        {
            base.OnDidApplyAnimationProperties();
            RaiseIfChanged();
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            RaiseIfChanged();
        }
#endif
    }
}