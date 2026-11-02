using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MyGame
{
    public class BattlePauseUIController : MonoBehaviour
    {
        [Header("Root")]
        [SerializeField] private GameObject pauseRoot;
        [SerializeField] private GameObject mainPanel;
        [SerializeField] private GameObject optionPanel;

        [Header("Buttons")]
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button optionButton;
        [SerializeField] private Button closeOptionButton;
        [SerializeField] private Button titleButton;
        [SerializeField] private Button quitButton;

        [Header("Audio")]
        [SerializeField] private Slider masterVolumeSlider;
        [SerializeField] private Slider bgmSlider;
        [SerializeField] private Slider seSlider;
        [SerializeField] private TMP_Text masterVolumeValueText;
        [SerializeField] private TMP_Text bgmValueText;
        [SerializeField] private TMP_Text seValueText;

        [Header("Camera")]
        [SerializeField] private Slider sensitivitySlider;
        [SerializeField] private Slider stickSensitivitySlider;
        [SerializeField] private Slider fovSlider;
        [SerializeField] private TMP_Text sensitivityValueText;
        [SerializeField] private TMP_Text stickSensitivityValueText;
        [SerializeField] private TMP_Text fovValueText;
        [SerializeField] private Toggle invertYToggle;
        [SerializeField] private Toggle cameraShakeToggle;
        [SerializeField] private Toggle fullscreenToggle;
        [SerializeField] private TMP_Text fullscreenLabel;
        [SerializeField] private Slider qualitySlider;
        [SerializeField] private TMP_Text qualityValueText;
        [SerializeField] private Slider fpsSlider;
        [SerializeField] private TMP_Text fpsValueText;
        [SerializeField] private Toggle vSyncToggle;
        [SerializeField] private TMP_Text vSyncLabel;
        [SerializeField] private Slider windowResolutionSlider;
        [SerializeField] private TMP_Text windowResolutionValueText;
        [SerializeField] private Toggle controlGuideToggle;
        [SerializeField] private TMP_Text controlGuideLabel;

        [Header("Scene")]
        [SerializeField] private string titleSceneName = "TitleScene";
        [SerializeField] private string fallbackReturnSceneName = "TitleScene";
        [SerializeField] private KeyCode pauseKey = KeyCode.Escape;
        [SerializeField] private bool freezeSoloGame = true;
        [SerializeField] private bool optionSceneMode;
        [SerializeField, Range(0f, 1f)] private float optionReturnWaitTime = 0.18f;

        public static bool IsPaused { get; private set; }

        private float m_previousTimeScale = 1f;
        private bool m_isClosingOptionScene;
        private readonly List<Vector2Int> m_windowResolutions = new();

        private void Awake()
        {
            BindButtons();
            BindOptions();
            ConfigureOptionUiSounds();
            ConfigureAutomaticNavigation(FindObjectsByType<Selectable>(FindObjectsInactive.Include));

            IsPaused = false;
            if (IsOptionSceneMode())
            {
                if (pauseRoot != null)
                    pauseRoot.SetActive(true);

                ShowOptionPanel();
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                return;
            }

            if (pauseRoot != null)
                pauseRoot.SetActive(false);

            ApplyGameplayCursor();
            ShowMainPanel();
        }

        private void OnDestroy()
        {
            if (IsPaused)
                SetPaused(false, false);

            if (!IsOptionSceneMode())
                ApplyMenuCursor();
        }

        private void Update()
        {
            bool pausePressed =
                Input.GetKeyDown(pauseKey) ||
                (Gamepad.current != null &&
                 Gamepad.current.startButton.wasPressedThisFrame);
            bool cancelPressed =
                Gamepad.current != null &&
                Gamepad.current.buttonEast.wasPressedThisFrame;

            if (cancelPressed)
            {
                if (IsOptionSceneMode())
                {
                    CloseOptionScene();
                    return;
                }

                if (IsPaused)
                {
                    if (optionPanel != null && optionPanel.activeSelf)
                    {
                        ShowMainPanel();
                        SelectButton(resumeButton);
                    }
                    else
                    {
                        Resume();
                    }

                    return;
                }
            }

            if (pausePressed)
            {
                if (IsOptionSceneMode())
                {
                    CloseOptionScene();
                    return;
                }

                if (IsPaused && optionPanel != null && optionPanel.activeSelf)
                    ShowMainPanel();
                else
                    SetPaused(!IsPaused, true);
            }
        }

        private void BindButtons()
        {
            AddButtonListener(resumeButton, Resume);
            AddButtonListener(optionButton, ShowOptionPanel);
            if (closeOptionButton != null)
            {
                if (IsOptionSceneMode())
                    AddButtonListener(closeOptionButton, CloseOptionScene);
                else
                    AddButtonListener(closeOptionButton, ShowMainPanel);
            }

            AddButtonListener(titleButton, ReturnToTitle);
            AddButtonListener(quitButton, QuitGame);
        }

        private static void AddButtonListener(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button == null)
                return;

            button.onClick.AddListener(action);
        }

        private void ConfigureOptionUiSounds()
        {
            GameObject soundRoot = optionPanel != null ? optionPanel : pauseRoot;
            if (soundRoot != null)
            {
                Selectable[] selectables = soundRoot.GetComponentsInChildren<Selectable>(true);
                for (int i = 0; i < selectables.Length; i++)
                    AddPointerDownSound(selectables[i]);
            }

            if (closeOptionButton != null &&
                (soundRoot == null || !closeOptionButton.transform.IsChildOf(soundRoot.transform)))
            {
                AddPointerDownSound(closeOptionButton);
            }

            AddPointerEnterSound(closeOptionButton);
        }

        private static void AddPointerDownSound(Selectable selectable)
        {
            if (selectable == null)
                return;

            if (selectable is Slider)
                return;

            EventTrigger trigger = GetOrAddEventTrigger(selectable.gameObject);
            AddEventTrigger(
                trigger,
                EventTriggerType.PointerDown,
                _ => PlayUiCue(global::AudioCue.UiSubmit));
            AddEventTrigger(
                trigger,
                EventTriggerType.Submit,
                _ => PlayUiCue(global::AudioCue.UiSubmit));
        }

        private static void AddPointerEnterSound(Selectable selectable)
        {
            if (selectable == null)
                return;

            EventTrigger trigger = GetOrAddEventTrigger(selectable.gameObject);
            AddEventTrigger(
                trigger,
                EventTriggerType.PointerEnter,
                _ => PlayUiCue(global::AudioCue.UiSelect));
        }

        private static EventTrigger GetOrAddEventTrigger(GameObject target)
        {
            EventTrigger trigger = target.GetComponent<EventTrigger>();
            if (trigger == null)
                trigger = target.AddComponent<EventTrigger>();

            return trigger;
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

        private static void PlayUiCue(global::AudioCue cue)
        {
            global::AudioManager.Instance?.PlayCue(cue);
        }

        private void BindOptions()
        {
            BindOptionReferences();
            SetupPercentSlider(masterVolumeSlider, PlayerPrefs.GetFloat(OptionManager.MasterVolumeKey, 1f), OnMasterVolumeChanged);
            SetupPercentSlider(bgmSlider, PlayerPrefs.GetFloat(OptionManager.BgmVolumeKey, 0.8f), OnBGMChanged);
            SetupPercentSlider(seSlider, PlayerPrefs.GetFloat(OptionManager.SeVolumeKey, 0.8f), OnSEChanged);

            if (sensitivitySlider != null)
            {
                sensitivitySlider.minValue = 0.1f;
                sensitivitySlider.maxValue = 3f;
                sensitivitySlider.SetValueWithoutNotify(PlayerPrefs.GetFloat(OptionManager.SensitivityKey, 1f));
                sensitivitySlider.onValueChanged.AddListener(OnSensitivityChanged);
                UpdateNumberText(sensitivityValueText, sensitivitySlider.value);
            }

            if (stickSensitivitySlider != null)
            {
                stickSensitivitySlider.minValue = 0.1f;
                stickSensitivitySlider.maxValue = 3f;
                stickSensitivitySlider.SetValueWithoutNotify(
                    PlayerPrefs.GetFloat(OptionManager.StickSensitivityKey, 1f));
                stickSensitivitySlider.onValueChanged.AddListener(OnStickSensitivityChanged);
                UpdateNumberText(stickSensitivityValueText, stickSensitivitySlider.value);
            }

            if (fovSlider != null)
            {
                fovSlider.minValue = 60f;
                fovSlider.maxValue = 120f;
                fovSlider.SetValueWithoutNotify(PlayerPrefs.GetFloat(OptionManager.FovKey, 90f));
                fovSlider.onValueChanged.AddListener(OnFOVChanged);
                UpdateFovText(fovSlider.value);
            }

            if (invertYToggle != null)
            {
                invertYToggle.SetIsOnWithoutNotify(OptionManager.IsInvertYEnabled);
                invertYToggle.onValueChanged.AddListener(OnInvertYChanged);
            }

            if (cameraShakeToggle != null)
            {
                cameraShakeToggle.SetIsOnWithoutNotify(OptionManager.IsCameraShakeEnabled);
                cameraShakeToggle.onValueChanged.AddListener(OnCameraShakeChanged);
            }

            if (controlGuideToggle != null)
            {
                controlGuideToggle.SetIsOnWithoutNotify(OptionManager.IsControlGuideEnabled);
                controlGuideToggle.onValueChanged.AddListener(OnControlGuideChanged);
                UpdateControlGuideLabel(OptionManager.IsControlGuideEnabled);
            }

            if (fullscreenToggle != null)
            {
                bool isFullscreen = OptionManager.IsFullscreenEnabled;
                fullscreenToggle.SetIsOnWithoutNotify(isFullscreen);
                fullscreenToggle.onValueChanged.AddListener(OnFullscreenChanged);
                UpdateFullscreenLabel(isFullscreen);
            }

            SetupQuality();
            SetupFrameRate();
            SetupVSync();
            SetupWindowResolution();

            UpdatePercentText(masterVolumeValueText, masterVolumeSlider != null ? masterVolumeSlider.value : PlayerPrefs.GetFloat(OptionManager.MasterVolumeKey, 1f));
            UpdatePercentText(bgmValueText, bgmSlider != null ? bgmSlider.value : PlayerPrefs.GetFloat(OptionManager.BgmVolumeKey, 0.8f));
            UpdatePercentText(seValueText, seSlider != null ? seSlider.value : PlayerPrefs.GetFloat(OptionManager.SeVolumeKey, 0.8f));
        }

        private void BindOptionReferences()
        {
            sensitivitySlider = sensitivitySlider != null
                ? sensitivitySlider
                : FindComponentByName<Slider>("SensitivitySlider");
            sensitivityValueText = sensitivityValueText != null
                ? sensitivityValueText
                : FindComponentByName<TMP_Text>("SensitivityValueText");
            stickSensitivitySlider = stickSensitivitySlider != null
                ? stickSensitivitySlider
                : FindComponentByName<Slider>("StickSensitivitySlider");
            stickSensitivityValueText = stickSensitivityValueText != null
                ? stickSensitivityValueText
                : FindComponentByName<TMP_Text>("StickSensitivityValueText");
            fovSlider = fovSlider != null
                ? fovSlider
                : FindComponentByName<Slider>("FOVSlider");
            fovValueText = fovValueText != null
                ? fovValueText
                : FindComponentByName<TMP_Text>("FOVValueText");
            invertYToggle = invertYToggle != null
                ? invertYToggle
                : FindComponentByName<Toggle>("InvertYToggle");
            cameraShakeToggle = cameraShakeToggle != null
                ? cameraShakeToggle
                : FindComponentByName<Toggle>("CameraShakeToggle");
            fullscreenToggle = fullscreenToggle != null
                ? fullscreenToggle
                : FindComponentByName<Toggle>("FullscreenToggle");
            fullscreenLabel = fullscreenLabel != null
                ? fullscreenLabel
                : FindComponentByName<TMP_Text>("FullscreenLabel");
            qualitySlider = qualitySlider != null
                ? qualitySlider
                : FindComponentByName<Slider>("QualitySlider");
            qualityValueText = qualityValueText != null
                ? qualityValueText
                : FindComponentByName<TMP_Text>("QualityValueText");
            fpsSlider = fpsSlider != null
                ? fpsSlider
                : FindComponentByName<Slider>("FpsSlider");
            fpsValueText = fpsValueText != null
                ? fpsValueText
                : FindComponentByName<TMP_Text>("FpsValueText");
            vSyncToggle = vSyncToggle != null
                ? vSyncToggle
                : FindComponentByName<Toggle>("VSyncToggle");
            vSyncLabel = vSyncLabel != null
                ? vSyncLabel
                : FindComponentByName<TMP_Text>("VSyncLabel");
            windowResolutionSlider = windowResolutionSlider != null
                ? windowResolutionSlider
                : FindComponentByName<Slider>("WindowResolutionSlider");
            windowResolutionValueText = windowResolutionValueText != null
                ? windowResolutionValueText
                : FindComponentByName<TMP_Text>("WindowResolutionValueText");
            controlGuideToggle = controlGuideToggle != null
                ? controlGuideToggle
                : FindComponentByName<Toggle>("ControlGuideToggle");
            controlGuideLabel = controlGuideLabel != null
                ? controlGuideLabel
                : FindComponentByName<TMP_Text>("ControlGuideLabel");
            if (controlGuideToggle == null)
                CreateControlGuideOptionRow();
            else
                EnsureControlGuideOptionRowParent();
        }

        private void SetupQuality()
        {
            int qualityCount = QualitySettings.names?.Length ?? 0;
            if (qualitySlider == null || qualityCount == 0)
                return;

            int current = Mathf.Clamp(
                PlayerPrefs.GetInt(OptionManager.QualityKey, QualitySettings.GetQualityLevel()),
                0,
                qualityCount - 1);
            qualitySlider.minValue = 0f;
            qualitySlider.maxValue = qualityCount - 1;
            qualitySlider.wholeNumbers = true;
            qualitySlider.SetValueWithoutNotify(current);
            qualitySlider.onValueChanged.AddListener(OnQualityChanged);
            UpdateQualityText(current);
        }

        private void SetupFrameRate()
        {
            if (fpsSlider == null)
                return;

            int current = Mathf.Clamp(
                PlayerPrefs.GetInt(OptionManager.FpsIndexKey, 1),
                0,
                OptionManager.FpsOptions.Length - 1);
            fpsSlider.minValue = 0f;
            fpsSlider.maxValue = OptionManager.FpsOptions.Length - 1;
            fpsSlider.wholeNumbers = true;
            fpsSlider.SetValueWithoutNotify(current);
            fpsSlider.onValueChanged.AddListener(OnFrameRateChanged);
            UpdateFrameRateText(current);
        }

        private void SetupVSync()
        {
            if (vSyncToggle == null)
                return;

            bool enabled = OptionManager.IsVSyncEnabled;
            vSyncToggle.SetIsOnWithoutNotify(enabled);
            vSyncToggle.onValueChanged.AddListener(OnVSyncChanged);
            UpdateVSyncLabel(enabled);
        }

        private void SetupWindowResolution()
        {
            m_windowResolutions.Clear();
            m_windowResolutions.AddRange(OptionManager.GetWindowResolutionOptions());
            if (windowResolutionSlider == null || m_windowResolutions.Count == 0)
                return;

            int savedWidth = PlayerPrefs.GetInt(OptionManager.ResolutionWidthKey, 1280);
            int savedHeight = PlayerPrefs.GetInt(OptionManager.ResolutionHeightKey, 720);
            int current = FindWindowResolutionIndex(savedWidth, savedHeight);

            windowResolutionSlider.minValue = 0f;
            windowResolutionSlider.maxValue = m_windowResolutions.Count - 1;
            windowResolutionSlider.wholeNumbers = true;
            windowResolutionSlider.SetValueWithoutNotify(current);
            windowResolutionSlider.onValueChanged.AddListener(OnWindowResolutionChanged);
            UpdateWindowResolutionText(current);
            UpdateWindowResolutionInteractable();
        }

        private void SetupPercentSlider(Slider slider, float value, UnityEngine.Events.UnityAction<float> callback)
        {
            if (slider == null) return;

            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.SetValueWithoutNotify(Mathf.Clamp01(value));
            slider.onValueChanged.AddListener(callback);
        }

        private void SetPaused(bool paused, bool rememberCursor)
        {
            IsPaused = paused;

            if (pauseRoot != null)
                pauseRoot.SetActive(paused);

            if (paused)
            {
                if (rememberCursor)
                {
                    m_previousTimeScale = Time.timeScale;
                }

                if (ShouldFreezeGame())
                    Time.timeScale = 0f;

                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                SetLocalPlayerControl(false);
                ShowMainPanel();
                SelectButton(resumeButton);
                return;
            }

            if (ShouldFreezeGame())
                Time.timeScale = m_previousTimeScale <= 0f ? 1f : m_previousTimeScale;

            ApplyGameplayCursor();

            SetLocalPlayerControl(true);
            ShowMainPanel();
        }

        private static void ApplyGameplayCursor()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private static void ApplyMenuCursor()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private bool ShouldFreezeGame()
        {
            return freezeSoloGame && !IsOnlineSession();
        }

        private static bool IsOnlineSession()
        {
            return GameModeManager.Instance != null &&
                   GameModeManager.Instance.IsOnlineMultiplayer() &&
                   NetworkManager.Singleton != null &&
                   NetworkManager.Singleton.IsListening;
        }

        private bool IsOptionSceneMode()
        {
            return optionSceneMode || SceneManager.GetActiveScene().name == "OptionScene";
        }

        public void Resume()
        {
            SetPaused(false, true);
        }

        public void ShowOptionPanel()
        {
            if (mainPanel != null) mainPanel.SetActive(false);
            if (optionPanel != null) optionPanel.SetActive(true);

            OptionTabController tabController =
                optionPanel != null
                    ? optionPanel.GetComponent<OptionTabController>()
                    : null;
            if (tabController != null)
                tabController.SwitchTab(0);

            SelectFirstActiveOption();
        }

        public void ShowMainPanel()
        {
            if (mainPanel != null) mainPanel.SetActive(true);
            if (optionPanel != null) optionPanel.SetActive(false);

            if (IsPaused)
                SelectButton(resumeButton);
        }

        public async void ReturnToTitle()
        {
            Time.timeScale = 1f;
            IsPaused = false;
            PlayerPrefs.Save();

            if (GameModeManager.Instance != null &&
                GameModeManager.Instance.IsOnlineMultiplayer())
            {
                if (LobbyManager.Instance != null)
                    await LobbyManager.Instance.LeaveLobbyAsync();

                if (RelayConnectionManager.Instance != null)
                    await RelayConnectionManager.Instance.ShutdownIfRunningAsync();
                else if (NetworkManager.Singleton != null &&
                         NetworkManager.Singleton.IsListening)
                    NetworkManager.Singleton.Shutdown();
            }

            SceneManager.LoadScene(titleSceneName);
        }

        private static void SetLocalPlayerControl(bool canControl)
        {
            PlayerEntity[] players =
                FindObjectsByType<PlayerEntity>(FindObjectsInactive.Include);
            for (int i = 0; i < players.Length; i++)
            {
                PlayerEntity player = players[i];
                if (player == null)
                    continue;

                NetworkObject networkObject = player.GetComponent<NetworkObject>();
                bool isLocalPlayer =
                    networkObject == null ||
                    !networkObject.IsSpawned ||
                    networkObject.IsOwner;
                if (!isLocalPlayer)
                    continue;

                player.SetLocalControl(canControl);

                TPSCameraEntity cameraEntity =
                    player.GetComponentInChildren<TPSCameraEntity>(true);
                if (cameraEntity != null)
                    cameraEntity.SetLocalControl(canControl);
                break;
            }
        }

        public void CloseOptionScene()
        {
            if (m_isClosingOptionScene)
                return;

            StartCoroutine(CloseOptionSceneAfterSubmitSound());
        }

        private IEnumerator CloseOptionSceneAfterSubmitSound()
        {
            m_isClosingOptionScene = true;
            PlayerPrefs.Save();

            string targetScene = string.IsNullOrWhiteSpace(OptionManager.PreviousScene)
                ? SceneTransitionManager.PreviousScene
                : OptionManager.PreviousScene;

            if (string.IsNullOrWhiteSpace(targetScene) || targetScene == "MainMenu" || targetScene == "OptionScene")
                targetScene = fallbackReturnSceneName;

            SceneTransitionManager.PreviousScene = targetScene;

            if (optionReturnWaitTime > 0f)
                yield return new WaitForSecondsRealtime(optionReturnWaitTime);

            if (SceneTransitionManager.Instance != null)
                SceneTransitionManager.Instance.LoadByName(targetScene);
            else
                SceneManager.LoadScene(targetScene);

            m_isClosingOptionScene = false;
        }

        public void QuitGame()
        {
            Time.timeScale = 1f;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void OnMasterVolumeChanged(float value)
        {
            value = Mathf.Clamp01(value);
            AudioListener.volume = value;
            PlayerPrefs.SetFloat(OptionManager.MasterVolumeKey, value);
            UpdatePercentText(masterVolumeValueText, value);
        }

        private void OnBGMChanged(float value)
        {
            value = Mathf.Clamp01(value);

            if (global::AudioManager.Instance != null)
                global::AudioManager.Instance.SetBGMVolume(value);
            else
                PlayerPrefs.SetFloat(OptionManager.BgmVolumeKey, value);

            UpdatePercentText(bgmValueText, value);
        }

        private void OnSEChanged(float value)
        {
            value = Mathf.Clamp01(value);

            if (global::AudioManager.Instance != null)
                global::AudioManager.Instance.SetSEVolume(value);
            else
                PlayerPrefs.SetFloat(OptionManager.SeVolumeKey, value);

            UpdatePercentText(seValueText, value);
        }

        private void OnSensitivityChanged(float value)
        {
            PlayerPrefs.SetFloat(OptionManager.SensitivityKey, value);
            UpdateNumberText(sensitivityValueText, value);
            ApplyCameraOptionSettings();
        }

        private void OnStickSensitivityChanged(float value)
        {
            PlayerPrefs.SetFloat(OptionManager.StickSensitivityKey, value);
            UpdateNumberText(stickSensitivityValueText, value);
            ApplyCameraOptionSettings();
        }

        private void OnFOVChanged(float value)
        {
            PlayerPrefs.SetFloat(OptionManager.FovKey, value);
            UpdateFovText(value);
            ApplyCameraOptionSettings();
        }

        private void OnInvertYChanged(bool isOn)
        {
            PlayerPrefs.SetInt(OptionManager.InvertYKey, isOn ? 1 : 0);
            ApplyCameraOptionSettings();
        }

        private void OnCameraShakeChanged(bool isOn)
        {
            PlayerPrefs.SetInt(OptionManager.CameraShakeKey, isOn ? 1 : 0);
            ApplyCameraOptionSettings();
        }

        private void OnControlGuideChanged(bool isOn)
        {
            PlayerPrefs.SetInt(OptionManager.ControlGuideKey, isOn ? 1 : 0);
            PlayerPrefs.Save();
            UpdateControlGuideLabel(isOn);
        }

        private static void ApplyCameraOptionSettings()
        {
            TPSCameraEntity[] cameras =
                FindObjectsByType<TPSCameraEntity>(FindObjectsInactive.Include);
            for (int i = 0; i < cameras.Length; i++)
            {
                if (cameras[i] != null)
                    cameras[i].ApplyOptionSettings();
            }
        }

        private void OnFullscreenChanged(bool isOn)
        {
            OptionManager.SetFullscreen(isOn);
            UpdateFullscreenLabel(isOn);
            UpdateWindowResolutionInteractable();
        }

        private void OnQualityChanged(float value)
        {
            int qualityCount = QualitySettings.names?.Length ?? 0;
            if (qualityCount == 0)
                return;

            int index = Mathf.Clamp(Mathf.RoundToInt(value), 0, qualityCount - 1);
            QualitySettings.SetQualityLevel(index, true);
            PlayerPrefs.SetInt(OptionManager.QualityKey, index);
            UpdateQualityText(index);
        }

        private void OnFrameRateChanged(float value)
        {
            int index = Mathf.Clamp(
                Mathf.RoundToInt(value),
                0,
                OptionManager.FpsOptions.Length - 1);
            OptionManager.ApplyFrameRate(index);
            UpdateFrameRateText(index);
        }

        private void OnVSyncChanged(bool enabled)
        {
            OptionManager.SetVSync(enabled);
            OptionManager.ApplyFrameRate(
                PlayerPrefs.GetInt(OptionManager.FpsIndexKey, 1));
            UpdateVSyncLabel(enabled);
        }

        private void OnWindowResolutionChanged(float value)
        {
            if (m_windowResolutions.Count == 0)
                return;

            int index = Mathf.Clamp(
                Mathf.RoundToInt(value),
                0,
                m_windowResolutions.Count - 1);
            Vector2Int resolution = m_windowResolutions[index];
            OptionManager.SetWindowResolution(resolution.x, resolution.y);
            UpdateWindowResolutionText(index);
        }

        private void SelectFirstActiveOption()
        {
            Selectable[] selectables =
                optionPanel != null
                    ? optionPanel.GetComponentsInChildren<Selectable>(false)
                    : null;
            if (selectables == null)
                return;

            ConfigureAutomaticNavigation(selectables);
            for (int i = 0; i < selectables.Length; i++)
            {
                if (selectables[i] != null && selectables[i].IsInteractable())
                {
                    SelectButton(selectables[i]);
                    return;
                }
            }
        }

        private static void SelectButton(Selectable selectable)
        {
            if (selectable == null || EventSystem.current == null)
                return;

            EventSystem.current.SetSelectedGameObject(selectable.gameObject);
        }

        private static void ConfigureAutomaticNavigation(Selectable[] selectables)
        {
            if (selectables == null)
                return;

            for (int i = 0; i < selectables.Length; i++)
            {
                if (selectables[i] == null)
                    continue;

                Navigation navigation = selectables[i].navigation;
                navigation.mode = Navigation.Mode.Automatic;
                selectables[i].navigation = navigation;
            }
        }

        private static void UpdatePercentText(TMP_Text target, float value)
        {
            if (target != null)
                target.text = Mathf.RoundToInt(Mathf.Clamp01(value) * 100f).ToString();
        }

        private static void UpdateNumberText(TMP_Text target, float value)
        {
            if (target != null)
                target.text = value.ToString("F1");
        }

        private void CreateControlGuideOptionRow()
        {
            Transform parent = FindControlTabContentParent();
            if (parent == null)
                return;

            GameObject row = new GameObject(
                "ControlGuideRow",
                typeof(RectTransform),
                typeof(HorizontalLayoutGroup),
                typeof(LayoutElement));
            row.transform.SetParent(parent, false);

            RectTransform rowRect = row.GetComponent<RectTransform>();
            ConfigureControlGuideRowRect(rowRect);

            LayoutElement rowLayout = row.GetComponent<LayoutElement>();
            rowLayout.preferredWidth = 720f;
            rowLayout.preferredHeight = 54f;
            rowLayout.minHeight = 54f;

            HorizontalLayoutGroup rowLayoutGroup = row.GetComponent<HorizontalLayoutGroup>();
            rowLayoutGroup.childAlignment = TextAnchor.MiddleCenter;
            rowLayoutGroup.spacing = 18f;
            rowLayoutGroup.childControlWidth = false;
            rowLayoutGroup.childControlHeight = false;
            rowLayoutGroup.childForceExpandWidth = false;
            rowLayoutGroup.childForceExpandHeight = false;

            controlGuideLabel = CreateOptionText(
                "ControlGuideLabel",
                row.transform,
                "操作説明：ON",
                new Vector2(500f, 48f),
                TextAlignmentOptions.Left);
            controlGuideLabel.text = "操作説明 : ON";
            controlGuideToggle = CreateOptionToggle(row.transform);
        }

        private void EnsureControlGuideOptionRowParent()
        {
            Transform targetParent = FindControlTabContentParent();
            Transform row = controlGuideToggle != null
                ? controlGuideToggle.transform.parent
                : null;
            if (targetParent == null || row == null || row.parent == targetParent)
                return;

            row.SetParent(targetParent, false);
            ConfigureControlGuideRowRect(row as RectTransform);
            row.SetAsLastSibling();
        }

        private Transform FindControlTabContentParent()
        {
            OptionTabController tabController =
                optionPanel != null
                    ? optionPanel.GetComponent<OptionTabController>()
                    : null;
            if (tabController != null && tabController.ControlPanelTransform != null)
                return tabController.ControlPanelTransform;

            Transform controlPanel = FindChildTransform("ControlPanel");
            if (controlPanel != null)
                return controlPanel;

            Transform row = GetOptionRowTransform(invertYToggle);
            if (row == null)
                row = GetOptionRowTransform(stickSensitivitySlider);
            if (row == null)
                row = GetOptionRowTransform(sensitivitySlider);

            if (row != null && row.parent != null)
                return row.parent;

            return optionPanel != null ? optionPanel.transform : transform;
        }

        private static void ConfigureControlGuideRowRect(RectTransform rowRect)
        {
            if (rowRect == null)
                return;

            rowRect.anchorMin = new Vector2(0f, 1f);
            rowRect.anchorMax = new Vector2(0f, 1f);
            rowRect.pivot = new Vector2(0.5f, 0.5f);
            rowRect.anchoredPosition = new Vector2(360f, -306f);
            rowRect.sizeDelta = new Vector2(720f, 54f);
        }

        private Transform FindChildTransform(string targetName)
        {
            if (string.IsNullOrEmpty(targetName))
                return null;

            Transform root = optionPanel != null ? optionPanel.transform : transform;
            Transform[] children = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < children.Length; i++)
            {
                if (children[i] != null && children[i].name == targetName)
                    return children[i];
            }

            return null;
        }

        private static Transform GetOptionRowTransform(UnityEngine.Component component)
        {
            if (component == null)
                return null;

            Transform candidate = component.transform;
            while (candidate != null)
            {
                if (candidate.GetComponent<HorizontalLayoutGroup>() != null)
                    return candidate;

                candidate = candidate.parent;
            }

            return component.transform.parent;
        }

        private TMP_Text CreateOptionText(
            string name,
            Transform parent,
            string text,
            Vector2 size,
            TextAlignmentOptions alignment)
        {
            GameObject textObject = new GameObject(
                name,
                typeof(RectTransform),
                typeof(TextMeshProUGUI),
                typeof(LayoutElement));
            textObject.transform.SetParent(parent, false);

            RectTransform rect = textObject.GetComponent<RectTransform>();
            rect.sizeDelta = size;

            LayoutElement layout = textObject.GetComponent<LayoutElement>();
            layout.preferredWidth = size.x;
            layout.preferredHeight = size.y;

            TextMeshProUGUI tmp = textObject.GetComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = 26f;
            tmp.fontStyle = FontStyles.Bold;
            tmp.color = Color.white;
            tmp.alignment = alignment;
            tmp.raycastTarget = false;
            return tmp;
        }

        private Toggle CreateOptionToggle(Transform parent)
        {
            GameObject toggleObject = new GameObject(
                "ControlGuideToggle",
                typeof(RectTransform),
                typeof(Toggle),
                typeof(LayoutElement));
            toggleObject.transform.SetParent(parent, false);

            RectTransform toggleRect = toggleObject.GetComponent<RectTransform>();
            toggleRect.sizeDelta = new Vector2(64f, 40f);

            LayoutElement layout = toggleObject.GetComponent<LayoutElement>();
            layout.preferredWidth = 64f;
            layout.preferredHeight = 40f;

            GameObject background = new GameObject(
                "Background",
                typeof(RectTransform),
                typeof(Image));
            background.transform.SetParent(toggleObject.transform, false);
            RectTransform backgroundRect = background.GetComponent<RectTransform>();
            backgroundRect.anchorMin = Vector2.zero;
            backgroundRect.anchorMax = Vector2.one;
            backgroundRect.offsetMin = Vector2.zero;
            backgroundRect.offsetMax = Vector2.zero;
            Image backgroundImage = background.GetComponent<Image>();
            backgroundImage.color = new Color(1f, 1f, 1f, 0.18f);

            GameObject checkmark = new GameObject(
                "Checkmark",
                typeof(RectTransform),
                typeof(Image));
            checkmark.transform.SetParent(background.transform, false);
            RectTransform checkRect = checkmark.GetComponent<RectTransform>();
            checkRect.anchorMin = new Vector2(0.15f, 0.2f);
            checkRect.anchorMax = new Vector2(0.85f, 0.8f);
            checkRect.offsetMin = Vector2.zero;
            checkRect.offsetMax = Vector2.zero;
            Image checkImage = checkmark.GetComponent<Image>();
            checkImage.color = new Color(0.44f, 1f, 0.68f, 1f);

            Toggle toggle = toggleObject.GetComponent<Toggle>();
            toggle.targetGraphic = backgroundImage;
            toggle.graphic = checkImage;
            toggle.transition = Selectable.Transition.ColorTint;
            return toggle;
        }

        private static T FindComponentByName<T>(string objectName) where T : UnityEngine.Component
        {
            T[] components = FindObjectsByType<T>(FindObjectsInactive.Include);
            for (int i = 0; i < components.Length; i++)
            {
                if (components[i] != null && components[i].gameObject.name == objectName)
                    return components[i];
            }

            return null;
        }

        private void UpdateFullscreenLabel(bool isFullscreen)
        {
            if (fullscreenLabel != null)
            {
                fullscreenLabel.text = isFullscreen
                    ? "表示モード：フルスクリーン"
                    : "表示モード：ウィンドウ";
            }
        }

        private void UpdateControlGuideLabel(bool isEnabled)
        {
            if (controlGuideLabel != null)
                controlGuideLabel.text = isEnabled
                    ? "操作説明：ON"
                    : "操作説明：OFF";
        }

        private void UpdateFovText(float value)
        {
            if (fovValueText != null)
                fovValueText.text = $"{Mathf.RoundToInt(value)}°";
        }

        private void UpdateQualityText(int index)
        {
            if (qualityValueText == null || QualitySettings.names == null ||
                QualitySettings.names.Length == 0)
                return;

            qualityValueText.text = OptionManager.GetQualityLabel(index);
        }

        private void UpdateFrameRateText(int index)
        {
            if (fpsValueText != null)
            {
                fpsValueText.text = OptionManager.FpsLabels[
                    Mathf.Clamp(index, 0, OptionManager.FpsLabels.Length - 1)];
            }
        }

        private void UpdateVSyncLabel(bool enabled)
        {
            if (vSyncLabel != null)
                vSyncLabel.text = $"VSync：{(enabled ? "ON" : "OFF")}";
        }

        private int FindWindowResolutionIndex(int width, int height)
        {
            for (int i = 0; i < m_windowResolutions.Count; i++)
            {
                if (m_windowResolutions[i].x == width &&
                    m_windowResolutions[i].y == height)
                    return i;
            }

            return Mathf.Max(0, m_windowResolutions.Count - 1);
        }

        private void UpdateWindowResolutionText(int index)
        {
            if (windowResolutionValueText == null || m_windowResolutions.Count == 0)
                return;

            Vector2Int resolution =
                m_windowResolutions[Mathf.Clamp(index, 0, m_windowResolutions.Count - 1)];
            windowResolutionValueText.text = $"{resolution.x} × {resolution.y}";
        }

        private void UpdateWindowResolutionInteractable()
        {
            if (windowResolutionSlider != null)
                windowResolutionSlider.interactable = !OptionManager.IsFullscreenEnabled;
        }
    }
}
