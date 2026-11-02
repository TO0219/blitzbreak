using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MyGame
{
    public enum SceneType
    {
        Title,
        Preparation,
        MainGame,
        GameOver,
        Result,
        OptionScene,  // 追加
    }

    /// <summary>
    /// シーン遷移を管理するマネージャークラス
    /// </summary>
    public class SceneTransitionManager : MonoBehaviour
    {
        public static SceneTransitionManager Instance { get; private set; }

        [Header("フェイドの設定")]
        [SerializeField] private CanvasGroup m_fadeCanvasGroup;
        [SerializeField] private float m_fadeOutTime = 0.35f;
        [SerializeField] private float m_fadeInTime = 0.35f;

        // オプションから戻るときの遷移先を記憶
        public static string PreviousScene = "TitleScene";

        private bool m_isTransitioning = false;
        public bool IsTransitioning => m_isTransitioning;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
                return;
            }

            if (m_fadeCanvasGroup == null)
                m_fadeCanvasGroup = GetComponentInChildren<CanvasGroup>(true);

            if (m_fadeCanvasGroup != null)
            {
                m_fadeCanvasGroup.alpha = 0f;
                m_fadeCanvasGroup.blocksRaycasts = false;
                m_fadeCanvasGroup.interactable = false;
            }
        }

        // SceneTypeで遷移
        public void Load(SceneType nextScene)
        {
            LoadByName(GetSceneName(nextScene));
        }

        // シーン名(string)で遷移（オプションから戻るときに使用）
        public void LoadByName(string sceneName)
        {
            if (m_isTransitioning) return;

            if (string.IsNullOrWhiteSpace(sceneName) ||
                !Application.CanStreamedLevelBeLoaded(sceneName))
            {
                Debug.LogWarning("シーンがBuild Settingsに登録されていません: " + sceneName);
                return;
            }

            StartCoroutine(LoadRoutine(sceneName));
        }

        public static string GetSceneName(SceneType sceneType)
        {
            return sceneType switch
            {
                SceneType.Title => "TitleScene",
                SceneType.Preparation => "VehicleSelectScene",
                SceneType.MainGame => "BattleScene",
                SceneType.GameOver => "ResultScene",
                SceneType.Result => "ResultScene",
                SceneType.OptionScene => "OptionScene",
                _ => string.Empty
            };
        }

        private IEnumerator LoadRoutine(string sceneName)
        {
            m_isTransitioning = true;

            if (m_fadeCanvasGroup != null)
                m_fadeCanvasGroup.blocksRaycasts = true;

            yield return Fade(1f, m_fadeOutTime);

            SceneManager.LoadScene(sceneName);

            yield return null;

            yield return Fade(0f, m_fadeInTime);

            if (m_fadeCanvasGroup != null)
                m_fadeCanvasGroup.blocksRaycasts = false;

            m_isTransitioning = false;
        }

        private IEnumerator Fade(float targetAlpha, float duration)
        {
            if (m_fadeCanvasGroup == null || duration <= 0f)
            {
                if (m_fadeCanvasGroup != null)
                    m_fadeCanvasGroup.alpha = targetAlpha;
                yield break;
            }

            float start = m_fadeCanvasGroup.alpha;
            float t = 0f;

            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / duration);
                m_fadeCanvasGroup.alpha = Mathf.Lerp(start, targetAlpha, k);
                yield return null;
            }

            m_fadeCanvasGroup.alpha = targetAlpha;
        }
    }
}
