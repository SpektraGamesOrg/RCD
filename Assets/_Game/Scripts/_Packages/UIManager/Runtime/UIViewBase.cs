using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Sirenix.OdinInspector;
using SpektraGames.SpektraUtilities.Runtime;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace UIManager
{
    public abstract class UIViewBase : MonoBehaviour
    {
        public enum UIVisibilityState
        {
            Showing = 0,
            Showed = 1,
            Hiding = 2,
            Hidden = 3
        }

        public enum ViewTypeEnum
        {
            ScreenBase = 0,
            TabbedScreenBase = 1,
            TabBase = 2,
            OverlayBase = 3,
            PopupBase = 4
        }

        private const float AnimationTimeoutDuration = 6f;

        private UIVisibilityState _visibilityState = UIVisibilityState.Hidden;
        [ShowInInspector, ReadOnly, BoxGroup("UI Base")]
        public UIVisibilityState VisibilityState
        {
            get
            {
#if UNITY_EDITOR
                if (!Application.isPlaying)
                {
                    // Manual check for editor
                    if (content && content.gameObject.activeInHierarchy)
                    {
                        return UIVisibilityState.Showing;
                    }
                    else
                    {
                        return UIVisibilityState.Hidden;
                    }
                }
#endif

                return _visibilityState;
            }
            private set { _visibilityState = value; }
        }

        public virtual ViewTypeEnum ViewType => ViewTypeEnum.OverlayBase;

        [ShowInInspector, ReadOnly, BoxGroup("UI Base")]
        public virtual bool CanBackNow => true;

        public virtual bool HaveCustomBackAction => false;

        [SerializeField, BoxGroup("UI Base")]
        protected RectTransform content;
        public RectTransform Content => content;

        [SerializeField, BoxGroup("UI Base")]
        protected CanvasGroup canvasGroup;
        public CanvasGroup CanvasGroup => canvasGroup;

        [SerializeField, BoxGroup("UI Base")]
        protected bool hasTransitionAnimations = false;

        [SerializeField, BoxGroup("UI Base")]
        protected bool disablePlayerInputsWhenThisUIActive = true;
        public bool DisablePlayerInputsWhenThisUIActive => disablePlayerInputsWhenThisUIActive;

        // Callbacks
        [SerializeField, BoxGroup("UI Base"), ShowIf(nameof(hasTransitionAnimations))]
        private UnityEvent playShowAnimation = new UnityEvent();
        [SerializeField, BoxGroup("UI Base"), ShowIf(nameof(hasTransitionAnimations))]
        private UnityEvent playHideAnimation = new UnityEvent();

        public bool ShowingOrShown => VisibilityState == UIVisibilityState.Showing || VisibilityState == UIVisibilityState.Showed;
        public bool HidingOrHidden => VisibilityState == UIVisibilityState.Hiding || VisibilityState == UIVisibilityState.Hidden;

        protected GameUIManager GameUIManagerInstance => GameUIManager.Instance;

        private bool _showOrHideAnimationPlayed = false;

        [ShowInInspector, ReadOnly, BoxGroup("UI Base")]
        private bool _hovered = false;

        protected virtual void Awake()
        {
            canvasGroup.alpha = 0f;
            content.gameObject.SetActive(false);
        }

        // This function should call when Show/Hide animation done
        public void OnAnimationDone()
        {
            _showOrHideAnimationPlayed = true;
        }

        protected virtual void OnHovered()
        {
            // Set ready control hints
        }

        protected virtual void OnUnHovered()
        {
        }

        protected virtual void OnBeforeShowing(bool immediate, object uiData = null)
        {
        }

        protected virtual void OnShowed(bool immediate, object uiData = null)
        {
        }

        protected virtual void OnBeforeHiding(bool immediate = false)
        {
        }

        protected virtual void OnHidden(bool immediate = false)
        {
        }

        protected internal virtual void OnCustomBackAction()
        {
        }


        internal void InvokeOnHovered()
        {
            try
            {
                if (!_hovered)
                    OnHovered();
            }
            catch (Exception e)
            {
                Debug.LogError("Error happened when call OnHovered: " + System.Environment.NewLine + e.ParseException());
            }
            finally
            {
                _hovered = true;
            }
        }

        internal void InvokeOnUnHovered()
        {
            try
            {
                if (_hovered)
                    OnUnHovered();
            }
            catch (Exception e)
            {
                Debug.LogError("Error happened when call OnUnHovered: " + System.Environment.NewLine + e.ParseException());
            }
            finally
            {
                _hovered = false;
            }
        }

        internal async UniTask ShowAsync(bool immediate = false, object uiData = null, bool canCallOnShowed = true)
        {
            // if (Application.isPlaying && !immediate && ShowingOrShown)
            //     return;

            if (Application.isPlaying && canCallOnShowed)
            {
                try
                {
                    if (!_hovered)
                        OnHovered();
                }
                catch (Exception e)
                {
                    Debug.LogError("Error happened when call OnHovered: " + System.Environment.NewLine + e.ParseException());
                }
                finally
                {
                    _hovered = true;
                }

                try
                {
                    OnBeforeShowing(immediate, uiData);
                    GameUIManagerInstance?.OnBeforeShowingAnyUIView(this);
                }
                catch (Exception e)
                {
                    Debug.LogError("Error happened when call OnBeforeShowing: " + System.Environment.NewLine + e.ParseException());
                }
            }

            if (immediate || !Application.isPlaying || !hasTransitionAnimations)
            {
                canvasGroup.alpha = 1f;
                content.gameObject.SetActive(true);
                canvasGroup.blocksRaycasts = true;

                VisibilityState = UIVisibilityState.Showed;

                if (Application.isPlaying)
                {
                    try
                    {
                        OnShowed(immediate, uiData);
                    }
                    catch (Exception e)
                    {
                        Debug.LogError("Error happened when call OnShowed: " + System.Environment.NewLine + e.ParseException());
                    }
                }

#if UNITY_EDITOR
                if (!Application.isPlaying)
                {
                    UnityEditor.EditorUtility.SetDirty(canvasGroup);
                    UnityEditor.EditorUtility.SetDirty(content);
                }
#endif
            }
            else
            {
                canvasGroup.alpha = 0f;
                content.gameObject.SetActive(true);
                canvasGroup.blocksRaycasts = false;

                _showOrHideAnimationPlayed = false;
                VisibilityState = UIVisibilityState.Showing;

                playShowAnimation?.Invoke();

                bool timeout = false;

                using var timeoutCts = new CancellationTokenSource();
                timeoutCts.CancelAfterSlim(TimeSpan.FromSeconds(AnimationTimeoutDuration), DelayType.UnscaledDeltaTime);

                try
                {
                    await UniTask.WaitUntil(() => _showOrHideAnimationPlayed, cancellationToken: timeoutCts.Token);
                }
                catch (OperationCanceledException ex)
                {
                    // timeout !
                    timeout = true;
                }

                if (timeout)
                {
                    Debug.LogError($"Show animation timed out: {gameObject.name}", gameObject);
                    await ShowAsync(true, uiData, canCallOnShowed: false);
                }
                else
                {
                    VisibilityState = UIVisibilityState.Showed;

                    canvasGroup.blocksRaycasts = true;

                    if (Application.isPlaying)
                    {
                        try
                        {
                            OnShowed(immediate, uiData);
                        }
                        catch (Exception e)
                        {
                            Debug.LogError("Error happened when call OnShowed: " + System.Environment.NewLine + e.ParseException());
                        }
                    }
                }
            }
        }

        internal async UniTask HideAsync(bool immediate = false, bool canCallOnHide = true)
        {
            // if (Application.isPlaying && !immediate && HidingOrHidden)
            //     return;

            if (Application.isPlaying && canCallOnHide)
            {
                try
                {
                    OnBeforeHiding(immediate);
                }
                catch (Exception e)
                {
                    Debug.LogError("Error happened when call OnBeforeHiding: " + System.Environment.NewLine + e.ParseException());
                }

                try
                {
                    if (_hovered)
                        OnUnHovered();
                }
                catch (Exception e)
                {
                    Debug.LogError("Error happened when call OnHovered: " + System.Environment.NewLine + e.ParseException());
                }
                finally
                {
                    _hovered = false;
                }
            }

            if (immediate || !Application.isPlaying || !hasTransitionAnimations)
            {
                canvasGroup.alpha = 0f;
                content.gameObject.SetActive(false);
                canvasGroup.blocksRaycasts = false;

                VisibilityState = UIVisibilityState.Hidden;

                if (Application.isPlaying)
                {
                    try
                    {
                        OnHidden(immediate);
                        GameUIManagerInstance?.OnHiddenAnyUIView(this);
                    }
                    catch (Exception e)
                    {
                        Debug.LogError("Error happened when call OnShowed: " + System.Environment.NewLine + e.ParseException());
                    }
                }

#if UNITY_EDITOR
                if (!Application.isPlaying)
                {
                    UnityEditor.EditorUtility.SetDirty(canvasGroup);
                    UnityEditor.EditorUtility.SetDirty(content);
                }
#endif
            }
            else
            {
                canvasGroup.blocksRaycasts = false;

                _showOrHideAnimationPlayed = false;
                VisibilityState = UIVisibilityState.Hiding;

                playHideAnimation?.Invoke();

                bool timeout = false;

                using var timeoutCts = new CancellationTokenSource();
                timeoutCts.CancelAfterSlim(TimeSpan.FromSeconds(AnimationTimeoutDuration), DelayType.UnscaledDeltaTime);

                try
                {
                    await UniTask.WaitUntil(() => _showOrHideAnimationPlayed, cancellationToken: timeoutCts.Token);
                }
                catch (OperationCanceledException ex)
                {
                    // timeout !
                    timeout = true;
                }

                if (timeout)
                {
                    Debug.LogError($"Hide animation timed out: {gameObject.name}", gameObject);
                    await HideAsync(true, canCallOnHide: false);
                }
                else
                {
                    VisibilityState = UIVisibilityState.Hidden;

                    if (Application.isPlaying)
                    {
                        try
                        {
                            OnHidden(immediate);
                            GameUIManagerInstance?.OnHiddenAnyUIView(this);
                        }
                        catch (Exception e)
                        {
                            Debug.LogError("Error happened when call OnShowed: " + System.Environment.NewLine + e.ParseException());
                        }
                    }
                }
            }
        }

#if UNITY_EDITOR
        [EnableIf("@this.VisibilityState == UIManager.UIViewBase.UIVisibilityState.Hidden")]
        [Button("Show")]
        [BoxGroup("UI Base")]
        [HorizontalGroup("UI Base/Buttons")]
        public void ShowForEditor()
        {
            ShowAsync().Forget();
        }

        [EnableIf("@this.VisibilityState == UIManager.UIViewBase.UIVisibilityState.Showed")]
        [Button("Hide")]
        [BoxGroup("UI Base")]
        [HorizontalGroup("UI Base/Buttons")]
        public void HideForEditor()
        {
            HideAsync(false).Forget();
        }

        protected void Reset()
        {
            if (transform.childCount > 0 && transform.GetChild(0) is RectTransform)
            {
                content = transform.GetChild(0).GetComponent<RectTransform>();
                canvasGroup = content.GetComponent<CanvasGroup>();

                UnityEditor.EditorUtility.SetDirty(content);
                UnityEditor.EditorUtility.SetDirty(canvasGroup);
            }
            else
            {
                // Create content
                content = new GameObject("Content", typeof(RectTransform), typeof(CanvasGroup))
                    .GetComponent<RectTransform>();
                content.SetParent(transform);
                content.localPosition = Vector3.zero;
                content.localRotation = Quaternion.identity;
                content.localScale = Vector3.one;
                content.anchorMin = Vector2.zero;
                content.anchorMax = Vector2.one;
                content.SetLRTB(Vector4.zero);

                canvasGroup = content.GetComponent<CanvasGroup>();

                UnityEditor.EditorUtility.SetDirty(content);
                UnityEditor.EditorUtility.SetDirty(canvasGroup);
            }

            UnityEditor.EditorUtility.SetDirty(this);
            ShowAsync(true).Forget();

            var uiManager = FindFirstObjectByType<GameUIManager>();
            if (uiManager)
                uiManager.RefreshReferences();
        }
#endif
    }
}