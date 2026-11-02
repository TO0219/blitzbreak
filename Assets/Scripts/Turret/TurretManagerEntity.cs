using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace MyGame
{
    public enum NetworkProjectileType : byte
    {
        Cannon,
        Gatling,
        Missile
    }

    public class TurretManagerEntity : Entity
    {
        private const string GatlingStartStateName = "Gatling_Start";
        private const string GatlingLoopStateName = "Gatling_Loop";
        private const string GatlingEndStateName = "Gatling_End";

        [SerializeField] private PlayerEntity owner;
        [SerializeField] private Transform muzzleTransform;

        [Header("Turret Model Prefabs")]
        [SerializeField] private GameObject cannonModelPrefab;
        [SerializeField] private GameObject missileModelPrefab;
        [SerializeField] private GameObject gatlingModelPrefab;
        [SerializeField] private GameObject railgunModelPrefab;
        [SerializeField] private Vector3 missileModelLocalOffset = new Vector3(0f, 0.1f, 0f);
        [SerializeField] private Vector3 gatlingModelLocalOffset = new Vector3(0f, 0.1f, 0f);
        [SerializeField] private Vector3 railgunModelLocalOffset = new Vector3(0f, 0.1f, 0f);
        [SerializeField] private Vector3 railgunModelLocalScale = Vector3.one;

        [Header("Cannon")]
        [SerializeField] private GameObject cannonBulletPrefab;
        [SerializeField] private float cannonFireCooldown = 0.2f;

        [Header("Missile")]
        [SerializeField] private GameObject missilePrefab;
        [SerializeField] private float missileFireCooldown = 2.0f;
        [SerializeField, Range(0.05f, 0.45f)] private float missileLockScreenRadius = 0.18f;
        [SerializeField] private float missileLockMaxDistance = 180f;
        [SerializeField] private LayerMask missileLockMask = ~0;

        [Header("Gatling")]
        [SerializeField] private GameObject gatlingBulletPrefab;
        [SerializeField] private float gatlingFireCooldown = 0.1f;
        [SerializeField] private RuntimeAnimatorController gatlingAnimationController;
        [SerializeField] private string gatlingAnimationRootName = "MachineGunMotionBase";
        [SerializeField, Min(0.1f)] private float gatlingStartAnimationSpeed = 2.5f;

        [Header("Railgun")]
        [SerializeField, Min(0.1f)] private float railgunFireCooldown = 1.4f;
        [SerializeField, Min(1f)] private float railgunDamage = 150f;
        [SerializeField, Min(1f)] private float railgunRange = 220f;

        [Header("Timed Weapon Duration")]
        [SerializeField, Min(0.1f)] private float missileEquipDuration = 15f;
        [SerializeField, Min(0.1f)] private float gatlingEquipDuration = 15f;
        [SerializeField, Min(0.1f)] private float railgunEquipDuration = 10f;

        [Header("Fire Effects")]
        [SerializeField] private GameObject muzzleFlashPrefab;
        [SerializeField] private GameObject cannonSmokePrefab;
        [SerializeField] private GameObject missileLaunchSmokePrefab;

        private TurretBase currentTurret;
        private CannonTurret defaultTurret;
        private GatlingTurret airGatlingTurret;
        private MissileTurret airMissileTurret;
        private GameObject currentTurretModel;
        private Transform currentTurretModelPitchTransform;
        private readonly List<GameObject> defaultTurretModels = new List<GameObject>();
        private Transform[] currentMuzzleTransforms;
        private float timedWeaponDuration;
        private float timedWeaponRemaining;
        private bool m_initialized;
        private int m_lockTargetFrame = -1;
        private Transform m_cachedLockTarget;
        private readonly float[] m_networkNextFireTimes = new float[3];
        private readonly float[] m_networkBurstWindowEnds = new float[3];
        private readonly int[] m_networkBurstShotCounts = new int[3];
        private readonly List<EnemyEntity> m_cachedEnemies = new List<EnemyEntity>(16);
        private readonly List<EnemyCPUEntity> m_cachedCpus = new List<EnemyCPUEntity>(4);
        private readonly List<PlayerEntity> m_cachedPlayers = new List<PlayerEntity>(4);
        private readonly RaycastHit[] m_missileLineOfSightHits = new RaycastHit[64];
        private Animator[] m_gatlingAnimators;
        private bool m_gatlingAnimationPlaying;
        private float m_gatlingAnimationTime;
        private float m_gatlingStartClipLength = 1f;
        private float m_gatlingLoopClipLength = 1f;
        private float m_gatlingEndClipLength = 1f;
        private GatlingAnimationPhase m_gatlingAnimationPhase;

        private enum GatlingAnimationPhase
        {
            Idle,
            Start,
            Loop,
            End
        }

        public override void InitEntity()
        {
            EnsureInitialized();
        }

        public override void UpdateEntity()
        {
            EnsureInitialized();
            UpdateGatlingAnimationState();

            if (currentTurret == null) return;
            if (owner == null) return;

            UpdateTimedWeapon();
            SyncCurrentTurretModelPitch();

            if (!owner.CanControl)
            {
                ReleaseFire();
                return;
            }

            if (!owner.RefreshFireHeldState())
            {
                ReleaseFire();
            }

            if (owner.IsFireHeld)
                QueueFire();

            bool usedAirWeapon = false;

            if (owner.TryGetAirGatlingMuzzles(out Transform[] airMuzzles))
            {
                airGatlingTurret.SetMuzzleTransforms(airMuzzles);
                airGatlingTurret.OnFire(owner);
                usedAirWeapon = true;
            }

            if (owner.TryGetAirMissileMuzzles(out Transform[] airMissileMuzzles))
            {
                airMissileTurret.SetMuzzleTransforms(airMissileMuzzles);
                airMissileTurret.OnFire(owner);
                usedAirWeapon = true;
            }

            if (usedAirWeapon)
                return;

            currentTurret.OnFire(owner);
        }

        public bool TryEquipWeaponItem(string weaponId)
        {
            EnsureInitialized();

            switch (weaponId)
            {
                case "cannon":
                    return ResetToDefault();
                case "missile":
                    return EquipMissileTurret();
                case "gatling":
                    return EquipGatlingTurret();
                case "railgun":
                    return EquipRailgunTurret();
                default:
                    Debug.LogWarning($"[TurretManagerEntity] Unknown weapon item id: {weaponId}");
                    return false;
            }
        }

        public bool EquipTurret(TurretBase turret, GameObject modelPrefab)
        {
            return EquipTurret(turret, modelPrefab, true);
        }

        private bool EquipTurret(TurretBase turret, GameObject modelPrefab, bool clearTimedWeapon)
        {
            EnsureInitialized();

            if (owner == null)
            {
                Debug.LogWarning("[TurretManagerEntity] owner is not assigned.");
                return false;
            }

            if (muzzleTransform == null)
            {
                Debug.LogWarning("[TurretManagerEntity] muzzleTransform is not assigned.");
                return false;
            }

            if (turret == null)
            {
                Debug.LogWarning("[TurretManagerEntity] turret is null.");
                return false;
            }

            if (clearTimedWeapon)
                ClearTimedWeapon();

            if (currentTurret != null)
                currentTurret.OnUnequip(owner);

            ReplaceTurretModel(modelPrefab);

            currentMuzzleTransforms = new Transform[] { muzzleTransform };
            currentTurret = turret;
            currentTurret.OnEquip(owner);

            return true;
        }

        public bool EquipRailgunTurret()
        {
            EnsureInitialized();

            RailgunTurret railgun = new RailgunTurret(
                new Transform[] { muzzleTransform },
                railgunFireCooldown,
                railgunDamage,
                railgunRange,
                muzzleFlashPrefab
            );

            if (!EquipTurret(railgun, railgunModelPrefab))
                return false;

            StartTimedWeapon(railgunEquipDuration);
            currentMuzzleTransforms = GetCurrentModelMuzzles();
            return true;
        }

        public bool EquipMissileTurret()
        {
            EnsureInitialized();

            if (missilePrefab == null)
            {
                Debug.LogWarning("[TurretManagerEntity] missilePrefab is not assigned.");
                return false;
            }

            MissileTurret missile = new MissileTurret(
                missilePrefab,
                new Transform[] { muzzleTransform },
                missileFireCooldown,
                muzzleFlashPrefab,
                missileLaunchSmokePrefab
            );

            if (!EquipTurret(missile, missileModelPrefab))
                return false;

            StartTimedWeapon(missileEquipDuration);
            currentMuzzleTransforms = GetCurrentModelMuzzles();
            missile.SetMuzzleTransforms(currentMuzzleTransforms);
            return true;
        }

        public bool EquipGatlingTurret()
        {
            EnsureInitialized();

            if (gatlingBulletPrefab == null)
            {
                Debug.LogWarning("[TurretManagerEntity] gatlingBulletPrefab is not assigned.");
                return false;
            }

            GatlingTurret gatling = new GatlingTurret(
                gatlingBulletPrefab,
                new Transform[] { muzzleTransform },
                gatlingFireCooldown,
                muzzleFlashPrefab
            );

            if (!EquipTurret(gatling, gatlingModelPrefab))
                return false;

            StartTimedWeapon(gatlingEquipDuration);
            currentMuzzleTransforms = GetCurrentModelMuzzles();
            gatling.SetMuzzleTransforms(currentMuzzleTransforms);
            return true;
        }

        public bool ResetToDefault()
        {
            EnsureInitialized();
            ReleaseFire();
            ClearTimedWeapon();

            defaultTurret = new CannonTurret(
                cannonBulletPrefab,
                muzzleTransform,
                cannonFireCooldown,
                muzzleFlashPrefab,
                cannonSmokePrefab
            );

            if (currentTurret != null)
                currentTurret.OnUnequip(owner);

            if (currentTurretModel != null)
            {
                Destroy(currentTurretModel);
                currentTurretModel = null;
                currentTurretModelPitchTransform = null;
            }
            ClearGatlingAnimationCache();

            SetDefaultTurretModelsActive(true);

            currentMuzzleTransforms = new Transform[] { muzzleTransform };
            currentTurret = defaultTurret;
            currentTurret.OnEquip(owner);
            return true;
        }

        public void QueueFire()
        {
            EnsureInitialized();

            bool queuedAirWeapon = false;

            if (owner != null && airMissileTurret != null && owner.TryGetAirMissileMuzzles(out Transform[] airMissileMuzzles))
            {
                airMissileTurret.SetMuzzleTransforms(airMissileMuzzles);
                airMissileTurret.QueueFire();
                queuedAirWeapon = true;
            }

            if (owner != null && airGatlingTurret != null && owner.TryGetAirGatlingMuzzles(out Transform[] airMuzzles))
            {
                airGatlingTurret.SetMuzzleTransforms(airMuzzles);
                airGatlingTurret.QueueFire();
                queuedAirWeapon = true;
            }

            if (queuedAirWeapon)
                return;

            if (currentTurret is CannonTurret cannon)
                cannon.QueueFire();
            else if (currentTurret is MissileTurret missile)
                missile.QueueFire();
            else if (currentTurret is GatlingTurret gatling)
                gatling.QueueFire();
            else if (currentTurret is RailgunTurret railgun)
                railgun.QueueFire();
        }

        public void ReleaseFire()
        {
            if (airGatlingTurret != null)
                airGatlingTurret.ReleaseFire();

            if (airMissileTurret != null)
                airMissileTurret.ReleaseFire();

            if (currentTurret is CannonTurret cannon)
                cannon.ReleaseFire();
            else if (currentTurret is GatlingTurret gatling)
                gatling.ReleaseFire();
            else if (currentTurret is MissileTurret missile)
                missile.ReleaseFire();
            else if (currentTurret is RailgunTurret railgun)
                railgun.ReleaseFire();

            UpdateGatlingAnimationState();
        }

        public Transform GetPrimaryMuzzleTransform()
        {
            if (currentMuzzleTransforms != null)
            {
                for (int i = 0; i < currentMuzzleTransforms.Length; i++)
                {
                    if (currentMuzzleTransforms[i] != null)
                        return currentMuzzleTransforms[i];
                }
            }

            return muzzleTransform;
        }

        public bool TryGetAimRay(out Ray aimRay)
        {
            Transform targetMuzzle = GetPrimaryMuzzleTransform();
            if (targetMuzzle == null)
            {
                aimRay = default;
                return false;
            }

            aimRay = new Ray(targetMuzzle.position, targetMuzzle.forward);
            return true;
        }

        public bool IsMissileActive
        {
            get
            {
                EnsureInitialized();

                if (owner != null && owner.IsAirMode)
                    return IsAirProjectileAvailable(
                        NetworkProjectileType.Missile,
                        owner.CurrentAirModeStock) &&
                        airMissileTurret != null &&
                        owner.TryGetAirMissileMuzzles(out _);

                if (currentTurret is MissileTurret)
                    return true;

                return false;
            }
        }

        public float MissileLockScreenRadius => missileLockScreenRadius;

        public bool TryGetMissileLockTarget(out Transform target)
        {
            EnsureInitialized();
            target = null;

            if (!IsMissileActive || owner == null || owner.IsRespawning)
                return false;

            if (m_lockTargetFrame == Time.frameCount)
            {
                target = m_cachedLockTarget;
                return target != null;
            }

            m_lockTargetFrame = Time.frameCount;
            m_cachedLockTarget = FindMissileLockTarget();
            target = m_cachedLockTarget;
            return target != null;
        }

        public ulong GetMissileLockTargetNetworkObjectId()
        {
            if (!TryGetMissileLockTarget(out Transform target) || target == null)
                return ulong.MaxValue;

            NetworkObject targetNetworkObject = target.GetComponentInParent<NetworkObject>();
            return targetNetworkObject != null && targetNetworkObject.IsSpawned
                ? targetNetworkObject.NetworkObjectId
                : ulong.MaxValue;
        }

        public bool TryFireRailgunFromNetwork(
            Vector3 origin,
            Vector3 direction,
            out Vector3 endPoint)
        {
            EnsureInitialized();
            endPoint = origin;

            if (owner == null ||
                owner.IsRespawning ||
                owner.IsAirMode ||
                currentTurret is not RailgunTurret railgun)
            {
                return false;
            }

            Transform primaryMuzzle = GetPrimaryMuzzleTransform();
            if (primaryMuzzle == null ||
                (origin - primaryMuzzle.position).sqrMagnitude > 25f ||
                direction.sqrMagnitude < 0.001f)
            {
                return false;
            }

            float angle = Vector3.Angle(primaryMuzzle.forward, direction.normalized);
            if (angle > 75f)
                return false;

            return railgun.TryFireAuthoritative(owner, origin, direction, out endPoint);
        }

        public bool SpawnNetworkProjectile(
            NetworkProjectileType projectileType,
            Vector3 position,
            Quaternion rotation,
            ulong ownerClientId,
            ulong targetNetworkObjectId)
        {
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
                return false;

            EnsureInitialized();

            GameObject projectilePrefab = projectileType switch
            {
                NetworkProjectileType.Cannon => cannonBulletPrefab,
                NetworkProjectileType.Gatling => gatlingBulletPrefab,
                NetworkProjectileType.Missile => missilePrefab,
                _ => null
            };

            if (projectilePrefab == null)
                return false;

            GameObject projectileObject = Instantiate(projectilePrefab, position, rotation);
            NetworkObject projectileNetworkObject = projectileObject.GetComponent<NetworkObject>();
            if (projectileNetworkObject == null)
            {
                Debug.LogError($"NetworkObject not found on projectile prefab: {projectilePrefab.name}");
                Destroy(projectileObject);
                return false;
            }

            projectileNetworkObject.Spawn(true);

            switch (projectileType)
            {
                case NetworkProjectileType.Cannon:
                    projectileObject.GetComponent<PlayerBulletEntity>()
                        ?.SetupNetworkOwner(ownerClientId, owner);
                    break;
                case NetworkProjectileType.Gatling:
                    projectileObject.GetComponent<GatlingBulletEntity>()
                        ?.SetupNetworkOwner(ownerClientId, owner);
                    break;
                case NetworkProjectileType.Missile:
                    projectileObject.GetComponent<MissileBulletEntity>()
                        ?.SetupNetworkOwner(
                            ownerClientId,
                            targetNetworkObjectId,
                            owner);
                    break;
            }

            return true;
        }

        public bool TryAuthorizeNetworkProjectile(
            NetworkProjectileType projectileType,
            Vector3 requestedPosition,
            Quaternion requestedRotation,
            int authorizedAirStock,
            ulong requestedTargetNetworkObjectId,
            out Vector3 spawnPosition,
            out Quaternion spawnRotation,
            out ulong targetNetworkObjectId)
        {
            EnsureInitialized();

            spawnPosition = requestedPosition;
            spawnRotation = requestedRotation;
            targetNetworkObjectId = ulong.MaxValue;

            if (owner == null || owner.IsRespawning)
                return false;

            bool isAuthorizedAirWeapon =
                authorizedAirStock > 0 &&
                IsAirProjectileAvailable(projectileType, authorizedAirStock);
            Transform[] validMuzzles = isAuthorizedAirWeapon
                ? GetAirNetworkMuzzles(projectileType)
                : GetGroundNetworkMuzzles(projectileType);

            int burstSize;
            if (isAuthorizedAirWeapon)
            {
                if (validMuzzles == null || validMuzzles.Length == 0)
                    return false;

                if ((requestedPosition - owner.transform.position).sqrMagnitude >
                    625f)
                {
                    return false;
                }

                spawnPosition = requestedPosition;
                burstSize = Mathf.Max(1, validMuzzles.Length);
            }
            else
            {
                if (validMuzzles == null || validMuzzles.Length == 0)
                    return false;

                if ((requestedPosition - owner.transform.position).sqrMagnitude >
                    225f)
                {
                    return false;
                }

                spawnPosition = requestedPosition;
                burstSize = Mathf.Max(1, validMuzzles.Length);
            }

            if (!TryConsumeNetworkFireWindow(
                    projectileType,
                    GetNetworkFireCooldown(projectileType),
                    burstSize))
            {
                return false;
            }

            if (projectileType == NetworkProjectileType.Missile)
            {
                targetNetworkObjectId = ValidateMissileTarget(
                    requestedTargetNetworkObjectId,
                    spawnPosition,
                    spawnRotation);
            }

            return true;
        }

        private Transform[] GetGroundNetworkMuzzles(
            NetworkProjectileType projectileType)
        {
            if (owner != null && owner.IsAirMode)
                return null;

            string expectedTurretId = projectileType switch
            {
                NetworkProjectileType.Cannon => "cannon",
                NetworkProjectileType.Gatling => "gatling",
                NetworkProjectileType.Missile => "missile",
                _ => string.Empty
            };

            if (currentTurret == null || currentTurret.TurretId != expectedTurretId)
                return null;

            return currentMuzzleTransforms;
        }

        private Transform[] GetAirNetworkMuzzles(
            NetworkProjectileType projectileType)
        {
            if (owner == null || !owner.IsAirMode)
                return null;

            if (projectileType == NetworkProjectileType.Gatling &&
                owner.TryGetAirGatlingMuzzles(out Transform[] gatlingMuzzles))
            {
                return gatlingMuzzles;
            }

            if (projectileType == NetworkProjectileType.Missile &&
                owner.TryGetAirMissileMuzzles(out Transform[] missileMuzzles))
            {
                return missileMuzzles;
            }

            return null;
        }

        private static bool IsAirProjectileAvailable(
            NetworkProjectileType projectileType,
            int airStock)
        {
            return projectileType switch
            {
                NetworkProjectileType.Gatling => airStock == 1 || airStock == 3,
                NetworkProjectileType.Missile => airStock == 2 || airStock == 3,
                _ => false
            };
        }

        private Transform FindClosestMuzzle(
            Transform[] muzzles,
            Vector3 requestedPosition,
            out float closestDistance)
        {
            Transform closest = null;
            float closestSqrDistance = float.MaxValue;

            for (int i = 0; i < muzzles.Length; i++)
            {
                Transform muzzle = muzzles[i];
                if (muzzle == null)
                    continue;

                float sqrDistance =
                    (muzzle.position - requestedPosition).sqrMagnitude;
                if (sqrDistance >= closestSqrDistance)
                    continue;

                closest = muzzle;
                closestSqrDistance = sqrDistance;
            }

            closestDistance = closest != null
                ? Mathf.Sqrt(closestSqrDistance)
                : float.MaxValue;
            return closest;
        }

        private bool TryConsumeNetworkFireWindow(
            NetworkProjectileType projectileType,
            float cooldown,
            int burstSize)
        {
            int index = (int)projectileType;
            if (index < 0 || index >= m_networkNextFireTimes.Length)
                return false;

            float now = Time.time;
            if (now >= m_networkNextFireTimes[index])
            {
                m_networkNextFireTimes[index] = now + Mathf.Max(0.02f, cooldown);
                m_networkBurstWindowEnds[index] = now + 0.04f;
                m_networkBurstShotCounts[index] = 1;
                return true;
            }

            if (now <= m_networkBurstWindowEnds[index] &&
                m_networkBurstShotCounts[index] < Mathf.Max(1, burstSize))
            {
                m_networkBurstShotCounts[index]++;
                return true;
            }

            return false;
        }

        private float GetNetworkFireCooldown(NetworkProjectileType projectileType)
        {
            float baseCooldown = projectileType switch
            {
                NetworkProjectileType.Cannon => cannonFireCooldown,
                NetworkProjectileType.Gatling => gatlingFireCooldown,
                NetworkProjectileType.Missile => missileFireCooldown,
                _ => 1f
            };
            float attackSpeed = owner != null
                ? Mathf.Max(0.01f, owner.Stats.GetStat(StatKey.FireInterval))
                : 1f;
            return baseCooldown / attackSpeed;
        }

        private ulong ValidateMissileTarget(
            ulong requestedTargetNetworkObjectId,
            Vector3 spawnPosition,
            Quaternion spawnRotation)
        {
            if (requestedTargetNetworkObjectId == ulong.MaxValue ||
                NetworkManager.Singleton == null ||
                NetworkManager.Singleton.SpawnManager == null ||
                !NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(
                    requestedTargetNetworkObjectId,
                    out NetworkObject targetObject) ||
                targetObject == null ||
                targetObject.transform.IsChildOf(owner.transform) ||
                owner.transform.IsChildOf(targetObject.transform))
            {
                return ulong.MaxValue;
            }

            Vector3 targetAimPoint =
                MissileBulletEntity.GetTargetAimPoint(targetObject.transform);
            Vector3 toTarget = targetAimPoint - spawnPosition;
            float distance = toTarget.magnitude;
            if (distance <= 0.01f || distance > missileLockMaxDistance)
                return ulong.MaxValue;

            Vector3 fireDirection = spawnRotation * Vector3.forward;
            if (Vector3.Dot(fireDirection.normalized, toTarget / distance) < 0.35f)
                return ulong.MaxValue;

            if (Physics.Linecast(
                    spawnPosition,
                    targetAimPoint,
                    out RaycastHit hit,
                    missileLockMask,
                    QueryTriggerInteraction.Ignore))
            {
                Transform hitTransform = hit.collider != null
                    ? hit.collider.transform
                    : null;
                if (hitTransform != null &&
                    !hitTransform.IsChildOf(targetObject.transform) &&
                    !targetObject.transform.IsChildOf(hitTransform))
                {
                    return ulong.MaxValue;
                }
            }

            return requestedTargetNetworkObjectId;
        }

        private Transform FindMissileLockTarget()
        {
            Camera targetCamera = ResolveOwnerCamera();
            if (targetCamera == null)
                return null;

            Transform bestTarget = null;
            float bestScore = float.MaxValue;

            RefreshMissileTargetCache();

            for (int i = 0; i < m_cachedEnemies.Count; i++)
            {
                EnemyEntity enemy = m_cachedEnemies[i];
                if (enemy == null ||
                    enemy.IsDestroy ||
                    enemy.CurrentHealth <= 0f)
                {
                    continue;
                }

                TrySelectMissileTarget(
                    enemy.transform,
                    targetCamera,
                    ref bestTarget,
                    ref bestScore);
            }

            for (int i = 0; i < m_cachedCpus.Count; i++)
            {
                EnemyCPUEntity cpu = m_cachedCpus[i];
                if (cpu == null ||
                    cpu.CPUCurrentHealth <= 0f ||
                    cpu.IsRespawning)
                {
                    continue;
                }

                TrySelectMissileTarget(
                    cpu.transform,
                    targetCamera,
                    ref bestTarget,
                    ref bestScore);
            }

            for (int i = 0; i < m_cachedPlayers.Count; i++)
            {
                PlayerEntity player = m_cachedPlayers[i];
                if (player == null ||
                    player == owner ||
                    player.IsRespawning ||
                    player.CurrentHealth <= 0f)
                {
                    continue;
                }

                TrySelectMissileTarget(
                    player.transform,
                    targetCamera,
                    ref bestTarget,
                    ref bestScore);
            }

            return bestTarget;
        }

        private void RefreshMissileTargetCache()
        {
            if (EntityManager.Instance != null)
            {
                EntityManager.Instance.GetEntities(m_cachedEnemies);
                EntityManager.Instance.GetEntities(m_cachedCpus);
                EntityManager.Instance.GetEntities(m_cachedPlayers);
                AppendMissingMissileTargetsFromScene();
                return;
            }

            m_cachedEnemies.Clear();
            m_cachedCpus.Clear();
            m_cachedPlayers.Clear();
            AppendMissingMissileTargetsFromScene();
        }

        private void AppendMissingMissileTargetsFromScene()
        {
            EnemyEntity[] enemies = FindObjectsByType<EnemyEntity>();
            for (int i = 0; i < enemies.Length; i++)
                AddUniqueTarget(m_cachedEnemies, enemies[i]);

            EnemyCPUEntity[] cpus = FindObjectsByType<EnemyCPUEntity>();
            for (int i = 0; i < cpus.Length; i++)
                AddUniqueTarget(m_cachedCpus, cpus[i]);

            PlayerEntity[] players = FindObjectsByType<PlayerEntity>();
            for (int i = 0; i < players.Length; i++)
                AddUniqueTarget(m_cachedPlayers, players[i]);
        }

        private static void AddUniqueTarget<T>(List<T> targets, T target)
            where T : Entity
        {
            if (targets == null ||
                target == null ||
                targets.Contains(target))
            {
                return;
            }

            targets.Add(target);
        }

        private void TrySelectMissileTarget(
            Transform candidate,
            Camera targetCamera,
            ref Transform bestTarget,
            ref float bestScore)
        {
            if (candidate == null || !candidate.gameObject.activeInHierarchy)
                return;

            float lockRadius = Mathf.Max(1f, Screen.height * missileLockScreenRadius);
            if (!TryGetMissileLockAimPoint(
                    candidate,
                    targetCamera,
                    lockRadius,
                    out Vector3 aimPoint,
                    out float screenDistance,
                    out float distance))
            {
                return;
            }

            if (distance <= 0.01f || distance > Mathf.Max(1f, missileLockMaxDistance))
                return;

            if (screenDistance > lockRadius)
                return;

            if (!HasMissileLineOfSight(
                    GetMissileLineOfSightOrigin(),
                    aimPoint,
                    candidate))
            {
                return;
            }

            float score =
                screenDistance / lockRadius +
                distance / Mathf.Max(1f, missileLockMaxDistance) * 0.15f;
            if (score >= bestScore)
                return;

            bestScore = score;
            bestTarget = candidate;
        }

        private Vector3 GetMissileLineOfSightOrigin()
        {
            if (owner != null && owner.TurretTransform != null)
                return owner.TurretTransform.position;

            Transform primaryMuzzle = GetPrimaryMuzzleTransform();
            if (primaryMuzzle != null)
                return primaryMuzzle.position;

            return owner != null ? owner.transform.position : transform.position;
        }

        private bool TryGetMissileLockAimPoint(
            Transform candidate,
            Camera targetCamera,
            float lockRadius,
            out Vector3 aimPoint,
            out float screenDistance,
            out float targetDistance)
        {
            aimPoint = MissileBulletEntity.GetTargetAimPoint(candidate);
            screenDistance = float.MaxValue;
            targetDistance = 0f;

            if (candidate == null || targetCamera == null)
                return false;

            Vector2 screenCenter = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            bool hasPoint = TryEvaluateMissileLockPoint(
                aimPoint,
                targetCamera,
                screenCenter,
                ref aimPoint,
                ref screenDistance,
                ref targetDistance);

            if (TryGetMissileTargetBounds(candidate, out Bounds bounds))
            {
                Vector3 min = bounds.min;
                Vector3 max = bounds.max;
                Vector3 center = bounds.center;

                hasPoint |= TryEvaluateMissileLockPoint(
                    center,
                    targetCamera,
                    screenCenter,
                    ref aimPoint,
                    ref screenDistance,
                    ref targetDistance);

                hasPoint |= TryEvaluateMissileLockPoint(
                    new Vector3(center.x, max.y, center.z),
                    targetCamera,
                    screenCenter,
                    ref aimPoint,
                    ref screenDistance,
                    ref targetDistance);
                hasPoint |= TryEvaluateMissileLockPoint(
                    new Vector3(center.x, min.y, center.z),
                    targetCamera,
                    screenCenter,
                    ref aimPoint,
                    ref screenDistance,
                    ref targetDistance);
                hasPoint |= TryEvaluateMissileLockPoint(
                    new Vector3(min.x, center.y, center.z),
                    targetCamera,
                    screenCenter,
                    ref aimPoint,
                    ref screenDistance,
                    ref targetDistance);
                hasPoint |= TryEvaluateMissileLockPoint(
                    new Vector3(max.x, center.y, center.z),
                    targetCamera,
                    screenCenter,
                    ref aimPoint,
                    ref screenDistance,
                    ref targetDistance);
                hasPoint |= TryEvaluateMissileLockPoint(
                    new Vector3(center.x, center.y, min.z),
                    targetCamera,
                    screenCenter,
                    ref aimPoint,
                    ref screenDistance,
                    ref targetDistance);
                hasPoint |= TryEvaluateMissileLockPoint(
                    new Vector3(center.x, center.y, max.z),
                    targetCamera,
                    screenCenter,
                    ref aimPoint,
                    ref screenDistance,
                    ref targetDistance);

                for (int x = 0; x < 2; x++)
                {
                    for (int y = 0; y < 2; y++)
                    {
                        for (int z = 0; z < 2; z++)
                        {
                            Vector3 corner = new Vector3(
                                x == 0 ? min.x : max.x,
                                y == 0 ? min.y : max.y,
                                z == 0 ? min.z : max.z);
                            hasPoint |= TryEvaluateMissileLockPoint(
                                corner,
                                targetCamera,
                                screenCenter,
                                ref aimPoint,
                                ref screenDistance,
                                ref targetDistance);
                        }
                    }
                }
            }

            return hasPoint && screenDistance <= lockRadius;
        }

        private static bool TryEvaluateMissileLockPoint(
            Vector3 point,
            Camera targetCamera,
            Vector2 screenCenter,
            ref Vector3 bestPoint,
            ref float bestScreenDistance,
            ref float bestTargetDistance)
        {
            Vector3 screenPoint = targetCamera.WorldToScreenPoint(point);
            if (screenPoint.z <= 0f)
                return false;

            float screenDistance = Vector2.Distance(
                new Vector2(screenPoint.x, screenPoint.y),
                screenCenter);
            if (screenDistance >= bestScreenDistance)
                return true;

            bestScreenDistance = screenDistance;
            bestPoint = point;
            bestTargetDistance = Vector3.Distance(targetCamera.transform.position, point);
            return true;
        }

        private static bool TryGetMissileTargetBounds(Transform candidate, out Bounds bounds)
        {
            bounds = default;
            if (candidate == null)
                return false;

            bool hasBounds = false;
            Renderer[] renderers = candidate.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer targetRenderer = renderers[i];
                if (targetRenderer == null ||
                    !targetRenderer.enabled ||
                    !targetRenderer.gameObject.activeInHierarchy)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = targetRenderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(targetRenderer.bounds);
                }
            }

            Collider[] colliders = candidate.GetComponentsInChildren<Collider>(true);
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

        private bool HasMissileLineOfSight(
            Vector3 origin,
            Vector3 aimPoint,
            Transform candidate)
        {
            Vector3 direction = aimPoint - origin;
            float distance = direction.magnitude;
            if (distance <= 0.01f)
                return true;

            int hitCount = Physics.RaycastNonAlloc(
                origin,
                direction / distance,
                m_missileLineOfSightHits,
                distance,
                missileLockMask,
                QueryTriggerInteraction.Ignore);

            float nearestDistance = float.MaxValue;
            Transform nearestTransform = null;
            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = m_missileLineOfSightHits[i];
                Collider hitCollider = hit.collider;
                if (hitCollider == null)
                    continue;

                Transform hitTransform = hitCollider.transform;
                if (hitTransform.IsChildOf(owner.transform))
                    continue;

                if (hit.distance >= nearestDistance)
                    continue;

                nearestDistance = hit.distance;
                nearestTransform = hitTransform;
            }

            return nearestTransform == null ||
                   nearestTransform == candidate ||
                   nearestTransform.IsChildOf(candidate);
        }

        private Camera ResolveOwnerCamera()
        {
            if (owner == null)
                return null;

            if (owner.CameraTransform != null)
            {
                Camera ownerCamera = owner.CameraTransform.GetComponent<Camera>();
                if (ownerCamera != null && ownerCamera.isActiveAndEnabled)
                    return ownerCamera;
            }

            Camera childCamera = owner.GetComponentInChildren<Camera>(true);
            if (childCamera != null && childCamera.isActiveAndEnabled)
                return childCamera;

            return Camera.main;
        }

        public string CurrentTurretLabel
        {
            get
            {
                if (currentTurret == null) return "なし";

                if (owner != null && owner.IsAirMode)
                {
                    switch (owner.CurrentAirModeStock)
                    {
                        case 1:
                            return "ガトリング";
                        case 2:
                            return "ミサイル";
                        case 3:
                            return "ガトリング＋ミサイル";
                    }
                }

                switch (currentTurret.TurretId)
                {
                    case "cannon":
                        return "通常砲";
                    case "missile":
                        return "ミサイル";
                    case "gatling":
                        return "ガトリング";
                    case "railgun":
                        return "レールガン";
                    default:
                        return currentTurret.TurretId.ToUpperInvariant();
                }
            }
        }

        public bool HasTimedWeapon =>
            currentTurret != null &&
            currentTurret.TurretId != "cannon" &&
            timedWeaponDuration > 0f;

        public float TimedWeaponRemaining => HasTimedWeapon ? Mathf.Max(0f, timedWeaponRemaining) : 0f;

        public float TimedWeaponDuration => HasTimedWeapon ? timedWeaponDuration : 0f;

        public int CurrentWeaponType
        {
            get
            {
                if (currentTurret == null)
                    return 0;

                switch (currentTurret.TurretId)
                {
                    case "missile":
                        return 1;
                    case "gatling":
                        return 2;
                    case "railgun":
                        return 3;
                    default:
                        return 0;
                }
            }
        }

        public void ApplyNetworkWeaponState(int weaponType, float remaining)
        {
            EnsureInitialized();
            int clampedType = Mathf.Clamp(weaponType, 0, 3);

            if (CurrentWeaponType != clampedType)
            {
                switch (clampedType)
                {
                    case 1:
                        EquipMissileTurret();
                        break;
                    case 2:
                        EquipGatlingTurret();
                        break;
                    case 3:
                        EquipRailgunTurret();
                        break;
                    default:
                        ResetToDefault();
                        break;
                }
            }

            if (clampedType == 0)
                return;

            timedWeaponDuration = clampedType switch
            {
                1 => missileEquipDuration,
                2 => gatlingEquipDuration,
                3 => railgunEquipDuration,
                _ => timedWeaponDuration
            };
            timedWeaponRemaining = Mathf.Clamp(
                remaining,
                0f,
                timedWeaponDuration);
        }

        public GameObject CannonModelPrefab => cannonModelPrefab;

        private void EnsureInitialized()
        {
            if (owner == null)
                owner = GetComponent<PlayerEntity>();

            if (muzzleTransform == null && owner != null)
                muzzleTransform = owner.MuzzleTransform;

            if (m_initialized)
                return;

            defaultTurret = new CannonTurret(
                cannonBulletPrefab,
                muzzleTransform,
                cannonFireCooldown,
                muzzleFlashPrefab,
                cannonSmokePrefab
            );
            currentTurret = defaultTurret;
            currentMuzzleTransforms = new Transform[] { muzzleTransform };

            CacheDefaultTurretModels();
            SetDefaultTurretModelsActive(true);

            if (cannonModelPrefab != null && defaultTurretModels.Count == 0 && currentTurretModel == null)
            {
                ReplaceTurretModel(cannonModelPrefab);
            }

            if (currentTurret != null)
                currentTurret.OnEquip(owner);

            airGatlingTurret = new GatlingTurret(
                gatlingBulletPrefab,
                new Transform[] { muzzleTransform },
                gatlingFireCooldown,
                muzzleFlashPrefab
            );

            airMissileTurret = new MissileTurret(
                missilePrefab,
                new Transform[] { muzzleTransform },
                missileFireCooldown,
                muzzleFlashPrefab,
                missileLaunchSmokePrefab
            );

            m_initialized = true;
        }

        private void CacheDefaultTurretModels()
        {
            defaultTurretModels.Clear();

            if (owner == null || owner.TurretTransform == null)
                return;

            foreach (Transform child in owner.TurretTransform)
            {
                if (!child.gameObject.activeSelf)
                    continue;

                if (IsLegacyInlineCannonModel(child))
                {
                    child.gameObject.SetActive(false);
                    continue;
                }

                defaultTurretModels.Add(child.gameObject);
            }
        }

        private bool IsLegacyInlineCannonModel(Transform child)
        {
            if (child == null)
                return false;

            return child.name == "Cylinder";
        }

        private void SetDefaultTurretModelsActive(bool active)
        {
            for (int i = 0; i < defaultTurretModels.Count; i++)
            {
                if (defaultTurretModels[i] != null)
                    defaultTurretModels[i].SetActive(active);
            }
        }

        private void ReplaceTurretModel(GameObject modelPrefab)
        {
            if (currentTurretModel != null)
            {
                Destroy(currentTurretModel);
                currentTurretModel = null;
                currentTurretModelPitchTransform = null;
            }
            ClearGatlingAnimationCache();

            SetDefaultTurretModelsActive(false);

            if (modelPrefab == null || owner == null || owner.TurretTransform == null)
                return;

            currentTurretModel = Instantiate(modelPrefab, owner.TurretTransform);
            currentTurretModel.transform.localPosition = GetTurretModelLocalOffset(modelPrefab);
            currentTurretModel.transform.localRotation = Quaternion.identity;
            currentTurretModel.transform.localScale = GetTurretModelLocalScale(modelPrefab);
            owner.ApplyMachineVariantMaterials(currentTurretModel);
            currentTurretModelPitchTransform = FindChildRecursive(currentTurretModel.transform, "Barrel");
            SyncCurrentTurretModelPitch();
            CacheGatlingAnimation();
        }

        private Vector3 GetTurretModelLocalOffset(GameObject modelPrefab)
        {
            if (modelPrefab == missileModelPrefab)
                return missileModelLocalOffset;

            if (modelPrefab == gatlingModelPrefab)
                return gatlingModelLocalOffset;

            if (modelPrefab == railgunModelPrefab)
                return railgunModelLocalOffset;

            return Vector3.zero;
        }

        private Vector3 GetTurretModelLocalScale(GameObject modelPrefab)
        {
            if (modelPrefab == railgunModelPrefab)
                return railgunModelLocalScale;

            return new Vector3(4f, 4f, 4f);
        }

        private void SyncCurrentTurretModelPitch()
        {
            if (currentTurretModel == null || owner == null || owner.BarrelPitchTransform == null)
                return;

            if (currentTurretModelPitchTransform == null)
                currentTurretModelPitchTransform = FindChildRecursive(currentTurretModel.transform, "Barrel");

            if (currentTurretModelPitchTransform == null)
                return;

            currentTurretModel.transform.localRotation = Quaternion.identity;
            currentTurretModelPitchTransform.localRotation = owner.BarrelPitchTransform.localRotation;
        }

        private Transform[] GetCurrentModelMuzzles()
        {
            if (currentTurretModel == null)
                return new Transform[] { muzzleTransform };

            Transform leftMuzzle = FindChildRecursive(currentTurretModel.transform, "Muzzle_L");
            Transform rightMuzzle = FindChildRecursive(currentTurretModel.transform, "Muzzle_R");
            Transform singleMuzzle = FindChildRecursive(currentTurretModel.transform, "muzzleTransform");

            if (singleMuzzle != null)
                return new Transform[] { singleMuzzle };

            if (leftMuzzle == null || rightMuzzle == null)
            {
                Debug.LogWarning("[TurretManagerEntity] Muzzle_L or Muzzle_R was not found. Using fallback muzzleTransform.");
                return new Transform[] { muzzleTransform };
            }

            return new Transform[] { leftMuzzle, rightMuzzle };
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

        private void CacheGatlingAnimation()
        {
            EnsureGatlingAnimator();

            m_gatlingAnimators = currentTurretModel != null
                ? currentTurretModel.GetComponentsInChildren<Animator>(true)
                : null;
            m_gatlingAnimationPlaying = false;
            m_gatlingAnimationTime = 0f;
            m_gatlingStartClipLength = GetGatlingAnimationClipLength("SpinUp");
            m_gatlingLoopClipLength = GetGatlingAnimationClipLength("FireLoop");
            m_gatlingEndClipLength = GetGatlingAnimationClipLength("SpinDown");
            m_gatlingAnimationPhase = GatlingAnimationPhase.Idle;
            SetGatlingAnimationPlaying(false);
        }

        private void EnsureGatlingAnimator()
        {
            if (currentTurretModel == null || gatlingAnimationController == null)
                return;

            Transform animationRoot = FindChildRecursive(
                currentTurretModel.transform,
                gatlingAnimationRootName);

            if (animationRoot == null)
                animationRoot = currentTurretModel.transform;

            Animator animator = animationRoot.GetComponent<Animator>();
            if (animator == null)
                animator = animationRoot.gameObject.AddComponent<Animator>();

            animator.runtimeAnimatorController = gatlingAnimationController;
            animator.enabled = true;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.updateMode = AnimatorUpdateMode.Normal;
            animator.speed = 0f;
            animator.Rebind();
        }

        private void ClearGatlingAnimationCache()
        {
            m_gatlingAnimators = null;
            m_gatlingAnimationPlaying = false;
            m_gatlingAnimationTime = 0f;
            m_gatlingStartClipLength = 1f;
            m_gatlingLoopClipLength = 1f;
            m_gatlingEndClipLength = 1f;
            m_gatlingAnimationPhase = GatlingAnimationPhase.Idle;
        }

        private void UpdateGatlingAnimationState()
        {
            bool shouldPlay =
                owner != null &&
                owner.CanControl &&
                !owner.IsRespawning &&
                currentTurret is GatlingTurret &&
                owner.IsFireHeld;

            SetGatlingAnimationPlaying(shouldPlay);
        }

        private void SetGatlingAnimationPlaying(bool isPlaying)
        {
            if (m_gatlingAnimators == null)
                return;

            UpdateGatlingAnimationPhase(isPlaying);
            PlayGatlingAnimationAt(m_gatlingAnimationTime);
        }

        private void UpdateGatlingAnimationPhase(bool fireHeld)
        {
            if (fireHeld)
            {
                if (!m_gatlingAnimationPlaying ||
                    m_gatlingAnimationPhase == GatlingAnimationPhase.Idle ||
                    m_gatlingAnimationPhase == GatlingAnimationPhase.End)
                {
                    m_gatlingAnimationPhase = GatlingAnimationPhase.Start;
                    m_gatlingAnimationTime = 0f;
                }

                if (m_gatlingAnimationPhase == GatlingAnimationPhase.Start)
                {
                    m_gatlingAnimationTime +=
                        GetGatlingNormalizedDelta(m_gatlingStartClipLength) *
                        Mathf.Max(0.1f, gatlingStartAnimationSpeed);
                    if (m_gatlingAnimationTime >= 1f)
                    {
                        m_gatlingAnimationPhase = GatlingAnimationPhase.Loop;
                        m_gatlingAnimationTime = 0f;
                    }
                }
                else
                {
                    m_gatlingAnimationPhase = GatlingAnimationPhase.Loop;
                    m_gatlingAnimationTime = Mathf.Repeat(
                        m_gatlingAnimationTime + GetGatlingNormalizedDelta(m_gatlingLoopClipLength),
                        1f);
                }

                m_gatlingAnimationPlaying = true;
                return;
            }

            if (m_gatlingAnimationPhase == GatlingAnimationPhase.Start ||
                m_gatlingAnimationPhase == GatlingAnimationPhase.Loop)
            {
                m_gatlingAnimationPhase = GatlingAnimationPhase.End;
                m_gatlingAnimationTime = 0f;
                m_gatlingAnimationPlaying = true;
            }

            if (m_gatlingAnimationPhase != GatlingAnimationPhase.End)
            {
                m_gatlingAnimationPhase = GatlingAnimationPhase.Idle;
                m_gatlingAnimationPlaying = false;
                m_gatlingAnimationTime = 0f;
                return;
            }

            m_gatlingAnimationTime += GetGatlingNormalizedDelta(m_gatlingEndClipLength);
            if (m_gatlingAnimationTime >= 1f)
            {
                m_gatlingAnimationPhase = GatlingAnimationPhase.Idle;
                m_gatlingAnimationPlaying = false;
                m_gatlingAnimationTime = 0f;
            }
        }

        private void PlayGatlingAnimationAt(float normalizedTime)
        {
            string stateName = GetGatlingStateName();

            for (int i = 0; i < m_gatlingAnimators.Length; i++)
            {
                Animator animator = m_gatlingAnimators[i];
                if (animator == null ||
                    !animator.enabled ||
                    !animator.gameObject.activeInHierarchy)
                {
                    continue;
                }

                animator.speed = 0f;
                animator.Play(stateName, 0, Mathf.Clamp01(normalizedTime));
                animator.Update(0f);
            }
        }

        private string GetGatlingStateName()
        {
            return m_gatlingAnimationPhase switch
            {
                GatlingAnimationPhase.Start => GatlingStartStateName,
                GatlingAnimationPhase.Loop => GatlingLoopStateName,
                GatlingAnimationPhase.End => GatlingEndStateName,
                _ => GatlingStartStateName
            };
        }

        private float GetGatlingNormalizedDelta(float clipLength)
        {
            return Time.deltaTime / Mathf.Max(0.01f, clipLength);
        }

        private float GetGatlingAnimationClipLength(string clipName)
        {
            if (gatlingAnimationController == null ||
                gatlingAnimationController.animationClips == null)
            {
                return 1f;
            }

            for (int i = 0; i < gatlingAnimationController.animationClips.Length; i++)
            {
                AnimationClip clip = gatlingAnimationController.animationClips[i];
                if (clip != null &&
                    clip.name == clipName &&
                    clip.length > 0f)
                {
                    return clip.length;
                }
            }

            return 1f;
        }

        private void UpdateTimedWeapon()
        {
            if (!HasTimedWeapon) return;

            timedWeaponRemaining -= Time.deltaTime;
            if (timedWeaponRemaining > 0f) return;

            timedWeaponRemaining = 0f;
            ResetToDefault();
        }

        private void StartTimedWeapon(float duration)
        {
            timedWeaponDuration = Mathf.Max(0.1f, duration);
            timedWeaponRemaining = timedWeaponDuration;
        }

        private void ClearTimedWeapon()
        {
            timedWeaponDuration = 0f;
            timedWeaponRemaining = 0f;
        }
    }
}
