using TMPro;
using UnityEngine;

namespace MyGame
{
    [RequireComponent(typeof(CanvasGroup))]
    public class CountdownUIEntity : MonoBehaviour
    {
        [SerializeField] private TMP_Text countdownText;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private float startMessageDuration = 0.75f;
        [SerializeField] private float oneMinuteMessageDuration = 2f;
        [SerializeField, Range(0f, 1f)] private float matchWarningAlpha = 0.58f;
        [SerializeField] private Color normalColor = Color.white;
        [SerializeField] private Color warningColor = new Color(1f, 0.24f, 0.18f, 1f);
        [SerializeField] private string finishMessage = "FINISH";
        [SerializeField] private Color finishColor = new Color(1f, 0.82f, 0.18f, 1f);

        private bool m_wasStarted;
        private float m_startMessageTimer;
        private float m_lastMatchTime = float.PositiveInfinity;
        private float m_matchWarningTimer;

        private void Awake()
        {
            EnsureReferences();
            Hide();
        }

        public void SetCountdown(float remaining, bool isStarted, bool isFinished)
        {
            EnsureReferences();

            if (isFinished)
            {
                Show(finishMessage, 1.12f, finishColor);
                return;
            }

            if (!isStarted)
            {
                m_wasStarted = false;
                m_startMessageTimer = 0f;
                m_lastMatchTime = float.PositiveInfinity;
                m_matchWarningTimer = 0f;

                int count = Mathf.Max(1, Mathf.CeilToInt(remaining));
                float pulse = 1f + (1f - Mathf.Clamp01(remaining - Mathf.Floor(remaining))) * 0.18f;
                Show(count.ToString(), pulse, normalColor);
                return;
            }

            if (!m_wasStarted)
            {
                m_wasStarted = true;
                m_startMessageTimer = startMessageDuration;
            }

            if (m_startMessageTimer > 0f)
            {
                m_startMessageTimer -= Time.deltaTime;
                Show("START!", 1.08f, normalColor);
                return;
            }

            Hide();
        }

        public void SetRespawnCountdown(float remaining, bool isRespawning, bool isFinished)
        {
            EnsureReferences();

            if (isFinished)
            {
                Show(finishMessage, 1.12f, finishColor);
                return;
            }

            if (!isRespawning)
            {
                Hide();
                return;
            }

            int count = Mathf.Max(1, Mathf.CeilToInt(remaining));
            float pulse = 1f + (1f - Mathf.Clamp01(remaining - Mathf.Floor(remaining))) * 0.12f;
            Show("RESPAWN " + count, pulse, normalColor);
        }

        public void SetMatchTimeWarning(float remaining, bool isStarted, bool isFinished)
        {
            EnsureReferences();

            if (!isStarted || isFinished)
            {
                m_lastMatchTime = float.PositiveInfinity;
                m_matchWarningTimer = 0f;
                return;
            }

            if (remaining > m_lastMatchTime + 1f)
            {
                m_lastMatchTime = remaining;
                m_matchWarningTimer = 0f;
            }

            if (m_lastMatchTime > 60f && remaining <= 60f)
                m_matchWarningTimer = oneMinuteMessageDuration;

            m_lastMatchTime = remaining;

            if (remaining > 0f && remaining <= 10f)
            {
                int count = Mathf.Max(1, Mathf.CeilToInt(remaining));
                float fraction = remaining - Mathf.Floor(remaining);
                float pulse = 1f + (1f - fraction) * 0.25f;
                Show(count.ToString(), pulse, warningColor, matchWarningAlpha);
                return;
            }

            if (m_matchWarningTimer > 0f)
            {
                m_matchWarningTimer = Mathf.Max(
                    0f,
                    m_matchWarningTimer - Time.deltaTime);
                float pulse = 1f + Mathf.Sin(Time.unscaledTime * 8f) * 0.05f;
                Show("残り1分", pulse, warningColor);
            }
        }

        private void EnsureReferences()
        {
            if (canvasGroup == null)
                canvasGroup = GetComponent<CanvasGroup>();

            if (countdownText == null)
                countdownText = GetComponentInChildren<TMP_Text>(true);
        }

        private void Show(string text, float scale, Color color)
        {
            Show(text, scale, color, 1f);
        }

        private void Show(string text, float scale, Color color, float alpha)
        {
            if (countdownText != null)
            {
                countdownText.text = text;
                countdownText.color = color;
                countdownText.rectTransform.localScale = Vector3.one * scale;
            }

            if (canvasGroup != null)
            {
                canvasGroup.alpha = Mathf.Clamp01(alpha);
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }
        }

        private void Hide()
        {
            if (canvasGroup == null) return;

            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }
    }
}
