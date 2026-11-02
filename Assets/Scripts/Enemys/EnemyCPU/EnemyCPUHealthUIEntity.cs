using UnityEngine;
using UnityEngine.UI;

namespace MyGame
{
    public class EnemyCPUHealthUIEntity : Entity
    {
        [SerializeField] private Image healthFill;
        [SerializeField] private Color healthFillColor = new Color(1f, 0.18f, 0.16f, 1f);

        public void SetHealth(float currentValue, float maxValue)
        {
            if (healthFill == null || maxValue <= 0) return;

            healthFill.color = healthFillColor;

            float rate = Mathf.Clamp01(currentValue / maxValue);
            RectTransform rect = healthFill.rectTransform;

            rect.anchorMin = Vector2.zero;
            rect.anchorMax = new Vector2(rate, 1f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
