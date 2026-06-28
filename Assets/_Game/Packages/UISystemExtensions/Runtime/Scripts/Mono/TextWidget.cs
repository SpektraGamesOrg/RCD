using System.Text;
using TMPro;
using UnityEngine;

namespace UISystem.Runtime.Scripts.Mono
{
    public partial class TextWidget : UIWidget
    {
        [SerializeField, SetRef(typeof(TextMeshProUGUI))]
        public TextMeshProUGUI textMeshProUGUI;

        [SerializeField]
        protected string prefix;

        [SerializeField]
        protected string suffix;
        
        // Reusable buffers to avoid allocations
        protected readonly StringBuilder Sb = new StringBuilder(64);
        private char[] _intBuffer = new char[16];

        protected override void Awake()
        {
            base.Awake();
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();

        }

        public void SetText(string text)
        {

            Sb.Clear();

            if (!string.IsNullOrEmpty(prefix))
            {
                Sb.Append(prefix);
            }

            // Main text
            Sb.Append(text);

            // Suffix (localized or static)
           if (!string.IsNullOrEmpty(suffix))
            {
                Sb.Append(suffix);
            }

            textMeshProUGUI.SetText(Sb.ToString());
        }
        
        public void SetCharArr(char[] charArr)
        {
            textMeshProUGUI.SetCharArray(charArr);
        }

        public void SetMaterialPreset(Material material)
        {
            textMeshProUGUI.fontSharedMaterial = material;
        }

        public void SetColor(Color color)
        {
            textMeshProUGUI.color = color;
        }

        public void SetVertexColor(Color color)
        {
            textMeshProUGUI.color = color;
        }

        public void SetOutlineColor(Color color)
        {
            textMeshProUGUI.outlineWidth = 0.1f;
            textMeshProUGUI.outlineColor = color;
        }

        public void SetGradientPreset(TMP_ColorGradient gradient)
        {
            textMeshProUGUI.colorGradientPreset = gradient;
        }

        public void SetIntText(int number)
        {
            int digitCount = number.GetDigitCount();
            if (digitCount > _intBuffer.Length)
            {
                _intBuffer = new char[digitCount];
            }

            var arr = _intBuffer;
            textMeshProUGUI.TMP_Text_IntConverter(number, digitCount, ref arr);

            if (!ReferenceEquals(arr, _intBuffer))
            {
                _intBuffer = arr;
            }
        }
    }
}