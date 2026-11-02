using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;

namespace MyGame
{
    public class BootstrapFlow : MonoBehaviour
    {
        [SerializeField] private string firstSceneName = "TitleScene";
        [Header("起動ロゴ動画")]
        [SerializeField] private VideoPlayer startupVideoPlayer;
        [SerializeField, Min(1f)] private float startupVideoTimeout = 30f;
        [Header("タイトル表示演出")]
        [SerializeField] private RectTransform startupOverlayRoot;
        [SerializeField] private GameObject startupVideoDisplay;
        [SerializeField] private RectTransform titleRevealCurtain;
        [SerializeField] private CanvasGroup titleRevealCurtainCanvasGroup;
        [SerializeField, Min(0f)] private float fadeToBlackDuration = 0.2f;
        [SerializeField, Min(0.1f)] private float curtainOpenDuration = 0.9f;

        private bool m_hasStartedSceneLoad;
        private Coroutine m_startupVideoTimeoutRoutine;

        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            UnsubscribeStartupVideo();
        }

        private void Start()
        {
            ResetTitleRevealCurtain();

            if (startupVideoPlayer == null || startupVideoPlayer.clip == null)
            {
                LoadFirstScene();
                return;
            }

            startupVideoPlayer.prepareCompleted += OnStartupVideoPrepared;
            startupVideoPlayer.loopPointReached += OnStartupVideoFinished;
            startupVideoPlayer.errorReceived += OnStartupVideoError;
            startupVideoPlayer.playOnAwake = false;
            startupVideoPlayer.waitForFirstFrame = true;
            startupVideoPlayer.skipOnDrop = false;
            startupVideoPlayer.timeUpdateMode = VideoTimeUpdateMode.UnscaledGameTime;
            startupVideoPlayer.audioOutputMode = VideoAudioOutputMode.None;
            startupVideoPlayer.Prepare();
            m_startupVideoTimeoutRoutine = StartCoroutine(WaitForStartupVideoTimeout());
        }

        private void OnStartupVideoPrepared(VideoPlayer source)
        {
            if (!m_hasStartedSceneLoad)
                source.Play();
        }

        private void OnStartupVideoFinished(VideoPlayer source)
        {
            LoadFirstScene();
        }

        private void OnStartupVideoError(VideoPlayer source, string message)
        {
            Debug.LogWarning($"起動ロゴ動画を再生できなかったため、タイトル画面へ進みます: {message}");
            LoadFirstScene();
        }

        private IEnumerator WaitForStartupVideoTimeout()
        {
            yield return new WaitForSecondsRealtime(Mathf.Max(1f, startupVideoTimeout));

            if (!m_hasStartedSceneLoad)
            {
                Debug.LogWarning("起動ロゴ動画の再生が完了しなかったため、タイトル画面へ進みます。");
                LoadFirstScene();
            }
        }

        private void LoadFirstScene()
        {
            if (m_hasStartedSceneLoad || string.IsNullOrWhiteSpace(firstSceneName))
                return;

            m_hasStartedSceneLoad = true;
            if (m_startupVideoTimeoutRoutine != null)
            {
                StopCoroutine(m_startupVideoTimeoutRoutine);
                m_startupVideoTimeoutRoutine = null;
            }

            UnsubscribeStartupVideo();
            StartCoroutine(LoadFirstSceneWithCurtain());
        }

        private IEnumerator LoadFirstSceneWithCurtain()
        {
            if (startupOverlayRoot == null ||
                titleRevealCurtain == null ||
                titleRevealCurtainCanvasGroup == null)
            {
                SceneManager.LoadScene(firstSceneName);
                yield break;
            }

            GameObject overlayObject = startupOverlayRoot.gameObject;
            DontDestroyOnLoad(overlayObject);

            titleRevealCurtain.gameObject.SetActive(true);
            titleRevealCurtain.anchoredPosition = Vector2.zero;
            titleRevealCurtainCanvasGroup.alpha = 0f;

            float fadeElapsed = 0f;
            while (fadeElapsed < fadeToBlackDuration)
            {
                fadeElapsed += Time.unscaledDeltaTime;
                float progress = fadeToBlackDuration <= 0f
                    ? 1f
                    : Mathf.Clamp01(fadeElapsed / fadeToBlackDuration);
                titleRevealCurtainCanvasGroup.alpha = progress;
                yield return null;
            }

            titleRevealCurtainCanvasGroup.alpha = 1f;
            if (startupVideoDisplay != null)
                startupVideoDisplay.SetActive(false);

            AsyncOperation loadOperation = SceneManager.LoadSceneAsync(firstSceneName, LoadSceneMode.Single);
            if (loadOperation == null)
            {
                Destroy(overlayObject);
                yield break;
            }

            while (!loadOperation.isDone)
                yield return null;

            yield return null;

            float curtainWidth = Mathf.Max(Screen.width, startupOverlayRoot.rect.width);
            Vector2 startPosition = Vector2.zero;
            Vector2 endPosition = Vector2.left * curtainWidth;
            float elapsed = 0f;

            while (elapsed < curtainOpenDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / curtainOpenDuration);
                float easedProgress = 1f - Mathf.Pow(1f - progress, 3f);
                titleRevealCurtain.anchoredPosition =
                    Vector2.LerpUnclamped(startPosition, endPosition, easedProgress);
                titleRevealCurtainCanvasGroup.alpha =
                    1f - Mathf.Clamp01((progress - 0.65f) / 0.35f);
                yield return null;
            }

            Destroy(overlayObject);
        }

        private void ResetTitleRevealCurtain()
        {
            if (titleRevealCurtain == null || titleRevealCurtainCanvasGroup == null)
                return;

            titleRevealCurtain.gameObject.SetActive(true);
            titleRevealCurtain.anchorMin = Vector2.zero;
            titleRevealCurtain.anchorMax = Vector2.one;
            titleRevealCurtain.offsetMin = Vector2.zero;
            titleRevealCurtain.offsetMax = Vector2.zero;
            titleRevealCurtain.anchoredPosition = Vector2.zero;
            titleRevealCurtainCanvasGroup.alpha = 0f;
            titleRevealCurtainCanvasGroup.interactable = false;
            titleRevealCurtainCanvasGroup.blocksRaycasts = false;
        }

        private void UnsubscribeStartupVideo()
        {
            if (startupVideoPlayer == null)
                return;

            startupVideoPlayer.prepareCompleted -= OnStartupVideoPrepared;
            startupVideoPlayer.loopPointReached -= OnStartupVideoFinished;
            startupVideoPlayer.errorReceived -= OnStartupVideoError;
        }

        private void Update()
        {
            Gamepad gamepad = Gamepad.current;
            if (gamepad == null)
                return;

            if (gamepad.buttonEast.wasPressedThisFrame)
            {
                HandleGamepadCancel();
                return;
            }

            if (EventSystem.current == null ||
                EventSystem.current.currentSelectedGameObject != null)
            {
                return;
            }

            bool wantsUi =
                gamepad.leftStick.ReadValue().sqrMagnitude >= 0.25f ||
                gamepad.dpad.ReadValue().sqrMagnitude >= 0.25f ||
                gamepad.buttonSouth.wasPressedThisFrame;
            if (wantsUi)
                SelectFirstAvailable();
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            StartCoroutine(SelectFirstAvailableNextFrame());
        }

        private IEnumerator SelectFirstAvailableNextFrame()
        {
            yield return null;
            SelectFirstAvailable();
        }

        private void SelectFirstAvailable()
        {
            if (EventSystem.current == null)
                return;

            Selectable[] selectables =
                FindObjectsByType<Selectable>(FindObjectsInactive.Exclude);
            Selectable fallback = null;

            for (int i = 0; i < selectables.Length; i++)
            {
                Selectable selectable = selectables[i];
                if (selectable == null ||
                    !selectable.gameObject.activeInHierarchy ||
                    !selectable.IsInteractable())
                {
                    continue;
                }

                if (selectable is Button)
                {
                    EventSystem.current.SetSelectedGameObject(selectable.gameObject);
                    return;
                }

                if (fallback == null)
                    fallback = selectable;
            }

            if (fallback != null)
                EventSystem.current.SetSelectedGameObject(fallback.gameObject);
        }

        private void HandleGamepadCancel()
        {
            string sceneName = SceneManager.GetActiveScene().name;

            if (sceneName == "BattleScene" ||
                sceneName == "BattleUIScene" ||
                sceneName == "BattlePauseUIScene")
            {
                return;
            }

            if (sceneName == "VehicleSelectScene")
            {
                FindAnyObjectByType<VehicleSelectUIController>()?.OnClickBack();
                return;
            }

            if (sceneName == "LobbyScene")
            {
                FindAnyObjectByType<LobbyUIController>()?.OnGamepadCancel();
                return;
            }

            if (sceneName == "ResultScene")
            {
                FindAnyObjectByType<ResultUIController>()?.OnGamepadCancel();
                return;
            }

        }
    }
}
