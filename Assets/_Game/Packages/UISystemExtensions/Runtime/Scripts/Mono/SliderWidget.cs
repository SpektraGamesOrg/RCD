using UISystem.Runtime.Scripts;
using UISystem.Runtime.Scripts.Mono;
using UnityEngine;
using UnityEngine.UI;

namespace Packages.UISystem.Runtime.Scripts.Mono
{
    public class SliderWidget : UIWidget
    {
        [SerializeField] private Slider slider;

        [SerializeField, SetRef("Text_Value")]
        private TextWidget textValue;

        [SerializeField] private int textValueOffset = 0;

        public bool DisableAutoTextUpdate { get; set; } = false;

        public Slider Slider => slider;

        protected override void Awake()
        {
            base.Awake();
            Slider.onValueChanged.AddListener(OnSliderValueChanged);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            Slider.onValueChanged.RemoveListener(OnSliderValueChanged);
        }

        public void OnSliderValueChanged(float value)
        {
            if (DisableAutoTextUpdate)
                return;
            
            value = Mathf.Clamp(value, slider.minValue, slider.maxValue);
            // get 2 decimal places if not whole numbers
            if (slider.wholeNumbers)
            {
                value = Mathf.Round(value);
            }
            else
            {
                value = Mathf.Round(value * 100f) / 100f; // Round to 2 decimal places
            }
            
            textValue.SetText(slider.wholeNumbers ? ((int)value + textValueOffset).ToString() : $"{value:0.##}");
        }

        public void SetCustomValueText(string text)
        {
            textValue.SetText(text);
        }
    }
}