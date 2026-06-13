using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Sirenix.OdinInspector;
using SpektraGames.SpektraUtilities.Runtime;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

#if UNITY_EDITOR
using NUnit.Framework;
using UnityEditor;
using Sirenix.OdinInspector.Editor;
using UnityEditorInternal;
#endif

namespace UIManager
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(1)]
    public class ButtonUIState : MonoBehaviour
    {
        [ShowInInspector]
        [HideInEditorMode]
        private EnhancedButton.PublicState? _lastAppliedState = null;

        // [SerializeField]
        // private List<EnhancedButton.PublicState> listenStates = new()
        // {
        //     EnhancedButton.PublicState.Normal,
        //     EnhancedButton.PublicState.Highlighted,
        //     EnhancedButton.PublicState.Pressed,
        //     EnhancedButton.PublicState.Selected,
        //     EnhancedButton.PublicState.Disabled
        // };

        [SerializeField]
        private bool listenButtonStates = true;

        [SerializeField]
        private Image targetGraphic = null;
        [SerializeField]
        private EnhancedButton enhancedButton = null;
        [SerializeField]
        private bool canChangeSprite = false;
        [SerializeField]
        private bool canChangeTextProperties = false;
        [ShowIf(nameof(canChangeTextProperties))]
        [LabelText("   Target Text")]
        public TMP_Text targetText = null;
        [ShowIf(nameof(canChangeTextProperties))]
        [LabelText("   Change Font Style")]
        public bool changeFontStyle = false;
        [ShowIf(nameof(canChangeTextProperties))]
        [LabelText("   Change Text Color")]
        [PropertySpace(0f, 30f)]
        public bool changeTextColor = true;

        [SerializeField, TabGroup("Normal")]
        private ButtonUIStateData normalState = new ButtonUIStateData() { };
        [SerializeField, TabGroup("Highlighted")]
        private ButtonUIStateData highlightedState = new ButtonUIStateData() { };
        [SerializeField, TabGroup("Pressed")]
        private ButtonUIStateData pressedState = new ButtonUIStateData() { };
        [SerializeField, TabGroup("Selected")]
        private ButtonUIStateData selectedState = new ButtonUIStateData() { };
        [SerializeField, TabGroup("Disabled")]
        private ButtonUIStateData disabledState = new ButtonUIStateData() { };

        private void Start()
        {
        }

        private void OnEnable()
        {
            if (enhancedButton)
            {
                enhancedButton.StateChanged += OnStateChanged;
                if (listenButtonStates)
                    ApplyState(enhancedButton.State);
            }
        }

        private void OnDisable()
        {
            if (enhancedButton)
                enhancedButton.StateChanged -= OnStateChanged;
        }

        private void OnStateChanged(EnhancedButton.PublicState state)
        {
            if (listenButtonStates)
                ApplyState(state);
        }

        public void ApplyState(EnhancedButton.PublicState publicState)
        {
            _lastAppliedState = publicState;

            ButtonUIStateData stateToApply = null;

            if (publicState == EnhancedButton.PublicState.Normal)
            {
                stateToApply = normalState;
            }
            else if (publicState == EnhancedButton.PublicState.Highlighted)
            {
                stateToApply = highlightedState;
            }
            else if (publicState == EnhancedButton.PublicState.Pressed)
            {
                stateToApply = pressedState;
            }
            else if (publicState == EnhancedButton.PublicState.Selected)
            {
                stateToApply = selectedState;
            }
            else if (publicState == EnhancedButton.PublicState.Disabled)
            {
                stateToApply = disabledState;
            }
            else
            {
                stateToApply = normalState;
            }

            if (targetGraphic)
            {
                targetGraphic.color = stateToApply.imageColor;

                if (canChangeSprite && stateToApply.targetSprite)
                {
                    targetGraphic.sprite = stateToApply.targetSprite;
                }

#if UNITY_EDITOR
                if (!Application.isPlaying)
                    UnityEditor.EditorUtility.SetDirty(targetGraphic);
#endif
            }

            if (canChangeTextProperties && targetText)
            {
                if (changeFontStyle)
                {
                    targetText.fontStyle = stateToApply.fontStyle;
                }

                if (changeTextColor)
                {
                    targetText.color = stateToApply.textColor;
                }

#if UNITY_EDITOR
                if (!Application.isPlaying)
                    UnityEditor.EditorUtility.SetDirty(targetText);
#endif
            }
        }

        public ButtonUIStateData GetState(EnhancedButton.PublicState publicState)
        {
            ButtonUIStateData state = null;

            if (publicState == EnhancedButton.PublicState.Normal)
            {
                state = normalState;
            }
            else if (publicState == EnhancedButton.PublicState.Highlighted)
            {
                state = highlightedState;
            }
            else if (publicState == EnhancedButton.PublicState.Pressed)
            {
                state = pressedState;
            }
            else if (publicState == EnhancedButton.PublicState.Selected)
            {
                state = selectedState;
            }
            else if (publicState == EnhancedButton.PublicState.Disabled)
            {
                state = disabledState;
            }
            else
            {
                state = normalState;
            }

            return state;
        }

#if UNITY_EDITOR
        private void Reset()
        {
            if (!GetComponent<Button>() &&
                !GetComponent<EnhancedButton>())
            {
                Debug.LogError("There is no button found in game object", gameObject);
                DestroyAfterWhile(this).Forget();
                return;
            }

            if (GetComponent<Button>() && !GetComponent<EnhancedButton>())
            {
                // Convert this button to EnhancedButton
                ConvertButtonToEnhanced(GetComponent<Button>());
            }

            if (!targetGraphic)
            {
                targetGraphic = GetComponent<Button>().targetGraphic as Image;
                UnityEditor.EditorUtility.SetDirty(gameObject);
            }

            if (!enhancedButton)
            {
                enhancedButton = GetComponent<EnhancedButton>();
                UnityEditor.EditorUtility.SetDirty(gameObject);
            }

            if (enhancedButton)
            {
                var colors = enhancedButton.colors;

                colors.normalColor = Color.white;
                colors.highlightedColor = Color.white;
                colors.pressedColor = Color.white;
                colors.selectedColor = Color.white;
                colors.disabledColor = Color.white;

                enhancedButton.colors = colors;
                UnityEditor.EditorUtility.SetDirty(gameObject);
            }

            SetReferencesForEditor();
            ClearHideFlags();
        }

        private void OnValidate()
        {
            SetReferencesForEditor();
            ClearHideFlags();
        }

        private void SetReferencesForEditor()
        {
            bool anyChange = false;

            if (normalState.parent != this || normalState.publicState != EnhancedButton.PublicState.Normal)
            {
                normalState.parent = this;
                normalState.publicState = EnhancedButton.PublicState.Normal;
                anyChange = true;
            }

            if (highlightedState.parent != this || highlightedState.publicState != EnhancedButton.PublicState.Highlighted)
            {
                highlightedState.parent = this;
                highlightedState.publicState = EnhancedButton.PublicState.Highlighted;
                anyChange = true;
            }

            if (pressedState.parent != this || pressedState.publicState != EnhancedButton.PublicState.Pressed)
            {
                pressedState.parent = this;
                pressedState.publicState = EnhancedButton.PublicState.Pressed;
                anyChange = true;
            }

            if (selectedState.parent != this || selectedState.publicState != EnhancedButton.PublicState.Selected)
            {
                selectedState.parent = this;
                selectedState.publicState = EnhancedButton.PublicState.Selected;
                anyChange = true;
            }

            if (disabledState.parent != this || disabledState.publicState != EnhancedButton.PublicState.Disabled)
            {
                disabledState.parent = this;
                disabledState.publicState = EnhancedButton.PublicState.Disabled;
                anyChange = true;
            }

            if (!enhancedButton)
            {
                enhancedButton = GetComponent<EnhancedButton>();
                anyChange = true;
            }

            if (anyChange)
            {
                UnityEditor.EditorUtility.SetDirty(this);
            }
        }

        private void ClearHideFlags()
        {
            var go = gameObject;

            if (!go.hideFlags.HasFlag(HideFlags.DontSave) &&
                !this.hideFlags.HasFlag(HideFlags.DontSave) &&
                !go.hideFlags.HasFlag(HideFlags.HideAndDontSave) &&
                !this.hideFlags.HasFlag(HideFlags.HideAndDontSave) &&
                (int)go.hideFlags != 64 &&
                (int)this.hideFlags != 64)
            {
                return;
            }

            Undo.RegisterFullObjectHierarchyUndo(go, "ClearHideFlags");

            go.hideFlags = HideFlags.None;

            foreach (var c in go.GetComponents<Component>())
            {
                if (!c) continue;
                c.hideFlags = HideFlags.None;
                EditorUtility.SetDirty(c);
            }

            EditorUtility.SetDirty(go);
            Debug.Log("Cleared hideFlags on selected GameObject + components.");
        }

        private async UniTask DestroyAfterWhile(ButtonUIState button)
        {
            await UniTask.NextFrame();

            if (button)
            {
                UnityEditor.Undo.RecordObject(gameObject, "_Destroy");
                DestroyImmediate(button);
                if (gameObject)
                    UnityEditor.EditorUtility.SetDirty(gameObject);
            }
        }

        private void ConvertButtonToEnhanced(Button oldButton)
        {
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();

            var go = oldButton.gameObject;

            // cache data
            bool interactable = oldButton.interactable;
            Graphic oldTargetGraphic = oldButton.targetGraphic;
            var oldOnClick = oldButton.onClick;
            Color normalColor = oldTargetGraphic.color * oldButton.colors.normalColor;
            Color highlightedColor = oldTargetGraphic.color * oldButton.colors.highlightedColor;
            Color pressedColor = oldTargetGraphic.color * oldButton.colors.pressedColor;
            Color selectedColor = oldTargetGraphic.color * oldButton.colors.selectedColor;
            Color disabledColor = oldTargetGraphic.color * oldButton.colors.disabledColor;

            // IMPORTANT: Undo-aware destroy
            Undo.DestroyObjectImmediate(oldButton);

            // IMPORTANT: Undo-aware add
            var newButton = Undo.AddComponent<EnhancedButton>(go);

            // record changes on the new component (fields)
            Undo.RecordObject(newButton, "_Convert");
            newButton.interactable = interactable;
            newButton.targetGraphic = oldTargetGraphic;
            newButton.onClick = oldOnClick;

            // record your own component change too
            Undo.RecordObject(this, "_Convert");

            this.targetGraphic = oldTargetGraphic as Image;
            this.normalState.imageColor = normalColor;
            this.highlightedState.imageColor = highlightedColor;
            this.pressedState.imageColor = pressedColor;
            this.selectedState.imageColor = selectedColor;
            this.disabledState.imageColor = disabledColor;

            // move your component (ordering) - also Undoable usually, but no harm if not
            while (ComponentUtility.MoveComponentDown(this))
            {
            }

            EditorUtility.SetDirty(go);
            Undo.CollapseUndoOperations(group);
        }
#endif

        [System.Serializable]
        [InlineProperty]
        [HideLabel]
        public class ButtonUIStateData
        {
            [HideInInspector]
            public ButtonUIState parent = null;
            [HideInInspector]
            public EnhancedButton.PublicState publicState = EnhancedButton.PublicState.Normal;

            [Space]
            public Color imageColor = Color.white;

            [Title("Change Sprite")]
            [ShowIf("@parent." + nameof(canChangeSprite))]
            [LabelText("   Target Sprite")]
            public Sprite targetSprite = null;

            [Title("Change Font Properties")]
            [ShowInInspector, HideLabel, DisplayAsString]
            private string _infoForFonts = "Properties:";

            [ShowIf("@parent." + nameof(canChangeTextProperties))]
            [ShowIf("@parent." + nameof(targetText))]
            [EnableIf("@parent." + nameof(changeFontStyle))]
            [LabelText("@parent." + nameof(changeFontStyle) + " ? \"   Font Style\" : \"   Font Style(Disabled)\"")]
            public FontStyles fontStyle = FontStyles.Normal;

            [ShowIf("@parent." + nameof(canChangeTextProperties))]
            [ShowIf("@parent." + nameof(targetText))]
            [EnableIf("@parent." + nameof(changeTextColor))]
            [LabelText("@parent." + nameof(changeTextColor) + " ? \"   Text Color\" : \"   Text Color(Disabled)\"")]
            [PropertySpace(0f, 30f)]
            public Color textColor = Color.white;

            [HorizontalGroup("ActionButtons"), Button("Copy State", Icon = SdfIconType.Clipboard)]
            [GUIColor("yellow")]
            private void CopySettings()
            {
                string json = JsonUtility.ToJson(this, true);
                json.CopyToClipboard();
            }

            [HorizontalGroup("ActionButtons"), Button("Paste State", Icon = SdfIconType.ClipboardCheck)]
            [GUIColor("yellow")]
            private void PasteSettings()
            {
                string json = SpektraHelpers.Other.GetClipboardText();

                if (string.IsNullOrEmpty(json) || !json.Contains(nameof(publicState)))
                {
                    Debug.LogError("Json is invalid: " + json);
                    return;
                }

                ButtonUIStateData newState = JsonUtility.FromJson<ButtonUIStateData>(json);

                if (parent)
                {
#if UNITY_EDITOR
                    if (publicState == EnhancedButton.PublicState.Normal)
                    {
                        parent.normalState = newState;
                        parent.normalState.publicState = EnhancedButton.PublicState.Normal;
                    }
                    else if (publicState == EnhancedButton.PublicState.Highlighted)
                    {
                        parent.highlightedState = newState;
                        parent.highlightedState.publicState = EnhancedButton.PublicState.Highlighted;
                    }
                    else if (publicState == EnhancedButton.PublicState.Pressed)
                    {
                        parent.pressedState = newState;
                        parent.pressedState.publicState = EnhancedButton.PublicState.Pressed;
                    }
                    else if (publicState == EnhancedButton.PublicState.Selected)
                    {
                        parent.selectedState = newState;
                        parent.selectedState.publicState = EnhancedButton.PublicState.Selected;
                    }
                    else if (publicState == EnhancedButton.PublicState.Disabled)
                    {
                        parent.disabledState = newState;
                        parent.disabledState.publicState = EnhancedButton.PublicState.Disabled;
                    }

                    UnityEditor.EditorUtility.SetDirty(parent);
#endif
                }
            }

            [HorizontalGroup("ActionButtons"), Button("Apply State", Icon = SdfIconType.CheckCircleFill)]
            [GUIColor("red")]
            private void ApplyThisState()
            {
                parent.ApplyState(publicState);
            }
        }
    }
}