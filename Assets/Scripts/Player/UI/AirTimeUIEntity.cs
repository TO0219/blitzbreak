using UnityEngine;
using UnityEngine.UI;

namespace MyGame
{
    public class AirTimeUIEntity : Entity
    {
        [SerializeField] private Image fillImage;

        public override void InitEntity()
        {
            SetRate(0f);
        }

        public void SetRate(float rate)
        {
            if (fillImage == null) return;
            fillImage.fillAmount = Mathf.Clamp01(rate);
        }
    }
}