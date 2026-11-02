using UnityEngine;
using UnityEngine.UI;

namespace MyGame
{
    public class AirGaugeUIEntity : Entity
    {
        [SerializeField] private Image gaugeFill1;
        [SerializeField] private Image gaugeFill2;
        [SerializeField] private Image gaugeFill3;
        [SerializeField] private float gaugePerStock = 100f;

        public override void InitEntity()
        {
            SetGauge(0f, 300f);
        }

        public void SetGauge(float currentValue, float maxValue)
        {
            if (gaugePerStock <= 0f) return;

            SetStockFill(gaugeFill1, currentValue, 0f);
            SetStockFill(gaugeFill2, currentValue, gaugePerStock);
            SetStockFill(gaugeFill3, currentValue, gaugePerStock * 2f);
        }

        private void SetStockFill(Image targetImage, float currentValue, float stockStart)
        {
            if (targetImage == null) return;

            float stockValue = Mathf.Clamp(currentValue - stockStart, 0f, gaugePerStock);
            float rate = stockValue / gaugePerStock;
            targetImage.fillAmount = rate;
        }
    }
}