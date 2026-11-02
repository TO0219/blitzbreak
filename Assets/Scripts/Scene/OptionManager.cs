using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MyGame
{
    public class OptionManager : MonoBehaviour
    {
        public const string MasterVolumeKey = "MasterVolume";
        public const string BgmVolumeKey = "BGMVolume";
        public const string SeVolumeKey = "SEVolume";
        public const string SensitivityKey = "Sensitivity";
        public const string StickSensitivityKey = "StickSensitivity";
        public const string InvertYKey = "InvertY";
        public const string CameraShakeKey = "CameraShake";
        public const string QualityKey = "Quality";
        public const string FullscreenKey = "Fullscreen";
        public const string ResolutionWidthKey = "ResW";
        public const string ResolutionHeightKey = "ResH";
        public const string FovKey = "FOV";
        public const string FpsIndexKey = "FPSIndex";
        public const string VSyncKey = "VSync";
        public const string ControlGuideKey = "ControlGuide";

        // スライダー上の表示値(1.0など)に対して、実際に視点操作へ反映する倍率。
        // UI側の見た目やPlayerPrefsの保存値は変えず、体感の感度だけを下げたい場合はここを調整する。
        public const float SensitivityScale = 0.4f;
        public const float StickSensitivityScale = 0.4f;

        public static readonly int[] FpsOptions = { 30, 60, 90, 120, 144, 165, 240, -1 };
        public static readonly string[] FpsLabels =
        {
            "30 FPS",
            "60 FPS",
            "90 FPS",
            "120 FPS",
            "144 FPS",
            "165 FPS",
            "240 FPS",
            "無制限"
        };

        [Header("Audio")]
        [SerializeField] private Slider masterVolumeSlider;
        [SerializeField] private Slider bgmSlider;
        [SerializeField] private Slider seSlider;
        [SerializeField] private TMP_Text masterVolumeText;
        [SerializeField] private TMP_Text bgmText;
        [SerializeField] private TMP_Text seText;
        [SerializeField] private TMP_Text masterVolumeValueText;
        [SerializeField] private TMP_Text bgmValueText;
        [SerializeField] private TMP_Text seValueText;

        [Header("Mouse")]
        [SerializeField] private Slider sensitivitySlider;
        [SerializeField] private TMP_Text sensitivityText;
        [SerializeField] private TMP_Text sensitivityValueText;
        [SerializeField] private Slider stickSensitivitySlider;
        [SerializeField] private TMP_Text stickSensitivityText;
        [SerializeField] private TMP_Text stickSensitivityValueText;
        [SerializeField] private Toggle invertYToggle;
        [SerializeField] private TMP_Text invertYText;

        [Header("Graphics")]
        [SerializeField] private TMP_Dropdown qualityDropdown;
        [SerializeField] private TMP_Text qualityText;
        [SerializeField] private Toggle fullscreenToggle;
        [SerializeField] private TMP_Text fullscreenText;
        [SerializeField] private TMP_Dropdown resolutionDropdown;
        [SerializeField] private TMP_Text resolutionText;
        [SerializeField] private Slider fovSlider;
        [SerializeField] private TMP_Text fovText;
        [SerializeField] private TMP_Text fovValueText;
        [SerializeField] private Toggle cameraShakeToggle;
        [SerializeField] private TMP_Text cameraShakeText;
        [SerializeField] private Toggle controlGuideToggle;
        [SerializeField] private TMP_Text controlGuideText;

        [Header("FPS")]
        [SerializeField] private TMP_Dropdown fpsDropdown;
        [SerializeField] private TMP_Text fpsText;

        public static string PreviousScene = "TitleScene";

        private readonly List<Resolution> m_availableResolutions = new();
        private readonly int[] m_fpsOptions = FpsOptions;
        private readonly string[] m_fpsLabels = FpsLabels;

        private string[] m_qualityLabels = { "低", "中", "高" };

        private void Start()
        {
            BindSceneReferences();
            SetupAudio();
            SetupSensitivity();
            SetupStickSensitivity();
            SetupInvertY();
            SetupQualityDropdown();
            SetupFullscreen();
            SetupResolutionDropdown();
            SetupFov();
            SetupFpsDropdown();
            SetupCameraShake();
            SetupControlGuide();
            ApplyStaticLabels();
            SelectFirstInteractable();
        }

        private void SetupAudio()
        {
            AudioListener.volume = GetMasterVolume();
            SetupPercentSlider(masterVolumeSlider, GetMasterVolume(), OnMasterVolumeChanged);
            SetupPercentSlider(bgmSlider, GetBgmVolume(), OnBGMChanged);
            SetupPercentSlider(seSlider, GetSeVolume(), OnSEChanged);

            UpdatePercentText(masterVolumeValueText, masterVolumeSlider != null ? masterVolumeSlider.value : GetMasterVolume());
            UpdatePercentText(bgmValueText, bgmSlider != null ? bgmSlider.value : GetBgmVolume());
            UpdatePercentText(seValueText, seSlider != null ? seSlider.value : GetSeVolume());
        }

        private void SetupSensitivity()
        {
            if (sensitivitySlider == null)
            {
                UpdateNumberText(sensitivityValueText, PlayerPrefs.GetFloat(SensitivityKey, 1f));
                return;
            }

            sensitivitySlider.minValue = 0.1f;
            sensitivitySlider.maxValue = 3f;
            sensitivitySlider.SetValueWithoutNotify(PlayerPrefs.GetFloat(SensitivityKey, 1f));
            sensitivitySlider.onValueChanged.RemoveListener(OnSensitivityChanged);
            sensitivitySlider.onValueChanged.AddListener(OnSensitivityChanged);
            UpdateNumberText(sensitivityValueText, sensitivitySlider.value);
        }

        private void SetupStickSensitivity()
        {
            if (stickSensitivitySlider == null)
            {
                UpdateNumberText(
                    stickSensitivityValueText,
                    PlayerPrefs.GetFloat(StickSensitivityKey, 1f));
                return;
            }

            stickSensitivitySlider.minValue = 0.1f;
            stickSensitivitySlider.maxValue = 3f;
            stickSensitivitySlider.SetValueWithoutNotify(
                PlayerPrefs.GetFloat(StickSensitivityKey, 1f));
            stickSensitivitySlider.onValueChanged.RemoveListener(OnStickSensitivityChanged);
            stickSensitivitySlider.onValueChanged.AddListener(OnStickSensitivityChanged);
            UpdateNumberText(stickSensitivityValueText, stickSensitivitySlider.value);
        }

        private void SetupInvertY()
        {
            bool invertY = IsInvertYEnabled;

            if (invertYToggle != null)
            {
                invertYToggle.SetIsOnWithoutNotify(invertY);
                invertYToggle.onValueChanged.RemoveListener(OnInvertYChanged);
                invertYToggle.onValueChanged.AddListener(OnInvertYChanged);
            }

            UpdateToggleText(invertYText, "上下反転", invertY);
        }

        private void SetupQualityDropdown()
        {
            if (QualitySettings.names != null && QualitySettings.names.Length > 0)
                m_qualityLabels = QualitySettings.names;

            int savedIndex = Mathf.Clamp(PlayerPrefs.GetInt(QualityKey, QualitySettings.GetQualityLevel()), 0, m_qualityLabels.Length - 1);

            if (qualityDropdown != null)
            {
                qualityDropdown.ClearOptions();
                qualityDropdown.AddOptions(new List<string>(m_qualityLabels));
                qualityDropdown.SetValueWithoutNotify(savedIndex);
                qualityDropdown.RefreshShownValue();
                qualityDropdown.onValueChanged.RemoveListener(OnQualityChanged);
                qualityDropdown.onValueChanged.AddListener(OnQualityChanged);
            }

            ApplyQuality(savedIndex);
            UpdateQualityText(savedIndex);
        }

        private void SetupFullscreen()
        {
            bool fullscreen = IsFullscreenEnabled;

            if (fullscreenToggle != null)
            {
                fullscreenToggle.SetIsOnWithoutNotify(fullscreen);
                fullscreenToggle.onValueChanged.RemoveListener(OnFullscreenChanged);
                fullscreenToggle.onValueChanged.AddListener(OnFullscreenChanged);
            }

            ApplyFullscreen(fullscreen);
            UpdateFullscreenText(fullscreen);
        }

        private void SetupResolutionDropdown()
        {
            BuildResolutionList();

            int savedWidth = PlayerPrefs.GetInt(ResolutionWidthKey, Screen.currentResolution.width);
            int savedHeight = PlayerPrefs.GetInt(ResolutionHeightKey, Screen.currentResolution.height);
            int currentIndex = FindResolutionIndex(savedWidth, savedHeight);

            if (resolutionDropdown != null)
            {
                var options = new List<string>();
                foreach (Resolution resolution in m_availableResolutions)
                    options.Add(FormatResolution(resolution));

                resolutionDropdown.ClearOptions();
                resolutionDropdown.AddOptions(options);
                resolutionDropdown.SetValueWithoutNotify(currentIndex);
                resolutionDropdown.RefreshShownValue();
                resolutionDropdown.onValueChanged.RemoveListener(OnResolutionChanged);
                resolutionDropdown.onValueChanged.AddListener(OnResolutionChanged);
            }

            UpdateResolutionText(currentIndex);
        }

        private void SetupFov()
        {
            float savedFov = PlayerPrefs.GetFloat(FovKey, 90f);

            if (fovSlider != null)
            {
                fovSlider.minValue = 60f;
                fovSlider.maxValue = 120f;
                fovSlider.SetValueWithoutNotify(savedFov);
                fovSlider.onValueChanged.RemoveListener(OnFOVChanged);
                fovSlider.onValueChanged.AddListener(OnFOVChanged);
                savedFov = fovSlider.value;
            }

            ApplyFov(savedFov);
            UpdateFovText(savedFov);
        }

        private void SetupFpsDropdown()
        {
            int savedIndex = Mathf.Clamp(PlayerPrefs.GetInt(FpsIndexKey, 1), 0, m_fpsOptions.Length - 1);

            if (fpsDropdown != null)
            {
                fpsDropdown.ClearOptions();
                fpsDropdown.AddOptions(new List<string>(m_fpsLabels));
                fpsDropdown.SetValueWithoutNotify(savedIndex);
                fpsDropdown.RefreshShownValue();
                fpsDropdown.onValueChanged.RemoveListener(OnFPSChanged);
                fpsDropdown.onValueChanged.AddListener(OnFPSChanged);
            }

            ApplyFPS(savedIndex);
            UpdateFpsText(savedIndex);
        }

        private void SetupCameraShake()
        {
            bool enabled = IsCameraShakeEnabled;

            if (cameraShakeToggle != null)
            {
                cameraShakeToggle.SetIsOnWithoutNotify(enabled);
                cameraShakeToggle.onValueChanged.RemoveListener(OnCameraShakeChanged);
                cameraShakeToggle.onValueChanged.AddListener(OnCameraShakeChanged);
            }

            UpdateToggleText(cameraShakeText, "画面揺れ", enabled);
        }

        private void SetupControlGuide()
        {
            bool enabled = IsControlGuideEnabled;

            if (controlGuideToggle != null)
            {
                controlGuideToggle.SetIsOnWithoutNotify(enabled);
                controlGuideToggle.onValueChanged.RemoveListener(OnControlGuideChanged);
                controlGuideToggle.onValueChanged.AddListener(OnControlGuideChanged);
            }

            UpdateToggleText(controlGuideText, "操作説明", enabled);
        }

        private void BindSceneReferences()
        {
            masterVolumeSlider = masterVolumeSlider != null ? masterVolumeSlider : FindComponentByName<Slider>("MasterVolumeSlider");
            masterVolumeText = masterVolumeText != null ? masterVolumeText : FindComponentByName<TMP_Text>("MasterVolumeLabel");
            masterVolumeValueText = masterVolumeValueText != null ? masterVolumeValueText : FindComponentByName<TMP_Text>("MasterVolumeValueText");

            bgmSlider = bgmSlider != null ? bgmSlider : FindComponentByName<Slider>("BGMSlider");
            seSlider = seSlider != null ? seSlider : FindComponentByName<Slider>("SESlider");
            sensitivitySlider = sensitivitySlider != null ? sensitivitySlider : FindComponentByName<Slider>("SensSlider");
            sensitivitySlider = sensitivitySlider != null ? sensitivitySlider : FindComponentByName<Slider>("SensitivitySlider");
            sensitivityText = sensitivityText != null ? sensitivityText : FindComponentByName<TMP_Text>("SensitivityLabel");
            sensitivityValueText = sensitivityValueText != null ? sensitivityValueText : FindComponentByName<TMP_Text>("SensitivityValueText");
            stickSensitivitySlider = stickSensitivitySlider != null ? stickSensitivitySlider : FindComponentByName<Slider>("StickSensitivitySlider");
            stickSensitivityText = stickSensitivityText != null ? stickSensitivityText : FindComponentByName<TMP_Text>("StickSensitivityLabel");
            stickSensitivityValueText = stickSensitivityValueText != null ? stickSensitivityValueText : FindComponentByName<TMP_Text>("StickSensitivityValueText");
            fovSlider = fovSlider != null ? fovSlider : FindComponentByName<Slider>("FOVSlider");

            invertYToggle = invertYToggle != null ? invertYToggle : FindComponentByName<Toggle>("InvertYToggle");
            invertYText = invertYText != null ? invertYText : FindComponentByName<TMP_Text>("InvertYLabel");
            cameraShakeToggle = cameraShakeToggle != null ? cameraShakeToggle : FindComponentByName<Toggle>("CameraShakeToggle");
            cameraShakeText = cameraShakeText != null ? cameraShakeText : FindComponentByName<TMP_Text>("CameraShakeLabel");
            controlGuideToggle = controlGuideToggle != null ? controlGuideToggle : FindComponentByName<Toggle>("ControlGuideToggle");
            controlGuideText = controlGuideText != null ? controlGuideText : FindComponentByName<TMP_Text>("ControlGuideLabel");
        }

        private void SetupPercentSlider(Slider slider, float value, UnityEngine.Events.UnityAction<float> callback)
        {
            if (slider == null) return;

            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.SetValueWithoutNotify(Mathf.Clamp01(value));
            slider.onValueChanged.RemoveListener(callback);
            slider.onValueChanged.AddListener(callback);
        }

        private float GetMasterVolume()
        {
            return PlayerPrefs.GetFloat(MasterVolumeKey, 1f);
        }

        private float GetBgmVolume()
        {
            if (global::AudioManager.Instance != null)
                return global::AudioManager.Instance.GetBGMVolume();

            return PlayerPrefs.GetFloat(BgmVolumeKey, 0.8f);
        }

        private float GetSeVolume()
        {
            if (global::AudioManager.Instance != null)
                return global::AudioManager.Instance.GetSEVolume();

            return PlayerPrefs.GetFloat(SeVolumeKey, 0.8f);
        }

        private void OnMasterVolumeChanged(float value)
        {
            value = Mathf.Clamp01(value);
            AudioListener.volume = value;
            PlayerPrefs.SetFloat(MasterVolumeKey, value);
            UpdatePercentText(masterVolumeValueText, value);
        }

        private void OnBGMChanged(float value)
        {
            value = Mathf.Clamp01(value);

            if (global::AudioManager.Instance != null)
                global::AudioManager.Instance.SetBGMVolume(value);
            else
                PlayerPrefs.SetFloat(BgmVolumeKey, value);

            UpdatePercentText(bgmValueText, value);
        }

        private void OnSEChanged(float value)
        {
            value = Mathf.Clamp01(value);

            if (global::AudioManager.Instance != null)
                global::AudioManager.Instance.SetSEVolume(value);
            else
                PlayerPrefs.SetFloat(SeVolumeKey, value);

            UpdatePercentText(seValueText, value);
        }

        private void OnSensitivityChanged(float value)
        {
            PlayerPrefs.SetFloat(SensitivityKey, value);
            UpdateNumberText(sensitivityValueText, value);
        }

        private void OnStickSensitivityChanged(float value)
        {
            PlayerPrefs.SetFloat(StickSensitivityKey, value);
            UpdateNumberText(stickSensitivityValueText, value);
        }

        private void OnInvertYChanged(bool invertY)
        {
            PlayerPrefs.SetInt(InvertYKey, invertY ? 1 : 0);
            UpdateToggleText(invertYText, "上下反転", invertY);
        }

        private void OnQualityChanged(int index)
        {
            int safeIndex = Mathf.Clamp(index, 0, m_qualityLabels.Length - 1);
            ApplyQuality(safeIndex);
            UpdateQualityText(safeIndex);
            ApplyFPS(Mathf.Clamp(PlayerPrefs.GetInt(FpsIndexKey, 1), 0, m_fpsOptions.Length - 1));
        }

        private void ApplyQuality(int index)
        {
            int safeIndex = Mathf.Clamp(index, 0, m_qualityLabels.Length - 1);
            int qualityLevelCount = QualitySettings.names != null ? QualitySettings.names.Length : 0;

            if (qualityLevelCount > 0)
                QualitySettings.SetQualityLevel(Mathf.Clamp(safeIndex, 0, qualityLevelCount - 1), true);

            PlayerPrefs.SetInt(QualityKey, safeIndex);
        }

        private void OnFullscreenChanged(bool isFullscreen)
        {
            ApplyFullscreen(isFullscreen);
            UpdateFullscreenText(isFullscreen);
        }

        private void ApplyFullscreen(bool isFullscreen)
        {
            SetFullscreen(isFullscreen);
        }

        private void OnResolutionChanged(int index)
        {
            int safeIndex = Mathf.Clamp(index, 0, m_availableResolutions.Count - 1);
            Resolution resolution = m_availableResolutions[safeIndex];

            SetWindowResolution(resolution.width, resolution.height);
            PlayerPrefs.SetInt(ResolutionWidthKey, resolution.width);
            PlayerPrefs.SetInt(ResolutionHeightKey, resolution.height);
            UpdateResolutionText(safeIndex);
        }

        private void OnFOVChanged(float value)
        {
            ApplyFov(value);
            UpdateFovText(value);
        }

        private void ApplyFov(float value)
        {
            PlayerPrefs.SetFloat(FovKey, value);

            if (Camera.main != null)
                Camera.main.fieldOfView = value;
        }

        private void OnFPSChanged(int index)
        {
            int safeIndex = Mathf.Clamp(index, 0, m_fpsOptions.Length - 1);
            PlayerPrefs.SetInt(FpsIndexKey, safeIndex);
            ApplyFPS(safeIndex);
            UpdateFpsText(safeIndex);
        }

        private void OnCameraShakeChanged(bool enabled)
        {
            PlayerPrefs.SetInt(CameraShakeKey, enabled ? 1 : 0);
            UpdateToggleText(cameraShakeText, "画面揺れ", enabled);
        }

        private void OnControlGuideChanged(bool enabled)
        {
            PlayerPrefs.SetInt(ControlGuideKey, enabled ? 1 : 0);
            PlayerPrefs.Save();
            UpdateToggleText(controlGuideText, "操作説明", enabled);
        }

        private void ApplyFPS(int index)
        {
            ApplyFrameRate(index);
        }

        private void BuildResolutionList()
        {
            m_availableResolutions.Clear();

            var added = new HashSet<string>();
            foreach (Resolution resolution in Screen.resolutions)
            {
                string key = $"{resolution.width}x{resolution.height}";
                if (!added.Add(key)) continue;

                m_availableResolutions.Add(resolution);
            }

            if (m_availableResolutions.Count == 0)
                m_availableResolutions.Add(Screen.currentResolution);
        }

        private int FindResolutionIndex(int width, int height)
        {
            for (int i = 0; i < m_availableResolutions.Count; i++)
            {
                Resolution resolution = m_availableResolutions[i];
                if (resolution.width == width && resolution.height == height)
                    return i;
            }

            for (int i = 0; i < m_availableResolutions.Count; i++)
            {
                Resolution resolution = m_availableResolutions[i];
                if (resolution.width == Screen.currentResolution.width &&
                    resolution.height == Screen.currentResolution.height)
                    return i;
            }

            return 0;
        }

        private static string FormatResolution(Resolution resolution)
        {
            return $"{resolution.width} x {resolution.height}";
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

        private void UpdateQualityText(int index)
        {
            if (qualityText != null)
                qualityText.text = m_qualityLabels[Mathf.Clamp(index, 0, m_qualityLabels.Length - 1)];
        }

        private void UpdateFullscreenText(bool isFullscreen)
        {
            UpdateToggleText(fullscreenText, "フルスクリーン", isFullscreen);
        }

        private void UpdateResolutionText(int index)
        {
            if (resolutionText == null || m_availableResolutions.Count == 0) return;

            int safeIndex = Mathf.Clamp(index, 0, m_availableResolutions.Count - 1);
            resolutionText.text = FormatResolution(m_availableResolutions[safeIndex]);
        }

        private void UpdateFovText(float value)
        {
            UpdatePercentOrLabel(fovText, "視野角");
            if (fovValueText != null)
                fovValueText.text = $"{Mathf.RoundToInt(value)}°";
        }

        private void UpdateFpsText(int index)
        {
            if (fpsText != null)
                fpsText.text = m_fpsLabels[Mathf.Clamp(index, 0, m_fpsLabels.Length - 1)];
        }

        private void ApplyStaticLabels()
        {
            UpdatePercentOrLabel(masterVolumeText, "マスター");
            UpdatePercentOrLabel(bgmText, "BGM");
            UpdatePercentOrLabel(seText, "SE");
            UpdatePercentOrLabel(sensitivityText, "マウス感度");
            UpdatePercentOrLabel(stickSensitivityText, "スティック感度");
            UpdatePercentOrLabel(fovText, "視野角");
            UpdateToggleText(fullscreenText, "フルスクリーン", fullscreenToggle == null || fullscreenToggle.isOn);
        }

        private static void UpdatePercentOrLabel(TMP_Text target, string text)
        {
            if (target != null)
                target.text = text;
        }

        private static void UpdateToggleText(TMP_Text target, string label, bool enabled)
        {
            if (target != null)
                target.text = $"{label}: {(enabled ? "ON" : "OFF")}";
        }

        private static T FindComponentByName<T>(string objectName) where T : UnityEngine.Component
        {
            if (string.IsNullOrEmpty(objectName)) return null;

            T[] components = FindObjectsByType<T>(FindObjectsInactive.Include);
            foreach (T component in components)
            {
                if (component.gameObject.name == objectName)
                    return component;
            }

            return null;
        }

        private static void SelectFirstInteractable()
        {
            if (EventSystem.current == null)
                return;

            Selectable[] selectables = FindObjectsByType<Selectable>(FindObjectsInactive.Exclude);
            ConfigureAutomaticNavigation(selectables);
            for (int i = 0; i < selectables.Length; i++)
            {
                if (selectables[i] == null || !selectables[i].IsInteractable())
                    continue;

                EventSystem.current.SetSelectedGameObject(selectables[i].gameObject);
                return;
            }
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

        public static bool IsInvertYEnabled => PlayerPrefs.GetInt(InvertYKey, 0) == 1;
        public static bool IsCameraShakeEnabled => PlayerPrefs.GetInt(CameraShakeKey, 1) == 1;
        public static bool IsControlGuideEnabled => PlayerPrefs.GetInt(ControlGuideKey, 1) == 1;
        public static bool IsFullscreenEnabled =>
            PlayerPrefs.GetInt(FullscreenKey, Screen.fullScreen ? 1 : 0) == 1;
        public static bool IsVSyncEnabled => PlayerPrefs.GetInt(VSyncKey, 0) == 1;

        // 視点操作スクリプト側は、この2つを通して感度を取得すること。
        // スライダー上の値(表示値)はそのままに、実際に反映される感度だけ SensitivityScale 分下げられる。
        public static float GetSensitivity() => PlayerPrefs.GetFloat(SensitivityKey, 1f) * SensitivityScale;
        public static float GetStickSensitivity() => PlayerPrefs.GetFloat(StickSensitivityKey, 1f) * StickSensitivityScale;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ApplySavedDisplayMode()
        {
            int qualityCount = QualitySettings.names?.Length ?? 0;
            if (qualityCount > 0)
            {
                int quality = Mathf.Clamp(
                    PlayerPrefs.GetInt(QualityKey, QualitySettings.GetQualityLevel()),
                    0,
                    qualityCount - 1);
                QualitySettings.SetQualityLevel(quality, true);
            }

            SetVSync(IsVSyncEnabled);
            ApplyFrameRate(PlayerPrefs.GetInt(FpsIndexKey, 1));
            SetFullscreen(IsFullscreenEnabled);
        }

        public static void SetFullscreen(bool enabled)
        {
            if (enabled)
            {
                Resolution nativeResolution = Screen.currentResolution;
                Screen.SetResolution(
                    nativeResolution.width,
                    nativeResolution.height,
                    FullScreenMode.FullScreenWindow);
            }
            else
            {
                int width = PlayerPrefs.GetInt(ResolutionWidthKey, 1280);
                int height = PlayerPrefs.GetInt(ResolutionHeightKey, 720);
                Screen.SetResolution(width, height, FullScreenMode.Windowed);
            }

            PlayerPrefs.SetInt(FullscreenKey, enabled ? 1 : 0);
            PlayerPrefs.Save();
        }

        public static void SetWindowResolution(int width, int height)
        {
            width = Mathf.Max(640, width);
            height = Mathf.Max(360, height);
            PlayerPrefs.SetInt(ResolutionWidthKey, width);
            PlayerPrefs.SetInt(ResolutionHeightKey, height);

            if (!IsFullscreenEnabled)
                Screen.SetResolution(width, height, FullScreenMode.Windowed);

            PlayerPrefs.Save();
        }

        public static List<Vector2Int> GetWindowResolutionOptions()
        {
            Resolution nativeResolution = Screen.currentResolution;
            var candidates = new[]
            {
                new Vector2Int(960, 540),
                new Vector2Int(1024, 576),
                new Vector2Int(1152, 648),
                new Vector2Int(1280, 720),
                new Vector2Int(1366, 768),
                new Vector2Int(1440, 810),
                new Vector2Int(1600, 900),
                new Vector2Int(1920, 1080),
                new Vector2Int(2560, 1440),
                new Vector2Int(3840, 2160)
            };

            var result = new List<Vector2Int>();
            foreach (Vector2Int candidate in candidates)
            {
                if (candidate.x <= nativeResolution.width &&
                    candidate.y <= nativeResolution.height)
                    result.Add(candidate);
            }

            Vector2Int saved = new(
                PlayerPrefs.GetInt(ResolutionWidthKey, 1280),
                PlayerPrefs.GetInt(ResolutionHeightKey, 720));
            if (!result.Contains(saved))
                result.Add(saved);

            result.Sort((a, b) =>
            {
                int areaComparison = (a.x * a.y).CompareTo(b.x * b.y);
                return areaComparison != 0 ? areaComparison : a.x.CompareTo(b.x);
            });
            return result;
        }

        public static void SetVSync(bool enabled)
        {
            PlayerPrefs.SetInt(VSyncKey, enabled ? 1 : 0);
            QualitySettings.vSyncCount = enabled ? 1 : 0;
            PlayerPrefs.Save();
        }

        public static void ApplyFrameRate(int index)
        {
            int safeIndex = Mathf.Clamp(index, 0, FpsOptions.Length - 1);
            PlayerPrefs.SetInt(FpsIndexKey, safeIndex);
            Application.targetFrameRate = IsVSyncEnabled ? -1 : FpsOptions[safeIndex];
        }

        public static string GetQualityLabel(int index)
        {
            if (QualitySettings.names == null || QualitySettings.names.Length == 0)
                return "-";

            string qualityName =
                QualitySettings.names[Mathf.Clamp(index, 0, QualitySettings.names.Length - 1)];
            return qualityName switch
            {
                "Very Low" => "最低",
                "Low" => "低",
                "Medium" => "中",
                "High" => "高",
                "Very High" => "最高",
                "Ultra" => "ウルトラ",
                _ => qualityName
            };
        }

        public void CloseOption()
        {
            PlayerPrefs.Save();

            string targetScene = string.IsNullOrWhiteSpace(PreviousScene)
                ? SceneTransitionManager.PreviousScene
                : PreviousScene;

            if (string.IsNullOrWhiteSpace(targetScene) || targetScene == "MainMenu" || targetScene == "OptionScene")
                targetScene = "TitleScene";

            SceneTransitionManager.PreviousScene = targetScene;

            if (SceneTransitionManager.Instance != null)
                SceneTransitionManager.Instance.LoadByName(targetScene);
            else
                SceneManager.LoadScene(targetScene);
        }

        public void ResetDefaults()
        {
            OnMasterVolumeChanged(1f);
            OnBGMChanged(0.8f);
            OnSEChanged(0.8f);
            OnSensitivityChanged(1f);
            OnStickSensitivityChanged(1f);
            OnInvertYChanged(false);
            ApplyQuality(Mathf.Clamp(1, 0, m_qualityLabels.Length - 1));
            ApplyFullscreen(Screen.fullScreen);
            ApplyFov(90f);
            OnFPSChanged(1);
            OnCameraShakeChanged(true);
            OnControlGuideChanged(true);
            SetVSync(false);
            SetWindowResolution(1280, 720);

            if (masterVolumeSlider != null) masterVolumeSlider.SetValueWithoutNotify(1f);
            if (bgmSlider != null) bgmSlider.SetValueWithoutNotify(0.8f);
            if (seSlider != null) seSlider.SetValueWithoutNotify(0.8f);
            if (sensitivitySlider != null) sensitivitySlider.SetValueWithoutNotify(1f);
            if (stickSensitivitySlider != null) stickSensitivitySlider.SetValueWithoutNotify(1f);
            if (invertYToggle != null) invertYToggle.SetIsOnWithoutNotify(false);
            if (qualityDropdown != null) qualityDropdown.SetValueWithoutNotify(Mathf.Clamp(1, 0, m_qualityLabels.Length - 1));
            if (fovSlider != null) fovSlider.SetValueWithoutNotify(90f);
            if (fpsDropdown != null) fpsDropdown.SetValueWithoutNotify(1);
            if (cameraShakeToggle != null) cameraShakeToggle.SetIsOnWithoutNotify(true);
            if (controlGuideToggle != null) controlGuideToggle.SetIsOnWithoutNotify(true);

            UpdatePercentText(masterVolumeValueText, 1f);
            UpdatePercentText(bgmValueText, 0.8f);
            UpdatePercentText(seValueText, 0.8f);
            UpdateNumberText(sensitivityValueText, 1f);
            UpdateNumberText(stickSensitivityValueText, 1f);
            UpdateQualityText(Mathf.Clamp(1, 0, m_qualityLabels.Length - 1));
            UpdateFullscreenText(Screen.fullScreen);
            UpdateFovText(90f);
            UpdateFpsText(1);
            ApplyStaticLabels();
            PlayerPrefs.Save();
        }

        public void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}