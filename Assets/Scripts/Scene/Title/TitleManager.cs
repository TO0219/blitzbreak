using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MyGame
{
    public class TitleManager : MonoBehaviour
    {
        [SerializeField] private string vehicleSelectSceneName = "VehicleSelectScene";
        [SerializeField] private string lobbySceneName = "LobbyScene";
        [Header("Rule Manual")]
        [SerializeField] private GameObject ruleManualPanel;
        [SerializeField] private RawImage ruleManualImage;
        [SerializeField] private TextMeshProUGUI ruleManualPageText;
        [SerializeField] private Texture2D[] ruleManualPages;
        [Header("Button Intro")]
        [SerializeField] private RectTransform buttonsRoot;
        [SerializeField] private bool playButtonIntroOnFirstOpen = true;
        [SerializeField] private float buttonIntroSlideDistance = 360f;
        [SerializeField] private float buttonIntroDuration = 0.35f;
        [SerializeField] private float buttonIntroStagger = 0.07f;
        [Header("Title Logo Intro")]
        [SerializeField] private RectTransform titleLogoRoot;
        [SerializeField] private CanvasGroup titleLogoCanvasGroup;
        [SerializeField] private RectTransform titleAccentLine;
        [SerializeField] private float titleLogoSlideDistance = 180f;
        [SerializeField] private float titleLogoIntroDelay = 0.4f;
        [SerializeField] private float titleLogoIntroDuration = 1.1f;
        [Header("Button Hover")]
        [SerializeField] private bool enableButtonHoverOffset = true;
        [SerializeField] private float buttonHoverOffset = 24f;
        [Header("Submit SE")]
        [SerializeField, Range(0f, 1f)] private float submitSoundWaitTime = 0.18f;

        private static bool s_hasPlayedButtonIntro;
        private readonly Dictionary<RectTransform, Vector2> m_buttonBasePositions =
            new Dictionary<RectTransform, Vector2>();
        private bool m_isStartingOnline;
        private bool m_isPlayingButtonIntro;
        private bool m_isSubmitTransitioning;
        private RectTransform m_currentOffsetButton;
        private Vector2 m_titleLogoBasePosition;
        private Vector3 m_titleLogoBaseScale;
        private Vector3 m_titleAccentBaseScale;
        private bool m_hasTitleLogoBaseState;
        private int m_ruleManualPageIndex;

        private void Start()
        {
            if (titleLogoRoot != null && titleLogoCanvasGroup != null)
                StartCoroutine(PlayTitleLogoIntro());

            RectTransform root = buttonsRoot != null
                ? buttonsRoot
                : GameObject.Find("Buttons")?.GetComponent<RectTransform>();
            if (root == null)
                return;

            SetupButtonHoverOffset(root);

            if (!playButtonIntroOnFirstOpen || s_hasPlayedButtonIntro)
                return;

            s_hasPlayedButtonIntro = true;
            StartCoroutine(PlayButtonIntro(root));
        }

        private void OnDisable()
        {
            ResetAllButtonOffsets();
            ResetTitleLogoVisual();
        }

        public void StartBtn()
        {
            StartSoloOffline();
        }

        public void StartSoloOffline()
        {
            if (m_isSubmitTransitioning)
                return;

            global::AudioManager.Instance?.PlayCue(global::AudioCue.UiSubmit);

            if (GameModeManager.Instance != null)
                GameModeManager.Instance.SetMode(GameModeType.SoloOffline);

            StartCoroutine(LoadSceneAfterSubmitSound(vehicleSelectSceneName, false));
        }

        public async void StartOnlineMode()
        {
            if (m_isStartingOnline || m_isSubmitTransitioning)
                return;

            global::AudioManager.Instance?.PlayCue(global::AudioCue.UiSubmit);
            m_isStartingOnline = true;
            m_isSubmitTransitioning = true;
            ResetAllButtonOffsets();
            bool shouldResetTransition = true;
            try
            {
                await WaitForSubmitSoundAsync(submitSoundWaitTime);

                bool initialized = await UGSBootstrap.EnsureInitializedAsync();
                if (!initialized)
                {
                    Debug.LogWarning("オンラインサービスを初期化できませんでした。通信環境を確認してください。");
                    return;
                }

                if (GameModeManager.Instance != null)
                    GameModeManager.Instance.SetMode(GameModeType.OnlineMultiplayer);

                shouldResetTransition = false;
                LoadScene(lobbySceneName);
            }
            finally
            {
                m_isStartingOnline = false;
                if (shouldResetTransition)
                    m_isSubmitTransitioning = false;
            }
        }

        private IEnumerator LoadSceneAfterSubmitSound(string sceneName, bool playSound = true)
        {
            if (m_isSubmitTransitioning)
                yield break;

            m_isSubmitTransitioning = true;
            ResetAllButtonOffsets();

            if (playSound)
                global::AudioManager.Instance?.PlayCue(global::AudioCue.UiSubmit);

            yield return WaitForSubmitSound(submitSoundWaitTime);
            LoadScene(sceneName);
        }

        private static IEnumerator WaitForSubmitSound(float waitTime)
        {
            if (waitTime > 0f)
                yield return new WaitForSecondsRealtime(waitTime);
        }

        private static async System.Threading.Tasks.Task WaitForSubmitSoundAsync(float waitTime)
        {
            int waitMilliseconds = Mathf.RoundToInt(Mathf.Max(0f, waitTime) * 1000f);
            if (waitMilliseconds > 0)
                await System.Threading.Tasks.Task.Delay(waitMilliseconds);
        }

        private static void LoadScene(string sceneName)
        {
            if (SceneTransitionManager.Instance != null)
                SceneTransitionManager.Instance.LoadByName(sceneName);
            else
                SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
        }

        public void OnClickExit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        public void OpenRuleManual()
        {
            if (ruleManualPanel == null ||
                ruleManualImage == null ||
                ruleManualPages == null ||
                ruleManualPages.Length <= 0)
                return;

            global::AudioManager.Instance?.PlayCue(global::AudioCue.UiSubmit);
            m_ruleManualPageIndex = 0;
            ruleManualPanel.SetActive(true);
            UpdateRuleManualPage();
        }

        public void CloseRuleManual()
        {
            if (ruleManualPanel == null)
                return;

            global::AudioManager.Instance?.PlayCue(global::AudioCue.UiSubmit);
            ruleManualPanel.SetActive(false);
        }

        public void ShowPreviousRuleManualPage()
        {
            ChangeRuleManualPage(-1);
        }

        public void ShowNextRuleManualPage()
        {
            ChangeRuleManualPage(1);
        }

        private void ChangeRuleManualPage(int direction)
        {
            if (ruleManualPages == null || ruleManualPages.Length <= 0)
                return;

            int nextIndex =
                (m_ruleManualPageIndex + direction + ruleManualPages.Length) %
                ruleManualPages.Length;

            global::AudioManager.Instance?.PlayCue(global::AudioCue.UiSelect);
            m_ruleManualPageIndex = nextIndex;
            UpdateRuleManualPage();
        }

        private void UpdateRuleManualPage()
        {
            if (ruleManualImage == null ||
                ruleManualPages == null ||
                ruleManualPages.Length <= 0)
                return;

            m_ruleManualPageIndex = Mathf.Clamp(
                m_ruleManualPageIndex,
                0,
                ruleManualPages.Length - 1);
            ruleManualImage.texture = ruleManualPages[m_ruleManualPageIndex];

            if (ruleManualPageText != null)
            {
                ruleManualPageText.text =
                    $"{m_ruleManualPageIndex + 1} / {ruleManualPages.Length}";
            }
        }

        private IEnumerator PlayButtonIntro(RectTransform root)
        {
            RectTransform[] buttons = GetDirectChildRects(root);
            if (buttons.Length <= 0)
                yield break;

            m_isPlayingButtonIntro = true;

            Button[] clickables = root.GetComponentsInChildren<Button>(true);
            bool[] originalInteractable = new bool[clickables.Length];
            for (int i = 0; i < clickables.Length; i++)
            {
                originalInteractable[i] = clickables[i] != null && clickables[i].interactable;
                if (clickables[i] != null)
                    clickables[i].interactable = false;
            }

            Vector2[] endPositions = new Vector2[buttons.Length];
            for (int i = 0; i < buttons.Length; i++)
            {
                endPositions[i] = buttons[i].anchoredPosition;
                buttons[i].anchoredPosition =
                    endPositions[i] + Vector2.left * Mathf.Max(0f, buttonIntroSlideDistance);
            }

            float duration = Mathf.Max(0.01f, buttonIntroDuration);
            float stagger = Mathf.Max(0f, buttonIntroStagger);
            float totalTime = duration + stagger * (buttons.Length - 1);
            float elapsed = 0f;

            while (elapsed < totalTime)
            {
                elapsed += Time.unscaledDeltaTime;
                for (int i = 0; i < buttons.Length; i++)
                {
                    float localTime = elapsed - stagger * i;
                    float t = Mathf.Clamp01(localTime / duration);
                    float eased = 1f - Mathf.Pow(1f - t, 3f);
                    Vector2 startPosition =
                        endPositions[i] + Vector2.left * Mathf.Max(0f, buttonIntroSlideDistance);
                    buttons[i].anchoredPosition =
                        Vector2.LerpUnclamped(startPosition, endPositions[i], eased);
                }

                yield return null;
            }

            for (int i = 0; i < buttons.Length; i++)
                buttons[i].anchoredPosition = endPositions[i];

            for (int i = 0; i < clickables.Length; i++)
            {
                if (clickables[i] != null)
                    clickables[i].interactable = originalInteractable[i];
            }

            m_isPlayingButtonIntro = false;
        }

        private IEnumerator PlayTitleLogoIntro()
        {
            m_titleLogoBasePosition = titleLogoRoot.anchoredPosition;
            m_titleLogoBaseScale = titleLogoRoot.localScale;
            m_titleAccentBaseScale = titleAccentLine != null
                ? titleAccentLine.localScale
                : Vector3.one;
            m_hasTitleLogoBaseState = true;

            float slideDistance = Mathf.Max(0f, titleLogoSlideDistance);
            float duration = Mathf.Max(0.01f, titleLogoIntroDuration);
            titleLogoCanvasGroup.alpha = 0f;
            titleLogoRoot.anchoredPosition =
                m_titleLogoBasePosition + Vector2.left * slideDistance;
            titleLogoRoot.localScale = m_titleLogoBaseScale * 0.9f;
            SetTitleAccentScale(0f);

            float delay = Mathf.Max(0f, titleLogoIntroDelay);
            if (delay > 0f)
                yield return new WaitForSecondsRealtime(delay);
            else
                yield return null;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = 1f - Mathf.Pow(1f - t, 3f);
                float overshoot = Mathf.Sin(t * Mathf.PI) * 0.035f;

                titleLogoCanvasGroup.alpha = Mathf.SmoothStep(
                    0f,
                    1f,
                    Mathf.Clamp01(t * 2.2f));
                titleLogoRoot.anchoredPosition = Vector2.LerpUnclamped(
                    m_titleLogoBasePosition + Vector2.left * slideDistance,
                    m_titleLogoBasePosition,
                    eased);
                titleLogoRoot.localScale = m_titleLogoBaseScale *
                    (Mathf.Lerp(0.9f, 1f, eased) + overshoot);

                float lineProgress = Mathf.SmoothStep(
                    0f,
                    1f,
                    Mathf.InverseLerp(0.3f, 0.85f, t));
                SetTitleAccentScale(lineProgress);
                yield return null;
            }

            titleLogoCanvasGroup.alpha = 1f;
            titleLogoRoot.anchoredPosition = m_titleLogoBasePosition;
            titleLogoRoot.localScale = m_titleLogoBaseScale;
            SetTitleAccentScale(1f);
        }

        private void SetTitleAccentScale(float horizontalScale)
        {
            if (titleAccentLine == null)
                return;

            titleAccentLine.localScale = new Vector3(
                m_titleAccentBaseScale.x * horizontalScale,
                m_titleAccentBaseScale.y,
                m_titleAccentBaseScale.z);
        }

        private void ResetTitleLogoVisual()
        {
            if (!m_hasTitleLogoBaseState || titleLogoRoot == null)
                return;

            titleLogoRoot.anchoredPosition = m_titleLogoBasePosition;
            titleLogoRoot.localScale = m_titleLogoBaseScale;
            if (titleLogoCanvasGroup != null)
                titleLogoCanvasGroup.alpha = 1f;
            if (titleAccentLine != null)
                titleAccentLine.localScale = m_titleAccentBaseScale;
        }

        private void SetupButtonHoverOffset(RectTransform root)
        {
            if (!enableButtonHoverOffset)
                return;

            RectTransform[] buttons = GetDirectChildRects(root);
            for (int i = 0; i < buttons.Length; i++)
            {
                RectTransform buttonRect = buttons[i];
                if (buttonRect == null)
                    continue;

                m_buttonBasePositions[buttonRect] = buttonRect.anchoredPosition;

                EventTrigger trigger = buttonRect.GetComponent<EventTrigger>();
                if (trigger == null)
                    trigger = buttonRect.gameObject.AddComponent<EventTrigger>();

                AddEventTrigger(
                    trigger,
                    EventTriggerType.PointerEnter,
                    _ => HandleButtonHoverEnter(buttonRect));
                AddEventTrigger(
                    trigger,
                    EventTriggerType.Select,
                    _ => SetButtonHoverOffset(buttonRect, true));
                AddEventTrigger(
                    trigger,
                    EventTriggerType.PointerExit,
                    _ => SetButtonHoverOffset(buttonRect, false));
                AddEventTrigger(
                    trigger,
                    EventTriggerType.Deselect,
                    _ => SetButtonHoverOffset(buttonRect, false));
            }
        }

        private void HandleButtonHoverEnter(RectTransform buttonRect)
        {
            if (m_isPlayingButtonIntro || buttonRect == null)
                return;

            global::AudioManager.Instance?.PlayCue(global::AudioCue.UiSelect);
            SetButtonHoverOffset(buttonRect, true);
        }

        private void SetButtonHoverOffset(RectTransform buttonRect, bool isHovered)
        {
            if (m_isPlayingButtonIntro || buttonRect == null)
                return;

            if (!m_buttonBasePositions.TryGetValue(buttonRect, out Vector2 basePosition))
            {
                basePosition = buttonRect.anchoredPosition;
                m_buttonBasePositions[buttonRect] = basePosition;
            }

            float offset = Mathf.Max(0f, buttonHoverOffset);
            if (isHovered)
            {
                if (m_currentOffsetButton != null &&
                    m_currentOffsetButton != buttonRect)
                {
                    ResetButtonOffset(m_currentOffsetButton);
                }

                m_currentOffsetButton = buttonRect;
                buttonRect.anchoredPosition = basePosition + Vector2.right * offset;
                return;
            }

            if (m_currentOffsetButton == buttonRect)
                m_currentOffsetButton = null;

            buttonRect.anchoredPosition = basePosition;
        }

        private void ResetButtonOffset(RectTransform buttonRect)
        {
            if (buttonRect == null)
                return;

            if (m_buttonBasePositions.TryGetValue(buttonRect, out Vector2 basePosition))
                buttonRect.anchoredPosition = basePosition;
        }

        private void ResetAllButtonOffsets()
        {
            foreach (KeyValuePair<RectTransform, Vector2> pair in m_buttonBasePositions)
            {
                if (pair.Key != null)
                    pair.Key.anchoredPosition = pair.Value;
            }

            m_currentOffsetButton = null;
        }

        private static void AddEventTrigger(
            EventTrigger trigger,
            EventTriggerType eventType,
            UnityEngine.Events.UnityAction<BaseEventData> callback)
        {
            EventTrigger.Entry entry = new EventTrigger.Entry
            {
                eventID = eventType
            };
            entry.callback.AddListener(callback);
            trigger.triggers.Add(entry);
        }

        private static RectTransform[] GetDirectChildRects(RectTransform root)
        {
            RectTransform[] buttons = new RectTransform[root.childCount];
            int count = 0;
            for (int i = 0; i < root.childCount; i++)
            {
                RectTransform child = root.GetChild(i) as RectTransform;
                if (child == null || !child.gameObject.activeSelf)
                    continue;

                buttons[count++] = child;
            }

            if (count == buttons.Length)
                return buttons;

            RectTransform[] trimmed = new RectTransform[count];
            for (int i = 0; i < count; i++)
                trimmed[i] = buttons[i];

            return trimmed;
        }
    }
}
