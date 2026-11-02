using Unity.Netcode;
using UnityEngine;

namespace MyGame
{
    [RequireComponent(typeof(Collider))]
    [RequireComponent(typeof(Rigidbody))]
    public class ItemEntity : Entity
    {
        [Header("Item")]
        [SerializeField] private string itemId = "Item";
        [SerializeField] private int amount = 1;

        [Header("Drop")]
        [SerializeField] private bool applyDropOnStart = true;
        [SerializeField] private bool fallToGround = true;
        [SerializeField] private float fallSpeed = 2.5f;
        [SerializeField] private float groundOffset = 0.35f;
        [SerializeField] private float groundProbeHeight = 0.2f;
        [SerializeField] private float groundProbeDistance = 80f;
        [SerializeField, Min(0f)] private float groundSweepRadius = 0.18f;
        [SerializeField] private float minimumWorldY = 0f;
        [SerializeField] private float maximumGroundY = 2f;
        [SerializeField] private float scatterGravity = 12f;
        [SerializeField] private float scatterHorizontalDamping = 1.5f;

        [Header("Ground Check")]
        [SerializeField] private LayerMask groundLayer = ~0;

        [Header("Pickup")]
        [SerializeField] private float pickupRadius = 2.5f;
        [SerializeField] private LayerMask pickupLayer = ~0;
        [SerializeField, Min(0.02f)] private float pickupScanInterval = 0.1f;

        [Header("Idle Motion")]
        [SerializeField] private bool enableIdleMotion = true;
        [SerializeField] private float floatAmplitude = 0.1f;
        [SerializeField] private float floatSpeed = 2.0f;
        [SerializeField] private float spinSpeed = 90.0f;

        [Header("Weapon Drop Models")]
        [SerializeField] private GameObject railgunDropModelPrefab;
        [SerializeField] private Vector3 railgunDropModelLocalPosition = Vector3.zero;
        [SerializeField] private Vector3 railgunDropModelLocalRotation = Vector3.zero;
        [SerializeField] private Vector3 railgunDropModelLocalScale = new Vector3(0.25f, 0.25f, 0.25f);

        private readonly Collider[] m_pickupHits = new Collider[16];
        private Rigidbody m_rigidbody;
        private Collider[] m_colliders;
        private NetworkObject m_networkObject;
        private NetworkBuffItemController m_networkController;
        private bool m_hasDropped;
        private bool m_landed;
        private bool m_scatterActive;
        private bool m_hasLastGroundPoint;
        private float m_baseY;
        private float m_idleTime;
        private Vector3 m_lastGroundPoint;
        private Vector3 m_scatterVelocity;
        private float m_pickupScanTimer;
        private GameObject m_railgunDropModel;
        private readonly System.Collections.Generic.List<PlayerEntity> m_cachedPlayers =
            new System.Collections.Generic.List<PlayerEntity>(4);
        private readonly System.Collections.Generic.List<EnemyCPUEntity> m_cachedCpus =
            new System.Collections.Generic.List<EnemyCPUEntity>(4);
        private readonly RaycastHit[] m_groundHits = new RaycastHit[64];

        public string ItemId => itemId;
        public int Amount => amount;
        public bool CanPickup => gameObject.activeInHierarchy && amount > 0;

        public override void InitEntity()
        {
            CacheComponents();
            UpdateModelVisibility();
        }

        private void Awake()
        {
            CacheComponents();
            UpdateModelVisibility();
        }

        private void Start()
        {
            if (m_hasDropped)
                return;

            if (applyDropOnStart && !m_hasDropped)
            {
                ApplyDrop();
                return;
            }

            m_landed = true;
            m_baseY = transform.position.y;
        }

        private void Update()
        {
            if (!CanRunAuthoritativeSimulation())
                return;

            UpdateFall();
            TryPickupNearbyPlayer();
            UpdateIdleMotion();
        }

        public void SetItemId(string id)
        {
            itemId = id;
            UpdateModelVisibility();
        }

        public bool Pickup(PlayerEntity player)
        {
            if (!CanRunAuthoritativeSimulation()) return false;
            if (!CanPickup) return false;
            if (player == null) return false;
            if (player.IsAirMode) return false;

            TurretManagerEntity turretManager = player.GetComponent<TurretManagerEntity>();
            if (turretManager == null)
            {
                Debug.LogWarning("[ItemEntity] TurretManagerEntity was not found.");
                return false;
            }

            if (!turretManager.TryEquipWeaponItem(itemId))
                return false;

            NotifyPlayerPickup(player);
            global::AudioManager.Instance?.PlayCueAtPosition(
                global::AudioCue.WeaponPickup,
                transform.position);
            ConsumeItem();
            return true;
        }

        public bool Pickup(EnemyCPUEntity cpu)
        {
            if (!CanRunAuthoritativeSimulation()) return false;
            if (!CanPickup || cpu == null || cpu.CPUCurrentHealth <= 0f)
                return false;

            if (!cpu.TryEquipWeaponItem(itemId))
                return false;

            global::AudioManager.Instance?.PlayCueAtPosition(
                global::AudioCue.WeaponPickup,
                transform.position);
            ConsumeItem();
            return true;
        }

        private void NotifyPlayerPickup(PlayerEntity player)
        {
            if (player == null)
                return;

            string itemName = GetWeaponDisplayName(itemId);
            PlayerNetworkController networkController =
                player.GetComponentInParent<PlayerNetworkController>();
            if (networkController != null &&
                networkController.IsSpawned &&
                networkController.IsServer)
            {
                networkController.ShowPickupNoticeOwnerClient("武器", itemName);
                return;
            }

            BattleHudUIEntity.ShowPickupNoticeFor(player, "武器", itemName);
        }

        private static string GetWeaponDisplayName(string id)
        {
            switch (id)
            {
                case "missile":
                    return "ミサイル";
                case "gatling":
                    return "ガトリング";
                case "railgun":
                    return "レイルガン";
                case "cannon":
                    return "通常砲";
                default:
                    return string.IsNullOrWhiteSpace(id) ? "武器" : id;
            }
        }

        public void ApplyDrop()
        {
            CacheComponents();

            m_hasDropped = true;
            m_landed = false;
            m_scatterActive = false;
            m_hasLastGroundPoint = false;
            m_scatterVelocity = Vector3.zero;
            m_idleTime = 0.0f;

            if (m_rigidbody != null)
            {
                StopRigidbodyMotion();
            }

            if (!fallToGround)
                Land();
        }

        public void ApplyDropScatter(Vector3 velocity)
        {
            ApplyDrop();

            m_scatterVelocity = velocity;
            m_scatterActive = velocity.sqrMagnitude > 0.001f;

            if (TryFindGroundBelow(out RaycastHit hit))
                CacheGroundPoint(hit.point);
        }

        private void OnTriggerEnter(Collider other)
        {
            TryPickupFromCollider(other);
        }

        private void OnTriggerStay(Collider other)
        {
            TryPickupFromCollider(other);
        }

        private void UpdateModelVisibility()
        {
            EnsureRailgunDropModel();

            SetModelActive("TankWeapon_Missile", itemId == "missile");
            SetModelActive("TankWeapon_MachineGun", itemId == "gatling");
            SetModelActive("TankWeapon_Railgun", itemId == "railgun");
        }

        private void EnsureRailgunDropModel()
        {
            if (m_railgunDropModel != null)
                return;

            Transform existing = transform.Find("TankWeapon_Railgun");
            if (existing != null)
            {
                m_railgunDropModel = existing.gameObject;
                return;
            }

            if (railgunDropModelPrefab == null)
                return;

            m_railgunDropModel = Instantiate(railgunDropModelPrefab, transform);
            m_railgunDropModel.name = "TankWeapon_Railgun";
            m_railgunDropModel.transform.localPosition = railgunDropModelLocalPosition;
            m_railgunDropModel.transform.localRotation =
                Quaternion.Euler(railgunDropModelLocalRotation);
            m_railgunDropModel.transform.localScale = railgunDropModelLocalScale;
        }

        private void SetModelActive(string modelName, bool active)
        {
            Transform model = transform.Find(modelName);
            if (model != null)
                model.gameObject.SetActive(active);
        }

        private void TryPickupNearbyPlayer()
        {
            if (!CanPickup) return;
            if (pickupRadius <= 0f) return;

            m_pickupScanTimer -= Time.deltaTime;
            if (m_pickupScanTimer > 0f)
                return;

            m_pickupScanTimer = Mathf.Max(0.02f, pickupScanInterval);

            if (TryPickupNearestRecipientByDistance())
                return;

            int hitCount = Physics.OverlapSphereNonAlloc(
                transform.position,
                pickupRadius,
                m_pickupHits,
                pickupLayer,
                QueryTriggerInteraction.Collide
            );

            for (int i = 0; i < hitCount; i++)
            {
                if (TryPickupFromCollider(m_pickupHits[i]))
                    return;
            }
        }

        private bool TryPickupNearestRecipientByDistance()
        {
            if (EntityManager.Instance != null)
            {
                EntityManager.Instance.GetEntities(m_cachedPlayers);
                EntityManager.Instance.GetEntities(m_cachedCpus);
            }
            else
            {
                m_cachedPlayers.Clear();
                PlayerEntity[] scenePlayers = FindObjectsByType<PlayerEntity>();
                for (int i = 0; i < scenePlayers.Length; i++)
                    m_cachedPlayers.Add(scenePlayers[i]);

                m_cachedCpus.Clear();
                EnemyCPUEntity[] sceneCpus = FindObjectsByType<EnemyCPUEntity>();
                for (int i = 0; i < sceneCpus.Length; i++)
                    m_cachedCpus.Add(sceneCpus[i]);
            }

            float sqrPickupRadius = pickupRadius * pickupRadius;
            float nearestPlayerSqr = sqrPickupRadius;
            float nearestCpuSqr = sqrPickupRadius;
            PlayerEntity nearestPlayer = null;
            EnemyCPUEntity nearestCpu = null;

            for (int i = 0; i < m_cachedPlayers.Count; i++)
            {
                PlayerEntity player = m_cachedPlayers[i];
                if (player == null) continue;
                if (!player.gameObject.activeInHierarchy) continue;
                if (player.IsRespawning) continue;
                if (player.IsAirMode) continue;

                Vector3 offset = player.transform.position - transform.position;
                offset.y = 0f;
                float sqrDistance = offset.sqrMagnitude;

                if (sqrDistance > nearestPlayerSqr)
                    continue;

                nearestPlayerSqr = sqrDistance;
                nearestPlayer = player;
            }

            for (int i = 0; i < m_cachedCpus.Count; i++)
            {
                EnemyCPUEntity cpu = m_cachedCpus[i];
                if (cpu == null || !cpu.gameObject.activeInHierarchy)
                    continue;
                if (cpu.CPUCurrentHealth <= 0f)
                    continue;

                Vector3 offset = cpu.transform.position - transform.position;
                offset.y = 0f;
                float sqrDistance = offset.sqrMagnitude;
                if (sqrDistance > nearestCpuSqr)
                    continue;

                nearestCpuSqr = sqrDistance;
                nearestCpu = cpu;
            }

            if (nearestPlayer != null && nearestPlayerSqr <= nearestCpuSqr)
            {
                if (Pickup(nearestPlayer))
                    return true;

                return nearestCpu != null && Pickup(nearestCpu);
            }

            if (nearestCpu != null)
            {
                if (Pickup(nearestCpu))
                    return true;

                return nearestPlayer != null && Pickup(nearestPlayer);
            }

            return false;
        }

        private bool TryPickupFromCollider(Collider other)
        {
            if (other == null) return false;
            if (IsOwnCollider(other)) return false;

            PlayerEntity player = other.GetComponentInParent<PlayerEntity>();
            if (player != null)
            {
                if (!CanRunAuthoritativeSimulation())
                {
                    if (m_networkController == null)
                        m_networkController =
                            GetComponent<NetworkBuffItemController>();
                    m_networkController?.RequestPickup(player);
                    return true;
                }

                return Pickup(player);
            }

            EnemyCPUEntity cpu = other.GetComponentInParent<EnemyCPUEntity>();
            if (cpu == null)
                return false;

            return Pickup(cpu);
        }

        private void UpdateFall()
        {
            if (!m_hasDropped || m_landed || !fallToGround) return;

            if (m_scatterActive)
            {
                UpdateScatterFall();
                return;
            }

            if (!TryFindGroundBelow(out RaycastHit hit))
            {
                transform.position += Vector3.down * fallSpeed * Time.deltaTime;
                ClampToMinimumWorldY();
                RecoverIfDroppedThroughGround();
                return;
            }

            CacheGroundPoint(hit.point);
            Vector3 targetPosition = hit.point + Vector3.up * groundOffset;
            if (transform.position.y <= targetPosition.y)
            {
                transform.position = targetPosition;
                Land();
                return;
            }

            transform.position = Vector3.MoveTowards(transform.position, targetPosition, fallSpeed * Time.deltaTime);
            ClampToMinimumWorldY();
        }

        private void UpdateScatterFall()
        {
            m_scatterVelocity += Vector3.down * scatterGravity * Time.deltaTime;

            Vector3 horizontalVelocity = new Vector3(m_scatterVelocity.x, 0f, m_scatterVelocity.z);
            horizontalVelocity = Vector3.MoveTowards(
                horizontalVelocity,
                Vector3.zero,
                scatterHorizontalDamping * Time.deltaTime
            );

            m_scatterVelocity.x = horizontalVelocity.x;
            m_scatterVelocity.z = horizontalVelocity.z;

            Vector3 currentPosition = transform.position;
            Vector3 nextPosition = currentPosition + m_scatterVelocity * Time.deltaTime;
            if (TryLandBeforePassingGround(currentPosition, nextPosition))
                return;

            transform.position = nextPosition;
            ClampToMinimumWorldY();

            if (!TryFindGroundBelow(out RaycastHit hit))
            {
                RecoverIfDroppedThroughGround();
                return;
            }

            CacheGroundPoint(hit.point);
            Vector3 targetPosition = hit.point + Vector3.up * groundOffset;
            if (transform.position.y > targetPosition.y)
                return;

            transform.position = targetPosition;
            m_scatterActive = false;
            m_scatterVelocity = Vector3.zero;
            Land();
        }

        private void Land()
        {
            m_landed = true;

            if (m_rigidbody != null)
            {
                StopRigidbodyMotion();
            }

            m_baseY = transform.position.y;
        }

        private void ClampToMinimumWorldY()
        {
            float minimumDropPositionY = GetMinimumDropPositionY();
            if (transform.position.y >= minimumDropPositionY)
                return;

            Vector3 position = transform.position;
            position.y = minimumDropPositionY;
            transform.position = position;
            m_scatterActive = false;
            m_scatterVelocity = Vector3.zero;
            Land();
        }

        private float GetMinimumDropPositionY()
        {
            return minimumWorldY + Mathf.Max(0f, groundOffset);
        }

        private void CacheGroundPoint(Vector3 groundPoint)
        {
            m_lastGroundPoint = groundPoint;
            m_hasLastGroundPoint = true;
        }

        private void RecoverIfDroppedThroughGround()
        {
            if (!m_hasLastGroundPoint)
                return;

            float recoverThreshold = Mathf.Max(1f, groundOffset + 0.5f);
            if (transform.position.y >= m_lastGroundPoint.y - recoverThreshold)
                return;

            transform.position = m_lastGroundPoint + Vector3.up * groundOffset;
            m_scatterActive = false;
            m_scatterVelocity = Vector3.zero;
            Land();
        }

        private bool TryLandBeforePassingGround(
            Vector3 currentPosition,
            Vector3 nextPosition)
        {
            if (m_scatterVelocity.y > 0f)
                return false;

            if (TryFindGroundBetween(currentPosition, nextPosition, out RaycastHit crossedHit))
                return LandOnGroundHit(crossedHit);

            if (TryFindGroundBelow(currentPosition, out RaycastHit currentHit) &&
                TryLandOnCrossedGround(currentPosition, nextPosition, currentHit))
            {
                return true;
            }

            return TryFindGroundBelow(nextPosition, out RaycastHit nextHit) &&
                   TryLandOnCrossedGround(currentPosition, nextPosition, nextHit);
        }

        private bool TryFindGroundBetween(
            Vector3 currentPosition,
            Vector3 nextPosition,
            out RaycastHit hit)
        {
            Vector3 displacement = nextPosition - currentPosition;
            float distance = displacement.magnitude;
            if (distance <= 0.0001f)
            {
                hit = default;
                return false;
            }

            Vector3 direction = displacement / distance;
            int hitCount = Physics.RaycastNonAlloc(
                currentPosition,
                direction,
                m_groundHits,
                distance,
                groundLayer,
                QueryTriggerInteraction.Ignore);

            if (TryGetGroundHit(m_groundHits, hitCount, out hit))
                return true;

            if (groundSweepRadius <= 0f)
                return false;

            hitCount = Physics.SphereCastNonAlloc(
                currentPosition + Vector3.up * Mathf.Max(0.02f, groundSweepRadius),
                groundSweepRadius,
                direction,
                m_groundHits,
                distance + groundSweepRadius,
                groundLayer,
                QueryTriggerInteraction.Ignore);

            return TryGetGroundHit(m_groundHits, hitCount, out hit);
        }

        private bool TryLandOnCrossedGround(
            Vector3 currentPosition,
            Vector3 nextPosition,
            RaycastHit hit)
        {
            CacheGroundPoint(hit.point);

            Vector3 targetPosition = hit.point + Vector3.up * groundOffset;
            if (currentPosition.y < targetPosition.y ||
                nextPosition.y > targetPosition.y)
            {
                return false;
            }

            transform.position = targetPosition;
            m_scatterActive = false;
            m_scatterVelocity = Vector3.zero;
            Land();
            return true;
        }

        private bool LandOnGroundHit(RaycastHit hit)
        {
            CacheGroundPoint(hit.point);
            transform.position = hit.point + Vector3.up * groundOffset;
            m_scatterActive = false;
            m_scatterVelocity = Vector3.zero;
            Land();
            return true;
        }

        private bool TryFindGroundBelow(out RaycastHit hit)
        {
            return TryFindGroundBelow(transform.position, out hit);
        }

        private bool TryFindGroundBelow(Vector3 position, out RaycastHit hit)
        {
            Vector3 rayOrigin = position + Vector3.up * Mathf.Max(groundProbeHeight, groundSweepRadius + 0.05f);
            int hitCount = Physics.RaycastNonAlloc(
                rayOrigin,
                Vector3.down,
                m_groundHits,
                groundProbeDistance,
                groundLayer,
                QueryTriggerInteraction.Ignore);

            if (TryGetGroundHit(m_groundHits, hitCount, out hit))
                return true;

            if (groundSweepRadius > 0f)
            {
                hitCount = Physics.SphereCastNonAlloc(
                    rayOrigin,
                    groundSweepRadius,
                    Vector3.down,
                    m_groundHits,
                    groundProbeDistance,
                    groundLayer,
                    QueryTriggerInteraction.Ignore);

                if (TryGetGroundHit(m_groundHits, hitCount, out hit))
                    return true;
            }

            return TryFindGroundFromAbove(position, out hit);
        }

        private bool TryFindGroundFromAbove(out RaycastHit hit)
        {
            return TryFindGroundFromAbove(transform.position, out hit);
        }

        private bool TryFindGroundFromAbove(Vector3 position, out RaycastHit hit)
        {
            float recoveryHeight = Mathf.Max(groundProbeDistance, groundProbeHeight + 1f);
            Vector3 rayOrigin = position + Vector3.up * recoveryHeight;
            float probeDistance = recoveryHeight + groundProbeDistance;
            int hitCount = Physics.RaycastNonAlloc(
                rayOrigin,
                Vector3.down,
                m_groundHits,
                probeDistance,
                groundLayer,
                QueryTriggerInteraction.Ignore);

            if (TryGetGroundHit(m_groundHits, hitCount, out hit))
                return true;

            if (groundSweepRadius <= 0f)
                return false;

            hitCount = Physics.SphereCastNonAlloc(
                rayOrigin,
                groundSweepRadius,
                Vector3.down,
                m_groundHits,
                probeDistance,
                groundLayer,
                QueryTriggerInteraction.Ignore);

            return TryGetGroundHit(m_groundHits, hitCount, out hit);
        }

        private void UpdateIdleMotion()
        {
            if (!enableIdleMotion) return;
            if (!m_landed) return;
            if (!gameObject.activeInHierarchy) return;

            m_idleTime += Time.deltaTime;

            Vector3 position = transform.position;
            position.y = m_baseY + Mathf.Sin(m_idleTime * floatSpeed) * floatAmplitude;
            transform.position = position;

            transform.Rotate(0.0f, spinSpeed * Time.deltaTime, 0.0f, Space.World);
        }

        private bool TryGetGroundHit(RaycastHit[] hits, int hitCount, out RaycastHit groundHit)
        {
            groundHit = default;
            float bestGroundY = float.PositiveInfinity;
            float bestDistance = float.PositiveInfinity;

            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = hits[i];
                if (hit.collider == null || IsOwnCollider(hit.collider)) continue;
                if (hit.normal.y < 0.35f) continue;
                if (maximumGroundY > minimumWorldY && hit.point.y > maximumGroundY) continue;
                if (hit.collider.GetComponentInParent<DestructibleBuilding>() != null) continue;
                if (hit.collider.GetComponentInParent<PlayerEntity>() != null) continue;
                if (hit.collider.GetComponentInParent<EnemyEntity>() != null) continue;
                if (hit.collider.GetComponentInParent<EnemyCPUEntity>() != null) continue;
                if (hit.collider.GetComponentInParent<BuffItemEntity>() != null) continue;
                if (hit.collider.GetComponentInParent<ItemEntity>() != null) continue;
                if (ProjectileSweepUtility.IsWorldBorder(hit.collider) &&
                    !ProjectileSweepUtility.IsBattlePlane(hit.collider))
                {
                    continue;
                }
                if (hit.point.y > bestGroundY + 0.01f) continue;
                if (Mathf.Abs(hit.point.y - bestGroundY) <= 0.01f &&
                    hit.distance >= bestDistance)
                {
                    continue;
                }

                groundHit = hit;
                bestGroundY = hit.point.y;
                bestDistance = hit.distance;
            }

            return groundHit.collider != null;
        }

        private bool IsOwnCollider(Collider target)
        {
            if (m_colliders == null) return false;

            for (int i = 0; i < m_colliders.Length; i++)
            {
                if (m_colliders[i] == target)
                    return true;
            }

            return false;
        }

        private void CacheComponents()
        {
            if (m_rigidbody == null)
                m_rigidbody = GetComponent<Rigidbody>();

            if (m_networkObject == null)
                m_networkObject = GetComponent<NetworkObject>();

            if (m_networkController == null)
                m_networkController = GetComponent<NetworkBuffItemController>();

            m_colliders = GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < m_colliders.Length; i++)
            {
                if (m_colliders[i] != null)
                    m_colliders[i].isTrigger = true;
            }

            if (m_rigidbody != null)
            {
                StopRigidbodyMotion();
            }
        }

        private bool CanRunAuthoritativeSimulation()
        {
            if (NetworkManager.Singleton == null ||
                !NetworkManager.Singleton.IsListening ||
                m_networkObject == null ||
                !m_networkObject.IsSpawned)
            {
                return true;
            }

            return NetworkManager.Singleton.IsServer;
        }

        private void ConsumeItem()
        {
            amount = 0;

            if (m_networkController != null &&
                m_networkController.TryDespawnFromServer())
            {
                return;
            }

            if (m_networkObject != null &&
                m_networkObject.IsSpawned &&
                NetworkManager.Singleton != null &&
                NetworkManager.Singleton.IsServer)
            {
                m_networkObject.Despawn(true);
                return;
            }

            gameObject.SetActive(false);
        }

        private void StopRigidbodyMotion()
        {
            if (m_rigidbody == null)
                return;

            if (!m_rigidbody.isKinematic)
            {
                m_rigidbody.linearVelocity = Vector3.zero;
                m_rigidbody.angularVelocity = Vector3.zero;
            }

            m_rigidbody.useGravity = false;
            m_rigidbody.isKinematic = true;
        }

        private void Reset()
        {
            Collider[] colliders = GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null)
                    colliders[i].isTrigger = true;
            }

            Rigidbody body = GetComponent<Rigidbody>();
            if (body != null)
            {
                body.useGravity = false;
                body.isKinematic = true;
                body.mass = 1.0f;
                body.linearDamping = 1.5f;
                body.angularDamping = 1.5f;
                body.interpolation = RigidbodyInterpolation.Interpolate;
                body.collisionDetectionMode = CollisionDetectionMode.Continuous;
            }
        }
    }
}
