using UnityEngine;
using UnityEngine.UI;

namespace MyGame
{
    public class PlayerHealthUIEntity : Entity
    {
        [SerializeField] private Image healthFill;

        public override void InitEntity()
        {
            SetHealth(100, 100);
        }

        public void SetHealth(float currentValue, float maxValue)
        {
            if (healthFill == null || maxValue <= 0) return;

            float rate = currentValue / maxValue;

            healthFill.fillAmount = Mathf.Clamp01(rate);
        }
    }
}