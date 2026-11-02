using TMPro;
using UnityEngine;

namespace MyGame
{
    public class TimerUIEntity : Entity
    {
        [SerializeField] private TMP_Text timerText;

        public override void InitEntity()
        {
            SetTime(180f);
        }

        public void SetTime(float time)
        {
            if (timerText == null) return;

            int totalSeconds = Mathf.CeilToInt(time);
            int minutes = totalSeconds / 60;
            int seconds = totalSeconds % 60;

            timerText.text = minutes.ToString("00") + ":" + seconds.ToString("00");
        }
    }
}