using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace UISystem.Runtime.Scripts.Mono
{
    public class ImageWidget : UIWidget
    {
        [SerializeField] private Image image;

        public Image Image => image;

        public virtual void SetSprite(Sprite sprite)
        {
            image.sprite = sprite;
        }

        public virtual void SetColor(Color color)
        {
            image.color = color;
        }

        public virtual void SetAlpha(float alpha)
        {
            var color = image.color;
            color.a = alpha;

            image.color = color;
        }

        public virtual void SetNativeSize()
        {
            image.SetNativeSize();
        }

        public virtual void SetFillAmount(float fillAmount)
        {
            image.fillAmount = fillAmount;
        }

        public virtual void SetMaterial(Material material)
        {
            image.material = material;
        }

        public virtual void SetRaycastTarget(bool value)
        {
            image.raycastTarget = value;
        }

        public virtual void SetPreserveAspect(bool value)
        {
            image.preserveAspect = value;
        }

        public virtual void SetType(Image.Type type, bool useGrayImage = false)
        {
            image.type = type;
        }

        public virtual void SetFillMethod(Image.FillMethod fillMethod)
        {
            image.fillMethod = fillMethod;
        }

        public virtual void SetFillOrigin(int fillOrigin)
        {
            image.fillOrigin = fillOrigin;
        }

        public virtual void SetFillClockwise(bool value)
        {
            image.fillClockwise = value;
        }

        public virtual void Fade(float targetAlpha, float duration
            , TweenCancelBehaviour cancelBehaviour = TweenCancelBehaviour.Complete,
            CancellationToken cancellationToken = default)
        {
            image.DOFade(targetAlpha, duration)
                .ToUniTask(tweenCancelBehaviour: cancelBehaviour, cancellationToken: cancellationToken).Forget();
        }
    }
}