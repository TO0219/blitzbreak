//AudioManager

using UnityEngine;
using UnityEngine.Audio;

public enum AudioCue
{
    None,
    UiSelect,
    UiSubmit,
    Countdown,
    TimeUp,
    RankFirst,
    RankOther,
    WeaponPickup,
    BuffPickup,
    PointGain,
    PlayerDeath,
    Respawn,
    PlayerHpLow,
    CannonLight,
    CannonMedium,
    CannonHeavy,
    Railgun,
    Gatling,
    Missile,
    TankMove,
    Plane,
    Fighter,
    Helicopter,
    EnemyGroundShot,
    EnemyAirShot,
    EnemyDeath,
    HitExplosion,
    AirModeEnter,
    AirModeExit,
    GaugeProgress,
    GaugeMax,
    BuildingCollapseA,
    BuildingCollapseB,
    NoTimeLimit
}

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance;

    [SerializeField] AudioMixer audioMixer;
    [SerializeField] private AudioSource bgmAudioSource;
    [SerializeField] private AudioSource seAudioSource;
    [SerializeField] private AudioMixerGroup bgmMixerGroup;
    [SerializeField] private AudioMixerGroup seMixerGroup;

    [Header("BGM")]
    [SerializeField] private AudioClip titleBgm;
    [SerializeField] private AudioClip selectBgm;
    [SerializeField] private AudioClip battleBgm;
    [SerializeField] private AudioClip resultBgm;

    [Header("BGM Volume")]
    [SerializeField, Range(0f, 1f)] private float titleBgmVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float selectBgmVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float battleBgmVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float resultBgmVolume = 1f;

    [Header("System SE")]
    [SerializeField] private AudioClip uiSelectClip;
    [SerializeField] private AudioClip uiSubmitClip;
    [SerializeField] private AudioClip countdownClip;
    [SerializeField] private AudioClip timeUpClip;
    [SerializeField] private AudioClip rankFirstClip;
    [SerializeField] private AudioClip rankOtherClip;
    [SerializeField] private AudioClip gaugeProgressClip;
    [SerializeField] private AudioClip gaugeMaxClip;
    [SerializeField] private AudioClip noTimeLimitClip;
    [SerializeField] private AudioClip airModeEnterClip;
    [SerializeField] private AudioClip airModeExitClip;
    [SerializeField] private AudioClip hitExplosionClip;
    [SerializeField] private AudioClip buildingCollapseAClip;
    [SerializeField] private AudioClip buildingCollapseBClip;

    [Header("System SE Volume")]
    [SerializeField, Range(0f, 1f)] private float uiSelectVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float uiSubmitVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float countdownVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float timeUpVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float rankFirstVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float rankOtherVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float gaugeProgressVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float gaugeMaxVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float noTimeLimitVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float airModeEnterVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float airModeExitVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float hitExplosionVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float buildingCollapseAVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float buildingCollapseBVolume = 1f;

    [Header("Player SE")]
    [SerializeField] private AudioClip weaponPickupClip;
    [SerializeField] private AudioClip buffPickupClip;
    [SerializeField] private AudioClip pointGainClip;
    [SerializeField] private AudioClip playerDeathClip;
    [SerializeField] private AudioClip respawnClip;
    [SerializeField] private AudioClip playerHpLowClip;
    [SerializeField] private AudioClip cannonLightClip;
    [SerializeField] private AudioClip cannonMediumClip;
    [SerializeField] private AudioClip cannonHeavyClip;
    [SerializeField] private AudioClip railgunClip;
    [SerializeField] private AudioClip gatlingClip;
    [SerializeField] private AudioClip missileClip;
    [SerializeField] private AudioClip tankMoveClip;
    [SerializeField] private AudioClip planeClip;
    [SerializeField] private AudioClip fighterClip;
    [SerializeField] private AudioClip helicopterClip;

    [Header("Player SE Volume")]
    [SerializeField, Range(0f, 1f)] private float weaponPickupVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float buffPickupVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float pointGainVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float playerDeathVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float respawnVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float playerHpLowVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float cannonLightVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float cannonMediumVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float cannonHeavyVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float railgunVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float gatlingVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float missileVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float tankMoveVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float planeVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float fighterVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float helicopterVolume = 1f;

    [Header("Enemy SE")]
    [SerializeField] private AudioClip enemyGroundShotClip;
    [SerializeField] private AudioClip enemyAirShotClip;
    [SerializeField] private AudioClip enemyDeathClip;

    [Header("Enemy SE Volume")]
    [SerializeField, Range(0f, 1f)] private float enemyGroundShotVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float enemyAirShotVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float enemyDeathVolume = 1f;

    private float m_lastPointGainTime = -999f;
    private float m_lastHpLowTime = -999f;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            if (Instance.gameObject.scene.name == gameObject.scene.name ||
                IsAdditiveUiAudioScene(gameObject.scene.name))
            {
                Destroy(gameObject);
                return;
            }
        }

        Instance = this;
        SetupAudioSources();
        LoadSettings();
        PlaySceneBgm(gameObject.scene.name);
    }

    private static bool IsAdditiveUiAudioScene(string sceneName)
    {
        return sceneName == "BattlePauseUIScene" ||
               sceneName == "BattleUIScene";
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void SetupAudioSources()
    {
        ResolveMixerGroups();

        if (bgmAudioSource == null)
            bgmAudioSource = gameObject.AddComponent<AudioSource>();

        bgmAudioSource.playOnAwake = false;
        bgmAudioSource.loop = true;
        bgmAudioSource.spatialBlend = 0f;
        bgmAudioSource.outputAudioMixerGroup = bgmMixerGroup;

        if (seAudioSource == null)
            seAudioSource = gameObject.AddComponent<AudioSource>();

        seAudioSource.playOnAwake = false;
        seAudioSource.loop = false;
        seAudioSource.spatialBlend = 0f;
        seAudioSource.outputAudioMixerGroup = seMixerGroup;
    }

    private void ResolveMixerGroups()
    {
        if (audioMixer == null)
            return;

        if (bgmMixerGroup == null)
            bgmMixerGroup = FindMixerGroup("BGMVolume");

        if (seMixerGroup == null)
            seMixerGroup = FindMixerGroup("SEVolume");
    }

    private AudioMixerGroup FindMixerGroup(string groupName)
    {
        AudioMixerGroup[] groups = audioMixer.FindMatchingGroups(groupName);
        return groups != null && groups.Length > 0 ? groups[0] : null;
    }

    public void SetBGMVolume(float value)
    {
        float db = value > 0.001f ? Mathf.Log10(value) * 20 : -80f;
        if (audioMixer != null)
            audioMixer.SetFloat("BGMVolume", db);
        PlayerPrefs.SetFloat("BGMVolume", value);
    }

    public void SetSEVolume(float value)
    {
        float db = value > 0.001f ? Mathf.Log10(value) * 20 : -80f;
        if (audioMixer != null)
            audioMixer.SetFloat("SEVolume", db);
        PlayerPrefs.SetFloat("SEVolume", value);
    }

    public float GetBGMVolume() => PlayerPrefs.GetFloat("BGMVolume", 0.8f);
    public float GetSEVolume() => PlayerPrefs.GetFloat("SEVolume", 0.8f);

    public void PlayBGM(AudioClip clip)
    {
        PlayBGM(clip, 1f);
    }

    public void PlayBGM(AudioClip clip, float volumeScale)
    {
        if (clip == null)
            return;

        SetupAudioSources();
        float volume = Mathf.Clamp01(volumeScale);
        if (bgmAudioSource.clip == clip && bgmAudioSource.isPlaying)
        {
            bgmAudioSource.volume = volume;
            return;
        }

        bgmAudioSource.clip = clip;
        bgmAudioSource.volume = volume;
        bgmAudioSource.Play();
    }

    public void StopBGM()
    {
        if (bgmAudioSource == null)
            return;

        bgmAudioSource.Stop();
        bgmAudioSource.clip = null;
    }

    public void PlaySE(AudioClip clip, float volumeScale = 1f)
    {
        if (clip == null)
            return;

        SetupAudioSources();
        seAudioSource.PlayOneShot(clip, Mathf.Clamp01(volumeScale));
    }

    public void PlaySEAtPosition(AudioClip clip, Vector3 position, float volumeScale = 1f)
    {
        if (clip == null)
            return;

        SetupAudioSources();

        GameObject soundObject = new GameObject($"SE_{clip.name}");
        soundObject.transform.position = position;

        AudioSource source = soundObject.AddComponent<AudioSource>();
        source.clip = clip;
        source.outputAudioMixerGroup = seMixerGroup;
        source.spatialBlend = 1f;
        source.rolloffMode = AudioRolloffMode.Linear;
        source.minDistance = 2f;
        source.maxDistance = 35f;
        source.volume = Mathf.Clamp01(volumeScale);
        source.Play();

        Destroy(soundObject, clip.length + 0.1f);
    }

    public void PlayCue(AudioCue cue, float volumeScale = 1f)
    {
        PlaySE(GetCueClip(cue), volumeScale * GetCueVolume(cue));
    }

    public void PlayCueAtPosition(AudioCue cue, Vector3 position, float volumeScale = 1f)
    {
        PlaySEAtPosition(
            GetCueClip(cue),
            position,
            volumeScale * GetCueVolume(cue));
    }

    public void PlayPointGain(float volumeScale = 1f)
    {
        if (Time.unscaledTime - m_lastPointGainTime < 0.08f)
            return;

        m_lastPointGainTime = Time.unscaledTime;
        PlayCue(AudioCue.PointGain, volumeScale);
    }

    public void PlayHpLow(float volumeScale = 1f)
    {
        if (Time.unscaledTime - m_lastHpLowTime < 1f)
            return;

        m_lastHpLowTime = Time.unscaledTime;
        PlayCue(AudioCue.PlayerHpLow, volumeScale);
    }

    public AudioClip GetCueClip(AudioCue cue)
    {
        switch (cue)
        {
            case AudioCue.UiSelect: return uiSelectClip;
            case AudioCue.UiSubmit: return uiSubmitClip;
            case AudioCue.Countdown: return countdownClip;
            case AudioCue.TimeUp: return timeUpClip;
            case AudioCue.RankFirst: return rankFirstClip;
            case AudioCue.RankOther: return rankOtherClip;
            case AudioCue.WeaponPickup: return weaponPickupClip;
            case AudioCue.BuffPickup: return buffPickupClip;
            case AudioCue.PointGain: return pointGainClip;
            case AudioCue.PlayerDeath: return playerDeathClip;
            case AudioCue.Respawn: return respawnClip;
            case AudioCue.PlayerHpLow: return playerHpLowClip;
            case AudioCue.CannonLight: return cannonLightClip;
            case AudioCue.CannonMedium: return cannonMediumClip;
            case AudioCue.CannonHeavy: return cannonHeavyClip;
            case AudioCue.Railgun: return railgunClip;
            case AudioCue.Gatling: return gatlingClip;
            case AudioCue.Missile: return missileClip;
            case AudioCue.TankMove: return tankMoveClip;
            case AudioCue.Plane: return planeClip;
            case AudioCue.Fighter: return fighterClip;
            case AudioCue.Helicopter: return helicopterClip;
            case AudioCue.EnemyGroundShot: return enemyGroundShotClip;
            case AudioCue.EnemyAirShot: return enemyAirShotClip;
            case AudioCue.EnemyDeath: return enemyDeathClip;
            case AudioCue.HitExplosion: return hitExplosionClip;
            case AudioCue.AirModeEnter: return airModeEnterClip;
            case AudioCue.AirModeExit: return airModeExitClip;
            case AudioCue.GaugeProgress: return gaugeProgressClip;
            case AudioCue.GaugeMax: return gaugeMaxClip;
            case AudioCue.BuildingCollapseA: return buildingCollapseAClip;
            case AudioCue.BuildingCollapseB: return buildingCollapseBClip;
            case AudioCue.NoTimeLimit: return noTimeLimitClip;
            default: return null;
        }
    }

    public float GetCueVolume(AudioCue cue)
    {
        switch (cue)
        {
            case AudioCue.UiSelect: return uiSelectVolume;
            case AudioCue.UiSubmit: return uiSubmitVolume;
            case AudioCue.Countdown: return countdownVolume;
            case AudioCue.TimeUp: return timeUpVolume;
            case AudioCue.RankFirst: return rankFirstVolume;
            case AudioCue.RankOther: return rankOtherVolume;
            case AudioCue.WeaponPickup: return weaponPickupVolume;
            case AudioCue.BuffPickup: return buffPickupVolume;
            case AudioCue.PointGain: return pointGainVolume;
            case AudioCue.PlayerDeath: return playerDeathVolume;
            case AudioCue.Respawn: return respawnVolume;
            case AudioCue.PlayerHpLow: return playerHpLowVolume;
            case AudioCue.CannonLight: return cannonLightVolume;
            case AudioCue.CannonMedium: return cannonMediumVolume;
            case AudioCue.CannonHeavy: return cannonHeavyVolume;
            case AudioCue.Railgun: return railgunVolume;
            case AudioCue.Gatling: return gatlingVolume;
            case AudioCue.Missile: return missileVolume;
            case AudioCue.TankMove: return tankMoveVolume;
            case AudioCue.Plane: return planeVolume;
            case AudioCue.Fighter: return fighterVolume;
            case AudioCue.Helicopter: return helicopterVolume;
            case AudioCue.EnemyGroundShot: return enemyGroundShotVolume;
            case AudioCue.EnemyAirShot: return enemyAirShotVolume;
            case AudioCue.EnemyDeath: return enemyDeathVolume;
            case AudioCue.HitExplosion: return hitExplosionVolume;
            case AudioCue.AirModeEnter: return airModeEnterVolume;
            case AudioCue.AirModeExit: return airModeExitVolume;
            case AudioCue.GaugeProgress: return gaugeProgressVolume;
            case AudioCue.GaugeMax: return gaugeMaxVolume;
            case AudioCue.BuildingCollapseA: return buildingCollapseAVolume;
            case AudioCue.BuildingCollapseB: return buildingCollapseBVolume;
            case AudioCue.NoTimeLimit: return noTimeLimitVolume;
            default: return 1f;
        }
    }

    public void ConfigureLoopSource(AudioSource source, AudioCue cue, float volumeScale = 1f)
    {
        if (source == null)
            return;

        SetupAudioSources();
        source.clip = GetCueClip(cue);
        source.outputAudioMixerGroup = seMixerGroup;
        source.playOnAwake = false;
        source.loop = true;
        source.spatialBlend = 1f;
        source.rolloffMode = AudioRolloffMode.Linear;
        source.minDistance = 2f;
        source.maxDistance = 45f;
        source.volume = Mathf.Clamp01(volumeScale * GetCueVolume(cue));
    }

    private void PlaySceneBgm(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName))
            return;

        AudioClip clip = GetSceneBgm(sceneName);
        if (clip != null)
            PlayBGM(clip, GetSceneBgmVolume(sceneName));
    }

    private AudioClip GetSceneBgm(string sceneName)
    {
        if (sceneName == "TitleScene")
            return titleBgm;

        if (sceneName == "VehicleSelectScene" ||
            sceneName == "ModeSelectScene" ||
            sceneName == "LobbyScene" ||
            sceneName == "OptionScene")
        {
            return selectBgm;
        }

        if (sceneName == "BattleScene")
            return battleBgm;

        if (sceneName == "ResultScene")
            return resultBgm;

        return sceneName.Contains("Battle") ? battleBgm : null;
    }

    private float GetSceneBgmVolume(string sceneName)
    {
        if (sceneName == "TitleScene")
            return titleBgmVolume;

        if (sceneName == "VehicleSelectScene" ||
            sceneName == "ModeSelectScene" ||
            sceneName == "LobbyScene" ||
            sceneName == "OptionScene")
        {
            return selectBgmVolume;
        }

        if (sceneName == "BattleScene")
            return battleBgmVolume;

        if (sceneName == "ResultScene")
            return resultBgmVolume;

        return sceneName.Contains("Battle") ? battleBgmVolume : 1f;
    }

    void LoadSettings()
    {
        AudioListener.volume = PlayerPrefs.GetFloat("MasterVolume", 1f);
        SetBGMVolume(GetBGMVolume());
        SetSEVolume(GetSEVolume());
    }
}
