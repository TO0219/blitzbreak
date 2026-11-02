using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using UnityEngine.EventSystems;

namespace MyGame
{
    public class EnemyCPUEntity : BaseEnemyEntity
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

        private enum CpuWeaponType
        {
            Cannon,
            Missile,
            Gatling,
            Railgun
        }

        private static readonly float[] ObstacleAvoidanceAngles =
            { 30f, 50f, 70f, 90f, 115f, 145f, 180f };
        private static readonly float[] StuckEscapeAngles =
            { 0f, 25f, -25f, 50f, -50f, 75f, -75f, 105f, -105f, 140f, -140f, 180f };
        private static readonly float[] ObstacleSideCheckAngles =
            { 35f, 60f, 90f, 120f };
        private static readonly float[] CombatRepositionAngles =
            { 0f, 35f, -35f, 70f, -70f, 105f, -105f, 180f };

        private BTNode m_root;

        [Header("Health")]
        [SerializeField] private int machineIndex;
        [SerializeField] private TankMaterialVariant[] tankMaterialVariants;
        [SerializeField] private int maxHealth = 10;
        [SerializeField] private GameObject deathExplosionPrefab;

        [Header("Defeat Rewards")]
        [SerializeField, Tooltip("このCPUを倒したプレイヤーまたはCPUが獲得するポイント")]
        private int pointValue = 100;
        [SerializeField] private float gaugeValue = 20f;
        [SerializeField] private float defeatHealAmount = 10f;
        [SerializeField] private float respawnDelay = 3f;

        [Header("Search")]
        [SerializeField] private float searchRange = 20f;
        [SerializeField] private float CPUAttackRange = 25f;
        [SerializeField] private float CPUChaseRange = 35f;
        [SerializeField, Min(0.05f)] private float cpuTargetSearchInterval = 0.15f;
        [SerializeField] private float GetItemRange = 0f;
        [SerializeField] private bool targetIsItems = false;
        [SerializeField] private bool escapeFromPlayer = false;
        [SerializeField] private float attackAimHeight = 1.4f;
        [SerializeField] private float preferredDistance = 5f;
        [SerializeField] private float distanceTolerance = 1f;
        [SerializeField] private float attackDiagonalDistanceWeight = 1f;
        [SerializeField] private float attackDiagonalStrafeWeight = 1f;
        [SerializeField] private float attackMoveDirectionSmooth = 8f;
        [SerializeField] private float attackStrafeSwitchInterval = 2f;
        [SerializeField] private float counterAttackMemoryTime = 4f;

        [Header("Weapons")]
        [SerializeField] private GameObject missilePrefab;
        [SerializeField] private GameObject gatlingBulletPrefab;
        [SerializeField] private GameObject missileModelPrefab;
        [SerializeField] private GameObject gatlingModelPrefab;
        [SerializeField] private GameObject railgunModelPrefab;
        [SerializeField] private Vector3 missileModelLocalOffset = new Vector3(0f, 0.5f, 0f);
        [SerializeField] private Vector3 gatlingModelLocalOffset = new Vector3(0f, 0.5f, 0f);
        [SerializeField] private Vector3 railgunModelLocalOffset = new Vector3(0f, 0.5f, 0f);
        [SerializeField] private Vector3 specialWeaponModelLocalScale = new Vector3(4f, 4f, 4f);
        [SerializeField] private Vector3 railgunModelLocalScale = Vector3.one;
        [SerializeField] private GameObject specialWeaponMuzzleFlashPrefab;
        [SerializeField] private GameObject muzzleFlashPrefab;
        [SerializeField] private GameObject cannonSmokePrefab;
        [SerializeField] private GameObject missileLaunchSmokePrefab;
        [SerializeField, Min(0f)] private float cannonMuzzleEffectForwardOffset = 0.6f;
        [SerializeField, Min(0.05f)] private float missileFireCooldown = 1.2f;
        [SerializeField, Min(0.02f)] private float gatlingFireCooldown = 0.12f;
        [SerializeField, Min(0.1f)] private float railgunFireCooldown = 1.5f;
        [SerializeField, Min(1f)] private float railgunDamage = 150f;
        [SerializeField, Min(1f)] private float railgunRange = 220f;
        [SerializeField, Min(0.1f)] private float specialWeaponDuration = 15f;

        [Header("Ground Move Effect")]
        [SerializeField] private GameObject groundMoveSmokePrefab;
        [SerializeField] private float groundMoveSmokeStartDelay = 0.18f;
        [SerializeField] private float groundMoveSmokeInterval = 0.08f;
        [SerializeField] private Vector3 groundMoveSmokeOffset = new Vector3(0.4f, 0.05f, -0.65f);
        [SerializeField] private float groundMoveSmokeTrackHalfWidth = 1.25f;
        [SerializeField] private bool scaleGroundMoveSmokeByMachineSize = true;
        [SerializeField, Min(0f)] private float groundMoveSmokeMaxVisibleDistance = 45f;

        [Header("Combat AI")]
        [SerializeField, Min(0f)] private float targetLockDuration = 1.25f;
        [SerializeField, Min(0f)] private float targetRetentionBonus = 2.5f;
        [SerializeField, Min(0f)] private float weakTargetPriority = 4f;
        [SerializeField, Min(0f)] private float playerTargetPriorityBonus = 8f;
        [SerializeField, Min(0f)] private float cpuTargetPriorityBonus = 2f;
        [SerializeField, Min(0f)] private float itemOpportunityDistance = 3.5f;
        [SerializeField, Min(0f)] private float itemSafeCombatDistance = 10f;
        [SerializeField, Min(0.1f)] private float itemProgressTimeout = 5f;
        [SerializeField, Min(0.1f)] private float itemProgressDistance = 1f;
        [SerializeField, Min(0.1f)] private float unreachableItemCooldown = 12f;
        [SerializeField, Min(1)] private int maxItemStuckRecoveryAttempts = 2;
        [SerializeField, Min(0.1f)] private float playerCombatCommitDistance = 8f;
        [SerializeField, Min(0f)] private float centralAdvanceWeight = 2f;
        [SerializeField, Min(0f)] private float centralRouteTargetPenalty = 4f;
        [SerializeField, Range(0f, 1f)] private float retreatHealthRatio = 0.3f;
        [SerializeField, Min(0f)] private float retreatDistance = 7f;
        [SerializeField, Min(0f)] private float maxAimLeadTime = 0.7f;
        [SerializeField, Min(0.05f)] private float repositionDecisionInterval = 0.25f;
        [SerializeField, Min(0.5f)] private float repositionProbeDistance = 3f;
        [SerializeField, Min(0.05f)] private float damageReactionCooldown = 0.45f;

        [Header("Obstacle")]
        [SerializeField] private LayerMask obstacleMask = ~0;
        [SerializeField] private float obstacleCheckDistance = 5f;
        [SerializeField] private float obstacleCheckRadius = 0.75f;
        [SerializeField] private float obstacleCheckHeight = 0.75f;
        [SerializeField] private float obstacleSideProbeOffset = 0.65f;
        [SerializeField] private float obstacleMinimumClearance = 1.1f;
        [SerializeField] private float obstacleAvoidanceHoldTime = 1.2f;
        [SerializeField] private float stuckCheckInterval = 0.6f;
        [SerializeField] private float stuckMoveThreshold = 0.15f;
        [SerializeField] private float stuckRecoveryCooldown = 0.8f;
        [SerializeField] private float stuckEscapeDuration = 3.5f;
        [SerializeField] private float stuckEscapeSideWeight = 0.9f;
        [SerializeField] private float stuckEscapeCheckDistance = 7f;
        [SerializeField] private float stuckBreadcrumbSpacing = 1.5f;
        [SerializeField] private float stuckBacktrackDistance = 9f;
        [SerializeField] private float stuckBreadcrumbArrivalDistance = 0.8f;
        [SerializeField] private int maxStuckBreadcrumbs = 32;
        [SerializeField] private float junctionDirectionHoldTime = 4f;
        [SerializeField] private bool debugStuckRecovery = false;

        [Header("Player Separation")]
        [SerializeField] private bool ignorePlayerPhysicsCollision = true;
        [SerializeField] private float playerSeparationDistance = 2.6f;
        [SerializeField] private float playerSeparationSpeed = 4f;
        [SerializeField] private float maxBuffedMoveSpeed = 8f;
        [SerializeField] private float maxPhysicsDepenetrationSpeed = 6f;

        [Header("Body")]
        [SerializeField] private Transform bodyTransform;
        [SerializeField] private Transform turretTransform;
        [SerializeField] private Transform barrelPitchTransform;
        [SerializeField] private float turretRotateSpeedMultiplier = 0.8f;
        [SerializeField] private float turretElevationAngle = 35f;
        [SerializeField] private float turretDepressionAngle = 10f;
        [SerializeField] private float idleTurretSweepAngle = 25f;
        [SerializeField] private float idleTurretSweepSpeed = 1.2f;
        [SerializeField] private float cpuRotateSpeedMultiplier = 0.5f;

        [Header("Wander")]
        [SerializeField] private int minWanderMoveCount = 2;
        [SerializeField] private int maxWanderMoveCount = 4;
        [SerializeField] private float landmarkApproachDistance = 18f;
        [SerializeField] private float landmarkArrivalDistance = 3f;
        [SerializeField] private float landmarkWanderInnerRadius = 14f;
        [SerializeField] private float landmarkWanderOuterRadius = 30f;

        [Header("UI")]
        [SerializeField] private EnemyCPUHealthUIEntity m_healthUI;

        private readonly Dictionary<int, int> buffList = new Dictionary<int, int>();

        protected Transform m_cpuTarget;
        private Rigidbody m_rigidbody;
        private Collider[] m_ownColliders;
        private Renderer[] m_respawnRenderers;
        private Vector3 m_respawnPosition;
        private Quaternion m_respawnRotation = Quaternion.identity;
        private float m_currentHealth;
        private float m_collisionRefreshTimer;
        private float m_cpuTargetSearchTimer;
        private bool m_isRespawning;
        private float m_respawnRemaining;
        private int m_score;
        private int m_defeatedEnemies;
        private float m_damageTaken;
        private int m_deathCount;
        private BuffManagerEntity m_buffManager;
        private float m_attackStrafeTimer;
        private int m_attackStrafeSide = 1;
        private Vector3 m_smoothedAttackMoveDirection;
        private float m_targetLockTimer;
        private Transform m_itemPursuitTarget;
        private float m_itemPursuitBestDistance;
        private float m_itemPursuitTimer;
        private int m_itemStuckRecoveryAttempts;
        private readonly Dictionary<Transform, float> m_unreachableItemUntil =
            new Dictionary<Transform, float>();
        private float m_repositionDecisionTimer;
        private float m_damageReactionTimer;
        private Vector3 m_cachedRepositionDirection;
        private int m_remainingWanderMoves;
        private Transform m_counterAttackTarget;
        private float m_counterAttackTimer;
        private Vector3 m_lastStuckCheckPosition;
        private Vector3 m_lastAttemptedMoveDirection;
        private float m_stuckCheckTimer;
        private float m_stuckRecoveryTimer;
        private float m_stuckEscapeTimer;
        private Vector3 m_stuckEscapePreferredDirection;
        private readonly List<Vector3> m_movementBreadcrumbs = new List<Vector3>();
        private bool m_isBacktracking;
        private int m_backtrackBreadcrumbIndex;
        private int m_backtrackStopIndex;
        private bool m_attemptedMoveThisFrame;
        private float m_groundMoveSmokeMoveTimer;
        private float m_groundMoveSmokeIntervalTimer;
        private int m_obstacleAvoidanceSide = 1;
        private float m_obstacleAvoidanceTimer;
        private float m_committedDirectionTimer;
        private Vector3 m_committedMoveDirection;
        private readonly RaycastHit[] m_obstacleHits = new RaycastHit[32];
        private Collider[] m_targetSearchHits = new Collider[128];
        private readonly Collider[] m_playerSeparationHits = new Collider[16];
        private readonly RaycastHit[] m_lineOfSightHits = new RaycastHit[32];
        private readonly List<PlayerEntity> m_cachedPlayers = new List<PlayerEntity>(4);
        private readonly Dictionary<PlayerEntity, Collider[]> m_playerColliderCache =
            new Dictionary<PlayerEntity, Collider[]>();
        private readonly HashSet<ColliderPair> m_ignoredCollisionPairs =
            new HashSet<ColliderPair>();
        private readonly List<PlayerEntity> m_staleColliderPlayers =
            new List<PlayerEntity>(4);
        private readonly HashSet<PlayerEntity> m_separatedPlayers = new HashSet<PlayerEntity>();
        private readonly HashSet<Transform> m_targetCandidates =
            new HashSet<Transform>();
        private bool m_hasBaseMachineValues;
        private int m_baseMaxHealth;
        private Vector3 m_baseScale;
        private Transform m_centralLandmark;
        private Vector3 m_landmarkApproachPosition;
        private bool m_hasReachedLandmark;
        private CpuWeaponType m_currentWeapon = CpuWeaponType.Cannon;
        private float m_specialWeaponRemaining;
        private GameObject m_specialWeaponModel;
        private Renderer[] m_defaultTurretRenderers;
        private Transform[] m_specialWeaponMuzzles;
        private int m_specialWeaponMuzzleIndex;
        private NetworkCpuController m_networkController;
        private Transform m_specialWeaponModelPitchTransform;
        private Quaternion m_barrelPitchBaseLocalRotation = Quaternion.identity;
        private bool m_hasBarrelPitchBaseLocalRotation;
        private float m_barrelPitch;
        private float m_lastSyncedBarrelPitch = float.NaN;

        private Transform turretDirection;
        private Transform bodyDirection;

        private Vector3 moveDirection;

        public Transform CPUTarget => m_cpuTarget;
        public float CPU_AttackRange => CPUAttackRange * Stats.GetStat(StatKey.AttackRange);
        public float CPU_ChaseRange => CPUChaseRange;
        public float CPU_GetItemRange => GetItemRange;
        public bool TargetIsItems => targetIsItems;
        public bool EscapeFromPlayer => escapeFromPlayer;
        public int Score => m_score;
        public int DefeatedEnemies => m_defeatedEnemies;
        public float DamageTaken => m_damageTaken;
        public int DeathCount => m_deathCount;
        public float CPUCurrentHealth => m_currentHealth;
        public float CPUMaxHealth => maxHealth;
        public bool IsRespawning => m_isRespawning;
        public int MachineIndex => machineIndex;
        public MachineProfile MachineProfile => MachineProfileCatalog.GetProfile(machineIndex);
        public int CurrentWeaponType => (int)m_currentWeapon;
        public float SpecialWeaponRemaining => m_specialWeaponRemaining;
        public float BarrelPitch => m_barrelPitch;
        public Quaternion NetworkBodyRotation =>
            bodyTransform != null ? bodyTransform.rotation : transform.rotation;
        public Quaternion NetworkTurretRotation =>
            turretTransform != null ? turretTransform.rotation : transform.rotation;

        private void Awake()
        {
            CachePhysicsComponents();
            ConfigureCpuPhysics();
            m_buffManager = GetComponent<BuffManagerEntity>();
            SetRespawnPoint(transform.position, transform.rotation);
            m_score = 0;
            ResetStuckTracking();
            CacheDefaultTurretRenderers();

            Stats.SetStat(StatKey.AttackPower, 1f);
            Stats.SetStat(StatKey.AttackRange, 1f);
            Stats.SetStat(StatKey.FireInterval, 1f);
            Stats.SetStat(StatKey.DamageTaken, 1f);
            Stats.SetStat(StatKey.MoveSpeed, 1f);

            ApplyMachineIndex(machineIndex);
        }

        protected virtual void Start()
        {
            BeginWanderMoveBurst();

            m_root = new SelectorNode(
                new List<BTNode>()
                {
                    new AttackNode(this),
                    new GetItemNode(this),
                    new WanderNode(this)
                }
            );
        }

        public override void InitEntity()
        {
            if (m_healthUI != null)
                m_healthUI.SetHealth(maxHealth, maxHealth);
        }

        protected virtual void Update()
        {
            if (BattlePauseUIController.IsPaused && Time.timeScale <= 0f)
                return;
            if (m_networkController != null &&
                m_networkController.IsSpawned &&
                !m_networkController.IsServer)
            {
                return;
            }

            RefreshPlayerCollisionIgnores();
            UpdateRespawnCountdown();
            UpdateCounterAttackTarget();
            UpdateStuckRecoveryTimer();
            m_targetLockTimer = Mathf.Max(0f, m_targetLockTimer - Time.deltaTime);
            m_repositionDecisionTimer = Mathf.Max(
                0f,
                m_repositionDecisionTimer - Time.deltaTime);
            m_damageReactionTimer = Mathf.Max(
                0f,
                m_damageReactionTimer - Time.deltaTime);

            if (m_isRespawning || !CanRunEnemyLogic())
            {
                StopHorizontalVelocity();
                return;
            }

            UpdateSpecialWeapon();
            SearchTarget();
            UpdateItemPursuit();

            m_fireTimer += Time.deltaTime;

            StopHorizontalVelocity();
            m_attemptedMoveThisFrame = false;

            if (m_root != null)
                m_root.Evaluate();

            UpdateGroundMoveSmoke(Time.deltaTime);
            PreventPlayerOverlap();
            RecordMovementBreadcrumb();
            MonitorStuckMovement();
        }

        private void LateUpdate()
        {
            ClampHorizontalVelocity();
        }

        protected override void Chase(Vector3 direction)
        {
            if (m_cpuTarget == null)
                return;

            MoveHorizontal(direction);
            RotateTurretToTarget();
        }

        protected override void SearchTarget()
        {
            m_cpuTargetSearchTimer -= Time.deltaTime;
            if (m_cpuTargetSearchTimer > 0f)
                return;

            m_cpuTargetSearchTimer = Mathf.Max(0.05f, cpuTargetSearchInterval);

            Transform previousTarget = m_cpuTarget;
            if (IsValidCounterAttackTarget())
            {
                SetCpuTarget(m_counterAttackTarget);
                return;
            }

            Transform bestCombatTarget = null;
            float bestCombatScore = Mathf.Infinity;
            float bestCombatDistance = Mathf.Infinity;
            Transform bestItemTarget = null;
            float bestItemDistance = Mathf.Infinity;
            float overlapRange = Mathf.Max(searchRange, CPUChaseRange);

            int hitCount = CollectTargetSearchHits(overlapRange);

            m_targetCandidates.Clear();
            for (int i = 0; i < hitCount; i++)
            {
                Collider col = m_targetSearchHits[i];
                if (col == null)
                    continue;

                if (col.transform.root == transform.root)
                    continue;

                BuffItemEntity item = col.GetComponentInParent<BuffItemEntity>();
                ItemEntity weaponItem = col.GetComponentInParent<ItemEntity>();
                EnemyEntity enemy = col.GetComponentInParent<EnemyEntity>();
                EnemyCPUEntity cpu = col.GetComponentInParent<EnemyCPUEntity>();
                PlayerEntity player = col.GetComponentInParent<PlayerEntity>();

                Transform candidate =
                    GetValidCandidateTransform(
                        item,
                        weaponItem,
                        enemy,
                        cpu,
                        player);
                if (candidate == null ||
                    !m_targetCandidates.Add(candidate))
                    continue;

                float distance =
                    GetHorizontalDistance(transform.position, candidate.position);
                bool isItem = item != null || weaponItem != null;
                float candidateRange = isItem ? searchRange : CPUChaseRange;
                if (distance > candidateRange)
                    continue;

                if (isItem)
                {
                    if (IsItemTemporarilyUnreachable(candidate))
                        continue;

                    if (distance < bestItemDistance)
                    {
                        bestItemDistance = distance;
                        bestItemTarget = candidate;
                    }

                    continue;
                }

                float score =
                    GetCombatTargetScore(candidate, distance, previousTarget);
                if (score < bestCombatScore)
                {
                    bestCombatScore = score;
                    bestCombatDistance = distance;
                    bestCombatTarget = candidate;
                }
            }

            if (IsValidTarget(previousTarget) &&
                m_targetLockTimer > 0f &&
                GetHorizontalDistance(transform.position, previousTarget.position) <=
                CPUChaseRange &&
                !IsItemTarget(previousTarget))
            {
                SetCpuTarget(previousTarget);
                return;
            }

            bool shouldTakeItem =
                bestItemTarget != null &&
                (bestCombatTarget == null ||
                 bestItemDistance <= itemOpportunityDistance ||
                 bestCombatDistance >= itemSafeCombatDistance);

            SetCpuTarget(shouldTakeItem ? bestItemTarget : bestCombatTarget);
        }

        private void UpdateItemPursuit()
        {
            if (!targetIsItems || m_cpuTarget == null)
            {
                ResetItemPursuit(null);
                return;
            }

            if (m_itemPursuitTarget != m_cpuTarget)
            {
                ResetItemPursuit(m_cpuTarget);
                return;
            }

            float distance =
                GetHorizontalDistance(transform.position, m_cpuTarget.position);
            float requiredProgress = Mathf.Max(0.1f, itemProgressDistance);
            if (distance <= m_itemPursuitBestDistance - requiredProgress)
            {
                m_itemPursuitBestDistance = distance;
                m_itemPursuitTimer = 0f;
                return;
            }

            m_itemPursuitTimer += Time.deltaTime;
            if (m_itemPursuitTimer >= Mathf.Max(0.1f, itemProgressTimeout))
                MarkCurrentItemTemporarilyUnreachable();
        }

        private void ResetItemPursuit(Transform itemTarget)
        {
            m_itemPursuitTarget = itemTarget;
            m_itemPursuitBestDistance = itemTarget != null
                ? GetHorizontalDistance(transform.position, itemTarget.position)
                : Mathf.Infinity;
            m_itemPursuitTimer = 0f;
            m_itemStuckRecoveryAttempts = 0;
        }

        private bool IsItemTemporarilyUnreachable(Transform itemTarget)
        {
            if (itemTarget == null)
                return false;

            if (!m_unreachableItemUntil.TryGetValue(itemTarget, out float retryTime))
                return false;

            if (Time.time < retryTime)
                return true;

            m_unreachableItemUntil.Remove(itemTarget);
            return false;
        }

        private void MarkCurrentItemTemporarilyUnreachable()
        {
            Transform unreachableItem = m_cpuTarget;
            if (unreachableItem != null)
            {
                m_unreachableItemUntil[unreachableItem] =
                    Time.time + Mathf.Max(0.1f, unreachableItemCooldown);
            }

            SetCpuTarget(null);
            m_stuckEscapeTimer = 0f;
            m_stuckEscapePreferredDirection = Vector3.zero;
            m_isBacktracking = false;
            ClearCommittedDirection();
            m_remainingWanderMoves = Mathf.Max(1, m_remainingWanderMoves);
            PickNewWanderTarget();
            StopHorizontalVelocity();
        }

        private int CollectTargetSearchHits(float searchRadius)
        {
            while (true)
            {
                int hitCount = Physics.OverlapSphereNonAlloc(
                    transform.position,
                    searchRadius,
                    m_targetSearchHits,
                    Physics.DefaultRaycastLayers,
                    QueryTriggerInteraction.Collide);

                if (hitCount < m_targetSearchHits.Length)
                    return hitCount;

                System.Array.Resize(
                    ref m_targetSearchHits,
                    m_targetSearchHits.Length * 2);
            }
        }

        private Transform GetValidCandidateTransform(
            BuffItemEntity item,
            ItemEntity weaponItem,
            EnemyEntity enemy,
            EnemyCPUEntity cpu,
            PlayerEntity player)
        {
            if (item != null)
                return !item.IsDestroy && item.gameObject.activeInHierarchy
                    ? item.transform
                    : null;

            if (weaponItem != null)
                return weaponItem.CanPickup
                    ? weaponItem.transform
                    : null;

            if (player != null)
                return !player.IsRespawning && player.CurrentHealth > 0f
                    ? player.transform
                    : null;

            if (cpu != null)
                return cpu != this && cpu.CPUCurrentHealth > 0f
                    ? cpu.transform
                    : null;

            if (enemy != null)
                return !enemy.IsDestroy && enemy.CurrentHealth > 0f
                    ? enemy.transform
                    : null;

            return null;
        }

        private float GetCombatTargetScore(
            Transform candidate,
            float distance,
            Transform previousTarget)
        {
            float score = distance;
            if (candidate == previousTarget)
                score -= targetRetentionBonus;

            score -=
                (1f - GetTargetHealthRatio(candidate)) * weakTargetPriority;

            if (candidate.GetComponentInParent<PlayerEntity>() != null)
                score -= playerTargetPriorityBonus;
            else if (candidate.GetComponentInParent<EnemyCPUEntity>() != null)
                score -= cpuTargetPriorityBonus;

            if (TryGetCentralAdvanceDirection(out Vector3 advanceDirection))
            {
                Vector3 toCandidate =
                    GetDirection(candidate.position, transform.position);
                if (toCandidate.sqrMagnitude > 0.001f)
                {
                    float routeAlignment =
                        Vector3.Dot(
                            advanceDirection.normalized,
                            toCandidate.normalized);
                    score +=
                        (1f - Mathf.Clamp(routeAlignment, -1f, 1f)) *
                        centralRouteTargetPenalty;
                }
            }

            return score;
        }

        private float GetTargetHealthRatio(Transform target)
        {
            PlayerEntity player = target.GetComponentInParent<PlayerEntity>();
            if (player != null)
                return player.GroundHealth > 0f
                    ? Mathf.Clamp01(player.CurrentHealth / player.GroundHealth)
                    : 1f;

            EnemyCPUEntity cpu = target.GetComponentInParent<EnemyCPUEntity>();
            if (cpu != null)
                return cpu.maxHealth > 0
                    ? Mathf.Clamp01(cpu.CPUCurrentHealth / cpu.maxHealth)
                    : 1f;

            EnemyEntity enemy = target.GetComponentInParent<EnemyEntity>();
            if (enemy != null)
                return enemy.CurrentHealth <= 1f ? 0f : 1f;

            return 1f;
        }

        private bool IsValidTarget(Transform target)
        {
            if (target == null || !target.gameObject.activeInHierarchy)
                return false;

            return GetValidCandidateTransform(
                target.GetComponentInParent<BuffItemEntity>(),
                target.GetComponentInParent<ItemEntity>(),
                target.GetComponentInParent<EnemyEntity>(),
                target.GetComponentInParent<EnemyCPUEntity>(),
                target.GetComponentInParent<PlayerEntity>()) != null;
        }

        private bool IsItemTarget(Transform target)
        {
            return target != null &&
                   (target.GetComponentInParent<BuffItemEntity>() != null ||
                    target.GetComponentInParent<ItemEntity>() != null);
        }

        private void SetCpuTarget(Transform target)
        {
            if (m_cpuTarget != target)
            {
                m_cpuTarget = target;
                m_targetLockTimer = Mathf.Max(0f, targetLockDuration);
                m_smoothedAttackMoveDirection = Vector3.zero;
                m_cachedRepositionDirection = Vector3.zero;
                m_repositionDecisionTimer = 0f;
                ResetItemPursuit(IsItemTarget(target) ? target : null);
            }

            targetIsItems = IsItemTarget(m_cpuTarget);
        }

        private float GetHorizontalDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }

        public float DistanceToTarget()
        {
            if (m_cpuTarget == null)
                return Mathf.Infinity;

            Vector3 dir = GetDirection(m_cpuTarget.position, transform.position);
            return dir.magnitude;
        }

        public bool CanFire()
        {
            return CanFireAt(m_cpuTarget);
        }

        public void FireWeapon()
        {
            FireWeaponAt(m_cpuTarget);
        }

        private bool CanFireAt(Transform target)
        {
            if (target == null)
                return false;

            float distance = GetHorizontalDistance(transform.position, target.position);
            if (distance > CPU_AttackRange)
                return false;

            float attackSpeed = Mathf.Max(0.01f, Stats.GetStat(StatKey.FireInterval));
            float currentInterval = GetCurrentWeaponFireCooldown() / attackSpeed;

            return m_fireTimer >= currentInterval && HasLineOfSightToTarget(target);
        }
        private void FireWeaponAt(Transform target)
        {
            if (!HasLineOfSightToTarget(target))
                return;

            FireAt(target);
            m_fireTimer = 0f;
        }

        protected override void Fire()
        {
            FireAt(m_cpuTarget);
        }

        private void FireAt(Transform target)
        {
            if (target == null)
                return;

            switch (m_currentWeapon)
            {
                case CpuWeaponType.Missile:
                    FireMissileAt(target);
                    return;
                case CpuWeaponType.Gatling:
                    FireGatlingAt(target);
                    return;
                case CpuWeaponType.Railgun:
                    FireRailgunAt(target);
                    return;
                default:
                    FireCannonAt(target);
                    return;
            }
        }

        private void FireCannonAt(Transform target)
        {
            if (bullet == null || enemyMuzzleTransform == null)
                return;

            Vector3 aimPosition = GetTargetAimPosition(target);
            Vector3 dir = (aimPosition - enemyMuzzleTransform.position).normalized;
            Quaternion fireRotation = Quaternion.LookRotation(dir);

            bool networked = IsOnlineServer();
            GameObject bulletObj = networked
                ? Instantiate(
                    bullet,
                    enemyMuzzleTransform.position,
                    fireRotation)
                : RuntimeObjectPool.Spawn(
                    bullet,
                    enemyMuzzleTransform.position,
                    fireRotation);

            Rigidbody rb = bulletObj.GetComponent<Rigidbody>();
            if (rb == null)
            {
                if (!RuntimeObjectPool.TryRelease(bulletObj))
                    Destroy(bulletObj);
                return;
            }

            EnemyCPUBulletEntity bulletEntity =
                bulletObj.GetComponent<EnemyCPUBulletEntity>();

            if (bulletEntity != null)
            {
                float attackPower = Stats.GetStat(StatKey.AttackPower);

                bulletEntity.SetDamage(bulletEntity.Damage * attackPower);
                bulletEntity.Setup(this);

                rb.linearVelocity =
                    dir * bulletEntity.Speed;
            }

            if (networked)
            {
                NetworkObject networkObject = bulletObj.GetComponent<NetworkObject>();
                if (networkObject != null)
                    networkObject.Spawn(true);
                else
                    Destroy(bulletObj);
            }

            SpawnCannonFireEffects(enemyMuzzleTransform, fireRotation);
            PlayFireSound(global::AudioCue.EnemyGroundShot, enemyMuzzleTransform.position);
        }

        private void FireMissileAt(Transform target)
        {
            Transform muzzle = GetNextSpecialWeaponMuzzle();
            if (missilePrefab == null || muzzle == null)
                return;

            Vector3 aimPosition = GetTargetAimPosition(target);
            Vector3 direction = aimPosition - muzzle.position;
            if (direction.sqrMagnitude < 0.001f)
                direction = muzzle.forward;

            Quaternion rotation = Quaternion.LookRotation(direction.normalized);
            bool networked = IsOnlineServer();
            GameObject missileObject = networked
                ? Instantiate(missilePrefab, muzzle.position, rotation)
                : RuntimeObjectPool.Spawn(missilePrefab, muzzle.position, rotation);

            MissileBulletEntity missile =
                missileObject.GetComponent<MissileBulletEntity>();
            if (missile != null)
                missile.Setup(this, target);

            if (networked)
            {
                NetworkObject networkObject = missileObject.GetComponent<NetworkObject>();
                if (networkObject != null)
                {
                    networkObject.Spawn(true);
                    NetworkProjectileAuthority authority =
                        missileObject.GetComponent<NetworkProjectileAuthority>();
                    authority?.ConfigureTarget(GetTargetNetworkObjectId(target));
                }
                else
                    Destroy(missileObject);
            }

            SpawnSpecialWeaponMuzzleFlash(muzzle, rotation);
            SpawnMissileLaunchSmoke(muzzle, rotation);
            PlayFireSound(global::AudioCue.Missile, muzzle.position);
        }

        private void FireGatlingAt(Transform target)
        {
            Transform muzzle = GetNextSpecialWeaponMuzzle();
            if (gatlingBulletPrefab == null || muzzle == null)
                return;

            Vector3 aimPosition = GetTargetAimPosition(target);
            Vector3 direction = aimPosition - muzzle.position;
            if (direction.sqrMagnitude < 0.001f)
                direction = muzzle.forward;

            Quaternion rotation = Quaternion.LookRotation(direction.normalized);
            bool networked = IsOnlineServer();
            GameObject bulletObject = networked
                ? Instantiate(gatlingBulletPrefab, muzzle.position, rotation)
                : RuntimeObjectPool.Spawn(gatlingBulletPrefab, muzzle.position, rotation);

            GatlingBulletEntity gatlingBullet =
                bulletObject.GetComponent<GatlingBulletEntity>();
            if (gatlingBullet != null)
                gatlingBullet.Setup(this);

            if (networked)
            {
                NetworkObject networkObject = bulletObject.GetComponent<NetworkObject>();
                if (networkObject != null)
                    networkObject.Spawn(true);
                else
                    Destroy(bulletObject);
            }

            SpawnSpecialWeaponMuzzleFlash(muzzle, rotation);
            PlayFireSound(global::AudioCue.Gatling, muzzle.position, 0.55f);
        }

        private void PlayFireSound(global::AudioCue cue, Vector3 position, float volumeScale = 0.75f)
        {
            global::AudioManager.Instance?.PlayCueAtPosition(cue, position, volumeScale);
        }

        private void FireRailgunAt(Transform target)
        {
            Transform muzzle = GetNextSpecialWeaponMuzzle();
            if (muzzle == null || target == null)
                return;

            Vector3 aimPosition = GetTargetAimPosition(target);
            Vector3 direction = aimPosition - muzzle.position;
            if (direction.sqrMagnitude < 0.001f)
                direction = muzzle.forward;

            RailgunTurret railgun = new RailgunTurret(
                new[] { muzzle },
                railgunFireCooldown,
                railgunDamage,
                railgunRange,
                specialWeaponMuzzleFlashPrefab);

            if (!railgun.TryFireAuthoritative(
                    this,
                    muzzle.position,
                    direction.normalized,
                    out Vector3 endPoint))
            {
                return;
            }

            if (IsOnlineServer())
                m_networkController?.BroadcastRailgunBeam(muzzle.position, endPoint);
            else
                RailgunBeamEffect.Play(muzzle.position, endPoint);

            SpawnSpecialWeaponMuzzleFlash(muzzle, Quaternion.LookRotation(direction.normalized));
        }

        public bool TryEquipWeaponItem(string weaponId)
        {
            switch (weaponId)
            {
                case "cannon":
                    ResetSpecialWeapon();
                    return true;
                case "missile":
                    if (missilePrefab == null)
                    {
                        Debug.LogWarning("[EnemyCPUEntity] missilePrefab is not assigned.");
                        return false;
                    }

                    EquipSpecialWeapon(
                        CpuWeaponType.Missile,
                        missileModelPrefab);
                    return true;
                case "gatling":
                    if (gatlingBulletPrefab == null)
                    {
                        Debug.LogWarning("[EnemyCPUEntity] gatlingBulletPrefab is not assigned.");
                        return false;
                    }

                    EquipSpecialWeapon(
                        CpuWeaponType.Gatling,
                        gatlingModelPrefab);
                    return true;
                case "railgun":
                    EquipSpecialWeapon(
                        CpuWeaponType.Railgun,
                        railgunModelPrefab);
                    return true;
                default:
                    Debug.LogWarning($"[EnemyCPUEntity] Unknown weapon item id: {weaponId}");
                    return false;
            }
        }

        private void EquipSpecialWeapon(
            CpuWeaponType weaponType,
            GameObject modelPrefab,
            float duration = -1f,
            bool syncNetwork = true)
        {
            RemoveSpecialWeaponModel();
            SetDefaultTurretRenderersActive(false);

            m_currentWeapon = weaponType;
            m_specialWeaponRemaining = Mathf.Max(
                0.1f,
                duration >= 0f ? duration : specialWeaponDuration);
            m_specialWeaponMuzzleIndex = 0;
            m_fireTimer = GetCurrentWeaponFireCooldown();

            if (modelPrefab != null && turretTransform != null)
            {
                m_specialWeaponModel = Instantiate(modelPrefab, turretTransform);
                m_specialWeaponModel.transform.localPosition =
                    GetSpecialWeaponModelLocalOffset(weaponType);
                m_specialWeaponModel.transform.localRotation = Quaternion.identity;
                m_specialWeaponModel.transform.localScale =
                    GetSpecialWeaponModelLocalScale(weaponType);
                ApplyMachineVariantMaterials(m_specialWeaponModel);
                m_specialWeaponModelPitchTransform =
                    FindChildRecursive(m_specialWeaponModel.transform, "Barrel");
                SyncSpecialWeaponModelPitch();
            }

            m_specialWeaponMuzzles = GetSpecialWeaponMuzzles();

            if (syncNetwork)
                SyncNetworkState();
        }

        private Vector3 GetSpecialWeaponModelLocalOffset(CpuWeaponType weaponType)
        {
            switch (weaponType)
            {
                case CpuWeaponType.Missile:
                    return missileModelLocalOffset;
                case CpuWeaponType.Gatling:
                    return gatlingModelLocalOffset;
                case CpuWeaponType.Railgun:
                    return railgunModelLocalOffset;
                default:
                    return Vector3.zero;
            }
        }

        private Vector3 GetSpecialWeaponModelLocalScale(CpuWeaponType weaponType)
        {
            if (weaponType == CpuWeaponType.Railgun)
                return railgunModelLocalScale;

            return specialWeaponModelLocalScale;
        }

        private void UpdateSpecialWeapon()
        {
            if (m_currentWeapon == CpuWeaponType.Cannon)
                return;

            m_specialWeaponRemaining -= Time.deltaTime;
            if (m_specialWeaponRemaining <= 0f)
                ResetSpecialWeapon();
        }

        private void ResetSpecialWeapon(bool syncNetwork = true)
        {
            m_currentWeapon = CpuWeaponType.Cannon;
            m_specialWeaponRemaining = 0f;
            m_specialWeaponMuzzleIndex = 0;
            m_specialWeaponMuzzles = null;
            RemoveSpecialWeaponModel();
            SetDefaultTurretRenderersActive(true);
            SyncSpecialWeaponModelPitch();

            if (syncNetwork)
                SyncNetworkState();
        }

        public void ApplyNetworkWeaponState(int weaponType, float remaining)
        {
            CpuWeaponType networkWeapon =
                weaponType >= (int)CpuWeaponType.Cannon &&
                weaponType <= (int)CpuWeaponType.Railgun
                    ? (CpuWeaponType)weaponType
                    : CpuWeaponType.Cannon;

            if (networkWeapon == CpuWeaponType.Cannon || remaining <= 0f)
            {
                if (m_currentWeapon != CpuWeaponType.Cannon ||
                    m_specialWeaponModel != null)
                {
                    ResetSpecialWeapon(false);
                }

                return;
            }

            if (m_currentWeapon == networkWeapon &&
                m_specialWeaponModel != null)
            {
                m_specialWeaponRemaining = remaining;
                return;
            }

            GameObject modelPrefab = networkWeapon == CpuWeaponType.Missile
                ? missileModelPrefab
                : networkWeapon == CpuWeaponType.Gatling
                    ? gatlingModelPrefab
                    : railgunModelPrefab;
            EquipSpecialWeapon(
                networkWeapon,
                modelPrefab,
                remaining,
                false);
        }

        private float GetCurrentWeaponFireCooldown()
        {
            switch (m_currentWeapon)
            {
                case CpuWeaponType.Missile:
                    return Mathf.Max(0.05f, missileFireCooldown);
                case CpuWeaponType.Gatling:
                    return Mathf.Max(0.02f, gatlingFireCooldown);
                case CpuWeaponType.Railgun:
                    return Mathf.Max(0.1f, railgunFireCooldown);
                default:
                    return Mathf.Max(0.05f, fireInterval);
            }
        }

        private Transform GetNextSpecialWeaponMuzzle()
        {
            if (m_specialWeaponMuzzles == null ||
                m_specialWeaponMuzzles.Length == 0)
            {
                return enemyMuzzleTransform;
            }

            Transform muzzle =
                m_specialWeaponMuzzles[
                    m_specialWeaponMuzzleIndex %
                    m_specialWeaponMuzzles.Length];
            m_specialWeaponMuzzleIndex =
                (m_specialWeaponMuzzleIndex + 1) %
                m_specialWeaponMuzzles.Length;

            return muzzle != null ? muzzle : enemyMuzzleTransform;
        }

        private Transform[] GetSpecialWeaponMuzzles()
        {
            if (m_specialWeaponModel == null)
                return new[] { enemyMuzzleTransform };

            Transform leftMuzzle =
                FindChildRecursive(m_specialWeaponModel.transform, "Muzzle_L");
            Transform rightMuzzle =
                FindChildRecursive(m_specialWeaponModel.transform, "Muzzle_R");

            if (leftMuzzle != null && rightMuzzle != null)
                return new[] { leftMuzzle, rightMuzzle };

            return new[] { enemyMuzzleTransform };
        }

        private static Transform FindChildRecursive(
            Transform parent,
            string childName)
        {
            if (parent == null)
                return null;

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

        private void CacheDefaultTurretRenderers()
        {
            m_defaultTurretRenderers =
                turretTransform != null
                    ? turretTransform.GetComponentsInChildren<Renderer>(true)
                    : null;
        }

        private void SetDefaultTurretRenderersActive(bool active)
        {
            if (m_defaultTurretRenderers == null)
                CacheDefaultTurretRenderers();

            if (m_defaultTurretRenderers == null)
                return;

            for (int i = 0; i < m_defaultTurretRenderers.Length; i++)
            {
                Renderer targetRenderer = m_defaultTurretRenderers[i];
                if (targetRenderer != null)
                    targetRenderer.enabled = active;
            }
        }

        private void RemoveSpecialWeaponModel()
        {
            if (m_specialWeaponModel == null)
            {
                m_specialWeaponModelPitchTransform = null;
                return;
            }

            Destroy(m_specialWeaponModel);
            m_specialWeaponModel = null;
            m_specialWeaponModelPitchTransform = null;
        }

        private void SpawnSpecialWeaponMuzzleFlash(
            Transform muzzle,
            Quaternion rotation)
        {
            if (specialWeaponMuzzleFlashPrefab == null || muzzle == null)
                return;

            RuntimeObjectPool.Spawn(
                specialWeaponMuzzleFlashPrefab,
                muzzle.position,
                rotation);
        }

        private void SpawnMissileLaunchSmoke(Transform muzzle, Quaternion rotation)
        {
            if (missileLaunchSmokePrefab == null || muzzle == null)
                return;

            RuntimeObjectPool.Spawn(
                missileLaunchSmokePrefab,
                muzzle.position,
                rotation);
        }

        private void SpawnCannonFireEffects(Transform muzzle, Quaternion rotation)
        {
            if (muzzle == null)
                return;

            Vector3 effectPosition =
                muzzle.position +
                rotation * Vector3.forward * cannonMuzzleEffectForwardOffset;

            if (muzzleFlashPrefab != null)
            {
                RuntimeObjectPool.Spawn(
                    muzzleFlashPrefab,
                    effectPosition,
                    rotation);
            }

            if (cannonSmokePrefab != null && ShouldShowCpuSmokeEffect(effectPosition))
            {
                RuntimeObjectPool.Spawn(
                    cannonSmokePrefab,
                    effectPosition,
                    rotation);
            }
        }

        private void UpdateGroundMoveSmoke(float deltaTime)
        {
            if (groundMoveSmokePrefab == null)
                return;

            if (!ShouldShowCpuSmokeEffect(transform.position))
            {
                m_groundMoveSmokeMoveTimer = 0f;
                m_groundMoveSmokeIntervalTimer = 0f;
                return;
            }

            bool shouldEmit = IsMovingHorizontally();
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

        private bool ShouldShowCpuSmokeEffect(Vector3 effectPosition)
        {
            if (groundMoveSmokeMaxVisibleDistance <= 0f)
                return true;

            if (!TryGetGroundMoveSmokeViewerPosition(out Vector3 viewerPosition))
                return true;

            Vector3 toEffect = effectPosition - viewerPosition;
            toEffect.y = 0f;

            float maxDistance = groundMoveSmokeMaxVisibleDistance;
            return toEffect.sqrMagnitude <= maxDistance * maxDistance;
        }

        private bool TryGetGroundMoveSmokeViewerPosition(out Vector3 viewerPosition)
        {
            PlayerEntity localPlayer = FindLocalPlayerForSmokeCull();
            if (localPlayer != null)
            {
                viewerPosition = localPlayer.transform.position;
                return true;
            }

            Camera mainCamera = Camera.main;
            if (mainCamera == null)
            {
                viewerPosition = default;
                return false;
            }

            viewerPosition = mainCamera.transform.position;
            return true;
        }

        private PlayerEntity FindLocalPlayerForSmokeCull()
        {
            PlayerEntity[] players = FindObjectsByType<PlayerEntity>(FindObjectsInactive.Exclude);
            for (int i = 0; i < players.Length; i++)
            {
                if (players[i] != null && players[i].CanControl)
                    return players[i];
            }

            return null;
        }

        private bool IsMovingHorizontally()
        {
            if (m_rigidbody != null && !m_rigidbody.isKinematic)
            {
                Vector3 velocity = m_rigidbody.linearVelocity;
                velocity.y = 0f;
                return velocity.sqrMagnitude > 0.04f;
            }

            return m_attemptedMoveThisFrame && m_lastAttemptedMoveDirection.sqrMagnitude > 0.04f;
        }

        private void SpawnGroundMoveSmokeForTracks()
        {
            Vector3 moveDirection = m_lastAttemptedMoveDirection.sqrMagnitude > 0.0001f
                ? m_lastAttemptedMoveDirection.normalized
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

            RuntimeObjectPool.Spawn(
                groundMoveSmokePrefab,
                smokePosition,
                smokeRotation);
        }

        private bool TryFindGroundPoint(Vector3 basePosition, out Vector3 groundPoint)
        {
            float rayDistance = 4f;
            Vector3 rayOrigin = basePosition + Vector3.up * 1.5f;

            if (TryGetOwnColliderBounds(out Bounds bounds))
            {
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

        private bool TryGetOwnColliderBounds(out Bounds bounds)
        {
            if (m_ownColliders == null || m_ownColliders.Length == 0)
                CachePhysicsComponents();

            bounds = default;
            bool hasBounds = false;

            for (int i = 0; i < m_ownColliders.Length; i++)
            {
                Collider ownCollider = m_ownColliders[i];
                if (ownCollider == null || !ownCollider.enabled || ownCollider.isTrigger)
                    continue;

                if (!hasBounds)
                {
                    bounds = ownCollider.bounds;
                    hasBounds = true;
                    continue;
                }

                bounds.Encapsulate(ownCollider.bounds);
            }

            return hasBounds;
        }


        public void Damage(float value, PlayerEntity attacker)
        {
            if (m_isRespawning) return;

            RememberCounterAttackTarget(attacker);
            ReactToIncomingDamage();

            float finalDamage = value * Stats.GetStat(StatKey.DamageTaken);
            m_damageTaken += Mathf.Min(m_currentHealth, finalDamage);
            m_currentHealth -= finalDamage;

            if (m_healthUI != null)
                m_healthUI.SetHealth(m_currentHealth, maxHealth);
            SyncNetworkState();

            if (m_currentHealth > 0)
                return;

            if (attacker != null)
            {
                attacker.AddScore(pointValue);
                attacker.AddAirGauge(gaugeValue);
                attacker.RecoverHealth(defeatHealAmount);
                attacker.RegisterDefeat();
            }

            BeginRespawn();
        }

        public void Damage(float value, EnemyCPUEntity attacker)
        {
            if (m_isRespawning) return;

            RememberCounterAttackTarget(attacker);
            ReactToIncomingDamage();

            float finalDamage = value * Stats.GetStat(StatKey.DamageTaken);
            m_damageTaken += Mathf.Min(m_currentHealth, finalDamage);
            m_currentHealth -= finalDamage;

            if (m_healthUI != null)
                m_healthUI.SetHealth(m_currentHealth, maxHealth);
            SyncNetworkState();

            if (m_currentHealth > 0f)
                return;

            if (attacker != null && attacker != this)
            {
                attacker.AddScore(pointValue);
                attacker.RecoverHealth(defeatHealAmount);
                attacker.RegisterDefeat();
            }

            BeginRespawn();
        }

        private void RotateTurret(Vector3 targetDir)
        {
            Transform rotateTarget = turretTransform != null ? turretTransform : transform;
            Vector3 desiredDirection = targetDir;

            targetDir.y = 0f;

            if (targetDir.sqrMagnitude < 0.01f)
                return;

            Quaternion targetRot =
                Quaternion.LookRotation(targetDir);

            rotateTarget.rotation = Quaternion.Slerp(
                rotateTarget.rotation,
                targetRot,
                GetCpuRotateSpeed() * turretRotateSpeedMultiplier * Time.deltaTime
            );

            ApplyBarrelPitch(desiredDirection);
        }

        private void RotateBody(Vector3 moveDir)
        {
            Transform rotateTarget = bodyTransform != null ? bodyTransform : transform;

            moveDir.y = 0f;

            if (moveDir.sqrMagnitude < 0.01f)
                return;

            Quaternion targetRot =
                Quaternion.LookRotation(moveDir);

            rotateTarget.rotation = Quaternion.Slerp(
                rotateTarget.rotation,
                targetRot,
                GetCpuRotateSpeed() * Time.deltaTime
            );
        }

        private float GetCpuRotateSpeed()
        {
            return rotateSpeed * cpuRotateSpeedMultiplier;
        }

        private void RotateTurretToTarget()
        {
            RotateTurretToTarget(m_cpuTarget);
        }

        private void RotateTurretToTarget(Transform target)
        {
            if (target == null)
                return;

            Vector3 aimPosition = GetTargetAimPosition(target);
            Vector3 aimOrigin = turretTransform != null ? turretTransform.position : transform.position;

            RotateTurret(aimPosition - aimOrigin);
        }

        private void ApplyBarrelPitch(Vector3 desiredWorldDirection)
        {
            ResolveBarrelPitchTransform();

            if (barrelPitchTransform == null || turretTransform == null)
                return;

            CacheBarrelPitchBaseRotation();

            Vector3 localDirection = turretTransform.InverseTransformDirection(desiredWorldDirection);
            if (localDirection.sqrMagnitude < 0.0001f)
                return;

            float pitch = Mathf.Atan2(localDirection.y, Mathf.Max(0.0001f, localDirection.z)) * Mathf.Rad2Deg;
            pitch = Mathf.Clamp(pitch, -turretDepressionAngle, turretElevationAngle);

            ApplyBarrelPitchValue(pitch, true);
        }

        private void ApplyBarrelPitchValue(float pitch, bool smooth)
        {
            ResolveBarrelPitchTransform();

            if (barrelPitchTransform == null)
                return;

            CacheBarrelPitchBaseRotation();

            m_barrelPitch = pitch;
            Quaternion targetLocalRotation =
                m_barrelPitchBaseLocalRotation *
                Quaternion.Euler(-pitch, 0f, 0f);

            barrelPitchTransform.localRotation = smooth
                ? Quaternion.Slerp(
                    barrelPitchTransform.localRotation,
                    targetLocalRotation,
                    GetCpuRotateSpeed() * turretRotateSpeedMultiplier * Time.deltaTime)
                : targetLocalRotation;

            SyncSpecialWeaponModelPitch();
            TrySyncBarrelPitch();
        }

        private void CacheBarrelPitchBaseRotation()
        {
            ResolveBarrelPitchTransform();

            if (m_hasBarrelPitchBaseLocalRotation || barrelPitchTransform == null)
                return;

            m_barrelPitchBaseLocalRotation = barrelPitchTransform.localRotation;
            m_hasBarrelPitchBaseLocalRotation = true;
        }

        private void ResolveBarrelPitchTransform()
        {
            if (barrelPitchTransform != null || turretTransform == null)
                return;

            barrelPitchTransform = FindChildRecursive(turretTransform, "Barrel");
        }

        private void SyncSpecialWeaponModelPitch()
        {
            if (m_specialWeaponModel == null)
                return;

            if (m_specialWeaponModelPitchTransform == null)
                m_specialWeaponModelPitchTransform =
                    FindChildRecursive(m_specialWeaponModel.transform, "Barrel");

            if (m_specialWeaponModelPitchTransform == null ||
                barrelPitchTransform == null)
            {
                return;
            }

            m_specialWeaponModelPitchTransform.localRotation =
                barrelPitchTransform.localRotation;
        }

        private void TrySyncBarrelPitch()
        {
            if (!IsOnlineServer())
                return;

            if (!float.IsNaN(m_lastSyncedBarrelPitch) &&
                Mathf.Abs(m_lastSyncedBarrelPitch - m_barrelPitch) < 0.25f)
            {
                return;
            }

            m_lastSyncedBarrelPitch = m_barrelPitch;
            SyncNetworkState();
        }

        private void RotateTurretIdle()
        {
            if (m_cpuTarget != null || turretTransform == null)
                return;

            Vector3 baseDirection = bodyTransform != null ? bodyTransform.forward : transform.forward;
            baseDirection.y = 0f;

            if (baseDirection.sqrMagnitude < 0.01f)
                return;

            float sweepYaw = Mathf.Sin(Time.time * idleTurretSweepSpeed) * idleTurretSweepAngle;
            Vector3 sweepDirection = Quaternion.AngleAxis(sweepYaw, Vector3.up) * baseDirection.normalized;

            RotateTurret(sweepDirection);
        }

        public void SetRespawnPoint(Vector3 position, Quaternion rotation)
        {
            m_respawnPosition = position;
            m_respawnRotation = rotation;
        }

        public void SetCentralLandmark(Transform landmark, Vector3 startPosition)
        {
            m_centralLandmark = landmark;
            m_hasReachedLandmark = false;

            if (m_centralLandmark == null)
                return;

            Vector3 outwardDirection = startPosition - m_centralLandmark.position;
            outwardDirection.y = 0f;

            if (outwardDirection.sqrMagnitude < 0.001f)
                outwardDirection = transform.forward;

            outwardDirection.y = 0f;
            outwardDirection.Normalize();

            m_landmarkApproachPosition =
                m_centralLandmark.position +
                outwardDirection * Mathf.Max(0f, landmarkApproachDistance);
            m_landmarkApproachPosition.y = startPosition.y;
        }

        public void SetObstacleAvoidanceSide(int side)
        {
            m_obstacleAvoidanceSide = side < 0 ? -1 : 1;
            m_obstacleAvoidanceTimer = 0f;
        }

        public void AddScore(int value)
        {
            m_score += value;
            SyncNetworkState();
        }

        public void AddResultCarryOver(int score, int defeatedEnemies, float damageTaken, int deathCount)
        {
            m_score += Mathf.Max(0, score);
            m_defeatedEnemies += Mathf.Max(0, defeatedEnemies);
            m_damageTaken += Mathf.Max(0f, damageTaken);
            m_deathCount += Mathf.Max(0, deathCount);
            SyncNetworkState();
        }

        public void RegisterDefeat()
        {
            m_defeatedEnemies++;
            SyncNetworkState();
        }

        public void RecoverHealth(float value)
        {
            if (value <= 0f || m_isRespawning || m_currentHealth <= 0f)
                return;

            m_currentHealth = Mathf.Min(maxHealth, m_currentHealth + value);
            if (m_healthUI != null)
                m_healthUI.SetHealth(m_currentHealth, maxHealth);
            SyncNetworkState();
        }

        public void ApplyMachineIndex(int index)
        {
            CacheBaseMachineValues();

            machineIndex = MachineProfileCatalog.ClampIndex(index);
            MachineProfile profile = MachineProfileCatalog.GetProfile(machineIndex);

            maxHealth = Mathf.Max(1, Mathf.RoundToInt(m_baseMaxHealth * profile.HealthMultiplier));
            transform.localScale = m_baseScale * profile.SizeMultiplier;

            Stats.SetStat(StatKey.AttackPower, profile.AttackPowerMultiplier);
            Stats.SetStat(StatKey.FireInterval, profile.AttackSpeedMultiplier);
            Stats.SetStat(StatKey.DamageTaken, profile.DamageTakenMultiplier);
            Stats.SetStat(StatKey.MoveSpeed, profile.MoveSpeedMultiplier);

            ApplyTankMaterialVariant(profile.Type);
            ApplyMachineVariantMaterials(m_specialWeaponModel);

            m_currentHealth = maxHealth;
            if (m_healthUI != null)
                m_healthUI.SetHealth(m_currentHealth, maxHealth);
            SyncNetworkState();
        }

        private void ApplyTankMaterialVariant(MachineType machineType)
        {
            if (!TryGetTankMaterialVariant(machineType, out TankMaterialVariant variant))
                return;

            ApplyMachineVariantMaterials(gameObject, variant);
        }

        private void ApplyMachineVariantMaterials(GameObject targetRoot)
        {
            MachineProfile profile = MachineProfileCatalog.GetProfile(machineIndex);
            if (!TryGetTankMaterialVariant(profile.Type, out TankMaterialVariant variant))
                return;

            ApplyMachineVariantMaterials(targetRoot, variant);
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

            m_baseMaxHealth = maxHealth;
            m_baseScale = transform.localScale;
            m_hasBaseMachineValues = true;
        }

        private void BeginRespawn()
        {
            if (m_isRespawning) return;

            SpawnDeathExplosion();
            global::AudioManager.Instance?.PlayCueAtPosition(
                global::AudioCue.PlayerDeath,
                transform.position);
            m_deathCount++;
            m_currentHealth = 0f;
            m_cpuTarget = null;
            targetIsItems = false;
            m_targetLockTimer = 0f;
            m_cachedRepositionDirection = Vector3.zero;
            m_repositionDecisionTimer = 0f;
            m_counterAttackTarget = null;
            m_counterAttackTimer = 0f;
            m_fireTimer = 0f;
            m_wanderTimer = 0f;
            ResetSpecialWeapon();
            ResetStuckTracking();
            m_isRespawning = true;
            m_respawnRemaining = Mathf.Max(0f, respawnDelay);
            StopHorizontalVelocity();
            SetRespawnVisualActive(false);
            SyncNetworkState();

            if (m_respawnRemaining <= 0f)
                Respawn();
        }

        private void SpawnDeathExplosion()
        {
            if (deathExplosionPrefab == null)
                return;

            Vector3 explosionPosition = transform.position + Vector3.up;
            if (TryGetOwnColliderBounds(out Bounds bounds))
                explosionPosition = bounds.center;

            PlayDeathExplosionAt(explosionPosition);

            if (NetworkManager.Singleton != null &&
                NetworkManager.Singleton.IsListening &&
                NetworkManager.Singleton.IsServer)
            {
                m_networkController?.BroadcastDeathExplosion(explosionPosition);
            }
        }

        public void PlayDeathExplosionAt(Vector3 explosionPosition)
        {
            if (deathExplosionPrefab == null)
                return;

            RuntimeObjectPool.Spawn(
                deathExplosionPrefab,
                explosionPosition,
                Quaternion.identity);
        }

        private void UpdateRespawnCountdown()
        {
            if (!m_isRespawning) return;

            m_respawnRemaining -= Time.deltaTime;
            if (m_respawnRemaining > 0f) return;

            Respawn();
        }

        private void Respawn()
        {
            m_isRespawning = false;
            m_respawnRemaining = 0f;
            m_currentHealth = maxHealth;
            m_cpuTarget = null;
            targetIsItems = false;
            m_targetLockTimer = 0f;
            m_cachedRepositionDirection = Vector3.zero;
            m_repositionDecisionTimer = 0f;
            m_fireTimer = 0f;
            m_wanderTimer = 0f;
            m_hasReachedLandmark = false;
            buffList.Clear();
            if (m_buffManager != null)
                m_buffManager.ClearAllBuffs();

            if (m_rigidbody != null)
            {
                m_rigidbody.linearVelocity = Vector3.zero;
                m_rigidbody.angularVelocity = Vector3.zero;
                m_rigidbody.position = m_respawnPosition;
                m_rigidbody.rotation = m_respawnRotation;
            }

            transform.SetPositionAndRotation(m_respawnPosition, m_respawnRotation);
            if (m_healthUI != null)
                m_healthUI.SetHealth(m_currentHealth, maxHealth);

            SetRespawnVisualActive(true);
            global::AudioManager.Instance?.PlayCueAtPosition(
                global::AudioCue.Respawn,
                transform.position);
            BeginWanderMoveBurst();
            ResetStuckTracking();
            SyncNetworkState();
        }

        public void SetNetworkController(NetworkCpuController controller)
        {
            m_networkController = controller;
        }

        public void ConfigureNetworkReplica(bool isServer)
        {
            CachePhysicsComponents();

            if (m_rigidbody != null)
            {
                m_rigidbody.isKinematic = !isServer;
                m_rigidbody.useGravity = isServer;
            }
        }

        public void ApplyNetworkState(NetworkCpuState state)
        {
            if (machineIndex != state.MachineIndex)
                ApplyMachineIndex(state.MachineIndex);

            maxHealth = Mathf.Max(1, Mathf.RoundToInt(state.MaxHealth));
            m_currentHealth = Mathf.Clamp(state.Health, 0f, maxHealth);
            m_score = state.Score;
            m_defeatedEnemies = state.DefeatedEnemies;
            m_damageTaken = state.DamageTaken;
            m_deathCount = state.DeathCount;
            m_isRespawning = state.IsRespawning;
            ApplyBarrelPitchValue(state.BarrelPitch, false);
            ApplyNetworkVisualRotation(state.BodyRotation, state.TurretRotation);

            if (m_healthUI != null)
                m_healthUI.SetHealth(m_currentHealth, maxHealth);

            SetRespawnVisualActive(!m_isRespawning);
        }

        private void ApplyNetworkVisualRotation(
            Quaternion bodyRotation,
            Quaternion turretRotation)
        {
            float t = 1f - Mathf.Exp(-12f * Time.deltaTime);

            if (IsValidNetworkRotation(bodyRotation))
            {
                Transform rotateTarget =
                    bodyTransform != null ? bodyTransform : transform;
                rotateTarget.rotation = Quaternion.Slerp(
                    rotateTarget.rotation,
                    bodyRotation,
                    t);
            }

            if (turretTransform != null &&
                IsValidNetworkRotation(turretRotation))
            {
                turretTransform.rotation = Quaternion.Slerp(
                    turretTransform.rotation,
                    turretRotation,
                    t);
            }
        }

        private static bool IsValidNetworkRotation(Quaternion rotation)
        {
            return Mathf.Abs(rotation.x) +
                   Mathf.Abs(rotation.y) +
                   Mathf.Abs(rotation.z) +
                   Mathf.Abs(rotation.w) > 0.0001f;
        }

        private void SyncNetworkState()
        {
            m_networkController?.SyncFromServer();
        }

        private static bool IsOnlineServer()
        {
            return NetworkManager.Singleton != null &&
                   NetworkManager.Singleton.IsListening &&
                   NetworkManager.Singleton.IsServer;
        }

        private static ulong GetTargetNetworkObjectId(Transform target)
        {
            NetworkObject networkObject =
                target != null ? target.GetComponentInParent<NetworkObject>() : null;
            return networkObject != null && networkObject.IsSpawned
                ? networkObject.NetworkObjectId
                : ulong.MaxValue;
        }

        private void CachePhysicsComponents()
        {
            m_rigidbody = GetComponent<Rigidbody>();
            m_ownColliders = GetComponentsInChildren<Collider>(true);
            m_respawnRenderers = GetComponentsInChildren<Renderer>(true);
            m_ignoredCollisionPairs.Clear();
        }

        private void SetRespawnVisualActive(bool isActive)
        {
            if (m_respawnRenderers == null || m_respawnRenderers.Length == 0)
                m_respawnRenderers = GetComponentsInChildren<Renderer>(true);

            for (int i = 0; i < m_respawnRenderers.Length; i++)
            {
                if (m_respawnRenderers[i] != null)
                    m_respawnRenderers[i].enabled = isActive;
            }

            if (m_ownColliders == null || m_ownColliders.Length == 0)
                m_ownColliders = GetComponentsInChildren<Collider>(true);

            for (int i = 0; i < m_ownColliders.Length; i++)
            {
                if (m_ownColliders[i] != null)
                    m_ownColliders[i].enabled = isActive;
            }

            if (m_healthUI != null)
                m_healthUI.gameObject.SetActive(isActive);
        }

        private void ConfigureCpuPhysics()
        {
            if (m_rigidbody == null)
                return;

            m_rigidbody.useGravity = true;
            m_rigidbody.isKinematic = false;
            m_rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
            m_rigidbody.collisionDetectionMode = CollisionDetectionMode.Continuous;
            m_rigidbody.constraints = RigidbodyConstraints.FreezeRotation;
            m_rigidbody.maxDepenetrationVelocity =
                Mathf.Max(0.1f, maxPhysicsDepenetrationSpeed);
        }

        private void RefreshPlayerCollisionIgnores()
        {
            if (!ignorePlayerPhysicsCollision)
                return;

            m_collisionRefreshTimer -= Time.deltaTime;
            if (m_collisionRefreshTimer > 0f)
                return;

            m_collisionRefreshTimer = 1f;

            if (m_ownColliders == null || m_ownColliders.Length == 0)
                CachePhysicsComponents();

            CollectPlayers();
            RemoveStalePlayerColliderCacheEntries();
            for (int i = 0; i < m_cachedPlayers.Count; i++)
            {
                PlayerEntity player = m_cachedPlayers[i];
                if (player == null)
                    continue;

                if (!m_playerColliderCache.TryGetValue(
                        player,
                        out Collider[] playerColliders) ||
                    playerColliders == null)
                {
                    playerColliders =
                        player.GetComponentsInChildren<Collider>(true);
                    m_playerColliderCache[player] = playerColliders;
                }

                for (int j = 0; j < m_ownColliders.Length; j++)
                {
                    Collider ownCollider = m_ownColliders[j];
                    if (ownCollider == null)
                        continue;

                    for (int k = 0; k < playerColliders.Length; k++)
                    {
                        Collider playerCollider = playerColliders[k];
                        if (playerCollider == null)
                            continue;

                        ColliderPair pairKey = new ColliderPair(
                            ownCollider,
                            playerCollider);
                        if (!m_ignoredCollisionPairs.Add(pairKey))
                            continue;

                        Physics.IgnoreCollision(
                            ownCollider,
                            playerCollider,
                            true);
                    }
                }
            }
        }

        private void RemoveStalePlayerColliderCacheEntries()
        {
            m_staleColliderPlayers.Clear();
            foreach (KeyValuePair<PlayerEntity, Collider[]> pair in
                     m_playerColliderCache)
            {
                if (pair.Key == null || !m_cachedPlayers.Contains(pair.Key))
                    m_staleColliderPlayers.Add(pair.Key);
            }

            for (int i = 0; i < m_staleColliderPlayers.Count; i++)
                m_playerColliderCache.Remove(m_staleColliderPlayers[i]);
        }

        private readonly struct ColliderPair :
            System.IEquatable<ColliderPair>
        {
            private readonly Collider m_first;
            private readonly Collider m_second;

            public ColliderPair(Collider first, Collider second)
            {
                m_first = first;
                m_second = second;
            }

            public bool Equals(ColliderPair other)
            {
                return ReferenceEquals(m_first, other.m_first) &&
                       ReferenceEquals(m_second, other.m_second);
            }

            public override bool Equals(object obj)
            {
                return obj is ColliderPair other && Equals(other);
            }

            public override int GetHashCode()
            {
                int firstHash = m_first != null
                    ? System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(m_first)
                    : 0;
                int secondHash = m_second != null
                    ? System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(m_second)
                    : 0;
                return (firstHash * 397) ^ secondHash;
            }
        }

        private void PreventPlayerOverlap()
        {
            if (playerSeparationDistance <= 0f)
                return;

            int hitCount = Physics.OverlapSphereNonAlloc(
                transform.position,
                playerSeparationDistance,
                m_playerSeparationHits,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Collide);

            m_separatedPlayers.Clear();
            Vector3 separationDirection = Vector3.zero;
            for (int i = 0; i < hitCount; i++)
            {
                PlayerEntity player = m_playerSeparationHits[i] != null
                    ? m_playerSeparationHits[i].GetComponentInParent<PlayerEntity>()
                    : null;
                if (player == null || !m_separatedPlayers.Add(player))
                    continue;

                Vector3 offset = transform.position - player.transform.position;
                offset.y = 0f;

                float distance = offset.magnitude;
                if (distance >= playerSeparationDistance)
                    continue;

                Vector3 pushDirection = distance > 0.001f
                    ? offset / distance
                    : -player.transform.forward;

                separationDirection +=
                    pushDirection * (playerSeparationDistance - distance);
            }

            separationDirection.y = 0f;
            if (separationDirection.sqrMagnitude < 0.001f)
                return;

            float separationSpeed = Mathf.Min(
                Mathf.Max(0f, playerSeparationSpeed),
                GetCurrentMoveSpeed());
            if (separationSpeed <= 0f)
                return;

            Vector3 separationVelocity =
                separationDirection.normalized * separationSpeed;
            if (m_rigidbody != null && !m_rigidbody.isKinematic)
            {
                Vector3 velocity = m_rigidbody.linearVelocity;
                velocity.x = separationVelocity.x;
                velocity.z = separationVelocity.z;
                m_rigidbody.linearVelocity = velocity;
                return;
            }

            MoveCpuPosition(separationVelocity * Time.deltaTime);
        }

        private void CollectPlayers()
        {
            if (EntityManager.Instance != null)
            {
                EntityManager.Instance.GetEntities(m_cachedPlayers);
                return;
            }

            m_cachedPlayers.Clear();
            PlayerEntity[] players = FindObjectsByType<PlayerEntity>();
            for (int i = 0; i < players.Length; i++)
                m_cachedPlayers.Add(players[i]);
        }

        protected override void Wander()
        {
            if (MoveTowardCentralLandmark())
                return;

            if (m_remainingWanderMoves <= 0)
            {
                m_wanderTimer += Time.deltaTime;
                StopHorizontalVelocity();
                RotateTurretIdle();

                if (m_wanderTimer >= wanderInterval)
                    BeginWanderMoveBurst();

                return;
            }

            Vector3 direction = m_wanderTarget - transform.position;
            direction.y = 0f;

            if (direction.sqrMagnitude < 0.5f)
            {
                m_remainingWanderMoves--;

                if (m_remainingWanderMoves <= 0)
                {
                    m_wanderTimer = 0f;
                    StopHorizontalVelocity();
                    RotateTurretIdle();
                    return;
                }

                PickNewWanderTarget();
                direction = m_wanderTarget - transform.position;
                direction.y = 0f;

                if (direction.sqrMagnitude < 0.5f)
                {
                    StopHorizontalVelocity();
                    RotateTurretIdle();
                    return;
                }
            }

            MoveHorizontal(direction);
            RotateTurretIdle();
        }

        private bool MoveTowardCentralLandmark()
        {
            if (!TryGetCentralAdvanceDirection(out Vector3 direction))
                return false;

            MoveHorizontal(direction);
            RotateTurretIdle();
            return true;
        }

        private bool TryGetCentralAdvanceDirection(out Vector3 direction)
        {
            direction = Vector3.zero;
            if (m_centralLandmark == null || m_hasReachedLandmark)
                return false;

            direction = m_landmarkApproachPosition - transform.position;
            direction.y = 0f;

            float arrivalDistance = Mathf.Max(0.5f, landmarkArrivalDistance);
            if (direction.sqrMagnitude <= arrivalDistance * arrivalDistance)
            {
                m_hasReachedLandmark = true;
                BeginWanderMoveBurst();
                direction = Vector3.zero;
                return false;
            }

            return direction.sqrMagnitude > 0.001f;
        }

        protected override void PickNewWanderTarget()
        {
            if (m_centralLandmark == null)
            {
                base.PickNewWanderTarget();
                return;
            }

            if (!m_hasReachedLandmark)
            {
                m_wanderTarget = m_landmarkApproachPosition;
                return;
            }

            float innerRadius = Mathf.Max(0f, landmarkWanderInnerRadius);
            float outerRadius = Mathf.Max(innerRadius, landmarkWanderOuterRadius);
            Vector2 direction = Random.insideUnitCircle;

            if (direction.sqrMagnitude < 0.001f)
                direction = Vector2.right;

            direction.Normalize();
            float radius = Random.Range(innerRadius, outerRadius);
            Vector3 center = m_centralLandmark.position;

            m_wanderTarget = new Vector3(
                center.x + direction.x * radius,
                transform.position.y,
                center.z + direction.y * radius
            );
        }

        private void BeginWanderMoveBurst()
        {
            int minMoveCount = Mathf.Max(1, minWanderMoveCount);
            int maxMoveCount = Mathf.Max(minMoveCount, maxWanderMoveCount);

            m_remainingWanderMoves = Random.Range(minMoveCount, maxMoveCount + 1);
            m_wanderTimer = 0f;
            PickNewWanderTarget();
        }

        private void MoveHorizontal(Vector3 direction)
        {
            direction.y = 0f;

            if (direction.sqrMagnitude < 0.001f)
            {
                StopHorizontalVelocity();
                return;
            }

            bool isEscaping = TryGetStuckEscapeMoveDirection(out Vector3 moveDirection);
            if (!isEscaping)
                moveDirection = GetObstacleAdjustedMoveDirection(direction.normalized);

            if (moveDirection.sqrMagnitude < 0.001f)
            {
                StopHorizontalVelocity();
                RecoverFromStuck("blocked");
                return;
            }

            RegisterMoveAttempt(moveDirection);
            RotateBody(moveDirection);

            if (m_rigidbody != null && !m_rigidbody.isKinematic)
            {
                float currentMoveSpeed = GetCurrentMoveSpeed();

                Vector3 velocity = moveDirection * currentMoveSpeed;
                velocity.y = m_rigidbody.linearVelocity.y;

                m_rigidbody.linearVelocity = velocity;
                return;
            }

            transform.position += moveDirection * GetCurrentMoveSpeed() * Time.deltaTime;
        }

        private void MoveHorizontalImmediate(Vector3 direction)
        {
            direction.y = 0f;

            if (direction.sqrMagnitude < 0.001f)
            {
                StopHorizontalVelocity();
                return;
            }

            if (!TryGetStuckEscapeMoveDirection(out Vector3 moveDirection))
                moveDirection = GetObstacleAdjustedMoveDirection(direction.normalized);

            if (moveDirection.sqrMagnitude < 0.001f)
            {
                StopHorizontalVelocity();
                RecoverFromStuck("blocked");
                return;
            }

            RegisterMoveAttempt(moveDirection);
            RotateBody(moveDirection);

            if (m_rigidbody != null && !m_rigidbody.isKinematic)
            {
                float currentMoveSpeed = GetCurrentMoveSpeed();

                Vector3 velocity = moveDirection * currentMoveSpeed;
                velocity.y = m_rigidbody.linearVelocity.y;

                m_rigidbody.linearVelocity = velocity;
                return;
            }

            transform.position += moveDirection * GetCurrentMoveSpeed() * Time.deltaTime;
        }

        private float GetCurrentMoveSpeed()
        {
            float currentMoveSpeed = moveSpeed * Stats.GetStat(StatKey.MoveSpeed);
            if (maxBuffedMoveSpeed > 0f)
                currentMoveSpeed = Mathf.Min(currentMoveSpeed, maxBuffedMoveSpeed);

            return currentMoveSpeed;
        }

        private Vector3 GetObstacleAdjustedMoveDirection(Vector3 moveDirection)
        {
            moveDirection.y = 0f;

            if (moveDirection.sqrMagnitude < 0.001f)
                return Vector3.zero;

            moveDirection.Normalize();
            float checkDistance = Mathf.Max(0.1f, obstacleCheckDistance);
            float minimumClearance = Mathf.Max(
                obstacleCheckRadius,
                obstacleMinimumClearance);

            if (m_committedDirectionTimer > 0f &&
                m_committedMoveDirection.sqrMagnitude >= 0.001f)
            {
                Vector3 committedDirection = m_committedMoveDirection.normalized;
                if (GetMoveClearance(committedDirection, checkDistance) >= minimumClearance)
                    return committedDirection;

                ClearCommittedDirection();
            }

            if (GetMoveClearance(moveDirection, checkDistance) >= checkDistance)
            {
                m_obstacleAvoidanceTimer = 0f;
                return moveDirection;
            }

            int preferredSide = m_obstacleAvoidanceSide;
            if (m_obstacleAvoidanceTimer <= 0f)
            {
                preferredSide = ChooseObstacleAvoidanceSide(moveDirection, checkDistance);
                m_obstacleAvoidanceSide = preferredSide;
                m_obstacleAvoidanceTimer = Mathf.Max(0.1f, obstacleAvoidanceHoldTime);
            }

            Vector3 bestDirection = Vector3.zero;
            float bestScore = float.NegativeInfinity;

            for (int sidePass = 0; sidePass < 2; sidePass++)
            {
                int side = sidePass == 0 ? preferredSide : -preferredSide;

                for (int i = 0; i < ObstacleAvoidanceAngles.Length; i++)
                {
                    Vector3 candidate =
                        Quaternion.AngleAxis(
                            ObstacleAvoidanceAngles[i] * side,
                            Vector3.up
                        ) * moveDirection;
                    candidate.Normalize();

                    float clearance = GetMoveClearance(candidate, checkDistance);
                    if (clearance < minimumClearance)
                        continue;

                    float directionScore = Vector3.Dot(candidate, moveDirection);
                    float preferredSideBonus = side == preferredSide ? 0.75f : 0f;
                    float score = clearance + directionScore * 1.5f + preferredSideBonus;

                    if (score <= bestScore)
                        continue;

                    bestScore = score;
                    bestDirection = candidate;
                    m_obstacleAvoidanceSide = side;
                }
            }

            if (bestDirection.sqrMagnitude >= 0.001f)
                CommitMoveDirection(bestDirection);

            return bestDirection;
        }

        private bool TryGetStuckEscapeMoveDirection(out Vector3 moveDirection)
        {
            moveDirection = Vector3.zero;
            if (m_stuckEscapeTimer <= 0f)
                return false;

            if (TryGetBreadcrumbBacktrackDirection(out moveDirection))
                return true;

            moveDirection = FindBestStuckEscapeDirection();
            if (moveDirection.sqrMagnitude >= 0.001f)
            {
                CommitMoveDirection(moveDirection);
                return true;
            }

            m_stuckEscapeTimer = 0f;
            m_stuckEscapePreferredDirection = Vector3.zero;
            return false;
        }

        private bool TryGetBreadcrumbBacktrackDirection(out Vector3 direction)
        {
            direction = Vector3.zero;
            if (!m_isBacktracking)
                return false;

            float arrivalDistance = Mathf.Max(0.25f, stuckBreadcrumbArrivalDistance);
            float arrivalDistanceSqr = arrivalDistance * arrivalDistance;

            while (m_backtrackBreadcrumbIndex >= m_backtrackStopIndex)
            {
                direction =
                    m_movementBreadcrumbs[m_backtrackBreadcrumbIndex] -
                    transform.position;
                direction.y = 0f;

                if (direction.sqrMagnitude > arrivalDistanceSqr)
                    return true;

                m_backtrackBreadcrumbIndex--;
            }

            m_isBacktracking = false;
            direction = Vector3.zero;
            return false;
        }

        private Vector3 FindBestStuckEscapeDirection()
        {
            Vector3 preferredDirection = m_stuckEscapePreferredDirection;
            preferredDirection.y = 0f;

            if (preferredDirection.sqrMagnitude < 0.001f)
                preferredDirection = -transform.forward;

            preferredDirection.Normalize();
            float checkDistance = Mathf.Max(
                obstacleCheckDistance,
                stuckEscapeCheckDistance);
            float minimumClearance = Mathf.Max(
                obstacleCheckRadius,
                obstacleMinimumClearance);
            Vector3 bestDirection = Vector3.zero;
            float bestScore = float.NegativeInfinity;

            for (int i = 0; i < StuckEscapeAngles.Length; i++)
            {
                Vector3 candidate =
                    Quaternion.AngleAxis(
                        StuckEscapeAngles[i],
                        Vector3.up
                    ) * preferredDirection;
                candidate.Normalize();

                float clearance = GetMoveClearance(candidate, checkDistance);
                if (clearance < minimumClearance)
                    continue;

                float preferredScore = Vector3.Dot(candidate, preferredDirection);
                float score = clearance + preferredScore * 2.5f;
                if (score <= bestScore)
                    continue;

                bestScore = score;
                bestDirection = candidate;
            }

            return bestDirection;
        }

        private void CommitMoveDirection(Vector3 direction)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.001f)
                return;

            m_committedMoveDirection = direction.normalized;
            m_committedDirectionTimer = Mathf.Max(
                0.2f,
                junctionDirectionHoldTime);
        }

        private void ClearCommittedDirection()
        {
            m_committedMoveDirection = Vector3.zero;
            m_committedDirectionTimer = 0f;
        }

        private int ChooseObstacleAvoidanceSide(Vector3 moveDirection, float checkDistance)
        {
            float leftClearance = GetBestSideClearance(moveDirection, -1, checkDistance);
            float rightClearance = GetBestSideClearance(moveDirection, 1, checkDistance);

            if (Mathf.Abs(leftClearance - rightClearance) <= 0.25f)
                return m_obstacleAvoidanceSide;

            return rightClearance > leftClearance ? 1 : -1;
        }

        private float GetBestSideClearance(
            Vector3 moveDirection,
            int side,
            float checkDistance)
        {
            float bestClearance = 0f;

            for (int i = 0; i < ObstacleSideCheckAngles.Length; i++)
            {
                Vector3 candidate =
                    Quaternion.AngleAxis(
                        ObstacleSideCheckAngles[i] * side,
                        Vector3.up
                    ) * moveDirection;
                bestClearance = Mathf.Max(
                    bestClearance,
                    GetMoveClearance(candidate, checkDistance)
                );
            }

            return bestClearance;
        }

        private float GetMoveClearance(Vector3 direction, float checkDistance)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.001f)
                return 0f;

            direction.Normalize();

            Vector3 centerOrigin =
                transform.position + Vector3.up * obstacleCheckHeight;
            Vector3 sideDirection = Vector3.Cross(Vector3.up, direction).normalized;
            Vector3 scale = transform.lossyScale;
            float scaleMultiplier = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
            float sideOffset = Mathf.Max(0f, obstacleSideProbeOffset) * scaleMultiplier;

            float centerClearance =
                CastObstacleProbe(centerOrigin, direction, checkDistance);
            float leftClearance = CastObstacleProbe(
                centerOrigin - sideDirection * sideOffset,
                direction,
                checkDistance
            );
            float rightClearance = CastObstacleProbe(
                centerOrigin + sideDirection * sideOffset,
                direction,
                checkDistance
            );

            return Mathf.Min(centerClearance, leftClearance, rightClearance);
        }

        private float CastObstacleProbe(
            Vector3 origin,
            Vector3 direction,
            float checkDistance)
        {
            int hitCount = Physics.SphereCastNonAlloc(
                origin,
                obstacleCheckRadius,
                direction,
                m_obstacleHits,
                checkDistance,
                obstacleMask,
                QueryTriggerInteraction.Ignore
            );

            float nearestDistance = checkDistance;

            for (int i = 0; i < hitCount; i++)
            {
                Collider hitCollider = m_obstacleHits[i].collider;
                if (hitCollider == null)
                    continue;

                if (IsOwnCollider(hitCollider) || IsTargetCollider(hitCollider))
                    continue;

                if (m_obstacleHits[i].normal.y > 0.65f)
                    continue;

                nearestDistance = Mathf.Min(nearestDistance, m_obstacleHits[i].distance);
            }

            return nearestDistance;
        }

        private void StopHorizontalVelocity()
        {
            if (m_rigidbody == null || m_rigidbody.isKinematic)
                return;

            Vector3 velocity = m_rigidbody.linearVelocity;
            velocity.x = 0f;
            velocity.z = 0f;
            m_rigidbody.linearVelocity = velocity;
        }

        private void ClampHorizontalVelocity()
        {
            if (m_rigidbody == null || m_rigidbody.isKinematic)
                return;

            float maxSpeed = GetCurrentMoveSpeed();
            if (maxSpeed <= 0f)
            {
                StopHorizontalVelocity();
                return;
            }

            Vector3 velocity = m_rigidbody.linearVelocity;
            Vector2 horizontalVelocity = new Vector2(velocity.x, velocity.z);
            if (horizontalVelocity.sqrMagnitude <= maxSpeed * maxSpeed)
                return;

            horizontalVelocity = horizontalVelocity.normalized * maxSpeed;
            velocity.x = horizontalVelocity.x;
            velocity.z = horizontalVelocity.y;
            m_rigidbody.linearVelocity = velocity;
        }

        private void MoveCpuPosition(Vector3 delta)
        {
            if (delta.sqrMagnitude <= 0f)
                return;

            if (m_rigidbody != null && !m_rigidbody.isKinematic)
            {
                m_rigidbody.position += delta;
                return;
            }

            transform.position += delta;
        }

        private void RegisterMoveAttempt(Vector3 direction)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.001f)
                return;

            m_attemptedMoveThisFrame = true;
            m_lastAttemptedMoveDirection = direction.normalized;
        }

        private void RecordMovementBreadcrumb()
        {
            if (m_isRespawning || m_stuckEscapeTimer > 0f)
                return;

            Vector3 currentPosition = transform.position;
            currentPosition.y = 0f;
            float spacing = Mathf.Max(0.5f, stuckBreadcrumbSpacing);

            if (m_movementBreadcrumbs.Count > 0)
            {
                Vector3 lastPosition =
                    m_movementBreadcrumbs[m_movementBreadcrumbs.Count - 1];
                if ((currentPosition - lastPosition).sqrMagnitude < spacing * spacing)
                    return;
            }

            m_movementBreadcrumbs.Add(currentPosition);
            int maxCount = Mathf.Max(4, maxStuckBreadcrumbs);
            if (m_movementBreadcrumbs.Count > maxCount)
                m_movementBreadcrumbs.RemoveAt(0);
        }

        private bool BeginBreadcrumbBacktrack()
        {
            if (m_movementBreadcrumbs.Count < 2)
                return false;

            m_backtrackBreadcrumbIndex = m_movementBreadcrumbs.Count - 1;
            m_backtrackStopIndex = m_backtrackBreadcrumbIndex;
            float targetDistance = Mathf.Max(
                stuckBreadcrumbSpacing * 2f,
                stuckBacktrackDistance);
            float accumulatedDistance = 0f;

            for (int i = m_backtrackBreadcrumbIndex; i > 0; i--)
            {
                accumulatedDistance += Vector3.Distance(
                    m_movementBreadcrumbs[i],
                    m_movementBreadcrumbs[i - 1]);
                m_backtrackStopIndex = i - 1;

                if (accumulatedDistance >= targetDistance)
                    break;
            }

            m_isBacktracking =
                m_backtrackStopIndex < m_backtrackBreadcrumbIndex;
            return m_isBacktracking;
        }

        private void ResetStuckTracking()
        {
            m_lastStuckCheckPosition = transform.position;
            m_lastAttemptedMoveDirection = Vector3.zero;
            m_stuckCheckTimer = 0f;
            m_stuckRecoveryTimer = 0f;
            m_stuckEscapeTimer = 0f;
            m_stuckEscapePreferredDirection = Vector3.zero;
            m_isBacktracking = false;
            m_backtrackBreadcrumbIndex = -1;
            m_backtrackStopIndex = -1;
            m_movementBreadcrumbs.Clear();
            Vector3 startPosition = transform.position;
            startPosition.y = 0f;
            m_movementBreadcrumbs.Add(startPosition);
            m_obstacleAvoidanceTimer = 0f;
            ClearCommittedDirection();
            m_attemptedMoveThisFrame = false;
        }

        private void UpdateStuckRecoveryTimer()
        {
            if (m_stuckRecoveryTimer <= 0f)
                m_stuckRecoveryTimer = 0f;
            else
                m_stuckRecoveryTimer -= Time.deltaTime;

            if (m_stuckEscapeTimer <= 0f)
            {
                m_stuckEscapeTimer = 0f;
                m_stuckEscapePreferredDirection = Vector3.zero;
            }
            else
            {
                m_stuckEscapeTimer -= Time.deltaTime;
            }

            if (m_obstacleAvoidanceTimer <= 0f)
                m_obstacleAvoidanceTimer = 0f;
            else
                m_obstacleAvoidanceTimer -= Time.deltaTime;

            if (m_committedDirectionTimer <= 0f)
                ClearCommittedDirection();
            else
                m_committedDirectionTimer -= Time.deltaTime;
        }

        private void MonitorStuckMovement()
        {
            if (!m_attemptedMoveThisFrame)
            {
                m_stuckCheckTimer = 0f;
                m_lastStuckCheckPosition = transform.position;
                return;
            }

            m_stuckCheckTimer += Time.deltaTime;
            if (m_stuckCheckTimer < stuckCheckInterval)
                return;

            Vector3 moved = transform.position - m_lastStuckCheckPosition;
            moved.y = 0f;

            if (moved.magnitude < stuckMoveThreshold)
                RecoverFromStuck("low movement");

            m_stuckCheckTimer = 0f;
            m_lastStuckCheckPosition = transform.position;
        }

        private void RecoverFromStuck(string reason)
        {
            if (m_stuckRecoveryTimer > 0f || m_stuckEscapeTimer > 0f)
                return;

            if (targetIsItems && m_cpuTarget != null)
            {
                m_itemStuckRecoveryAttempts++;
                if (m_itemStuckRecoveryAttempts >=
                    Mathf.Max(1, maxItemStuckRecoveryAttempts))
                {
                    MarkCurrentItemTemporarilyUnreachable();
                    return;
                }
            }

            m_stuckRecoveryTimer = Mathf.Max(0.1f, stuckRecoveryCooldown);
            bool hasBacktrackPath = BeginBreadcrumbBacktrack();
            float minimumEscapeTime = hasBacktrackPath
                ? stuckBacktrackDistance / Mathf.Max(0.5f, GetCurrentMoveSpeed()) +
                  stuckEscapeDuration
                : stuckEscapeDuration;
            m_stuckEscapeTimer = Mathf.Max(0.5f, minimumEscapeTime);
            m_smoothedAttackMoveDirection = Vector3.zero;
            m_attackStrafeSide *= -1;
            m_obstacleAvoidanceSide *= -1;
            m_obstacleAvoidanceTimer = Mathf.Max(0.2f, obstacleAvoidanceHoldTime);
            m_attackStrafeTimer = Mathf.Max(0.2f, attackStrafeSwitchInterval * 0.5f);

            Vector3 blockedDirection = m_lastAttemptedMoveDirection;
            blockedDirection.y = 0f;
            if (blockedDirection.sqrMagnitude < 0.001f)
                blockedDirection = transform.forward;

            blockedDirection.Normalize();
            Vector3 sideDirection =
                Vector3.Cross(Vector3.up, blockedDirection) *
                m_obstacleAvoidanceSide;
            m_stuckEscapePreferredDirection =
                (-blockedDirection +
                 sideDirection * Mathf.Max(0f, stuckEscapeSideWeight)).normalized;
            ClearCommittedDirection();

            if (m_cpuTarget == null || targetIsItems)
            {
                m_remainingWanderMoves = Mathf.Max(1, m_remainingWanderMoves);
                PickNewWanderTarget();
            }

            StopHorizontalVelocity();

            if (debugStuckRecovery)
                Debug.Log($"[EnemyCPUEntity] Stuck recovery: {reason} / {gameObject.name}");
        }

        public void MoveToTarget()
        {
            if (m_cpuTarget == null)
                return;

            Vector3 dir = GetDirection(m_cpuTarget.position, transform.position);
            moveDirection = dir;
            Chase(dir);
        }

        public void AttackMove()
        {
            if (m_cpuTarget == null)
                return;

            Vector3 toTarget = GetDirection(m_cpuTarget.position, transform.position);
            if (toTarget.sqrMagnitude < 0.001f)
            {
                StopHorizontalVelocity();
                RotateTurretToTarget();
                return;
            }

            Vector3 targetDirection = toTarget.normalized;
            float distance = toTarget.magnitude;
            bool isPlayerTarget =
                m_cpuTarget.GetComponentInParent<PlayerEntity>() != null;
            bool commitToPlayerCombat =
                isPlayerTarget && distance <= playerCombatCommitDistance;

            if (TryGetCentralAdvanceDirection(out Vector3 advanceDirection) &&
                !commitToPlayerCombat)
            {
                Vector3 combatAdvanceDirection =
                    GetCombatAdvanceDirection(
                        advanceDirection.normalized,
                        targetDirection,
                        distance);
                MoveHorizontalImmediate(combatAdvanceDirection);
                RotateTurretToTarget();
                return;
            }

            if (!HasLineOfSightToTarget())
            {
                Vector3 repositionDirection =
                    GetCombatRepositionDirection(targetDirection);
                MoveHorizontalImmediate(repositionDirection);
                RotateTurretToTarget();
                return;
            }

            UpdateAttackStrafeSide(targetDirection);

            Vector3 strafeDirection =
                Vector3.Cross(Vector3.up, targetDirection) *
                m_attackStrafeSide;

            float radialDirection = 0f;
            if (ShouldRetreatFromTarget(distance))
                radialDirection = -1f;
            else if (distance > preferredDistance + distanceTolerance)
                radialDirection = 1f;
            else if (distance < preferredDistance - distanceTolerance)
                radialDirection = -1f;

            Vector3 attackMoveDirection =
                targetDirection * (radialDirection * attackDiagonalDistanceWeight) +
                strafeDirection * attackDiagonalStrafeWeight;

            attackMoveDirection = GetSmoothedAttackMoveDirection(attackMoveDirection);

            MoveHorizontalImmediate(attackMoveDirection);
            RotateTurretToTarget();
        }

        private Vector3 GetCombatAdvanceDirection(
            Vector3 advanceDirection,
            Vector3 targetDirection,
            float targetDistance)
        {
            Vector3 result = advanceDirection * centralAdvanceWeight;

            if (HasLineOfSightToTarget())
            {
                UpdateAttackStrafeSide(targetDirection);
                Vector3 strafeDirection =
                    Vector3.Cross(Vector3.up, targetDirection) *
                    m_attackStrafeSide;
                result += strafeDirection * attackDiagonalStrafeWeight;

                if (targetDistance < preferredDistance - distanceTolerance)
                {
                    result -=
                        targetDirection * attackDiagonalDistanceWeight;
                }
            }
            else
            {
                Vector3 repositionDirection =
                    GetCombatRepositionDirection(targetDirection);
                if (Vector3.Dot(repositionDirection, advanceDirection) > -0.25f)
                    result += repositionDirection;
            }

            result.y = 0f;
            return result.sqrMagnitude > 0.001f
                ? result.normalized
                : advanceDirection;
        }

        private bool ShouldRetreatFromTarget(float distance)
        {
            if (maxHealth <= 0)
                return false;

            float healthRatio = Mathf.Clamp01(m_currentHealth / maxHealth);
            return healthRatio <= retreatHealthRatio &&
                   distance < Mathf.Max(preferredDistance, retreatDistance);
        }

        private Vector3 GetCombatRepositionDirection(Vector3 targetDirection)
        {
            if (m_repositionDecisionTimer > 0f &&
                m_cachedRepositionDirection.sqrMagnitude > 0.001f)
            {
                return m_cachedRepositionDirection;
            }

            m_repositionDecisionTimer =
                Mathf.Max(0.05f, repositionDecisionInterval);

            Vector3 bestDirection = targetDirection;
            float bestScore = float.NegativeInfinity;
            float checkDistance = Mathf.Max(0.5f, obstacleCheckDistance);
            float minimumClearance = Mathf.Max(
                obstacleCheckRadius,
                obstacleMinimumClearance);

            for (int i = 0; i < CombatRepositionAngles.Length; i++)
            {
                Vector3 candidate =
                    Quaternion.AngleAxis(
                        CombatRepositionAngles[i],
                        Vector3.up) * targetDirection;
                candidate.y = 0f;
                candidate.Normalize();

                float clearance = GetMoveClearance(candidate, checkDistance);
                if (clearance < minimumClearance)
                    continue;

                Vector3 origin =
                    enemyMuzzleTransform != null
                        ? enemyMuzzleTransform.position
                        : transform.position + Vector3.up * attackAimHeight;
                Vector3 futureOrigin =
                    origin +
                    candidate * repositionProbeDistance;
                bool opensLineOfSight =
                    HasLineOfSightFrom(futureOrigin, m_cpuTarget);
                float progressScore = Vector3.Dot(candidate, targetDirection);
                float sidePreference =
                    Mathf.Sign(CombatRepositionAngles[i]) ==
                    m_attackStrafeSide ? 0.5f : 0f;
                float score =
                    clearance +
                    progressScore * 1.5f +
                    sidePreference +
                    (opensLineOfSight ? 8f : 0f);

                if (score <= bestScore)
                    continue;

                bestScore = score;
                bestDirection = candidate;
            }

            m_cachedRepositionDirection = bestDirection.normalized;
            return m_cachedRepositionDirection;
        }

        public void CounterAttackWhileMovingToItem()
        {
            if (!IsValidCounterAttackTarget())
                return;

            RotateTurretToTarget(m_counterAttackTarget);

            if (CanFireAt(m_counterAttackTarget))
                FireWeaponAt(m_counterAttackTarget);
        }

        private void RememberCounterAttackTarget(PlayerEntity attacker)
        {
            if (attacker == null)
                return;

            m_counterAttackTarget = attacker.transform;
            m_counterAttackTimer = counterAttackMemoryTime;
        }

        private void RememberCounterAttackTarget(EnemyCPUEntity attacker)
        {
            if (attacker == null || attacker == this)
                return;

            m_counterAttackTarget = attacker.transform;
            m_counterAttackTimer = counterAttackMemoryTime;
        }

        private void ReactToIncomingDamage()
        {
            if (m_damageReactionTimer > 0f)
                return;

            m_damageReactionTimer = Mathf.Max(0.05f, damageReactionCooldown);
            m_attackStrafeSide *= -1;
            m_attackStrafeTimer =
                Mathf.Max(0.25f, attackStrafeSwitchInterval * 0.35f);
            m_repositionDecisionTimer = 0f;
            m_cachedRepositionDirection = Vector3.zero;
        }

        private void UpdateCounterAttackTarget()
        {
            if (m_counterAttackTarget == null)
                return;

            m_counterAttackTimer -= Time.deltaTime;
            if (m_counterAttackTimer > 0f)
                return;

            m_counterAttackTarget = null;
        }

        private bool IsValidCounterAttackTarget()
        {
            if (m_counterAttackTarget == null)
                return false;

            if (!IsValidTarget(m_counterAttackTarget) ||
                IsItemTarget(m_counterAttackTarget))
            {
                return false;
            }

            Vector3 direction = GetDirection(m_counterAttackTarget.position, transform.position);
            if (direction.magnitude > CPUChaseRange)
                return false;

            return true;
        }

        private Vector3 GetSmoothedAttackMoveDirection(Vector3 direction)
        {
            direction.y = 0f;

            if (direction.sqrMagnitude < 0.001f)
                return direction;

            direction.Normalize();

            if (m_smoothedAttackMoveDirection.sqrMagnitude < 0.001f)
                m_smoothedAttackMoveDirection = direction;
            else
                m_smoothedAttackMoveDirection = Vector3.Slerp(
                    m_smoothedAttackMoveDirection,
                    direction,
                    attackMoveDirectionSmooth * Time.deltaTime
                );

            return m_smoothedAttackMoveDirection.normalized;
        }

        private void UpdateAttackStrafeSide(Vector3 targetDirection)
        {
            m_attackStrafeTimer -= Time.deltaTime;
            if (m_attackStrafeTimer > 0f)
                return;

            m_attackStrafeTimer = Mathf.Max(0.1f, attackStrafeSwitchInterval);
            Vector3 leftDirection =
                Vector3.Cross(Vector3.up, targetDirection) * -1f;
            Vector3 rightDirection =
                Vector3.Cross(Vector3.up, targetDirection);
            float checkDistance = Mathf.Max(0.5f, obstacleCheckDistance);
            float leftClearance =
                GetMoveClearance(leftDirection, checkDistance);
            float rightClearance =
                GetMoveClearance(rightDirection, checkDistance);

            if (Mathf.Abs(leftClearance - rightClearance) <= 0.25f)
                m_attackStrafeSide *= -1;
            else
                m_attackStrafeSide =
                    rightClearance > leftClearance ? 1 : -1;
        }

        private bool HasLineOfSightToTarget()
        {
            return HasLineOfSightToTarget(m_cpuTarget);
        }

        private bool HasLineOfSightToTarget(Transform target)
        {
            if (target == null || enemyMuzzleTransform == null)
                return false;

            return HasLineOfSightFrom(enemyMuzzleTransform.position, target);
        }

        private bool HasLineOfSightFrom(Vector3 origin, Transform target)
        {
            if (target == null)
                return false;

            Vector3 aimPosition = GetTargetVisiblePosition(target);
            Vector3 direction = aimPosition - origin;
            float distance = direction.magnitude;

            if (distance <= 0.001f)
                return true;

            int hitCount = Physics.RaycastNonAlloc(
                origin,
                direction / distance,
                m_lineOfSightHits,
                distance,
                obstacleMask,
                QueryTriggerInteraction.Ignore
            );

            ProjectileSweepUtility.SortByDistance(m_lineOfSightHits, hitCount);

            for (int i = 0; i < hitCount; i++)
            {
                Collider hitCollider = m_lineOfSightHits[i].collider;
                if (hitCollider == null)
                    continue;

                if (IsOwnCollider(hitCollider))
                    continue;

                if (IsTargetCollider(hitCollider, target))
                    return true;

                return false;
            }

            return true;
        }

        private bool IsOwnCollider(Collider col)
        {
            return col != null && col.transform.root == transform.root;
        }

        private bool IsTargetCollider(Collider col)
        {
            return IsTargetCollider(col, m_cpuTarget);
        }

        private bool IsTargetCollider(Collider col, Transform target)
        {
            return col != null &&
                   target != null &&
                   col.transform.root == target.transform.root;
        }

        private Vector3 GetTargetAimPosition()
        {
            return GetTargetAimPosition(m_cpuTarget);
        }

        private Vector3 GetTargetAimPosition(Transform target)
        {
            Vector3 aimPosition = GetTargetVisiblePosition(target);
            if (target == null)
                return aimPosition;

            Rigidbody targetBody = target.GetComponentInParent<Rigidbody>();
            if (targetBody == null || enemyMuzzleTransform == null)
                return aimPosition;

            float projectileSpeed = GetProjectileSpeed();
            if (projectileSpeed <= 0.01f)
                return aimPosition;

            float travelTime =
                Vector3.Distance(enemyMuzzleTransform.position, aimPosition) /
                projectileSpeed;
            travelTime = Mathf.Min(Mathf.Max(0f, maxAimLeadTime), travelTime);

            return aimPosition + targetBody.linearVelocity * travelTime;
        }

        private Vector3 GetTargetVisiblePosition(Transform target)
        {
            if (target == null)
                return transform.position + Vector3.up * attackAimHeight;

            PlayerEntity player = target.GetComponentInParent<PlayerEntity>();
            if (player != null)
                return player.GetActiveTargetAimPoint();

            Collider targetCollider = target.GetComponent<Collider>();
            if (targetCollider == null)
                targetCollider = target.GetComponentInParent<Collider>();

            return targetCollider != null
                ? targetCollider.bounds.center
                : target.position + Vector3.up * attackAimHeight;
        }

        private float GetProjectileSpeed()
        {
            if (bullet == null)
                return 0f;

            EnemyCPUBulletEntity bulletEntity =
                bullet.GetComponent<EnemyCPUBulletEntity>();
            return bulletEntity != null ? bulletEntity.Speed : 0f;
        }

        public void WanderMove()
        {
            Wander();
        }

        public void LookTarget()
        {
            if (m_cpuTarget == null)
                return;

            Vector3 dir = GetDirection(m_cpuTarget.position, transform.position);
            if (dir == Vector3.zero)
                return;

            RotateTurretToTarget();
        }


        public int GetBuffCount(int id)
        {
            if (buffList.ContainsKey(id))
                return buffList[id];

            return 0;
        }

        public bool HasBuff(int id)
        {
            return buffList.ContainsKey(id);
        }
    }
}
