using Unity.Netcode;
using UnityEngine;

namespace MyGame
{
    /// <summary>
    /// マップ上に出現するバフアイテムのEntity
    /// 常にカメラの方向を向くBillboard表示で、
    /// プレイヤーが触れると永続バフを付与して消える
    /// SpriteRenderer・Collider（IsTrigger）が必要
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    [RequireComponent(typeof(Collider))]
    public class BuffItemEntity : Entity
    {
        //********************************************************
        // 変数の定義
        //********************************************************

        /// <summary>このアイテムが持つバフの定義</summary>
        private BuffBase buff;

        /// <summary>Billboard用のカメラ参照</summary>
        private Camera mainCamera;

        /// <summary>このアイテムのCollider</summary>
        private Collider col;
        private NetworkObject m_networkObject;
        private PlayerEntity m_billboardTargetPlayer;
        private float m_billboardTargetRefreshTimer;
        private readonly RaycastHit[] m_groundHits = new RaycastHit[64];

        [Header("Fall")]
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
        [SerializeField, Min(0.02f)] private float billboardTargetRefreshInterval = 0.2f;

        private bool m_isGrounded;
        private bool m_scatterActive;
        private bool m_isCollected;
        private bool m_hasLastGroundPoint;
        private Vector3 m_lastGroundPoint;
        private Vector3 m_scatterVelocity;

        //********************************************************
        // セットアップ
        //********************************************************

        /// <summary>
        /// アイテムの初期設定
        /// EnemyEntityのDie()からスポーン直後に呼び出す
        /// </summary>
        /// <param name="buff">付与するバフ</param>
        public void Setup(BuffBase buff)
        {
            this.buff = buff;
        }

        public void ApplyScatter(Vector3 velocity)
        {
            m_scatterVelocity = velocity;
            m_scatterActive = velocity.sqrMagnitude > 0.001f;
            m_isGrounded = false;
            m_hasLastGroundPoint = false;

            if (TryFindGroundBelow(out RaycastHit hit))
                CacheGroundPoint(hit.point);
        }

        //********************************************************
        // Entityライフサイクル
        //********************************************************

        /// <summary>
        /// 初期化処理
        /// カメラとColliderを取得する
        /// </summary>
        public override void InitEntity()
        {
            mainCamera = Camera.main;
            col = GetComponent<Collider>();
            m_networkObject = GetComponent<NetworkObject>();

            // ColliderをTriggerに設定
            if (col != null)
                col.isTrigger = true;
        }

        /// <summary>
        /// 毎フレームの更新処理
        /// カメラの方向を向くBillboard処理を行う
        /// </summary>
        public override void UpdateEntity()
        {
            if (m_networkObject == null ||
                !m_networkObject.IsSpawned ||
                NetworkManager.Singleton == null ||
                NetworkManager.Singleton.IsServer)
            {
                UpdateFall();
            }
            UpdateBillboard();
        }

        //********************************************************
        // 内部処理
        //********************************************************

        /// <summary>
        /// Billboard処理
        /// 毎フレームカメラの方向を向くように回転を更新する
        /// </summary>
        private void UpdateBillboard()
        {
            if (!TryResolveBillboardCamera(out Camera billboardCamera)) return;

            // カメラの方向を向く
            Transform cameraTransform = billboardCamera.transform;
            transform.LookAt(
                transform.position + cameraTransform.rotation * Vector3.forward,
                cameraTransform.rotation * Vector3.up);
        }

        private bool TryResolveBillboardCamera(out Camera billboardCamera)
        {
            if (TryResolveBillboardPlayer(out PlayerEntity targetPlayer) &&
                targetPlayer.CameraTransform != null)
            {
                billboardCamera = targetPlayer.CameraTransform.GetComponent<Camera>();
                if (billboardCamera != null &&
                    billboardCamera.isActiveAndEnabled &&
                    billboardCamera.gameObject.activeInHierarchy)
                {
                    mainCamera = billboardCamera;
                    return true;
                }
            }

            if (mainCamera != null &&
                mainCamera.isActiveAndEnabled &&
                mainCamera.gameObject.activeInHierarchy)
            {
                billboardCamera = mainCamera;
                return true;
            }

            mainCamera = Camera.main;
            if (mainCamera == null ||
                !mainCamera.isActiveAndEnabled ||
                !mainCamera.gameObject.activeInHierarchy)
            {
                mainCamera = FindAnyObjectByType<Camera>();
            }

            billboardCamera = mainCamera;
            return billboardCamera != null &&
                   billboardCamera.isActiveAndEnabled &&
                   billboardCamera.gameObject.activeInHierarchy;
        }

        private bool TryResolveBillboardPlayer(out PlayerEntity targetPlayer)
        {
            m_billboardTargetRefreshTimer -= Time.deltaTime;
            if (m_billboardTargetPlayer != null &&
                m_billboardTargetRefreshTimer > 0f &&
                IsLocalOrSoloPlayer(m_billboardTargetPlayer))
            {
                targetPlayer = m_billboardTargetPlayer;
                return true;
            }

            m_billboardTargetRefreshTimer = billboardTargetRefreshInterval;
            m_billboardTargetPlayer = FindLocalPlayer();
            targetPlayer = m_billboardTargetPlayer;
            return targetPlayer != null;
        }

        private static PlayerEntity FindLocalPlayer()
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            {
                NetworkObject localPlayerObject = NetworkManager.Singleton.LocalClient?.PlayerObject;
                if (localPlayerObject != null)
                {
                    PlayerEntity localPlayer = localPlayerObject.GetComponent<PlayerEntity>();
                    if (localPlayer != null)
                        return localPlayer;
                }
            }

            PlayerEntity[] players = FindObjectsByType<PlayerEntity>();
            for (int i = 0; i < players.Length; i++)
            {
                if (IsLocalOrSoloPlayer(players[i]))
                    return players[i];
            }

            return null;
        }

        private static bool IsLocalOrSoloPlayer(PlayerEntity player)
        {
            if (player == null)
                return false;

            NetworkObject netObj = player.GetComponent<NetworkObject>();
            return netObj == null || !netObj.IsSpawned || netObj.IsOwner;
        }

        private void UpdateFall()
        {
            if (!fallToGround || m_isGrounded) return;

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
                m_isGrounded = true;
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
            m_isGrounded = true;
            m_scatterActive = false;
            m_scatterVelocity = Vector3.zero;
        }

        private void ClampToMinimumWorldY()
        {
            float minimumDropPositionY = GetMinimumDropPositionY();
            if (transform.position.y >= minimumDropPositionY)
                return;

            Vector3 position = transform.position;
            position.y = minimumDropPositionY;
            transform.position = position;
            m_isGrounded = true;
            m_scatterActive = false;
            m_scatterVelocity = Vector3.zero;
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
            m_isGrounded = true;
            m_scatterActive = false;
            m_scatterVelocity = Vector3.zero;
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
                ~0,
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
                ~0,
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
            m_isGrounded = true;
            m_scatterActive = false;
            m_scatterVelocity = Vector3.zero;
            return true;
        }

        private bool LandOnGroundHit(RaycastHit hit)
        {
            CacheGroundPoint(hit.point);
            transform.position = hit.point + Vector3.up * groundOffset;
            m_isGrounded = true;
            m_scatterActive = false;
            m_scatterVelocity = Vector3.zero;
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
                ~0,
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
                    ~0,
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
                ~0,
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
                ~0,
                QueryTriggerInteraction.Ignore);

            return TryGetGroundHit(m_groundHits, hitCount, out hit);
        }

        private bool TryGetGroundHit(RaycastHit[] hits, int hitCount, out RaycastHit groundHit)
        {
            groundHit = default;
            float bestGroundY = float.PositiveInfinity;
            float bestDistance = float.PositiveInfinity;

            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = hits[i];
                if (hit.collider == null || hit.collider == col) continue;
                if (hit.normal.y < 0.35f) continue;
                if (maximumGroundY > minimumWorldY && hit.point.y > maximumGroundY) continue;
                if (hit.collider.GetComponentInParent<DestructibleBuilding>() != null) continue;
                if (ProjectileSweepUtility.IsWorldBorder(hit.collider) &&
                    !ProjectileSweepUtility.IsBattlePlane(hit.collider))
                {
                    continue;
                }
                if (hit.collider.GetComponentInParent<PlayerEntity>() != null) continue;
                if (hit.collider.GetComponentInParent<EnemyEntity>() != null) continue;
                if (hit.collider.GetComponentInParent<EnemyCPUEntity>() != null) continue;
                if (hit.collider.GetComponentInParent<BuffItemEntity>() != null) continue;
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

        /// <summary>
        /// Colliderのトリガー判定
        /// プレイヤーが触れたときにバフを付与して自身を消す
        /// </summary>
        /// <param name="other">触れたCollider</param>
        private void OnTriggerEnter(Collider other)
        {
            if (buff == null || m_isCollected || IsDestroy) return;
            if (m_networkObject != null &&
                m_networkObject.IsSpawned &&
                NetworkManager.Singleton != null &&
                !NetworkManager.Singleton.IsServer)
            {
                PlayerEntity player = other.GetComponentInParent<PlayerEntity>();
                if (player != null)
                {
                    NetworkBuffItemController networkController =
                        GetComponent<NetworkBuffItemController>();
                    networkController?.RequestPickup(player);
                }

                return;
            }

            PlayerEntity targetPlayer = other.GetComponentInParent<PlayerEntity>();
            if (targetPlayer != null && Pickup(targetPlayer))
                return;

            BuffManagerEntity buffManager =
                other.GetComponentInParent<BuffManagerEntity>();
            if (buffManager == null)
                return;

            m_isCollected = true;
            if (col != null)
                col.enabled = false;

            buffManager.ApplyBuff(buff);
            PlayPickupSound();
            PlayerNetworkController networkControllerForBuff =
                other.GetComponentInParent<PlayerNetworkController>();
            networkControllerForBuff?.SyncBuffStateFromServer();

            if (m_networkObject != null &&
                m_networkObject.IsSpawned &&
                NetworkManager.Singleton != null &&
                NetworkManager.Singleton.IsServer)
            {
                m_networkObject.Despawn(true);
            }
            else
            {
                OnDestroyEntity();
            }
        }

        public bool Pickup(PlayerEntity player)
        {
            if (player == null || buff == null || m_isCollected || IsDestroy)
                return false;

            BuffManagerEntity buffManager =
                player.GetComponentInParent<BuffManagerEntity>();
            if (buffManager == null)
                return false;

            m_isCollected = true;
            if (col != null)
                col.enabled = false;

            buffManager.ApplyBuff(buff);
            PlayPickupSound();
            NotifyPlayerPickup(player);

            PlayerNetworkController networkController =
                player.GetComponentInParent<PlayerNetworkController>();
            networkController?.SyncBuffStateFromServer();

            if (m_networkObject != null &&
                m_networkObject.IsSpawned &&
                NetworkManager.Singleton != null &&
                NetworkManager.Singleton.IsServer)
            {
                m_networkObject.Despawn(true);
            }
            else
            {
                OnDestroyEntity();
            }

            return true;
        }

        private void NotifyPlayerPickup(PlayerEntity player)
        {
            if (player == null || buff == null)
                return;

            string itemName = GetBuffDisplayName(buff.BuffId);
            PlayerNetworkController networkController =
                player.GetComponentInParent<PlayerNetworkController>();
            if (networkController != null &&
                networkController.IsSpawned &&
                networkController.IsServer)
            {
                networkController.ShowPickupNoticeOwnerClient("バフ", itemName);
                return;
            }

            BattleHudUIEntity.ShowPickupNoticeFor(player, "バフ", itemName);
        }

        private void PlayPickupSound()
        {
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayCueAtPosition(
                    AudioCue.BuffPickup,
                    transform.position);
                return;
            }
        }

        private static string GetBuffDisplayName(string buffId)
        {
            switch (buffId)
            {
                case "moveSpeed":
                    return "移動速度";
                case "attackPower":
                    return "攻撃力";
                case "attackRange":
                    return "射程";
                case "attackSpeed":
                    return "連射速度";
                case StatKey.DamageTaken:
                    return "耐久";
                default:
                    return string.IsNullOrWhiteSpace(buffId) ? "バフ" : buffId;
            }
        }
    }
}
