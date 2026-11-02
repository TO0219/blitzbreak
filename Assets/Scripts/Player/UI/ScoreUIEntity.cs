using TMPro;
using UnityEngine;

namespace MyGame
{
    public class ScoreUIEntity : Entity
    {
        [SerializeField] private TMP_Text scoreText;

        public override void InitEntity()
        {
            SetScore(0);
        }

        public void SetScore(int score)
        {
            if (scoreText == null) return;
            scoreText.text = "スコア : " + score;
        }
    }
}
