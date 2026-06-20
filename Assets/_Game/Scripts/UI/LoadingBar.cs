using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// Modular, reusable loading bar. Wraps a uGUI <see cref="Slider"/> (plus an optional
    /// percentage label) and can be driven from anywhere in two ways:
    ///   • directly, via <see cref="SetProgress"/>;
    ///   • as an <see cref="IProgress{Single}"/>, so it can be handed straight to async loaders
    ///     (UniTask <c>Progress.Create</c>, <c>AsyncOperation</c> wrappers, etc.).
    ///
    /// Reported values are expected in the 0..1 range and are clamped. The displayed fill is
    /// smoothed in <see cref="Update"/> so coarse progress jumps animate instead of snapping.
    /// Smoothing runs on unscaled time so it keeps working while the game is paused/loading.
    /// </summary>
    [DisallowMultipleComponent]
    public class LoadingBar : MonoBehaviour, IProgress<float>
    {
        [SerializeField] private RectTransform fillParentRectTransform;
        [SerializeField] private RectTransform fillRectTransform;

        [Tooltip("Optional label that shows the rounded percentage, e.g. \"57%\".")]
        [SerializeField] private TMP_Text percentText;

        [Header("Smoothing")]
        [Tooltip("When enabled the fill eases toward the reported value instead of snapping.")]
        [SerializeField] private bool smooth = true;
        [Tooltip("Higher is snappier. Frame-rate independent.")]
        [SerializeField, Min(0.01f)] private float smoothSpeed = 8f;

        /// <summary>Raised whenever the displayed value changes. Argument is in the 0..1 range.</summary>
        public event Action<float> ProgressChanged;
        /// <summary>Raised once when the displayed value first reaches 1.</summary>
        public event Action Completed;

        private float _target;
        private float _displayed = -1f;
        private int _lastPercent = -1;
        private bool _completedRaised;

        /// <summary>The value currently shown on the bar, in the 0..1 range.</summary>
        public float Value => Mathf.Clamp01(_displayed);
        /// <summary>The latest requested value, in the 0..1 range (may differ from <see cref="Value"/> while smoothing).</summary>
        public float Target => _target;
        /// <summary>True once the bar has visually reached the end.</summary>
        public bool IsComplete => _displayed >= 1f - 0.0001f;

        private void Awake()
        {
            SetProgress(0f, true);
        }

        private void Update()
        {
            if (!smooth || Mathf.Approximately(_displayed, _target))
                return;

            // Exponential ease toward the target; the exp() keeps it frame-rate independent.
            float t = 1f - Mathf.Exp(-smoothSpeed * Time.unscaledDeltaTime);
            _displayed = Mathf.Lerp(_displayed, _target, t);

            // Snap the last sliver so it actually reaches the target instead of crawling forever.
            if (Mathf.Abs(_target - _displayed) < 0.001f)
                _displayed = _target;

            ApplyDisplayed();
        }

        /// <summary>
        /// Sets the progress to show. <paramref name="value01"/> is clamped to 0..1.
        /// Pass <paramref name="instant"/> = tru/newe to skip smoothing and snap immediately.
        /// </summary>
        public void SetProgress(float value01, bool instant = false)
        {
            _target = Mathf.Clamp01(value01);

            if (instant || !smooth || _displayed < 0f)
            {
                // First assignment (or an explicit snap) shows the value right away.
                _displayed = instant || !smooth ? _target : 0f;
                ApplyDisplayed();
            }
        }

        /// <summary>
        /// <see cref="IProgress{Single}"/> entry point. Safe to pass to any async loader.
        /// Expects a 0..1 value; the smoothing handles the visual animation.
        /// </summary>
        public void Report(float value) => SetProgress(value);

        /// <summary>
        /// Drives the bar to full and completes only once the displayed fill has actually reached 1 (after
        /// the smoothing tail). Use this to guarantee the player sees a completed bar before it is dismissed.
        /// </summary>
        public async UniTask WaitUntilFilledAsync(CancellationToken token = default)
        {
            SetProgress(1f);

            if (IsComplete)
                return;

            await UniTask.WaitUntil(() => IsComplete, cancellationToken: token);
        }

        /// <summary>Snaps the bar back to empty. Call before starting a new load.</summary>
        public void ResetBar()
        {
            _completedRaised = false;
            SetProgress(0f, true);
        }

        private void ApplyDisplayed()
        {
            float shown = Mathf.Clamp01(_displayed);

            if (fillRectTransform)
            {
                float maxWidth = fillParentRectTransform.rect.width;
                float x = shown * maxWidth;
                //Debug.LogError(maxWidth + " _ " + x);
                fillRectTransform.sizeDelta = new Vector2(x, fillRectTransform.sizeDelta.y);
            }

            // Only touch the label when the integer percentage actually changes (allocation-free).
            if (percentText)
            {
                int percent = Mathf.RoundToInt(shown * 100f);
                if (percent != _lastPercent)
                {
                    _lastPercent = percent;
                    percentText.SetText("{0}%", percent);
                }
            }

            ProgressChanged?.Invoke(shown);

            bool complete = shown >= 1f - 0.0001f;
            if (complete && !_completedRaised)
            {
                _completedRaised = true;
                Completed?.Invoke();
            }
            else if (!complete)
            {
                _completedRaised = false;
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
        }
#endif
    }
}