using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;

namespace UISystem.Runtime.Scripts.Mono
{
    public class CustomProgress : Progress<float>
    {
        private readonly float _minValue;
        private readonly float _maxValue;
        private float _currentValue;

        public CustomProgress(float minValue, float maxValue)
        {
            _minValue = minValue;
            _maxValue = maxValue;
            _currentValue = minValue;
        }

        public void Report(float value)
        {
            _currentValue = Mathf.Clamp(value, _minValue, _maxValue);
        }

        public float GetCurrentValue()
        {
            return _currentValue;
        }
    }

    public class ProgressionWidget : UIWidget
    {
        private Progress<float> Progression { get; set; }

        [SerializeField]
        [SetRef("CurrentImage")]
        public Image currentImage;

        [SerializeField]
        [SetRef("DeltaImage")]
        public Image deltaImage;

        [SerializeField]
        [SetRef("PeakImage")]
        public Image peakImage;

        [SerializeField]
        [SetRef("PotentialImage")]
        public Image potentialImage; // ADDED

        
        [SerializeField]
        private float _duration = 0.5f;

        [SerializeField]
        private RectTransform.Axis _fillAxis = RectTransform.Axis.Horizontal;

        private float _value;
        private float _targetValue;
        private float _initialCurrentSize;

        protected override void Awake()
        {
            // SetDeltaValue(0f);
            // SetPeakValue(0f);
            // SetProgressionValue(0f);
            base.Awake();
            
            if (currentImage == null)
            {
                currentImage = GetComponentInChildren<Image>();
            }

            if (currentImage != null)
            {
                if (_fillAxis == RectTransform.Axis.Horizontal)
                {
                    _initialCurrentSize = currentImage.rectTransform.rect.width;
                }
                else
                {
                    _initialCurrentSize = currentImage.rectTransform.rect.height;
                }
            }
        }


        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (Progression != null)
            {
                Progression.ProgressChanged -= ProgressionOnProgressChanged;
            }
        }

        /// <summary>
        /// Initialize the progression with a Progress object.
        /// You need to create a Progress object and pass it here.
        /// </summary>
        /// <param name="progression"></param>
        public void InitProgression(Progress<float> progression)
        {
            if (Progression != null)
            {
                Progression.ProgressChanged -= ProgressionOnProgressChanged;
            }

            Progression = progression;
            Progression.ProgressChanged += ProgressionOnProgressChanged;
        }

        private void ProgressionOnProgressChanged(object sender, float value)
        {
            SetProgressionValue(value);
        }


        /// <summary>
        /// You can call this method to play the progression to a specific value.
        /// It will report the value to the Progress object automatically.
        /// </summary>
        /// <param name="value"></param>
        /// 
        [Button]
        public virtual async UniTask PlayToTargetValue(float value,
            TweenCancelBehaviour cancelBehaviour = TweenCancelBehaviour.Complete,
            CancellationToken cancellationToken = default,
            float duration = 0.5f,
            Ease ease = Ease.Linear)
        {
            _duration = duration;
            _targetValue = value;
            InitProgressionIfNecessary();
            await UpdateProgression(cancelBehaviour, cancellationToken, ease);
        }

        protected void InitProgressionIfNecessary()
        {
            if (Progression != null)
                return;

            Progression = new CustomProgress(0, 1);
            InitProgression(Progression);
        }

        private async UniTask UpdateProgression(TweenCancelBehaviour cancelBehaviour,
            CancellationToken cancellationToken, Ease ease = Ease.Linear)
        {
            if (currentImage != null)
            {
                // Animate a float value and apply it to the image's fill amount
                await DOTween
                    .To(() => _value, x => { ((IProgress<float>)Progression).Report(x); }, _targetValue, _duration)
                    .SetEase(ease)
                    .ToUniTask(cancelBehaviour, cancellationToken);
            }
        }

        /// <summary>
        /// Set the progression value directly. 
        /// If the image type is filled, it will set the fill amount of the image.
        /// If the image type is sliced, it will set the width of the image.
        /// If the image type is sliced and the fill axis is vertical, it will set the height of the image.
        /// If the image type is sliced and the fill axis is horizontal, it will set the width of the image.
        /// You can override this method to add custom behavior.
        /// You MUST set vertical or horizontal fill type in inspector.
        /// </summary>
        /// <param name="value"></param>
        public void SetProgressionValue(float value)
        {
            _value = value;
            if (currentImage.type == Image.Type.Filled)
            {
                currentImage.fillAmount = _value;
            }
            else if (currentImage.type == Image.Type.Sliced)
            {
                // set width anchor set to strech image like filled image and clamp max width
                if (_fillAxis == RectTransform.Axis.Horizontal)
                {
                    currentImage.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,
                        Mathf.Clamp(_initialCurrentSize * _value, 0, _initialCurrentSize));
                }
                else
                {
                    currentImage.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,
                        Mathf.Clamp(_initialCurrentSize * _value, 0, _initialCurrentSize));
                }
            }
        }
        
        public void SetReverseProgressionValue(float value)
        {
            _value = 1f - value;
    
            if (currentImage.type == Image.Type.Filled)
            {
                currentImage.fillAmount = _value;
            }
            else if (currentImage.type == Image.Type.Sliced)
            {
                if (_fillAxis == RectTransform.Axis.Horizontal)
                {
                    currentImage.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,
                        Mathf.Clamp(_initialCurrentSize * _value, 0, _initialCurrentSize));
                }
                else
                {
                    currentImage.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,
                        Mathf.Clamp(_initialCurrentSize * _value, 0, _initialCurrentSize));
                }
            }
        }

        [InfoBox("This method can use only with filled image type")]
        public void SetDeltaValue(float delta)
        {
            deltaImage.gameObject.SetActive(true);
            deltaImage.fillAmount = delta;
        }
        
        [InfoBox("This method can use only with filled image type")]
        public void SetPeakValue(float peak)
        {
            peakImage.gameObject.SetActive(true);
            peakImage.fillAmount = peak;
        }

        [InfoBox("This method can use only with filled image type")]
        public void SetPotentialValue(float potential) // ADDED
        {
            potentialImage.gameObject.SetActive(true);
            potentialImage.fillAmount = potential;
        }

        [InfoBox("This method can use only with filled image type")]
        public void SetReversePotentialValue(float potential)
        {
            potentialImage.gameObject.SetActive(true);
            potentialImage.fillAmount = 1f - potential;
        }
        
        [InfoBox("This method can use only with filled image type")]
        public void SetReversePeakValue(float peak)
        {
            peakImage.gameObject.SetActive(true);
            peakImage.fillAmount = 1f - peak;
        }
        
        public async UniTask StartTimeProgression(float duration)
        {
            SetProgressionValue(1);
            await PlayToTargetValue(0, TweenCancelBehaviour.Complete, default, duration);
        }

        public void Reset()
        {
            SetDeltaValue(0f);
            SetPeakValue(0f);
            SetPotentialValue(0f);
            SetProgressionValue(0f);
        }

    }
}