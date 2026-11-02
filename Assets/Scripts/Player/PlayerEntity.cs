using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MyGame
{
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(BoxCollider))]
    public class PlayerEntity : Entity
    {
        [System.Serializable]
        private struct TankMaterialVariant
        {
            [SerializeField] private MachineType machineType;
            [SerializeField] private Material tankBaseMaterial;
            [SerializeField] private Material tankTrackMaterial;
            [SerializeField] private Material weaponBaseType1Material;
            [SerializeField] private Material weaponBaseType2Material;

            public MachineType MachineType => machineType;
            public Material TankBaseMaterial => tankBaseMaterial;
            public Material TankTrackMaterial => tankTrackMaterial;
            public Material WeaponBaseType1Material => weaponBaseType1Material;
            public Material WeaponBaseType2Material => weaponBaseType2Material;
        }

        [Header("Statas")]
        [SerializeField] private float groundHealth = 100f;
        [SerializeField] private float airHealth = 100f;
        [SerializeField] private float currentHealth = 100f;
        [SerializeField] private float respawnDelay = 3f;

        [Header("Defeat Rewards")]
        [SerializeField, Tooltip("このプレイヤーを倒した相手が獲得するポイント")]
        private int defeatPointValue = 100;

        [Header("Machine Type")]
        [SerializeField] private int machineIndex;
        [SerializeField] private TankMaterialVariant[] tankMaterialVariants;

        [Header("Death Penalty")]
        [SerializeField, Range(0f, 1f)] private float deathBuffLossRate = 0.5f;
        [SerializeField, Range(0f, 1f)] private float deathAirGaugeLossRate = 0.2f;
        [SerializeField] private GameObject deathExplosionPrefab;

        [Header("Respawn Protection")]
        [SerializeField] private float respawnProtectionDuration = 2.5f;
        [SerializeField] private float respawnProtectionBlinkInterval = 0.12f;
        [SerializeField] private bool cancelRespawnProtectionOnAttack = true;

        [Header("Camera Feedback")]
        [SerializeField, Range(0f, 1f)] private float damageShakeIntensity = 0.55f;
        [SerializeField] private float damageShakeDuration = 0.22f;

        [Header("Move")]
        [SerializeField] private float groundMoveSpeed = 6f;
        [SerializeField] private float airMoveSpeed = 10f;
        [SerializeField] private float rotateSpeed = 12f;

        [Header("Gravity")]
        [SerializeField] private float gravity = -25f;
        [SerializeField] private float groundCheckDistance = 0.2f;

        [Header("Movement Recovery")]
        [SerializeField, Min(0f)] private float worldBorderClampPadding = 0.05f;
        [SerializeField, Min(0f)] private float floorClearance = 0.03f;
        [SerializeField, Min(0f)] private float floorSinkRecoveryDepth = 0.35f;
        [SerializeField, Min(0.1f)] private float floorProbeHeight = 8f;
        [SerializeField, Min(0.1f)] private float floorProbeDistance = 40f;

        [Header("Camera")]
        [SerializeField] private Transform cameraTransform;

        [Header("Parts")]
        [SerializeField] private Transform bodyTransform;
        [SerializeField] private Transform turretTransform;
        [SerializeField] private Transform barrelPitchTransform;

        /// <summary>
        /// 砲台Transformへのアクセスプロパティ　TurretManagerEntityから砲台モデルの切り替えに使用する
        /// </summary>
        public Transform TurretTransform => turretTransform;
        public Transform BarrelPitchTransform => barrelPitchTransform;
        public Transform BodyTransform => bodyTransform;
        public Transform CameraTransform => cameraTransform;
        public Transform MuzzleTransform => muzzleTransform;

        public Quaternion NetworkVisualRotation
        {
            get
            {
                if (isAirMode && m_currentAirModeModelRoot != null)
                    return m_currentAirModeModelRoot.rotation;

                Transform groundVisual = FindGroundVisualRoot();
                if (groundVisual != null)
                    return groundVisual.rotation;

                return bodyTransform != null
                    ? bodyTransform.rotation
                    : transform.rotation;
            }
        }

        [Header("Gauge")]
        [SerializeField] private int maxGaugeStock = 3;
        [SerializeField] private float gaugePerStock = 100f;
        [SerializeField] private float currentAirGauge = 0f;

        [Header("Air Mode")]
        [SerializeField] private bool isAirMode = false;
        [SerializeField] private float airTimePerStock = 5f;
        [SerializeField] private float currentAirModeTime = 0f;
        [SerializeField, Min(1f)] private float airModeDamagePenaltyDivisor = 5f;
        [SerializeField, Min(0f)] private float airModeDamagePenaltySeconds = 1f;
        [SerializeField, Min(0f)] private float airModeRapidHitDamagePenaltySeconds = 0.1f;
        [SerializeField, Min(0.01f)] private float airModeRapidHitDamageThreshold = 1.1f;
        [SerializeField] private float airLandingSearchDistance = 100f;
        [SerializeField] private float airLandingSurfaceOffset = 0.05f;

        [Header("Air Mode Transition")]
        [SerializeField] private GameObject airModeTransitionEffectPrefab;
        [SerializeField, Min(0f)] private float airLandingBlinkDuration = 3f;
        [SerializeField, Min(0.02f)] private float airLandingBlinkInterval = 0.3f;
        [SerializeField, Min(0.02f)] private float airLandingBlinkMinInterval = 0.06f;

        [Header("Air Mode Visual")]
        /// <summary>空中形態中だけ非表示にするオブジェクト</summary>
        [SerializeField] private Transform[] hideObjectsInAirMode;

        /// <summary>現在表示中の空中専用モデル</summary>
        private Transform m_currentAirModeModelRoot;

        /// <summary>消費ゲージ数に対応する空中専用モデル</summary>
        [SerializeField] private Transform[] airModeModelRoots;

        /// <summary>現在の空中形態を発動したときに消費したゲージ数</summary>
        private int m_currentAirModeStock;

        /// <summary>空中形態中に空中専用モデルを表示する高さ</summary>
        [SerializeField, Tooltip("飛行モデルと移動Colliderを固定するワールドY座標")]
        private float airModeModelHeight = 3f;
        [SerializeField] private Vector3 airModeModelLocalOffset = new Vector3(0f, -2.2f, -1.2f);

        [Header("Air Mode Motion Visual")]
        [SerializeField] private float airVisualPitchAngle = 16f;
        [SerializeField] private float airVisualBankAngle = 28f;
        [SerializeField] private float airVisualTurnBankAngle = 24f;
        [SerializeField] private float airVisualLookPitchAngle = 8f;
        [SerializeField] private float airVisualRotationLerp = 8f;
        [SerializeField, Min(0f)] private float airTakeoffVisualDuration = 0.6f;
        [SerializeField] private float airTakeoffVisualPitchAngle = 12f;

        [Header("Air Movement Trail")]
        [SerializeField, Min(0.05f)] private float airTrailTime = 0.4f;
        [SerializeField, Min(0.01f)] private float airTrailWidth = 0.08f;
        [SerializeField, Min(0f)] private float airTrailMinMoveSpeed = 0.5f;
        [SerializeField] private Vector3 propellerTrailCenterOffset = new Vector3(0f, -0.05f, 0f);
        [SerializeField, Min(0f)] private float propellerTrailHalfWidth = 1.45f;
        [SerializeField] private Vector3 helicopterTrailCenterOffset = new Vector3(0f, 0f, -0.05f);
        [SerializeField, Min(0f)] private float helicopterTrailHalfWidth = 0.32f;
        [SerializeField] private Vector3 fighterTrailCenterOffset = new Vector3(0f, 0f, 0f);
        [SerializeField, Min(0f)] private float fighterTrailHalfWidth = 0.32f;
        [SerializeField] private Color airTrailStartColor = new Color(0.62f, 0.82f, 1f, 0.42f);
        [SerializeField] private Color airTrailEndColor = new Color(0.72f, 0.5f, 1f, 0f);

        [Header("Fighter Vernier")]
        [SerializeField, Min(0f)] private float fighterVernierMinForwardSpeed = 0.5f;
        [SerializeField] private Color fighterVernierCoreColor = new Color(0.92f, 0.98f, 1f, 0.95f);
        [SerializeField] private Color fighterVernierOuterColor = new Color(0.3f, 0.48f, 1f, 0.62f);
        [SerializeField, Min(0.01f)] private float fighterVernierCoreLength = 0.13f;
        [SerializeField, Min(0.01f)] private float fighterVernierOuterLength = 0.22f;
        [SerializeField, Min(0.005f)] private float fighterVernierCoreRadius = 0.018f;
        [SerializeField, Min(0.005f)] private float fighterVernierOuterRadius = 0.038f;

        /// <summary>現在表示中の空中専用モデルの地上時ローカル座標</summary>
        private Vector3 m_currentAirModeModelGroundLocalPosition;
        private Quaternion m_currentAirModeModelGroundLocalRotation;
        private TrailRenderer[] m_currentAirMovementTrails;
        private Vector3 m_lastAirTrailPlayerPosition;
        private bool m_hasLastAirTrailPlayerPosition;
        private static Material s_airTrailMaterial;
        private GameObject[] m_fighterVernierVisuals;
        private static Material s_fighterVernierCoreMaterial;
        private static Material s_fighterVernierOuterMaterial;

        [Header("Fire")]
        [SerializeField] private GameObject bulletPrefab;
        [SerializeField] private Transform muzzleTransform;
        [SerializeField] private float fireCooldown = 0.2f;

        [Header("Ground Move Effect")]
        [SerializeField] private GameObject groundMoveSmokePrefab;
        [SerializeField] private float groundMoveSmokeStartDelay = 0.18f;
        [SerializeField] private float groundMoveSmokeInterval = 0.08f;
        [SerializeField] private Vector3 groundMoveSmokeOffset = new Vector3(0f, 0.05f, -0.65f);
        [SerializeField] private float groundMoveSmokeTrackHalfWidth = 1.25f;
        [SerializeField] private bool scaleGroundMoveSmokeByMachineSize = true;

        [Header("Move Sound")]
        [SerializeField, Min(0f)] private float movementSoundMinInput = 0.08f;
        [SerializeField, Range(0f, 1f)] private float movementSoundVolume = 0.45f;

        [Header("Turret Aim")]
        [SerializeField] private float turretElevationAngle = 35f;
        [SerializeField] private float turretDepressionAngle = 10f;
        [SerializeField] private float airAimMaxDistance = 200f;

        private Rigidbody m_rigidbody;
        private Collider m_movementCollider;
        private Collider m_sourceMovementCollider;
        private Collider m_runtimeBodyMovementCollider;
        private Vector3 m_runtimeBodyMovementColliderGroundLocalPosition;
        private Renderer[] m_respawnRenderers;
        private bool[] m_protectionRendererStates;
        private Collider[] m_respawnColliders;
        private NetworkObject m_networkObject;
        private PlayerNetworkController m_networkController;
        private PlayerInput m_playerInput;
        /// <summary>砲台管理Entity　攻撃処理の委譲先</summary>
        private TurretManagerEntity m_turretManager;
        private BuffManagerEntity m_buffManager;
        private TPSCameraEntity m_tpsCameraEntity;
        private TurretReticleUIEntity m_turretReticleUI;
        private NearbyThreatIndicatorUIEntity m_damageDirectionUI;
        private TankTrackAnimationController m_trackAnimationController;
        private WorldBorderEntity m_worldBorderEntity;

        private Vector2 m_moveInput;
        private Vector3 m_velocity;
        private Vector3 m_moveDirection;
        private bool m_fireQueued;
        private bool m_isFireHeld;
        private float m_fireTimer;
        private int m_score;
        private int m_defeatedEnemies;
        private int m_priorityDefeatFeedbackCount;
        private int m_noGaugeFeedbackCount;
        private float m_damageTaken;
        private int m_deathCount;
        private float m_currentActivatedAirModeMaxTime;
        private float m_groundMoveSmokeMoveTimer;
        private float m_groundMoveSmokeIntervalTimer;
        private AudioSource m_movementAudioSource;
        private global::AudioCue m_currentMovementCue = global::AudioCue.None;
        private readonly RaycastHit[] m_floorProbeHits = new RaycastHit[32];
        private bool m_canControl = true;
        private Vector3 m_respawnPosition;
        private Quaternion m_respawnRotation = Quaternion.identity;
        private bool m_hasRespawnPoint;
        private bool m_isRespawning;
        private float m_respawnRemaining;
        private float m_respawnProtectionRemaining;
        private float m_respawnProtectionBlinkTimer;
        private bool m_respawnProtectionVisible = true;
        private Renderer[] m_airLandingBlinkRenderers;
        private bool[] m_airLandingBlinkRendererStates;
        private float m_airLandingBlinkTimer;
        private bool m_airLandingBlinkVisible = true;
        private bool m_isAirTakeoffVisualActive;
        private float m_airTakeoffVisualElapsed;
        private Vector3 m_airTakeoffVisualStartLocalPosition;
        private bool m_hasBaseMachineValues;
        private float m_baseGroundHealth;
        private float m_baseAirHealth;
        private Vector3 m_baseScale;
        private Quaternion m_barrelPitchBaseLocalRotation = Quaternion.identity;
        private bool m_hasBarrelPitchBaseLocalRotation;

        public float GroundHealth => groundHealth;
        public float AirHealth => airHealth;
        public float CurrentHealth => currentHealth;

        public bool IsAirMode => isAirMode;
        public bool CanControl => m_canControl;
        public bool IsFireHeld => m_isFireHeld;
        public bool IsRespawning => m_isRespawning;
        public float RespawnRemaining => m_respawnRemaining;
        public bool IsRespawnProtected => m_respawnProtectionRemaining > 0f;
        public float RespawnProtectionRemaining => m_respawnProtectionRemaining;
        public int Score => m_score;
        public int DefeatedEnemies => m_defeatedEnemies;
        public int PriorityDefeatFeedbackCount => m_priorityDefeatFeedbackCount;
        public int NoGaugeFeedbackCount => m_noGaugeFeedbackCount;
        public float DamageTaken => m_damageTaken;
        public int DeathCount => m_deathCount;
        public int MachineIndex => machineIndex;
        public MachineProfile MachineProfile => MachineProfileCatalog.GetProfile(machineIndex);

        public float CurrentAirGauge => currentAirGauge;
        public float MaxAirGauge => maxGaugeStock * gaugePerStock;
        public float GaugePerStock => gaugePerStock;

        public int LocalScore => m_score;
        public float LocalAirGauge => currentAirGauge;

        public int CurrentGaugeStock
        {
            get
            {
                return Mathf.FloorToInt(currentAirGauge / gaugePerStock);
            }
        }

        public float CurrentAirModeTime => currentAirModeTime;
        public float CurrentActivatedAirModeMaxTime => m_currentActivatedAirModeMaxTime;
        public float AirModeTimeRate
        {
            get
            {
                if (!isAirMode || m_currentActivatedAirModeMaxTime <= 0f)
                    return 0f;

                return Mathf.Clamp01(currentAirModeTime / m_currentActivatedAirModeMaxTime);
            }
        }

        /// <summary>現在の空中形態を発動したときに消費したゲージ数</summary>
        public int CurrentAirModeStock => m_currentAirModeStock;
        public float AirTakeoffVisualProgress
        {
            get
            {
                if (!isAirMode)
                    return 0f;

                if (!m_isAirTakeoffVisualActive || airTakeoffVisualDuration <= 0f)
                    return 1f;

                float progress = Mathf.Clamp01(
                    m_airTakeoffVisualElapsed / airTakeoffVisualDuration);
                return progress * progress * (3f - 2f * progress);
            }
        }

        public float AirGaugeRate
        {
            get
            {
                float maxGauge = MaxAirGauge;
                if (maxGauge <= 0f) return 0f;
                return Mathf.Clamp01(currentAirGauge / maxGauge);
            }
        }

        public bool IsActiveDamageCollider(Collider hitCollider)
        {
            if (hitCollider == null || hitCollider.transform.root != transform.root)
                return false;

            if (!isAirMode)
                return !IsColliderUnderAirModel(hitCollider);

            return m_currentAirModeModelRoot != null &&
                   (hitCollider.transform == m_currentAirModeModelRoot ||
                    hitCollider.transform.IsChildOf(m_currentAirModeModelRoot));
        }

        public Vector3 GetActiveTargetAimPoint()
        {
            if (isAirMode &&
                m_currentAirModeModelRoot != null &&
                TryGetColliderBounds(
                    m_currentAirModeModelRoot.GetComponentsInChildren<Collider>(),
                    out Bounds airBounds))
            {
                return airBounds.center;
            }

            if (m_movementCollider != null && m_movementCollider.enabled)
                return m_movementCollider.bounds.center;

            if (m_sourceMovementCollider is BoxCollider sourceBoxCollider)
                return sourceBoxCollider.transform.TransformPoint(
                    sourceBoxCollider.center);

            return transform.position + Vector3.up;
        }

        public override void InitEntity()
        {
            m_rigidbody = GetComponent<Rigidbody>();
            m_sourceMovementCollider = GetComponent<BoxCollider>();
            m_movementCollider = CreateBodyMovementCollider(m_sourceMovementCollider as BoxCollider);
            m_respawnRenderers = GetComponentsInChildren<Renderer>(true);
            m_respawnColliders = GetComponentsInChildren<Collider>(true);
            m_networkObject = GetComponent<NetworkObject>();
            m_networkController = GetComponent<PlayerNetworkController>();
            m_playerInput = GetComponent<PlayerInput>();
            m_turretManager = GetComponent<TurretManagerEntity>();
            m_buffManager = GetComponent<BuffManagerEntity>();
            m_tpsCameraEntity = GetComponent<TPSCameraEntity>();
            m_trackAnimationController = GetComponent<TankTrackAnimationController>();
            ConfigurePlayerPhysics();
            CacheBarrelPitchBaseRotation();

            Stats.SetStat(StatKey.AttackPower, 1f);
            Stats.SetStat(StatKey.AttackRange, 1f);
            Stats.SetStat(StatKey.FireInterval, 1f);
            Stats.SetStat(StatKey.DamageTaken, 1f);
            Stats.SetStat(StatKey.MoveSpeed, 1f);
            ApplyMachineIndex(machineIndex, true);

            currentAirGauge = 0f;
            isAirMode = false;
            currentAirModeTime = 0f;
            m_currentActivatedAirModeMaxTime = 0f;
            m_fireTimer = fireCooldown;
            m_score = 0;

            if (!m_hasRespawnPoint)
            {
                SetRespawnPoint(transform.position, transform.rotation);
            }
            SetRespawnVisualActive(true);
            HideAllAirModeModels();
            SetAirModeVisual(false);
        }

        public void ApplyMachineIndex(int index)
        {
            ApplyMachineIndex(index, true);
        }

        public void ApplyMachineIndex(int index, bool restoreHealth)
        {
            CacheBaseMachineValues();

            machineIndex = MachineProfileCatalog.ClampIndex(index);
            MachineProfile profile = MachineProfileCatalog.GetProfile(machineIndex);

            groundHealth = m_baseGroundHealth * profile.HealthMultiplier;
            airHealth = m_baseAirHealth * profile.HealthMultiplier;
            transform.localScale = m_baseScale * profile.SizeMultiplier;

            Stats.SetStat(StatKey.AttackPower, profile.AttackPowerMultiplier);
            Stats.SetStat(StatKey.FireInterval, profile.AttackSpeedMultiplier);
            Stats.SetStat(StatKey.DamageTaken, profile.DamageTakenMultiplier);
            Stats.SetStat(StatKey.MoveSpeed, profile.MoveSpeedMultiplier);

            ApplyTankMaterialVariant(profile.Type);

            if (restoreHealth)
                currentHealth = groundHealth;
            else
                currentHealth = Mathf.Clamp(currentHealth, 0f, groundHealth);
        }

        public void ApplyMachineVariantMaterials(GameObject targetRoot)
        {
            MachineProfile profile = MachineProfileCatalog.GetProfile(machineIndex);
            if (!TryGetTankMaterialVariant(profile.Type, out TankMaterialVariant variant))
                return;

            ApplyMachineVariantMaterials(targetRoot, variant);
        }

        private void ApplyTankMaterialVariant(MachineType machineType)
        {
            if (!TryGetTankMaterialVariant(machineType, out TankMaterialVariant variant))
                return;

            ApplyMachineVariantMaterials(gameObject, variant);
        }

        private bool TryGetTankMaterialVariant(
            MachineType machineType,
            out TankMaterialVariant variant)
        {
            if (tankMaterialVariants != null)
            {
                for (int i = 0; i < tankMaterialVariants.Length; i++)
                {
                    TankMaterialVariant candidate = tankMaterialVariants[i];
                    if (candidate.MachineType == machineType)
                    {
                        variant = candidate;
                        return true;
                    }
                }
            }

            variant = default;
            return false;
        }

        private void ApplyMachineVariantMaterials(
            GameObject targetRoot,
            TankMaterialVariant variant)
        {
            if (targetRoot == null)
                return;

            Renderer[] renderers = targetRoot.GetComponentsInChildren<Renderer>(true);
            for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
            {
                Renderer targetRenderer = renderers[rendererIndex];
                if (targetRenderer == null)
                    continue;

                Material[] materials = targetRenderer.sharedMaterials;
                bool changed = false;
                string hierarchyName = GetHierarchyName(targetRenderer.transform);

                for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
                {
                    Material sourceMaterial = materials[materialIndex];
                    if (!TryResolveVariantMaterial(
                            sourceMaterial,
                            hierarchyName,
                            variant,
                            out Material replacement))
                    {
                        continue;
                    }

                    if (sourceMaterial == replacement)
                        continue;

                    materials[materialIndex] = replacement;
                    changed = true;
                }

                if (changed)
                    targetRenderer.sharedMaterials = materials;
            }
        }

        private static bool TryResolveVariantMaterial(
            Material sourceMaterial,
            string hierarchyName,
            TankMaterialVariant variant,
            out Material replacement)
        {
            string materialName = sourceMaterial != null ? sourceMaterial.name : string.Empty;

            if (MatchesMaterialPart(materialName, hierarchyName, "WeaponBaseType1"))
            {
                replacement = variant.WeaponBaseType1Material;
                return replacement != null;
            }

            if (MatchesMaterialPart(materialName, hierarchyName, "WeaponBaseType2"))
            {
                replacement = variant.WeaponBaseType2Material;
                return replacement != null;
            }

            if (MatchesMaterialPart(materialName, hierarchyName, "TankTrack") ||
                MatchesMaterialPart(materialName, hierarchyName, "TankTracks"))
            {
                replacement = variant.TankTrackMaterial;
                return replacement != null;
            }

            if (MatchesMaterialPart(materialName, hierarchyName, "TankBase"))
            {
                replacement = variant.TankBaseMaterial;
                return replacement != null;
            }

            replacement = null;
            return false;
        }

        private static bool MatchesMaterialPart(
            string materialName,
            string hierarchyName,
            string partName)
        {
            return ContainsIgnoreCase(materialName, partName) ||
                   ContainsIgnoreCase(hierarchyName, partName);
        }

        private static bool ContainsIgnoreCase(string text, string value)
        {
            return !string.IsNullOrEmpty(text) &&
                   text.IndexOf(value, System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string GetHierarchyName(Transform target)
        {
            if (target == null)
                return string.Empty;

            string result = target.name;
            Transform current = target.parent;
            while (current != null)
            {
                result = current.name + "/" + result;
                current = current.parent;
            }

            return result;
        }

        private void CacheBaseMachineValues()
        {
            if (m_hasBaseMachineValues)
                return;

            m_baseGroundHealth = groundHealth;
            m_baseAirHealth = airHealth;
            m_baseScale = transform.localScale;
            m_hasBaseMachineValues = true;
        }

        public override void UpdateEntity()
        {
            UpdateRespawnCountdown();
            UpdateRespawnProtection();
            UpdateAirLandingBlink();
            UpdateAirMovementTrails(Time.deltaTime);

            if (!CanUpdateLocalPlayer() || m_isRespawning || !m_canControl || m_rigidbody == null)
            {
                StopMovementSound();
                return;
            }

            m_fireTimer += Time.deltaTime;

            UpdateAirMode();
            UpdateMoveDirection();
            UpdateMovementSound();
            RotateVisualToMoveDirection(Time.deltaTime);
            ApplyTurretAim();
            UpdateFireHoldState();

            // TurretManagerEntityがある場合は発射処理をスキップ
            if (m_turretManager == null)
            {
                if (m_fireQueued)
                {
                    Fire();
                    m_fireQueued = false;
                }
            }
            else
            {
                m_fireQueued = false;
            }
        }

        private bool CanUpdateLocalPlayer()
        {
            if (m_networkObject == null) return true;

            // ソロ時。NetworkObjectは付いていてもSpawnされていないのでローカルとして動かす
            if (!m_networkObject.IsSpawned) return true;

            // マルチ時。自分の所有Playerだけ動かす
            return m_networkObject.IsOwner;
        }

        private void UpdateFireHoldState()
        {
            if (m_turretManager == null) return;
            if (!m_isFireHeld) return;
            if (IsFireControlCurrentlyPressed()) return;

            m_isFireHeld = false;
            m_fireQueued = false;
            m_turretManager.ReleaseFire();
        }

        private bool IsFireControlCurrentlyPressed()
        {
            bool hasKnownControl = false;
            bool isPressed = false;

            if (Mouse.current != null)
            {
                hasKnownControl = true;
                isPressed |= Mouse.current.leftButton.isPressed;
            }

            if (Gamepad.current != null)
            {
                hasKnownControl = true;
                isPressed |= Gamepad.current.rightTrigger.isPressed;
            }

            return hasKnownControl ? isPressed : m_isFireHeld;
        }

        private bool IsNetworkedMultiplayer()
        {
            return m_networkObject != null &&
                   m_networkObject.IsSpawned &&
                   NetworkManager.Singleton != null &&
                   NetworkManager.Singleton.IsListening;
        }

        private void UpdateAirMode()
        {
            if (!isAirMode) return;

            currentAirModeTime -= Time.deltaTime;

            if (currentAirModeTime <= 0f)
            {
                SetAirMode(false);
            }
        }

        /// <summary>
        /// すべての空中専用モデルを非表示にする
        /// </summary>
        private void HideAllAirModeModels()
        {
            if (airModeModelRoots == null) return;

            for (int i = 0; i < airModeModelRoots.Length; i++)
            {
                if (airModeModelRoots[i] != null)
                    airModeModelRoots[i].gameObject.SetActive(false);
            }
        }

        private void UpdateAirMovementTrails(float deltaTime)
        {
            Vector3 currentPosition = transform.position;
            if (!m_hasLastAirTrailPlayerPosition)
            {
                m_lastAirTrailPlayerPosition = currentPosition;
                m_hasLastAirTrailPlayerPosition = true;
                SetAirMovementTrailEmission(false);
                SetFighterVernierActive(false);
                return;
            }

            Vector3 moveDelta = currentPosition - m_lastAirTrailPlayerPosition;
            float moveSpeed = moveDelta.magnitude / Mathf.Max(0.0001f, deltaTime);
            m_lastAirTrailPlayerPosition = currentPosition;

            bool shouldEmit =
                isAirMode &&
                m_currentAirModeModelRoot != null &&
                moveSpeed >= airTrailMinMoveSpeed;
            SetAirMovementTrailEmission(shouldEmit);

            bool shouldShowVernier =
                isAirMode &&
                m_currentAirModeStock == 3 &&
                m_currentAirModeModelRoot != null &&
                moveSpeed >= fighterVernierMinForwardSpeed &&
                moveDelta.sqrMagnitude > 0.000001f &&
                Vector3.Dot(
                    moveDelta.normalized,
                    m_currentAirModeModelRoot.forward) > 0.25f;
            SetFighterVernierActive(shouldShowVernier);
        }

        private TrailRenderer[] GetOrCreateAirMovementTrails(
            Transform modelRoot,
            int airModeStock)
        {
            if (modelRoot == null)
                return new TrailRenderer[0];

            GetAirTrailLocalPlacement(
                airModeStock,
                out Vector3 centerOffset,
                out float halfWidth);

            TrailRenderer leftTrail = GetOrCreateAirMovementTrail(
                modelRoot,
                "AirMovementTrail_L",
                centerOffset + Vector3.left * halfWidth);
            TrailRenderer rightTrail = GetOrCreateAirMovementTrail(
                modelRoot,
                "AirMovementTrail_R",
                centerOffset + Vector3.right * halfWidth);

            return new[] { leftTrail, rightTrail };
        }

        private void GetAirTrailLocalPlacement(
            int airModeStock,
            out Vector3 centerOffset,
            out float halfWidth)
        {
            switch (airModeStock)
            {
                case 2:
                    centerOffset = helicopterTrailCenterOffset;
                    halfWidth = helicopterTrailHalfWidth;
                    break;
                case 3:
                    centerOffset = fighterTrailCenterOffset;
                    halfWidth = fighterTrailHalfWidth;
                    break;
                default:
                    centerOffset = propellerTrailCenterOffset;
                    halfWidth = propellerTrailHalfWidth;
                    break;
            }
        }

        private TrailRenderer GetOrCreateAirMovementTrail(
            Transform modelRoot,
            string trailName,
            Vector3 localPosition)
        {
            Transform trailTransform = modelRoot.Find(trailName);
            if (trailTransform == null)
            {
                GameObject trailObject = new GameObject(trailName);
                trailTransform = trailObject.transform;
                trailTransform.SetParent(modelRoot, false);
            }

            trailTransform.localPosition = localPosition;
            trailTransform.localRotation = Quaternion.identity;

            TrailRenderer trail = trailTransform.GetComponent<TrailRenderer>();
            if (trail == null)
                trail = trailTransform.gameObject.AddComponent<TrailRenderer>();

            trail.time = airTrailTime;
            trail.widthMultiplier = airTrailWidth;
            trail.widthCurve = new AnimationCurve(
                new Keyframe(0f, 1f),
                new Keyframe(1f, 0f));
            trail.startColor = airTrailStartColor;
            trail.endColor = airTrailEndColor;
            trail.minVertexDistance = 0.05f;
            trail.numCornerVertices = 2;
            trail.numCapVertices = 2;
            trail.alignment = LineAlignment.View;
            trail.textureMode = LineTextureMode.Stretch;
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trail.receiveShadows = false;
            trail.sharedMaterial = GetAirTrailMaterial();
            trail.emitting = false;
            trail.Clear();
            return trail;
        }

        private static Material GetAirTrailMaterial()
        {
            if (s_airTrailMaterial != null)
                return s_airTrailMaterial;

            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null)
            {
                Debug.LogError("[PlayerEntity] Required shader 'Sprites/Default' was not found.");
                return null;
            }

            s_airTrailMaterial = new Material(shader)
            {
                name = "Runtime Air Movement Trail",
                hideFlags = HideFlags.HideAndDontSave
            };
            return s_airTrailMaterial;
        }

        private GameObject[] GetOrCreateFighterVernierVisuals(Transform fighterRoot)
        {
            Transform leftVernier = FindChildRecursive(fighterRoot, "Vernier_L");
            Transform rightVernier = FindChildRecursive(fighterRoot, "Vernier_R");
            if (leftVernier == null || rightVernier == null)
                return new GameObject[0];

            return new[]
            {
                GetOrCreateFighterVernierVisual(leftVernier),
                GetOrCreateFighterVernierVisual(rightVernier)
            };
        }

        private GameObject GetOrCreateFighterVernierVisual(Transform vernierRoot)
        {
            Transform existingVisual = vernierRoot.Find("VernierVisual");
            if (existingVisual != null)
                return existingVisual.gameObject;

            GameObject visualRoot = new GameObject("VernierVisual");
            visualRoot.transform.SetParent(vernierRoot, false);

            CreateFighterVernierCone(
                visualRoot.transform,
                "OuterFlame",
                fighterVernierOuterLength,
                fighterVernierOuterRadius,
                GetFighterVernierMaterial(false));
            CreateFighterVernierCone(
                visualRoot.transform,
                "CoreFlame",
                fighterVernierCoreLength,
                fighterVernierCoreRadius,
                GetFighterVernierMaterial(true));

            visualRoot.SetActive(false);
            return visualRoot;
        }

        private static void CreateFighterVernierCone(
            Transform parent,
            string objectName,
            float length,
            float radius,
            Material material)
        {
            const int segments = 16;
            GameObject coneObject = new GameObject(objectName);
            coneObject.transform.SetParent(parent, false);

            Vector3[] vertices = new Vector3[segments + 2];
            int[] triangles = new int[segments * 6];
            vertices[0] = Vector3.back * length;
            vertices[1] = Vector3.zero;

            for (int i = 0; i < segments; i++)
            {
                float angle = Mathf.PI * 2f * i / segments;
                vertices[i + 2] = new Vector3(
                    Mathf.Cos(angle) * radius,
                    Mathf.Sin(angle) * radius,
                    0f);
            }

            int triangleIndex = 0;
            for (int i = 0; i < segments; i++)
            {
                int current = i + 2;
                int next = i == segments - 1 ? 2 : current + 1;

                triangles[triangleIndex++] = 0;
                triangles[triangleIndex++] = current;
                triangles[triangleIndex++] = next;
                triangles[triangleIndex++] = 1;
                triangles[triangleIndex++] = next;
                triangles[triangleIndex++] = current;
            }

            Mesh mesh = new Mesh
            {
                name = objectName + "Mesh",
                vertices = vertices,
                triangles = triangles,
                hideFlags = HideFlags.HideAndDontSave
            };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            MeshFilter meshFilter = coneObject.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = mesh;

            MeshRenderer meshRenderer = coneObject.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = material;
            meshRenderer.shadowCastingMode =
                UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
        }

        private Material GetFighterVernierMaterial(bool core)
        {
            Material cachedMaterial = core
                ? s_fighterVernierCoreMaterial
                : s_fighterVernierOuterMaterial;
            if (cachedMaterial != null)
                return cachedMaterial;

            Shader shader = Shader.Find("Particles/Standard Unlit");
            if (shader == null)
            {
                Debug.LogError("[PlayerEntity] Required shader 'Particles/Standard Unlit' was not found.");
                return null;
            }

            Color color = core
                ? fighterVernierCoreColor
                : fighterVernierOuterColor;
            Material material = new Material(shader)
            {
                name = core
                    ? "Runtime Fighter Vernier Core"
                    : "Runtime Fighter Vernier Outer",
                color = color,
                hideFlags = HideFlags.HideAndDontSave
            };

            if (material.HasProperty("_Mode"))
                material.SetFloat("_Mode", 2f);
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);
            if (material.HasProperty("_EmissionColor"))
                material.SetColor("_EmissionColor", color);

            if (core)
                s_fighterVernierCoreMaterial = material;
            else
                s_fighterVernierOuterMaterial = material;
            return material;
        }

        private void SetFighterVernierActive(bool isActive)
        {
            if (m_fighterVernierVisuals == null)
                return;

            for (int i = 0; i < m_fighterVernierVisuals.Length; i++)
            {
                if (m_fighterVernierVisuals[i] != null)
                    m_fighterVernierVisuals[i].SetActive(isActive);
            }
        }

        private void SetAirMovementTrailEmission(bool emitting)
        {
            if (m_currentAirMovementTrails == null)
                return;

            for (int i = 0; i < m_currentAirMovementTrails.Length; i++)
            {
                TrailRenderer trail = m_currentAirMovementTrails[i];
                if (trail != null)
                    trail.emitting = emitting;
            }
        }

        private void ResetAirMovementTrails()
        {
            SetAirMovementTrailEmission(false);
            SetFighterVernierActive(false);
            if (m_currentAirMovementTrails != null)
            {
                for (int i = 0; i < m_currentAirMovementTrails.Length; i++)
                {
                    if (m_currentAirMovementTrails[i] != null)
                        m_currentAirMovementTrails[i].Clear();
                }
            }

            m_hasLastAirTrailPlayerPosition = false;
        }

        /// <summary>
        /// 消費したゲージ数に応じて、今回表示する空中専用モデルを選択する
        /// </summary>
        /// <param name="stockToUse">空中形態開始時に消費したゲージストック数</param>
        private void SelectAirModeModel(int stockToUse)
        {
            m_currentAirModeModelRoot = null;

            HideAllAirModeModels();

            if (airModeModelRoots == null) return;
            if (airModeModelRoots.Length <= 0) return;

            int index = Mathf.Clamp(stockToUse - 1, 0, airModeModelRoots.Length - 1);
            Transform selectedModel = airModeModelRoots[index];

            if (selectedModel == null) return;

            m_currentAirModeModelRoot = selectedModel;
            m_currentAirModeModelGroundLocalPosition = selectedModel.localPosition;
            m_currentAirModeModelGroundLocalRotation = selectedModel.localRotation;
            m_currentAirMovementTrails = GetOrCreateAirMovementTrails(
                selectedModel,
                stockToUse);
            m_fighterVernierVisuals = stockToUse == 3
                ? GetOrCreateFighterVernierVisuals(selectedModel)
                : null;
            ResetAirMovementTrails();
        }


        /// <summary>
        /// 空中形態の開始・終了に合わせて表示モデルを切り替える
        /// </summary>
        /// <param name="airMode">空中形態として表示する場合はtrue</param>
        private void SetAirModeVisual(
            bool airMode,
            bool placeOnSurface = true)
        {
            if (hideObjectsInAirMode != null)
            {
                for (int i = 0; i < hideObjectsInAirMode.Length; i++)
                {
                    if (hideObjectsInAirMode[i] != null)
                        hideObjectsInAirMode[i].gameObject.SetActive(!airMode);
                }
            }

            if (!airMode && isAirMode && placeOnSurface)
                PlacePlayerOnSurfaceBelowAirCollider();

            SetMovementColliderAirOffset(airMode);

            if (!airMode)
            {
                EndAirLandingBlink();
                EndAirTakeoffVisual();

                if (m_currentAirModeModelRoot != null)
                {
                    ResetAirMovementTrails();
                    m_currentAirModeModelRoot.localPosition = m_currentAirModeModelGroundLocalPosition;
                    m_currentAirModeModelRoot.localRotation = m_currentAirModeModelGroundLocalRotation;
                    m_currentAirModeModelRoot.gameObject.SetActive(false);
                    m_currentAirModeModelRoot = null;
                    m_currentAirMovementTrails = null;
                    m_fighterVernierVisuals = null;
                }

                HideAllAirModeModels();
                return;
            }

            if (m_currentAirModeModelRoot == null) return;

            m_currentAirModeModelRoot.gameObject.SetActive(true);
            ResetAirMovementTrails();
            BeginAirTakeoffVisual();
            m_currentAirModeModelRoot.localRotation = m_currentAirModeModelGroundLocalRotation;
            SpawnAirModeTransitionEffect();
        }

        private void BeginAirTakeoffVisual()
        {
            if (m_currentAirModeModelRoot == null)
                return;

            Vector3 targetLocalPosition = GetAirModeModelLocalPosition();
            m_airTakeoffVisualStartLocalPosition =
                m_currentAirModeModelGroundLocalPosition;
            m_airTakeoffVisualStartLocalPosition.x = targetLocalPosition.x;
            m_airTakeoffVisualStartLocalPosition.z = targetLocalPosition.z;
            m_currentAirModeModelRoot.localPosition =
                m_airTakeoffVisualStartLocalPosition;
            m_airTakeoffVisualElapsed = 0f;
            m_isAirTakeoffVisualActive = airTakeoffVisualDuration > 0f;

            if (!m_isAirTakeoffVisualActive)
                m_currentAirModeModelRoot.localPosition = targetLocalPosition;
        }

        private void EndAirTakeoffVisual()
        {
            m_isAirTakeoffVisualActive = false;
            m_airTakeoffVisualElapsed = 0f;
        }

        private void SpawnAirModeTransitionEffect()
        {
            if (airModeTransitionEffectPrefab == null)
                return;

            RuntimeObjectPool.Spawn(
                airModeTransitionEffectPrefab,
                transform.position + Vector3.up * 0.5f,
                Quaternion.identity);
        }

        private void UpdateAirLandingBlink()
        {
            bool shouldBlink =
                isAirMode &&
                m_currentAirModeModelRoot != null &&
                airLandingBlinkDuration > 0f &&
                currentAirModeTime <= airLandingBlinkDuration;

            if (!shouldBlink)
            {
                EndAirLandingBlink();
                return;
            }

            if (m_airLandingBlinkRenderers == null)
                BeginAirLandingBlink();

            m_airLandingBlinkTimer -= Time.deltaTime;
            if (m_airLandingBlinkTimer > 0f)
                return;

            m_airLandingBlinkTimer = GetAirLandingBlinkInterval();
            m_airLandingBlinkVisible = !m_airLandingBlinkVisible;
            ApplyAirLandingBlinkVisual(m_airLandingBlinkVisible);
        }

        private void BeginAirLandingBlink()
        {
            EndAirLandingBlink();

            m_airLandingBlinkRenderers =
                m_currentAirModeModelRoot.GetComponentsInChildren<Renderer>(true);
            m_airLandingBlinkRendererStates =
                new bool[m_airLandingBlinkRenderers.Length];

            for (int i = 0; i < m_airLandingBlinkRenderers.Length; i++)
            {
                Renderer targetRenderer = m_airLandingBlinkRenderers[i];
                m_airLandingBlinkRendererStates[i] =
                    targetRenderer != null && targetRenderer.enabled;
            }

            m_airLandingBlinkVisible = true;
            m_airLandingBlinkTimer = GetAirLandingBlinkInterval();
        }

        private float GetAirLandingBlinkInterval()
        {
            float startInterval = Mathf.Max(0.02f, airLandingBlinkInterval);
            float minimumInterval = Mathf.Clamp(
                airLandingBlinkMinInterval,
                0.02f,
                startInterval);

            if (airLandingBlinkDuration <= 0f)
                return minimumInterval;

            float progress = 1f - Mathf.Clamp01(
                currentAirModeTime / airLandingBlinkDuration);
            return Mathf.Lerp(startInterval, minimumInterval, progress);
        }

        private void ApplyAirLandingBlinkVisual(bool visible)
        {
            if (m_airLandingBlinkRenderers == null ||
                m_airLandingBlinkRendererStates == null)
            {
                return;
            }

            int count = Mathf.Min(
                m_airLandingBlinkRenderers.Length,
                m_airLandingBlinkRendererStates.Length);

            for (int i = 0; i < count; i++)
            {
                if (m_airLandingBlinkRenderers[i] != null)
                {
                    m_airLandingBlinkRenderers[i].enabled =
                        m_airLandingBlinkRendererStates[i] && visible;
                }
            }
        }

        private void EndAirLandingBlink()
        {
            if (m_airLandingBlinkRenderers != null &&
                m_airLandingBlinkRendererStates != null)
            {
                int count = Mathf.Min(
                    m_airLandingBlinkRenderers.Length,
                    m_airLandingBlinkRendererStates.Length);

                for (int i = 0; i < count; i++)
                {
                    if (m_airLandingBlinkRenderers[i] != null)
                    {
                        m_airLandingBlinkRenderers[i].enabled =
                            m_airLandingBlinkRendererStates[i];
                    }
                }
            }

            m_airLandingBlinkRenderers = null;
            m_airLandingBlinkRendererStates = null;
            m_airLandingBlinkTimer = 0f;
            m_airLandingBlinkVisible = true;
        }

        public override void FixedUpdateEntity()
        {
            if (!CanUpdateLocalPlayer()) return;
            if (m_isRespawning) return;
            if (!m_canControl) return;
            if (m_rigidbody == null) return;

            MovePlayer(Time.fixedDeltaTime);
            MaintainMovementSafety();
            UpdateGroundMoveSmoke(Time.fixedDeltaTime);
        }

        private void UpdateMoveDirection()
        {
            Vector2 input = Vector2.ClampMagnitude(m_moveInput, 1f);

            Vector3 forward;
            Vector3 right;

            if (cameraTransform != null)
            {
                forward = cameraTransform.forward;
                right = cameraTransform.right;
            }
            else
            {
                forward = transform.forward;
                right = transform.right;
            }

            forward.y = 0f;
            right.y = 0f;

            if (forward.sqrMagnitude > 0.0001f)
                forward.Normalize();
            else
                forward = transform.forward;

            if (right.sqrMagnitude > 0.0001f)
                right.Normalize();
            else
                right = transform.right;

            m_moveDirection = Vector3.ClampMagnitude(right * input.x + forward * input.y, 1f);
        }

        private void MovePlayer(float deltaTime)
        {
            bool isGrounded = IsGrounded();

            if (!isAirMode)
            {
                if (isGrounded && m_velocity.y < 0f)
                    m_velocity.y = -2f;

                m_velocity.y += gravity * deltaTime;
            }
            else
            {
                m_velocity.y = 0f;
            }

            float moveSpeed = (isAirMode ? airMoveSpeed : groundMoveSpeed) * Stats.GetStat(StatKey.MoveSpeed);
            Vector3 horizontalVelocity = m_moveDirection * moveSpeed;

            Vector3 move = horizontalVelocity;
            move += Vector3.up * m_velocity.y;

            m_rigidbody.linearVelocity = move;
        }

        private void MaintainMovementSafety()
        {
            if (m_rigidbody == null ||
                m_movementCollider == null ||
                !m_movementCollider.enabled)
            {
                return;
            }

            ClampInsideWorldBorder();

            if (!isAirMode)
                RecoverFromFloorSink();

            ClampInsideWorldBorder();
        }

        private bool ClampInsideWorldBorder()
        {
            WorldBorderEntity border = GetWorldBorderEntity();
            if (border == null || !border.TryGetSourceBounds(out Bounds borderBounds))
                return false;

            Bounds movementBounds = m_movementCollider.bounds;
            float padding = Mathf.Max(0f, worldBorderClampPadding);
            Vector3 correction = Vector3.zero;

            float minX = borderBounds.min.x + padding;
            float maxX = borderBounds.max.x - padding;
            float minZ = borderBounds.min.z + padding;
            float maxZ = borderBounds.max.z - padding;

            if (movementBounds.min.x < minX)
                correction.x += minX - movementBounds.min.x;
            else if (movementBounds.max.x > maxX)
                correction.x -= movementBounds.max.x - maxX;

            if (movementBounds.min.z < minZ)
                correction.z += minZ - movementBounds.min.z;
            else if (movementBounds.max.z > maxZ)
                correction.z -= movementBounds.max.z - maxZ;

            return ApplyMovementRecovery(correction);
        }

        private WorldBorderEntity GetWorldBorderEntity()
        {
            if (m_worldBorderEntity != null && m_worldBorderEntity.isActiveAndEnabled)
                return m_worldBorderEntity;

            m_worldBorderEntity = FindAnyObjectByType<WorldBorderEntity>();
            return m_worldBorderEntity;
        }

        private bool RecoverFromFloorSink()
        {
            if (!TryFindBattleFloorY(out float floorY))
                return false;

            Bounds movementBounds = m_movementCollider.bounds;
            float sinkDepth = floorY - movementBounds.min.y;
            if (sinkDepth < Mathf.Max(0f, floorSinkRecoveryDepth))
                return false;

            float targetMinY = floorY + Mathf.Max(0f, floorClearance);
            float lift = targetMinY - movementBounds.min.y;
            return ApplyMovementRecovery(Vector3.up * lift);
        }

        private bool ApplyMovementRecovery(Vector3 correction)
        {
            if (correction.sqrMagnitude <= 0.000001f || m_rigidbody == null)
                return false;

            Vector3 targetPosition = m_rigidbody.position + correction;
            m_rigidbody.position = targetPosition;
            transform.position = targetPosition;

            Vector3 velocity = m_rigidbody.linearVelocity;
            if (Mathf.Abs(correction.x) > 0.0001f)
                velocity.x = 0f;
            if (Mathf.Abs(correction.y) > 0.0001f && velocity.y < 0f)
                velocity.y = 0f;
            if (Mathf.Abs(correction.z) > 0.0001f)
                velocity.z = 0f;
            m_rigidbody.linearVelocity = velocity;

            Physics.SyncTransforms();
            return true;
        }

        private bool TryFindBattleFloorY(out float floorY)
        {
            floorY = 0f;

            Bounds bounds = m_movementCollider.bounds;
            Vector3 origin = bounds.center;
            origin.y = bounds.max.y + Mathf.Max(0.1f, floorProbeHeight);

            int hitCount = Physics.RaycastNonAlloc(
                origin,
                Vector3.down,
                m_floorProbeHits,
                Mathf.Max(0.1f, floorProbeHeight + floorProbeDistance),
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore);

            bool found = false;
            float bestY = float.NegativeInfinity;

            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = m_floorProbeHits[i];
                Collider hitCollider = hit.collider;
                if (hitCollider == null ||
                    hitCollider.transform.root == transform.root ||
                    hit.normal.y < 0.5f ||
                    !ProjectileSweepUtility.IsBattlePlane(hitCollider))
                {
                    continue;
                }

                if (!found || hit.point.y > bestY)
                {
                    bestY = hit.point.y;
                    found = true;
                }
            }

            if (!found)
                return false;

            floorY = bestY;
            return true;
        }

        private void UpdateGroundMoveSmoke(float deltaTime)
        {
            if (groundMoveSmokePrefab == null)
                return;

            bool shouldEmit =
                !isAirMode &&
                m_moveDirection.sqrMagnitude > 0.04f;

            if (!shouldEmit)
            {
                m_groundMoveSmokeMoveTimer = 0f;
                m_groundMoveSmokeIntervalTimer = 0f;
                return;
            }

            m_groundMoveSmokeMoveTimer += deltaTime;
            if (m_groundMoveSmokeMoveTimer < Mathf.Max(0f, groundMoveSmokeStartDelay))
                return;

            m_groundMoveSmokeIntervalTimer += deltaTime;
            if (m_groundMoveSmokeIntervalTimer < Mathf.Max(0.01f, groundMoveSmokeInterval))
                return;

            m_groundMoveSmokeIntervalTimer = 0f;
            SpawnGroundMoveSmokeForTracks();
        }

        private void SpawnGroundMoveSmokeForTracks()
        {
            Vector3 moveDirection = m_moveDirection.sqrMagnitude > 0.0001f
                ? m_moveDirection.normalized
                : transform.forward;
            Vector3 rightDirection = bodyTransform != null
                ? bodyTransform.right
                : transform.right;
            rightDirection.y = 0f;
            if (rightDirection.sqrMagnitude <= 0.0001f)
                rightDirection = transform.right;

            rightDirection.Normalize();
            Quaternion smokeRotation = Quaternion.LookRotation(Vector3.up, -moveDirection);
            float trackHalfWidth = GetGroundMoveSmokeTrackHalfWidth();

            SpawnGroundMoveSmokeAtTrack(
                moveDirection,
                rightDirection,
                -trackHalfWidth,
                smokeRotation);
            SpawnGroundMoveSmokeAtTrack(
                moveDirection,
                rightDirection,
                trackHalfWidth,
                smokeRotation);
        }

        private float GetGroundMoveSmokeTrackHalfWidth()
        {
            float baseHalfWidth =
                Mathf.Max(0f, groundMoveSmokeTrackHalfWidth) +
                Mathf.Abs(groundMoveSmokeOffset.x);

            if (!scaleGroundMoveSmokeByMachineSize)
                return baseHalfWidth;

            float referenceSize = Mathf.Max(
                0.01f,
                MachineProfileCatalog.GetDefaultProfile(MachineType.Light).SizeMultiplier);
            float currentSize = Mathf.Max(
                0.01f,
                MachineProfile.SizeMultiplier);

            return baseHalfWidth * (currentSize / referenceSize);
        }

        private void SpawnGroundMoveSmokeAtTrack(
            Vector3 moveDirection,
            Vector3 rightDirection,
            float sideOffset,
            Quaternion smokeRotation)
        {
            Vector3 basePosition =
                transform.position +
                rightDirection * sideOffset -
                moveDirection * Mathf.Abs(groundMoveSmokeOffset.z);
            Vector3 smokePosition = basePosition + Vector3.up * groundMoveSmokeOffset.y;

            if (TryFindGroundPoint(basePosition, out Vector3 groundPoint))
                smokePosition = groundPoint + Vector3.up * groundMoveSmokeOffset.y;

            SpawnGroundMoveSmoke(smokePosition, smokeRotation);
        }

        private void SpawnGroundMoveSmoke(Vector3 smokePosition, Quaternion smokeRotation)
        {
            RuntimeObjectPool.Spawn(
                groundMoveSmokePrefab,
                smokePosition,
                smokeRotation
            );
        }

        private void UpdateMovementSound()
        {
            bool shouldPlay =
                m_moveInput.sqrMagnitude > movementSoundMinInput * movementSoundMinInput &&
                !m_isRespawning &&
                m_canControl;

            if (!shouldPlay)
            {
                StopMovementSound();
                return;
            }

            global::AudioCue cue = isAirMode
                ? GetAirModeMovementCue()
                : global::AudioCue.TankMove;

            if (cue == global::AudioCue.None)
            {
                StopMovementSound();
                return;
            }

            EnsureMovementAudioSource();
            if (m_movementAudioSource == null)
                return;

            if (m_currentMovementCue != cue || m_movementAudioSource.clip == null)
            {
                global::AudioManager.Instance?.ConfigureLoopSource(
                    m_movementAudioSource,
                    cue,
                    movementSoundVolume);
                m_currentMovementCue = cue;
            }

            if (m_movementAudioSource.clip != null &&
                !m_movementAudioSource.isPlaying)
            {
                m_movementAudioSource.Play();
            }
        }

        private global::AudioCue GetAirModeMovementCue()
        {
            switch (m_currentAirModeStock)
            {
                case 1:
                    return global::AudioCue.Plane;
                case 2:
                    return global::AudioCue.Helicopter;
                case 3:
                    return global::AudioCue.Fighter;
                default:
                    return global::AudioCue.Plane;
            }
        }

        private void EnsureMovementAudioSource()
        {
            if (m_movementAudioSource != null)
                return;

            m_movementAudioSource = gameObject.AddComponent<AudioSource>();
        }

        private void StopMovementSound()
        {
            if (m_movementAudioSource != null && m_movementAudioSource.isPlaying)
                m_movementAudioSource.Stop();

            m_currentMovementCue = global::AudioCue.None;
        }

        private bool TryFindGroundPoint(Vector3 basePosition, out Vector3 groundPoint)
        {
            float rayDistance = 4f;
            Vector3 rayOrigin = basePosition + Vector3.up * 1.5f;

            if (m_movementCollider != null)
            {
                Bounds bounds = m_movementCollider.bounds;
                rayDistance = Mathf.Max(4f, bounds.size.y + 2f);
                rayOrigin.y = bounds.max.y + 0.5f;
            }

            RaycastHit[] hits = Physics.RaycastAll(
                rayOrigin,
                Vector3.down,
                rayDistance,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore);

            float closestDistance = float.MaxValue;
            groundPoint = basePosition;
            bool hasGround = false;

            for (int i = 0; i < hits.Length; i++)
            {
                Collider hitCollider = hits[i].collider;
                if (hitCollider == null)
                    continue;
                if (hitCollider.transform.root == transform.root)
                    continue;
                if (hits[i].distance >= closestDistance)
                    continue;

                closestDistance = hits[i].distance;
                groundPoint = hits[i].point;
                hasGround = true;
            }

            return hasGround;
        }

        private void RotateVisualToMoveDirection(float deltaTime)
        {
            Quaternion turretWorldRotation = turretTransform != null
                ? turretTransform.rotation
                : Quaternion.identity;

            if (isAirMode)
            {
                RotateAirModeVisual(deltaTime);
                RestoreTurretWorldRotation(turretWorldRotation);
                return;
            }

            if (m_moveDirection.sqrMagnitude <= 0.001f)
                return;

            Quaternion targetRotation = Quaternion.LookRotation(m_moveDirection);
            RotateTransformTo(targetRotation, bodyTransform, deltaTime);
            RotateGroundVisualRoots(targetRotation, deltaTime);
            RestoreTurretWorldRotation(turretWorldRotation);
        }

        private void RotateAirModeVisual(float deltaTime)
        {
            if (m_currentAirModeModelRoot == null)
                return;

            float takeoffProgress = 1f;
            if (m_isAirTakeoffVisualActive)
            {
                m_airTakeoffVisualElapsed += deltaTime;
                takeoffProgress = Mathf.Clamp01(
                    m_airTakeoffVisualElapsed /
                    Mathf.Max(0.01f, airTakeoffVisualDuration));
                float easedProgress = takeoffProgress * takeoffProgress *
                                      (3f - 2f * takeoffProgress);

                m_currentAirModeModelRoot.localPosition = Vector3.Lerp(
                    m_airTakeoffVisualStartLocalPosition,
                    GetAirModeModelLocalPosition(),
                    easedProgress);

                if (takeoffProgress >= 1f)
                    EndAirTakeoffVisual();
            }
            else
            {
                m_currentAirModeModelRoot.localPosition = Vector3.Lerp(
                    m_currentAirModeModelRoot.localPosition,
                    GetAirModeModelLocalPosition(),
                    1f - Mathf.Exp(-airVisualRotationLerp * deltaTime)
                );
            }

            Vector3 flatForward = GetAirVisualTargetForward();
            flatForward.y = 0f;

            if (flatForward.sqrMagnitude <= 0.001f)
                flatForward = transform.forward;

            Quaternion yawRotation = Quaternion.LookRotation(flatForward.normalized, Vector3.up);
            Vector2 input = Vector2.ClampMagnitude(m_moveInput, 1f);
            Vector3 currentForward = Vector3.ProjectOnPlane(m_currentAirModeModelRoot.forward, Vector3.up);
            if (currentForward.sqrMagnitude <= 0.001f)
                currentForward = flatForward;

            float yawDelta = Vector3.SignedAngle(currentForward.normalized, flatForward.normalized, Vector3.up);
            float turnBank = -Mathf.Clamp(yawDelta / 55f, -1f, 1f) * airVisualTurnBankAngle;
            float lookPitch = cameraTransform != null
                ? -Mathf.Clamp(cameraTransform.forward.y, -1f, 1f) * airVisualLookPitchAngle
                : 0f;
            Quaternion motionTilt = Quaternion.Euler(
                input.y * airVisualPitchAngle + lookPitch -
                (1f - takeoffProgress) * airTakeoffVisualPitchAngle,
                0f,
                Mathf.Clamp(-input.x * airVisualBankAngle + turnBank, -airVisualBankAngle, airVisualBankAngle)
            );
            Quaternion targetRotation = yawRotation * motionTilt;

            m_currentAirModeModelRoot.rotation = Quaternion.Slerp(
                m_currentAirModeModelRoot.rotation,
                targetRotation,
                1f - Mathf.Exp(-airVisualRotationLerp * deltaTime)
            );
        }

        private Vector3 GetAirModeModelLocalPosition()
        {
            if (m_currentAirModeModelRoot == null)
                return m_currentAirModeModelGroundLocalPosition;

            Vector3 localPosition = GetLocalPositionAtWorldHeight(
                m_currentAirModeModelRoot,
                m_currentAirModeModelGroundLocalPosition,
                airModeModelHeight
            );

            return localPosition + airModeModelLocalOffset;
        }

        private Vector3 GetAirVisualTargetForward()
        {
            if (m_isFireHeld && cameraTransform != null)
            {
                Vector3 cameraForward = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up);
                if (cameraForward.sqrMagnitude > 0.001f)
                    return cameraForward.normalized;
            }

            if (m_moveDirection.sqrMagnitude > 0.001f)
                return m_moveDirection.normalized;

            if (cameraTransform != null)
            {
                Vector3 cameraForward = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up);
                if (cameraForward.sqrMagnitude > 0.001f)
                    return cameraForward.normalized;
            }

            return m_currentAirModeModelRoot != null
                ? m_currentAirModeModelRoot.forward
                : transform.forward;
        }

        private void RestoreTurretWorldRotation(Quaternion turretWorldRotation)
        {
            if (turretTransform == null)
                return;

            turretTransform.rotation = turretWorldRotation;
        }

        private void RotateGroundVisualRoots(Quaternion targetRotation, float deltaTime)
        {
            if (hideObjectsInAirMode == null)
                return;

            for (int i = 0; i < hideObjectsInAirMode.Length; i++)
            {
                Transform visualRoot = hideObjectsInAirMode[i];
                if (visualRoot == null) continue;
                if (visualRoot == bodyTransform) continue;
                if (visualRoot == turretTransform) continue;

                RotateTransformTo(targetRotation, visualRoot, deltaTime);
            }
        }

        public void ApplyNetworkVisualRotation(
            Quaternion targetRotation,
            float lerpSpeed,
            float deltaTime)
        {
            float t = 1f - Mathf.Exp(-Mathf.Max(0f, lerpSpeed) * deltaTime);

            if (isAirMode && m_currentAirModeModelRoot != null)
            {
                RefreshNetworkAirModeVisualPosition(deltaTime);
                m_currentAirModeModelRoot.rotation = Quaternion.Slerp(
                    m_currentAirModeModelRoot.rotation,
                    targetRotation,
                    t);
                return;
            }

            if (bodyTransform != null)
            {
                bodyTransform.rotation = Quaternion.Slerp(
                    bodyTransform.rotation,
                    targetRotation,
                    t);
            }

            if (hideObjectsInAirMode == null)
                return;

            for (int i = 0; i < hideObjectsInAirMode.Length; i++)
            {
                Transform visualRoot = hideObjectsInAirMode[i];
                if (visualRoot == null ||
                    visualRoot == bodyTransform ||
                    visualRoot == turretTransform)
                {
                    continue;
                }

                visualRoot.rotation = Quaternion.Slerp(
                    visualRoot.rotation,
                    targetRotation,
                    t);
            }
        }

        private void RefreshNetworkAirModeVisualPosition(float deltaTime)
        {
            if (m_currentAirModeModelRoot == null)
                return;

            Vector3 targetPosition = GetAirModeModelLocalPosition();
            if (m_isAirTakeoffVisualActive)
            {
                m_currentAirModeModelRoot.localPosition = targetPosition;
                EndAirTakeoffVisual();
                return;
            }

            float t = 1f - Mathf.Exp(-12f * Mathf.Max(0f, deltaTime));
            m_currentAirModeModelRoot.localPosition = Vector3.Lerp(
                m_currentAirModeModelRoot.localPosition,
                targetPosition,
                t);
        }

        private Transform FindGroundVisualRoot()
        {
            if (hideObjectsInAirMode == null)
                return null;

            for (int i = 0; i < hideObjectsInAirMode.Length; i++)
            {
                Transform visualRoot = hideObjectsInAirMode[i];
                if (visualRoot == null ||
                    visualRoot == bodyTransform ||
                    visualRoot == turretTransform)
                {
                    continue;
                }

                return visualRoot;
            }

            return null;
        }

        private void RotateTransformTo(Quaternion targetRotation, Transform target, float deltaTime)
        {
            if (target == null)
                return;

            target.rotation = Quaternion.Slerp(
                target.rotation,
                targetRotation,
                rotateSpeed * deltaTime
            );
        }

        private Collider CreateBodyMovementCollider(BoxCollider sourceCollider)
        {
            if (sourceCollider == null)
                return GetComponent<Collider>();

            if (bodyTransform == null)
                return sourceCollider;

            sourceCollider.enabled = false;

            GameObject colliderObject = new GameObject("BodyMovementCollider_Runtime");
            colliderObject.transform.SetParent(bodyTransform, false);
            colliderObject.transform.localPosition = Vector3.zero;
            colliderObject.transform.localRotation = Quaternion.identity;
            colliderObject.transform.localScale = Vector3.one;
            m_runtimeBodyMovementColliderGroundLocalPosition = colliderObject.transform.localPosition;

            BoxCollider bodyCollider = colliderObject.AddComponent<BoxCollider>();
            bodyCollider.isTrigger = false;
            bodyCollider.size = sourceCollider.size;
            bodyCollider.center = sourceCollider.center;
            bodyCollider.material = sourceCollider.material;

            m_runtimeBodyMovementCollider = bodyCollider;
            return bodyCollider;
        }

        private void SetMovementColliderAirOffset(bool airMode)
        {
            if (m_runtimeBodyMovementCollider == null)
                return;

            m_runtimeBodyMovementCollider.transform.localPosition = airMode
                ? GetLocalPositionAtWorldHeight(
                    m_runtimeBodyMovementCollider.transform,
                    m_runtimeBodyMovementColliderGroundLocalPosition,
                    airModeModelHeight
                )
                : m_runtimeBodyMovementColliderGroundLocalPosition;
        }

        private static Vector3 GetLocalPositionAtWorldHeight(
            Transform target,
            Vector3 groundLocalPosition,
            float worldHeight)
        {
            if (target == null || target.parent == null)
            {
                groundLocalPosition.y = worldHeight;
                return groundLocalPosition;
            }

            Vector3 worldPosition = target.parent.TransformPoint(groundLocalPosition);
            worldPosition.y = worldHeight;
            return target.parent.InverseTransformPoint(worldPosition);
        }

        private void PlacePlayerOnSurfaceBelowAirCollider()
        {
            if (m_movementCollider == null || !m_movementCollider.enabled)
                return;

            Bounds airBounds = m_movementCollider.bounds;
            Vector3 castHalfExtents = airBounds.extents;
            castHalfExtents.x = Mathf.Max(0.05f, castHalfExtents.x * 0.8f);
            castHalfExtents.y = 0.02f;
            castHalfExtents.z = Mathf.Max(0.05f, castHalfExtents.z * 0.8f);

            RaycastHit[] hits = Physics.BoxCastAll(
                airBounds.center,
                castHalfExtents,
                Vector3.down,
                m_movementCollider.transform.rotation,
                Mathf.Max(1f, airLandingSearchDistance),
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore
            );

            bool foundSurface = false;
            RaycastHit closestSurface = default;

            for (int i = 0; i < hits.Length; i++)
            {
                RaycastHit hit = hits[i];
                if (hit.collider == null)
                    continue;
                if (hit.collider.transform.root == transform.root)
                    continue;
                if (hit.normal.y < 0.5f)
                    continue;
                if (foundSurface && hit.distance >= closestSurface.distance)
                    continue;

                closestSurface = hit;
                foundSurface = true;
            }

            if (!foundSurface)
                return;

            SetMovementColliderAirOffset(false);
            Physics.SyncTransforms();

            float verticalAdjustment =
                closestSurface.point.y +
                Mathf.Max(0f, airLandingSurfaceOffset) -
                m_movementCollider.bounds.min.y;

            if (Mathf.Abs(verticalAdjustment) <= 0.001f)
                return;

            Vector3 landingPosition = transform.position + Vector3.up * verticalAdjustment;
            if (m_rigidbody != null)
            {
                m_rigidbody.position = landingPosition;
                Vector3 velocity = m_rigidbody.linearVelocity;
                velocity.y = 0f;
                m_rigidbody.linearVelocity = velocity;
            }
            else
            {
                transform.position = landingPosition;
            }

            Physics.SyncTransforms();
        }

        private void ConfigurePlayerPhysics()
        {
            if (m_rigidbody == null) return;

            m_rigidbody.useGravity = false;
            m_rigidbody.isKinematic = false;
            m_rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
            m_rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            m_rigidbody.constraints = RigidbodyConstraints.FreezeRotation;

            if (m_sourceMovementCollider != null && m_sourceMovementCollider != m_movementCollider)
                m_sourceMovementCollider.enabled = false;

            if (m_movementCollider != null)
            {
                PhysicsMaterial noFrictionMaterial = new PhysicsMaterial("Player Movement No Friction")
                {
                    dynamicFriction = 0f,
                    staticFriction = 0f,
                    frictionCombine = PhysicsMaterialCombine.Minimum,
                    bounceCombine = PhysicsMaterialCombine.Minimum
                };

                m_movementCollider.material = noFrictionMaterial;
            }
        }

        private bool IsGrounded()
        {
            if (m_movementCollider == null)
                return false;

            Bounds bounds = m_movementCollider.bounds;
            Vector3 halfExtents = bounds.extents;
            halfExtents.x = Mathf.Max(0.01f, halfExtents.x * 0.9f);
            halfExtents.y = Mathf.Max(0.01f, groundCheckDistance * 0.5f);
            halfExtents.z = Mathf.Max(0.01f, halfExtents.z * 0.9f);

            Vector3 checkCenter = new Vector3(
                bounds.center.x,
                bounds.min.y - halfExtents.y,
                bounds.center.z
            );

            Collider[] hits = Physics.OverlapBox(
                checkCenter,
                halfExtents,
                m_movementCollider.transform.rotation,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore
            );

            for (int i = 0; i < hits.Length; i++)
            {
                Collider hitCollider = hits[i];
                if (hitCollider == null) continue;
                if (hitCollider.transform.root == transform.root) continue;

                return true;
            }

            return false;
        }

        private void ApplyTurretAim()
        {
            if (cameraTransform == null || turretTransform == null) return;

            Vector3 desiredDirection = cameraTransform.forward.sqrMagnitude > 0.0001f
                ? cameraTransform.forward.normalized
                : transform.forward;
            Vector3 flatDirection = Vector3.ProjectOnPlane(desiredDirection, Vector3.up);

            if (flatDirection.sqrMagnitude < 0.0001f)
                flatDirection = Vector3.ProjectOnPlane(turretTransform.forward, Vector3.up);

            if (flatDirection.sqrMagnitude < 0.0001f)
                flatDirection = Vector3.ProjectOnPlane(transform.forward, Vector3.up);

            Quaternion targetYawRotation = Quaternion.LookRotation(flatDirection.normalized, Vector3.up);

            turretTransform.rotation = Quaternion.Slerp(
                turretTransform.rotation,
                targetYawRotation,
                rotateSpeed * Time.deltaTime
            );

            ApplyBarrelPitch(desiredDirection);
        }

        private void ApplyBarrelPitch(Vector3 desiredWorldDirection)
        {
            if (barrelPitchTransform == null)
                return;

            CacheBarrelPitchBaseRotation();

            Vector3 localDirection = turretTransform.InverseTransformDirection(desiredWorldDirection);
            if (localDirection.sqrMagnitude < 0.0001f)
                return;

            float pitch = Mathf.Atan2(localDirection.y, Mathf.Max(0.0001f, localDirection.z)) * Mathf.Rad2Deg;
            pitch = Mathf.Clamp(pitch, -turretDepressionAngle, turretElevationAngle);

            Quaternion targetLocalRotation =
                m_barrelPitchBaseLocalRotation *
                Quaternion.Euler(-pitch, 0f, 0f);

            barrelPitchTransform.localRotation = Quaternion.Slerp(
                barrelPitchTransform.localRotation,
                targetLocalRotation,
                rotateSpeed * Time.deltaTime
            );
        }

        private void CacheBarrelPitchBaseRotation()
        {
            if (m_hasBarrelPitchBaseLocalRotation || barrelPitchTransform == null)
                return;

            m_barrelPitchBaseLocalRotation = barrelPitchTransform.localRotation;
            m_hasBarrelPitchBaseLocalRotation = true;
        }

        public Quaternion GetConstrainedTurretAimRotation(Vector3 desiredWorldDirection)
        {
            Vector3 aimDirection = GetConstrainedTurretAimDirection(desiredWorldDirection);
            return Quaternion.LookRotation(aimDirection, Vector3.up);
        }

        public Vector3 GetConstrainedTurretAimDirection(Vector3 desiredWorldDirection)
        {
            Vector3 normalizedDirection = desiredWorldDirection.sqrMagnitude > 0.0001f
                ? desiredWorldDirection.normalized
                : transform.forward;

            Vector3 flatDirection = Vector3.ProjectOnPlane(normalizedDirection, Vector3.up);
            if (flatDirection.sqrMagnitude < 0.0001f && turretTransform != null)
                flatDirection = Vector3.ProjectOnPlane(turretTransform.forward, Vector3.up);

            if (flatDirection.sqrMagnitude < 0.0001f)
                flatDirection = transform.forward;

            flatDirection.Normalize();

            float pitch = Mathf.Asin(Mathf.Clamp(normalizedDirection.y, -1f, 1f)) * Mathf.Rad2Deg;
            pitch = Mathf.Clamp(pitch, -turretDepressionAngle, turretElevationAngle);

            Quaternion yawRotation = Quaternion.LookRotation(flatDirection, Vector3.up);
            Vector3 pitchAxis = yawRotation * Vector3.right;
            Vector3 constrainedDirection = Quaternion.AngleAxis(-pitch, pitchAxis) * flatDirection;

            return constrainedDirection.normalized;
        }

        public bool TryGetAirAimRay(out Ray aimRay)
        {
            if (!isAirMode || cameraTransform == null)
            {
                aimRay = default;
                return false;
            }

            aimRay = new Ray(
                cameraTransform.position,
                cameraTransform.forward.normalized
            );
            return true;
        }

        public bool TryGetAirAimRotation(Vector3 shotPosition, out Quaternion rotation)
        {
            if (!TryGetAirAimPoint(out Vector3 targetPoint, out _))
            {
                rotation = default;
                return false;
            }

            Vector3 direction = targetPoint - shotPosition;
            Vector3 fallbackDirection = cameraTransform != null
                ? cameraTransform.forward
                : transform.forward;

            if (direction.sqrMagnitude <= 0.0001f)
                direction = fallbackDirection;

            Vector3 normalizedDirection = direction.normalized;
            Vector3 normalizedFallback = fallbackDirection.sqrMagnitude > 0.0001f
                ? fallbackDirection.normalized
                : transform.forward;

            if (Vector3.Dot(normalizedDirection, normalizedFallback) < 0.25f)
                normalizedDirection = normalizedFallback;

            rotation = Quaternion.LookRotation(normalizedDirection, Vector3.up);
            return true;
        }

        public bool TryGetAirAimPoint(out Vector3 aimPoint, out Collider hitCollider)
        {
            if (!TryGetAirAimRay(out Ray aimRay))
            {
                aimPoint = default;
                hitCollider = null;
                return false;
            }

            aimPoint = GetAirAimTargetPoint(aimRay, out hitCollider);
            return true;
        }

        private Vector3 GetAirAimTargetPoint(Ray aimRay, out Collider hitCollider)
        {
            float distance = Mathf.Max(1f, airAimMaxDistance);
            hitCollider = null;
            RaycastHit[] hits = Physics.RaycastAll(
                aimRay,
                distance,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore
            );
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            for (int i = 0; i < hits.Length; i++)
            {
                Collider candidateCollider = hits[i].collider;
                if (candidateCollider == null) continue;
                if (candidateCollider.transform.IsChildOf(transform)) continue;
                if (ProjectileSweepUtility.IsWorldBorder(candidateCollider)) continue;

                hitCollider = candidateCollider;
                return hits[i].point;
            }

            return aimRay.origin + aimRay.direction * distance;
        }

        private void Fire()
        {
            if (!CanUpdateLocalPlayer()) return;
            if (!m_canControl) return;
            if (bulletPrefab == null || muzzleTransform == null) return;
            if (m_fireTimer < GetEffectiveFireCooldown()) return;

            Vector3 shootPosition = muzzleTransform.position;
            Quaternion shootRotation = muzzleTransform.rotation;

            if (IsNetworkedMultiplayer() && m_networkController != null)
            {
                if (m_networkController.TryRequestFire(shootPosition, shootRotation))
                {
                    m_fireTimer = 0f;
                    return;
                }
            }

            SpawnLocalBullet(shootPosition, shootRotation);

            m_fireTimer = 0f;
        }

        private void SpawnLocalBullet(Vector3 position, Quaternion rotation)
        {
            GameObject bulletObject = RuntimeObjectPool.Spawn(bulletPrefab, position, rotation);

            PlayerBulletEntity bullet = bulletObject.GetComponent<PlayerBulletEntity>();
            if (bullet != null)
            {
                bullet.Setup(this);
            }
        }

        private float GetEffectiveFireCooldown()
        {
            float attackSpeed = Mathf.Max(0.01f, Stats.GetStat(StatKey.FireInterval));
            return fireCooldown / attackSpeed;
        }

        public void SpawnNetworkBullet(Vector3 position, Quaternion rotation, ulong ownerClientId)
        {
            if (!IsNetworkedMultiplayer()) return;
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;
            if (bulletPrefab == null) return;

            GameObject bulletObject = Instantiate(bulletPrefab, position, rotation);

            NetworkObject bulletNetworkObject = bulletObject.GetComponent<NetworkObject>();
            if (bulletNetworkObject == null)
            {
                Debug.LogError("NetworkObject not found on bullet prefab");
                Destroy(bulletObject);
                return;
            }

            bulletNetworkObject.SpawnWithOwnership(ownerClientId, true);

            PlayerBulletEntity bullet = bulletObject.GetComponent<PlayerBulletEntity>();
            if (bullet != null)
            {
                bullet.SetupNetworkOwner(ownerClientId);
            }
        }

        private void TryActivateAirMode()
        {
            if (isAirMode) return;

            int stockToUse = CurrentGaugeStock;
            if (stockToUse <= 0)
            {
                RegisterNoGaugeFeedback();
                return;
            }

            float consumeValue = stockToUse * gaugePerStock;
            if (IsNetworkedMultiplayer() && m_networkController != null)
            {
                if (!m_networkController.TryConsumeAirGaugeForMode(
                        stockToUse))
                {
                    RegisterNoGaugeFeedback();
                    return;
                }
            }
            else
            {
                currentAirGauge = Mathf.Clamp(
                    currentAirGauge - consumeValue,
                    0f,
                    MaxAirGauge);
            }

            isAirMode = true;
            m_currentAirModeStock = stockToUse;
            currentAirModeTime = GetAirModeDuration(stockToUse);
            m_currentActivatedAirModeMaxTime = currentAirModeTime;

            SelectAirModeModel(stockToUse);
            SetAirModeVisual(true);
            global::AudioManager.Instance?.PlayCueAtPosition(
                global::AudioCue.AirModeEnter,
                transform.position);
        }

        public float GetAirModeDuration(int stock)
        {
            return airTimePerStock * Mathf.Max(1, stock) + 5f;
        }

        //ダメージ反映
        public void Damage(float damage)
        {
            ApplyDamage(damage, null, false);
        }

        public void Damage(float damage, Vector3 damageSourcePosition)
        {
            ApplyDamage(damage, damageSourcePosition, false);
        }

        private void ApplyDamage(
            float damage,
            Vector3? damageSourcePosition,
            bool isRapidHit)
        {
            if (damage <= 0f || m_isRespawning || IsRespawnProtected) return;
            if (IsNetworkedMultiplayer() &&
                (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer))
            {
                if (m_networkObject != null &&
                    m_networkObject.IsOwner &&
                    m_networkController != null)
                {
                    Vector3 hitPosition =
                        damageSourcePosition ?? transform.position;
                    m_networkController.TryRequestEnvironmentDamage(
                        damage,
                        hitPosition);
                }

                return;
            }

            float finalDamage = damage * Stats.GetStat(StatKey.DamageTaken);
            bool absorbedByAirMode = isAirMode;
            m_damageTaken += finalDamage;

            if (absorbedByAirMode)
                ApplyAirModeDamagePenalty(finalDamage, isRapidHit);
            else
                currentHealth -= finalDamage;

            PlayDamageCameraFeedback();
            if (damageSourcePosition.HasValue)
                PlayDamageDirectionFeedback(damageSourcePosition.Value);

            if (!absorbedByAirMode &&
                currentHealth > 0f &&
                groundHealth > 0f &&
                currentHealth / groundHealth <= 0.3f)
            {
                global::AudioManager.Instance?.PlayHpLow();
            }

            if (!absorbedByAirMode && currentHealth <= 0f)
            {
                BeginRespawn();
            }

            if (IsNetworkedMultiplayer() && m_networkController != null)
            {
                m_networkController.SyncHealthFromServer(currentHealth);
                m_networkController.SyncRespawnStateFromServer(
                    m_isRespawning,
                    m_respawnRemaining);
            }
        }

        public void Damage(float damage, EnemyCPUEntity attacker)
        {
            bool wasAlive = !m_isRespawning && currentHealth > 0f;

            if (attacker != null)
                ApplyDamage(damage, attacker.transform.position, false);
            else
                ApplyDamage(damage, null, false);

            if (wasAlive && currentHealth <= 0f && attacker != null)
            {
                attacker.AddScore(defeatPointValue);
                attacker.RegisterDefeat();
            }
        }

        public void Damage(float damage, PlayerEntity attacker)
        {
            if (attacker == this)
                return;

            bool wasAlive = !m_isRespawning && currentHealth > 0f;

            if (attacker != null)
                ApplyDamage(damage, attacker.transform.position, false);
            else
                ApplyDamage(damage, null, false);

            if (wasAlive && currentHealth <= 0f && attacker != null)
            {
                attacker.AddScore(defeatPointValue);
                attacker.RegisterDefeat();
            }
        }

        public void DamageRapidHit(float damage, EnemyCPUEntity attacker)
        {
            bool wasAlive = !m_isRespawning && currentHealth > 0f;

            if (attacker != null)
                ApplyDamage(damage, attacker.transform.position, true);
            else
                ApplyDamage(damage, null, true);

            if (wasAlive && currentHealth <= 0f && attacker != null)
            {
                attacker.AddScore(defeatPointValue);
                attacker.RegisterDefeat();
            }
        }

        public void DamageRapidHit(float damage, PlayerEntity attacker)
        {
            if (attacker == this)
                return;

            bool wasAlive = !m_isRespawning && currentHealth > 0f;

            if (attacker != null)
                ApplyDamage(damage, attacker.transform.position, true);
            else
                ApplyDamage(damage, null, true);

            if (wasAlive && currentHealth <= 0f && attacker != null)
            {
                attacker.AddScore(defeatPointValue);
                attacker.RegisterDefeat();
            }
        }

        public bool TryRequestNetworkProjectile(
            NetworkProjectileType projectileType,
            Vector3 position,
            Quaternion rotation,
            ulong targetNetworkObjectId = ulong.MaxValue)
        {
            return IsNetworkedMultiplayer() &&
                   m_networkController != null &&
                   m_networkController.TryRequestProjectileFire(
                       projectileType,
                       position,
                       rotation,
                       targetNetworkObjectId);
        }

        public bool TryRequestRailgunFire(Vector3 position, Vector3 direction)
        {
            return IsNetworkedMultiplayer() &&
                   m_networkController != null &&
                   m_networkController.TryRequestRailgunFire(position, direction);
        }

        public void ApplyNetworkHealth(float value)
        {
            currentHealth = Mathf.Clamp(value, 0f, groundHealth);
        }

        public void ApplyNetworkRespawnState(
            bool isRespawning,
            float remaining = 0f)
        {
            bool startedRespawning = !m_isRespawning && isRespawning;
            m_isRespawning = isRespawning;
            if (startedRespawning &&
                NetworkManager.Singleton != null &&
                !NetworkManager.Singleton.IsServer)
            {
                PlayPlayerDeathCue();
            }

            if (m_isRespawning)
            {
                m_respawnRemaining = Mathf.Max(0f, remaining);
                m_moveInput = Vector2.zero;
                m_fireQueued = false;
                m_isFireHeld = false;
                m_turretManager?.ReleaseFire();
            }
            else
            {
                m_respawnRemaining = 0f;
            }

            SetRespawnVisualActive(!m_isRespawning);
        }

        public void ApplyNetworkRespawnRemaining(float remaining)
        {
            if (!m_isRespawning)
                return;

            m_respawnRemaining = Mathf.Max(0f, remaining);
        }

        public void SetRespawnPoint(Vector3 position, Quaternion rotation)
        {
            m_respawnPosition = position;
            m_respawnRotation = rotation;
            m_hasRespawnPoint = true;
        }

        public void RespawnFromNetwork(Vector3 position, Quaternion rotation)
        {
            SetRespawnPoint(position, rotation);
            RespawnAtCurrentPoint();
        }

        public void ApplyInitialNetworkSpawn(Vector3 position, Quaternion rotation)
        {
            SetRespawnPoint(position, rotation);
            m_velocity = Vector3.zero;
            m_moveInput = Vector2.zero;

            if (m_rigidbody != null)
            {
                m_rigidbody.linearVelocity = Vector3.zero;
                m_rigidbody.angularVelocity = Vector3.zero;
                m_rigidbody.position = position;
                m_rigidbody.rotation = rotation;
            }

            transform.SetPositionAndRotation(position, rotation);
        }

        private void BeginRespawn()
        {
            if (m_isRespawning) return;

            SpawnDeathExplosion();
            PlayPlayerDeathCue();
            EndRespawnProtection();
            m_deathCount++;
            currentHealth = 0f;
            m_isRespawning = true;
            m_respawnRemaining = Mathf.Max(0f, respawnDelay);
            m_velocity = Vector3.zero;
            m_moveInput = Vector2.zero;
            m_fireQueued = false;
            m_isFireHeld = false;

            if (m_rigidbody != null)
            {
                m_rigidbody.linearVelocity = Vector3.zero;
                m_rigidbody.angularVelocity = Vector3.zero;
            }

            if (m_turretManager != null)
            {
                m_turretManager.ReleaseFire();
                m_turretManager.ResetToDefault();
            }

            ApplyDeathBuffPenalty();
            ApplyDeathAirGaugePenalty();

            SetRespawnVisualActive(false);

            if (m_respawnRemaining <= 0f)
                CompleteRespawn();
        }

        private void PlayPlayerDeathCue()
        {
            global::AudioManager.Instance?.PlayCueAtPosition(
                global::AudioCue.PlayerDeath,
                transform.position);
        }

        private void SpawnDeathExplosion()
        {
            if (deathExplosionPrefab == null)
                return;

            Vector3 explosionPosition = GetActiveTargetAimPoint();
            PlayDeathExplosionAt(explosionPosition);

            if (IsNetworkedMultiplayer() &&
                m_networkController != null &&
                NetworkManager.Singleton != null &&
                NetworkManager.Singleton.IsServer)
            {
                m_networkController.BroadcastDeathExplosion(explosionPosition);
            }
        }

        public void PlayDeathExplosionAt(Vector3 position)
        {
            if (deathExplosionPrefab == null)
                return;

            RuntimeObjectPool.Spawn(
                deathExplosionPrefab,
                position,
                Quaternion.identity);
        }

        private void UpdateRespawnCountdown()
        {
            if (!m_isRespawning) return;

            SetRespawnRenderersActive(false);

            if (IsNetworkedMultiplayer() &&
                (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer))
            {
                return;
            }

            m_respawnRemaining -= Time.deltaTime;
            m_respawnRemaining = Mathf.Max(0f, m_respawnRemaining);

            if (IsNetworkedMultiplayer() && m_networkController != null)
            {
                m_networkController.SyncRespawnStateFromServer(
                    true,
                    m_respawnRemaining);
            }

            if (m_respawnRemaining > 0f) return;

            CompleteRespawn();
        }

        private void CompleteRespawn()
        {
            m_isRespawning = false;
            m_respawnRemaining = 0f;

            if (IsNetworkedMultiplayer() &&
                m_networkController != null &&
                NetworkManager.Singleton != null &&
                NetworkManager.Singleton.IsServer &&
                m_networkObject != null &&
                !m_networkObject.IsOwner)
            {
                RespawnAtCurrentPoint();
                m_networkController.RespawnOwnerClient(m_respawnPosition, m_respawnRotation);
                return;
            }

            RespawnAtCurrentPoint();
        }

        private void RespawnAtCurrentPoint()
        {
            SetAirMode(false);
            currentHealth = groundHealth;
            if (IsNetworkedMultiplayer() && m_networkController != null)
            {
                m_networkController.SyncHealthFromServer(currentHealth);
                m_networkController.SyncRespawnStateFromServer(false, 0f);
            }
            m_velocity = Vector3.zero;
            m_moveInput = Vector2.zero;
            m_fireQueued = false;
            m_isFireHeld = false;
            m_fireTimer = fireCooldown;

            if (m_turretManager != null)
            {
                m_turretManager.ReleaseFire();
                m_turretManager.ResetToDefault();
            }

            if (!m_hasRespawnPoint)
                SetRespawnPoint(transform.position, transform.rotation);

            if (m_rigidbody != null)
            {
                m_rigidbody.linearVelocity = Vector3.zero;
                m_rigidbody.angularVelocity = Vector3.zero;
                m_rigidbody.position = m_respawnPosition;
                m_rigidbody.rotation = m_respawnRotation;
            }

            transform.SetPositionAndRotation(m_respawnPosition, m_respawnRotation);

            SetRespawnVisualActive(true);
            StartRespawnProtection();
            global::AudioManager.Instance?.PlayCueAtPosition(
                global::AudioCue.Respawn,
                transform.position);
        }

        private void StartRespawnProtection()
        {
            EndRespawnProtection();

            m_respawnProtectionRemaining = Mathf.Max(0f, respawnProtectionDuration);
            if (m_respawnProtectionRemaining <= 0f)
                return;

            m_respawnRenderers = GetComponentsInChildren<Renderer>(true);
            m_protectionRendererStates = new bool[m_respawnRenderers.Length];
            for (int i = 0; i < m_respawnRenderers.Length; i++)
            {
                Renderer targetRenderer = m_respawnRenderers[i];
                m_protectionRendererStates[i] =
                    targetRenderer != null && targetRenderer.enabled;
            }

            m_respawnProtectionVisible = true;
            m_respawnProtectionBlinkTimer =
                Mathf.Max(0.02f, respawnProtectionBlinkInterval);
            ApplyRespawnProtectionVisual(true);
        }

        private void UpdateRespawnProtection()
        {
            if (m_respawnProtectionRemaining <= 0f)
                return;

            m_respawnProtectionRemaining -= Time.deltaTime;
            if (m_respawnProtectionRemaining <= 0f)
            {
                EndRespawnProtection();
                return;
            }

            m_respawnProtectionBlinkTimer -= Time.deltaTime;
            if (m_respawnProtectionBlinkTimer > 0f)
                return;

            m_respawnProtectionBlinkTimer =
                Mathf.Max(0.02f, respawnProtectionBlinkInterval);
            m_respawnProtectionVisible = !m_respawnProtectionVisible;
            ApplyRespawnProtectionVisual(m_respawnProtectionVisible);
        }

        private void CancelRespawnProtectionForAttack()
        {
            if (!cancelRespawnProtectionOnAttack || !IsRespawnProtected)
                return;

            EndRespawnProtection();

            if (IsNetworkedMultiplayer() && m_networkController != null)
                m_networkController.TryCancelRespawnProtection();
        }

        private void EndRespawnProtection()
        {
            if (m_respawnRenderers != null && m_protectionRendererStates != null)
            {
                int count = Mathf.Min(
                    m_respawnRenderers.Length,
                    m_protectionRendererStates.Length);
                for (int i = 0; i < count; i++)
                {
                    if (m_respawnRenderers[i] != null)
                        m_respawnRenderers[i].enabled = m_protectionRendererStates[i];
                }
            }

            m_respawnProtectionRemaining = 0f;
            m_respawnProtectionBlinkTimer = 0f;
            m_respawnProtectionVisible = true;
            m_protectionRendererStates = null;

            if (m_trackAnimationController != null && !m_isRespawning)
                m_trackAnimationController.SetTrackRenderersActive(true);
        }

        private void ApplyRespawnProtectionVisual(bool visible)
        {
            if (m_respawnRenderers == null || m_protectionRendererStates == null)
                return;

            int count = Mathf.Min(
                m_respawnRenderers.Length,
                m_protectionRendererStates.Length);
            for (int i = 0; i < count; i++)
            {
                if (m_respawnRenderers[i] != null)
                {
                    m_respawnRenderers[i].enabled =
                    m_protectionRendererStates[i] && visible;
                }
            }

            if (m_trackAnimationController != null)
                m_trackAnimationController.SetTrackRenderersActive(visible);
        }

        public void RequestCameraShake(float intensity, float duration)
        {
            if (BattlePauseUIController.IsPaused)
                return;

            if (m_tpsCameraEntity == null)
                m_tpsCameraEntity = GetComponent<TPSCameraEntity>();

            if (m_tpsCameraEntity != null)
                m_tpsCameraEntity.RequestScreenShake(intensity, duration);
        }

        public void CancelRespawnProtectionFromNetwork()
        {
            EndRespawnProtection();
        }

        private void PlayDamageCameraFeedback()
        {
            if (IsNetworkedMultiplayer() &&
                m_networkController != null &&
                NetworkManager.Singleton != null &&
                NetworkManager.Singleton.IsServer &&
                m_networkObject != null &&
                !m_networkObject.IsOwner)
            {
                m_networkController.PlayDamageFeedbackOwnerClient(
                    damageShakeIntensity,
                    damageShakeDuration);
                return;
            }

            RequestCameraShake(damageShakeIntensity, damageShakeDuration);
        }

        public void NotifyWeaponFired(float shakeIntensity, float shakeDuration)
        {
            CancelRespawnProtectionForAttack();
            RequestCameraShake(shakeIntensity, shakeDuration);
        }

        public void NotifyHitConfirmed(bool defeated, bool countsForPriorityDefeatFeedback = false)
        {
            if (IsNetworkedMultiplayer() &&
                m_networkController != null &&
                NetworkManager.Singleton != null &&
                NetworkManager.Singleton.IsServer &&
                m_networkObject != null &&
                !m_networkObject.IsOwner)
            {
                m_networkController.PlayHitMarkerOwnerClient(defeated, countsForPriorityDefeatFeedback);
                return;
            }

            ShowHitMarkerLocal(defeated);
            if (defeated && countsForPriorityDefeatFeedback)
                RegisterPriorityDefeatFeedback();
        }

        public void ShowHitMarkerLocal(bool defeated)
        {
            if (m_turretReticleUI == null)
                m_turretReticleUI = FindAnyObjectByType<TurretReticleUIEntity>();

            if (m_turretReticleUI != null)
                m_turretReticleUI.ShowHitMarker(defeated);
        }

        public void RegisterPriorityDefeatFeedback()
        {
            m_priorityDefeatFeedbackCount++;
        }

        private void RegisterNoGaugeFeedback()
        {
            m_noGaugeFeedbackCount++;
        }

        public void ShowDamageDirectionLocal(Vector3 damageSourcePosition)
        {
            if (m_damageDirectionUI == null)
                m_damageDirectionUI = FindAnyObjectByType<NearbyThreatIndicatorUIEntity>();

            if (m_damageDirectionUI != null)
                m_damageDirectionUI.ShowDamageDirection(damageSourcePosition);
        }

        private void PlayDamageDirectionFeedback(Vector3 damageSourcePosition)
        {
            if (IsNetworkedMultiplayer() &&
                m_networkController != null &&
                NetworkManager.Singleton != null &&
                NetworkManager.Singleton.IsServer &&
                m_networkObject != null &&
                !m_networkObject.IsOwner)
            {
                m_networkController.PlayDamageDirectionOwnerClient(damageSourcePosition);
                return;
            }

            ShowDamageDirectionLocal(damageSourcePosition);
        }

        private void SetRespawnVisualActive(bool isActive)
        {
            m_respawnRenderers = GetComponentsInChildren<Renderer>(true);
            SetRespawnRenderersActive(isActive);

            m_respawnColliders = GetComponentsInChildren<Collider>(true);

            for (int i = 0; i < m_respawnColliders.Length; i++)
            {
                if (m_respawnColliders[i] != null)
                    m_respawnColliders[i].enabled = isActive;
            }

            if (m_sourceMovementCollider != null && m_sourceMovementCollider != m_movementCollider)
                m_sourceMovementCollider.enabled = false;

            if (m_movementCollider != null)
                m_movementCollider.enabled = isActive;
        }

        private void SetRespawnRenderersActive(bool isActive)
        {
            if (m_respawnRenderers == null || m_respawnRenderers.Length == 0)
                m_respawnRenderers = GetComponentsInChildren<Renderer>(true);

            for (int i = 0; i < m_respawnRenderers.Length; i++)
            {
                if (m_respawnRenderers[i] != null)
                    m_respawnRenderers[i].enabled = isActive;
            }

            if (m_trackAnimationController != null)
                m_trackAnimationController.SetTrackRenderersActive(isActive);
        }

        private bool IsColliderUnderAirModel(Collider hitCollider)
        {
            if (airModeModelRoots == null)
                return false;

            for (int i = 0; i < airModeModelRoots.Length; i++)
            {
                Transform airModelRoot = airModeModelRoots[i];
                if (airModelRoot == null)
                    continue;

                if (hitCollider.transform == airModelRoot ||
                    hitCollider.transform.IsChildOf(airModelRoot))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryGetColliderBounds(Collider[] colliders, out Bounds bounds)
        {
            bounds = default;
            bool hasBounds = false;

            if (colliders == null)
                return false;

            for (int i = 0; i < colliders.Length; i++)
            {
                Collider targetCollider = colliders[i];
                if (targetCollider == null ||
                    !targetCollider.enabled ||
                    !targetCollider.gameObject.activeInHierarchy)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = targetCollider.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(targetCollider.bounds);
                }
            }

            return hasBounds;
        }

        private void ApplyDeathBuffPenalty()
        {
            if (m_buffManager == null)
                m_buffManager = GetComponent<BuffManagerEntity>();

            if (m_buffManager == null)
                return;

            int totalBuffCount = m_buffManager.GetTotalStackCount();
            if (totalBuffCount <= 0 || deathBuffLossRate <= 0f)
                return;

            int removeCount = Mathf.Max(1, Mathf.CeilToInt(totalBuffCount * deathBuffLossRate));
            m_buffManager.RemoveRandomStacks(removeCount);

            if (IsNetworkedMultiplayer() && m_networkController != null)
                m_networkController.SyncBuffStateFromServer();
        }

        private void ApplyDeathAirGaugePenalty()
        {
            if (currentAirGauge <= 0f || deathAirGaugeLossRate <= 0f)
                return;

            float lossValue = currentAirGauge * deathAirGaugeLossRate;
            currentAirGauge = Mathf.Clamp(currentAirGauge - lossValue, 0f, MaxAirGauge);

            if (IsNetworkedMultiplayer() && m_networkController != null)
                m_networkController.TrySetAirGauge(currentAirGauge);
        }

        private void ApplyAirModeDamagePenalty(float finalDamage, bool isRapidHit)
        {
            if (!isAirMode) return;
            if (finalDamage <= 0f || airModeDamagePenaltyDivisor <= 0f) return;

            float defaultPenalty = airModeDamagePenaltySeconds > 0f
                ? airModeDamagePenaltySeconds
                : m_currentActivatedAirModeMaxTime / airModeDamagePenaltyDivisor;
            float penalty = isRapidHit || finalDamage <= airModeRapidHitDamageThreshold
                ? airModeRapidHitDamagePenaltySeconds
                : defaultPenalty;

            currentAirModeTime = Mathf.Max(
                0f,
                currentAirModeTime - penalty
            );

            if (IsNetworkedMultiplayer() &&
                m_networkController != null &&
                NetworkManager.Singleton != null &&
                NetworkManager.Singleton.IsServer)
            {
                m_networkController.SyncAirModeRemainingFromServer(
                    currentAirModeTime);
            }

            if (currentAirModeTime <= 0f)
                SetAirMode(false);
        }

        public void AddScore(int value)
        {
            if (IsNetworkedMultiplayer() && m_networkController != null)
            {
                if (m_networkController.TryAddScore(value))
                {
                    global::AudioManager.Instance?.PlayPointGain();
                    return;
                }
            }

            m_score += value;
            global::AudioManager.Instance?.PlayPointGain();
        }

        public void RegisterDefeat()
        {
            m_defeatedEnemies++;
        }

        public void RecoverHealth(float value)
        {
            if (value <= 0f || m_isRespawning || currentHealth <= 0f)
                return;

            currentHealth = Mathf.Min(groundHealth, currentHealth + value);

            if (IsNetworkedMultiplayer() && m_networkController != null)
                m_networkController.SyncHealthFromServer(currentHealth);
        }

        public void AddAirGauge(float value)
        {
            int previousStock = CurrentGaugeStock;
            currentAirGauge = Mathf.Clamp(currentAirGauge + value, 0f, MaxAirGauge);
            int currentStock = CurrentGaugeStock;

            if (value > 0f && currentStock > previousStock)
            {
                global::AudioManager.Instance?.PlayCue(
                    currentStock >= maxGaugeStock
                        ? global::AudioCue.GaugeMax
                        : global::AudioCue.GaugeProgress);
            }

            if (IsNetworkedMultiplayer() && m_networkController != null)
            {
                m_networkController.TrySetAirGauge(currentAirGauge);
            }
        }

        public void SetAirMode(bool value)
        {
            if (!value)
            {
                bool wasAirMode = isAirMode;
                currentAirModeTime = 0f;
                m_currentActivatedAirModeMaxTime = 0f;
                m_currentAirModeStock = 0;

                SetAirModeVisual(false);
                isAirMode = false;
                if (wasAirMode)
                {
                    global::AudioManager.Instance?.PlayCueAtPosition(
                        global::AudioCue.AirModeExit,
                        transform.position);
                }
                return;
            }

            TryActivateAirMode();
        }

        private Transform FindChildRecursive(Transform parent, string childName)
        {
            if (parent == null) return null;

            foreach (Transform child in parent)
            {
                if (child.name == childName)
                    return child;

                Transform result = FindChildRecursive(child, childName);
                if (result != null)
                    return result;
            }

            return null;
        }

        /// <summary>
        /// 空中ガトリング用のマズルを取得する
        /// 1ゲージのプロペラ機と3ゲージの戦闘機で使用する
        /// </summary>
        /// <param name="muzzleTransforms">取得できたマズルTransform</param>
        /// <returns>空中ガトリング用マズルを取得できた場合はtrue</returns>
        public bool TryGetAirGatlingMuzzles(out Transform[] muzzleTransforms)
        {
            muzzleTransforms = null;

            if (!isAirMode) return false;
            if (m_currentAirModeStock != 1 && m_currentAirModeStock != 3) return false;
            if (m_currentAirModeModelRoot == null) return false;

            if (m_currentAirModeStock == 3)
            {
                Transform leftFighterMuzzle = FindChildRecursive(
                    m_currentAirModeModelRoot,
                    "G_Muzzle_L");
                Transform rightFighterMuzzle = FindChildRecursive(
                    m_currentAirModeModelRoot,
                    "G_Muzzle_R");

                if (leftFighterMuzzle == null || rightFighterMuzzle == null)
                    return false;

                muzzleTransforms = new Transform[]
                {
                    leftFighterMuzzle,
                    rightFighterMuzzle
                };
                return true;
            }

            Transform leftMuzzle = FindChildRecursive(m_currentAirModeModelRoot, "Muzzle_L");
            Transform rightMuzzle = FindChildRecursive(m_currentAirModeModelRoot, "Muzzle_R");

            if (leftMuzzle == null || rightMuzzle == null)
                return false;

            muzzleTransforms = new Transform[] { leftMuzzle, rightMuzzle };
            return true;
        }

        /// <summary>
        /// 空中形態で使うミサイル用の左右マズルを取得する
        /// ヘリコプターモデル内の MissileMuzzle_L / MissileMuzzle_R を優先し、未設定なら Muzzle_L / Muzzle_R を使用する
        /// </summary>
        /// <param name="muzzleTransforms">取得できた左右のミサイル発射位置</param>
        /// <returns>空中形態用のミサイル発射位置を取得できた場合はtrue</returns>
        public bool TryGetAirMissileMuzzles(out Transform[] muzzleTransforms)
        {
            muzzleTransforms = null;

            if (!isAirMode) return false;
            if (m_currentAirModeStock != 2 && m_currentAirModeStock != 3) return false;
            if (m_currentAirModeModelRoot == null) return false;

            Transform leftMuzzle = null;
            Transform rightMuzzle = null;

            if (m_currentAirModeStock == 3)
            {
                leftMuzzle = FindChildRecursive(
                    m_currentAirModeModelRoot,
                    "M_Muzzle_L");
                rightMuzzle = FindChildRecursive(
                    m_currentAirModeModelRoot,
                    "M_Muzzle_R");
            }
            else if (m_currentAirModeStock == 2)
            {
                leftMuzzle = FindChildRecursive(m_currentAirModeModelRoot, "MissileMuzzle_L");
                rightMuzzle = FindChildRecursive(m_currentAirModeModelRoot, "MissileMuzzle_R");
            }

            if (leftMuzzle == null || rightMuzzle == null)
            {
                leftMuzzle = FindChildRecursive(m_currentAirModeModelRoot, "Muzzle_L");
                rightMuzzle = FindChildRecursive(m_currentAirModeModelRoot, "Muzzle_R");
            }

            if (leftMuzzle == null || rightMuzzle == null)
                return false;

            muzzleTransforms = new Transform[] { leftMuzzle, rightMuzzle };
            return true;
        }

        public void SetLocalControl(bool canControl)
        {
            m_canControl = canControl;

            if (!m_canControl)
            {
                m_moveInput = Vector2.zero;
                m_fireQueued = false;
                m_isFireHeld = false;
                m_velocity = Vector3.zero;

                if (m_rigidbody != null)
                {
                    if (!m_rigidbody.isKinematic)
                    {
                        m_rigidbody.linearVelocity = Vector3.zero;
                        m_rigidbody.angularVelocity = Vector3.zero;
                    }
                }

                if (m_turretManager != null)
                    m_turretManager.ReleaseFire();
            }
        }

        public void SetCameraTransform(Transform cam)
        {
            cameraTransform = cam;
        }

        public void SetNetworkController(PlayerNetworkController controller)
        {
            m_networkController = controller;
        }

        public bool RefreshFireHeldState()
        {
            bool isPressed = m_isFireHeld;

            if (m_playerInput != null && m_playerInput.actions != null)
            {
                InputAction fireAction = m_playerInput.actions.FindAction("Fire", false);
                if (fireAction != null)
                {
                    isPressed = fireAction.IsPressed();
                }
            }

            m_isFireHeld = isPressed;

            if (!m_isFireHeld)
                m_fireQueued = false;

            return m_isFireHeld;
        }

        public void ApplyNetworkScore(int score)
        {
            m_score = score;
        }

        public void ApplyNetworkAirGauge(float airGauge)
        {
            currentAirGauge = Mathf.Clamp(airGauge, 0f, MaxAirGauge);
        }

        public void ApplyNetworkAirModeState(int stock, float remaining)
        {
            int normalizedStock = Mathf.Clamp(stock, 0, 3);
            if (normalizedStock <= 0 || remaining <= 0f)
            {
                if (isAirMode || m_currentAirModeModelRoot != null)
                {
                    SetAirModeVisual(false, CanUpdateLocalPlayer());
                    isAirMode = false;
                }

                m_currentAirModeStock = 0;
                currentAirModeTime = 0f;
                m_currentActivatedAirModeMaxTime = 0f;
                return;
            }

            bool requiresVisualRefresh =
                !isAirMode ||
                m_currentAirModeStock != normalizedStock ||
                m_currentAirModeModelRoot == null;

            isAirMode = true;
            m_currentAirModeStock = normalizedStock;
            m_currentActivatedAirModeMaxTime =
                GetAirModeDuration(normalizedStock);
            currentAirModeTime = Mathf.Clamp(
                remaining,
                0f,
                m_currentActivatedAirModeMaxTime);

            if (requiresVisualRefresh)
            {
                SelectAirModeModel(normalizedStock);
                SetAirModeVisual(true);
            }
        }

        public void OnMove(InputValue value)
        {
            if (BattlePauseUIController.IsPaused) return;
            if (!CanUpdateLocalPlayer()) return;
            if (m_isRespawning) return;
            if (!m_canControl) return;

            m_moveInput = value.Get<Vector2>();
        }

        public void OnMove(InputAction.CallbackContext context)
        {
            if (BattlePauseUIController.IsPaused) return;
            if (!CanUpdateLocalPlayer()) return;
            if (m_isRespawning) return;
            if (!m_canControl) return;

            m_moveInput = context.ReadValue<Vector2>();
        }

        public void OnTransformMode(InputValue value)
        {
            if (BattlePauseUIController.IsPaused) return;
            if (!CanUpdateLocalPlayer()) return;
            if (m_isRespawning) return;
            if (!m_canControl) return;
            if (!value.isPressed) return;

            TryActivateAirMode();
        }

        public void OnTransformMode(InputAction.CallbackContext context)
        {
            if (BattlePauseUIController.IsPaused) return;
            if (!CanUpdateLocalPlayer()) return;
            if (m_isRespawning) return;
            if (!m_canControl) return;
            if (!context.performed) return;

            TryActivateAirMode();
        }

        public void OnFire(InputValue value)
        {
            if (!value.isPressed)
            {
                m_isFireHeld = false;
                m_fireQueued = false;

                if (m_turretManager != null)
                    m_turretManager.ReleaseFire();
            }

            if (BattlePauseUIController.IsPaused) return;
            if (!CanUpdateLocalPlayer()) return;
            if (m_isRespawning) return;
            if (!m_canControl) return;

            if (value.isPressed)
            {
                CancelRespawnProtectionForAttack();
                m_isFireHeld = true;
                m_fireQueued = true;
                if (m_turretManager != null)
                {
                    m_turretManager.QueueFire();
                }
            }
            else
            {
                // ボタンを離したときの処理
                m_isFireHeld = false;
                m_fireQueued = false;
                if (m_turretManager != null)
                    m_turretManager.ReleaseFire();
            }
        }

        public void OnFire(InputAction.CallbackContext context)
        {
            if (context.canceled)
            {
                m_isFireHeld = false;
                m_fireQueued = false;

                if (m_turretManager != null)
                    m_turretManager.ReleaseFire();
            }

            if (BattlePauseUIController.IsPaused) return;
            if (!CanUpdateLocalPlayer()) return;
            if (m_isRespawning) return;
            if (!m_canControl) return;

            if (context.performed)
            {
                CancelRespawnProtectionForAttack();
                m_isFireHeld = true;
                m_fireQueued = true;
                if (m_turretManager != null)
                    m_turretManager.QueueFire();
            }
            else if (context.canceled)
            {
                m_isFireHeld = false;
                m_fireQueued = false;
                // ボタンを離したときの処理
                if (m_turretManager != null)
                    m_turretManager.ReleaseFire();
            }
        }
    }
}
