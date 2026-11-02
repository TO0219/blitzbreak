using UnityEngine;

namespace MyGame
{
    public enum MachineType
    {
        Light = 0,
        Medium = 1,
        Heavy = 2
    }

    [System.Serializable]
    public struct MachineProfile
    {
        public MachineType Type;
        public string DisplayName;
        public string SizeLabel;
        public string StatusLabel;
        public string Description;
        public float SizeMultiplier;
        public float HealthMultiplier;
        public float AttackPowerMultiplier;
        public float AttackSpeedMultiplier;
        public float MoveSpeedMultiplier;
        public float DamageTakenMultiplier;

        public MachineProfile(
            MachineType type,
            string displayName,
            string sizeLabel,
            string statusLabel,
            string description,
            float sizeMultiplier,
            float healthMultiplier,
            float attackPowerMultiplier,
            float attackSpeedMultiplier,
            float moveSpeedMultiplier,
            float damageTakenMultiplier)
        {
            Type = type;
            DisplayName = displayName;
            SizeLabel = sizeLabel;
            StatusLabel = statusLabel;
            Description = description;
            SizeMultiplier = sizeMultiplier;
            HealthMultiplier = healthMultiplier;
            AttackPowerMultiplier = attackPowerMultiplier;
            AttackSpeedMultiplier = attackSpeedMultiplier;
            MoveSpeedMultiplier = moveSpeedMultiplier;
            DamageTakenMultiplier = damageTakenMultiplier;
        }
    }

    public static class MachineProfileCatalog
    {
        private static readonly MachineProfile[] DefaultProfiles =
        {
            new MachineProfile(
                MachineType.Light,
                "ライト",
                "サイズ 小",
                "速度重視",
                "移動・攻撃速度が高いが、火力は低い。",
                0.88f,
                0.8f,
                0.8f,
                1.05f,
                1.5f,
                1.0f
            ),
            new MachineProfile(
                MachineType.Medium,
                "ミディアム",
                "サイズ 中",
                "火力重視",
                "火力が高いが、耐久は低い。",
                1.0f,
                1.0f,
                1.05f,
                1.0f,
                1.2f,
                1.2f
            ),
            new MachineProfile(
                MachineType.Heavy,
                "ヘビー",
                "サイズ 大",
                "耐久重視",
                "移動・攻撃速度は低いが、耐久が高い。",
                1.15f,
                1.5f,
                1.25f,
                0.8f,
                1.0f,
                0.75f
            )
        };

        public static int Count => GetProfiles().Length;

        public static MachineProfile[] CreateDefaultProfiles()
        {
            MachineProfile[] copy = new MachineProfile[DefaultProfiles.Length];
            for (int i = 0; i < DefaultProfiles.Length; i++)
                copy[i] = DefaultProfiles[i];

            return copy;
        }

        public static MachineProfile GetDefaultProfile(MachineType type)
        {
            for (int i = 0; i < DefaultProfiles.Length; i++)
            {
                if (DefaultProfiles[i].Type == type)
                    return DefaultProfiles[i];
            }

            return DefaultProfiles[0];
        }

        public static int ClampIndex(int index)
        {
            MachineProfile[] profiles = GetProfiles();
            if (profiles.Length <= 0)
                return 0;

            return Mathf.Clamp(index, 0, profiles.Length - 1);
        }

        public static MachineProfile GetProfile(int index)
        {
            MachineProfile[] profiles = GetProfiles();
            if (profiles.Length <= 0)
                return DefaultProfiles[0];

            return profiles[ClampIndex(index)];
        }

        private static MachineProfile[] GetProfiles()
        {
            if (GameModeManager.Instance != null &&
                GameModeManager.Instance.MachineProfiles != null &&
                GameModeManager.Instance.MachineProfiles.Length > 0)
            {
                return GameModeManager.Instance.MachineProfiles;
            }

            return DefaultProfiles;
        }
    }

    public class GameModeManager : MonoBehaviour
    {
        public static GameModeManager Instance { get; private set; }

        public GameModeType CurrentMode { get; private set; } = GameModeType.None;
        public int SelectedMachineIndex { get; private set; }
        public MachineProfile[] MachineProfiles => machineProfiles;

        [Header("Machine Profiles")]
        [SerializeField] private MachineProfile[] machineProfiles = MachineProfileCatalog.CreateDefaultProfiles();

        private void Awake()
        {
            EnsureMachineProfiles();

            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void OnValidate()
        {
            EnsureMachineProfiles();
        }

        private void EnsureMachineProfiles()
        {
            if (machineProfiles == null || machineProfiles.Length == 0)
            {
                machineProfiles = MachineProfileCatalog.CreateDefaultProfiles();
                return;
            }

            for (int i = 0; i < machineProfiles.Length; i++)
            {
                MachineProfile profile = machineProfiles[i];
                MachineProfile defaultProfile = MachineProfileCatalog.GetDefaultProfile(profile.Type);

                profile.DisplayName = UseDefaultTextIfOld(profile.DisplayName, defaultProfile.DisplayName);
                profile.SizeLabel = UseDefaultTextIfOld(profile.SizeLabel, defaultProfile.SizeLabel);
                profile.StatusLabel = UseDefaultTextIfOld(profile.StatusLabel, defaultProfile.StatusLabel);
                profile.Description = UseDefaultTextIfOld(profile.Description, defaultProfile.Description);

                machineProfiles[i] = profile;
            }
        }

        private static string UseDefaultTextIfOld(string current, string defaultText)
        {
            if (string.IsNullOrWhiteSpace(current) || IsOldMachineProfileText(current))
                return defaultText;

            return current;
        }

        private static bool IsOldMachineProfileText(string text)
        {
            return text == "LIGHT"
                || text == "MEDIUM"
                || text == "HEAVY"
                || text == "SIZE S"
                || text == "SIZE M"
                || text == "SIZE L"
                || text == "SPEED TYPE"
                || text == "POWER TYPE"
                || text == "ARMOR TYPE"
                || text == "Movement and attack speed are high, but firepower is low."
                || text == "Firepower is high, but durability is low."
                || text == "Movement and attack speed are low, but durability is high.";
        }

        public void SetMode(GameModeType mode)
        {
            CurrentMode = mode;
        }

        public void SetSelectedMachineIndex(int machineIndex)
        {
            SelectedMachineIndex = MachineProfileCatalog.ClampIndex(machineIndex);
        }

        public bool IsSoloOffline()
        {
            return CurrentMode == GameModeType.SoloOffline;
        }

        public bool IsOnlineMultiplayer()
        {
            return CurrentMode == GameModeType.OnlineMultiplayer;
        }
    }
}
