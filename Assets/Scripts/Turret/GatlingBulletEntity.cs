using UnityEngine;

namespace MyGame
{
    /// <summary>
    /// ガトリング型砲台の弾Entity
    /// 高速で直進する弾を連射する
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class GatlingBulletEntity : Entity, IRuntimePoolable
    {
        //********************************************************
        // 変数の定義
        //********************************************************

        /// <summary>弾の速度</summary>
        [SerializeField] private float speed = 30f;

        /// <summary>弾の寿命（秒）</summary>
        [SerializeField] private float lifeTime = 2f;

        /// <summary>弾のダメージ量</summary>
        [SerializeField] private int damage = 1;
        [SerializeField] private float hitRadius = 0.08f;
        [SerializeField] private GameObject hitEffectPrefab;

        /// <summary>弾の所有者</summary>
        private PlayerEntity m_owner;
        private EnemyCPUEntity m_cpuOwner;
        private NetworkProjectileAuthority m_networkAuthority;
        private TrailRenderer[] m_trailRenderers;

        /// <summary>寿命タイマー</summary>
        private float m_timer;
        private Vector3 m_lastPosition;
        private bool m_hasHit;
        private int m_baseDamage;
        private float m_baseLifeTime;
        // Keep the launch direction fixed so later rotation changes do not bend the bullet.
        private Vector3 m_moveDirection;
        private Vector3 m_lastTravelDirection;
        private Vector3 m_lastHitNormal;
        private readonly RaycastHit[] m_sweepHits = new RaycastHit[32];

        private void Awake()
        {
            m_baseDamage = damage;
            m_baseLifeTime = lifeTime;
            m_networkAuthority = GetComponent<NetworkProjectileAuthority>();
            m_trailRenderers = GetComponentsInChildren<TrailRenderer>(true);
        }

        //********************************************************
        // セットアップ
        //********************************************************

        /// <summary>
        /// 弾の初期設定
        /// GatlingTurretから発射時に呼び出す
        /// </summary>
        /// <param name="owner">発射したプレイヤー</param>
        public void Setup(PlayerEntity owner)
        {
            m_owner = owner;
            ApplyOwnerStats(owner);
        }

        public void SetLaunchDirection(Vector3 direction)
        {
            if (direction.sqrMagnitude <= 0.0001f)
                return;

            m_moveDirection = direction.normalized;
            m_lastTravelDirection = m_moveDirection;
            m_lastHitNormal = -m_moveDirection;
        }

        public void SetupNetworkOwner(
            ulong ownerClientId,
            PlayerEntity knownOwner = null)
        {
            if (m_networkAuthority == null)
                m_networkAuthority = GetComponent<NetworkProjectileAuthority>();

            m_networkAuthority?.ConfigurePlayer(ownerClientId);
            m_owner = knownOwner != null
                ? knownOwner
                : m_networkAuthority?.ResolvePlayerOwner();
            ApplyOwnerStats(m_owner);
        }

        public void Setup(EnemyCPUEntity owner)
        {
            m_cpuOwner = owner;
            if (owner == null) return;

            damage = Mathf.RoundToInt(
                damage * owner.Stats.GetStat(StatKey.AttackPower));
            lifeTime *= owner.Stats.GetStat(StatKey.AttackRange);
        }

        public override void InitEntity()
        {
            m_lastPosition = transform.position;
            CaptureMoveDirection();
            ClearTrails();
        }

        private void ApplyOwnerStats(PlayerEntity owner)
        {
            if (owner == null) return;

            damage = Mathf.RoundToInt(damage * owner.Stats.GetStat(StatKey.AttackPower));
            lifeTime *= owner.Stats.GetStat(StatKey.AttackRange);
        }

        //********************************************************
        // Entityライフサイクル
        //********************************************************

        /// <summary>
        /// 毎フレームの更新処理
        /// 前方に直進する
        /// </summary>
        public override void UpdateEntity()
        {
            if (m_hasHit) return;

            if (m_moveDirection.sqrMagnitude <= 0.0001f)
                CaptureMoveDirection();

            m_lastTravelDirection = m_moveDirection;
            transform.position += m_moveDirection * speed * Time.deltaTime;
            if (CanProcessHits())
                CheckSweptHit();
            else
                m_lastPosition = transform.position;

            m_timer += Time.deltaTime;
            if (CanProcessHits() && m_timer >= lifeTime)
            {
                DestroyProjectile();
            }
        }

        //********************************************************
        // 衝突判定
        //********************************************************

        /// <summary>
        /// Colliderのトリガー判定
        /// 敵に当たったらダメージを与えて消える
        /// </summary>
        private void OnTriggerEnter(Collider other)
        {
            if (m_hasHit || !CanProcessHits()) return;

            Vector3 hitPoint = ResolveHitPoint(other);
            m_lastHitNormal = ResolveHitNormal(hitPoint);
            TryHandleHit(other, hitPoint);
        }

        private void CheckSweptHit()
        {
            int hitCount = ProjectileSweepUtility.Cast(
                m_lastPosition,
                transform.position,
                hitRadius,
                m_sweepHits);

            for (int i = 0; i < hitCount; i++)
            {
                Collider hitCollider = m_sweepHits[i].collider;
                if (ProjectileSweepUtility.ShouldIgnore(hitCollider, transform, GetOwnerTransform()))
                    continue;

                m_lastHitNormal = m_sweepHits[i].normal;
                if (TryHandleHit(hitCollider, m_sweepHits[i].point))
                    return;
            }

            m_lastPosition = transform.position;
        }

        private bool TryHandleHit(Collider other, Vector3 hitPoint)
        {
            ResolveNetworkOwner();

            if (ProjectileSweepUtility.ShouldIgnore(other, transform, GetOwnerTransform()))
                return false;

            PlayerEntity player = other.GetComponentInParent<PlayerEntity>();
            if (player != null && player != m_owner)
            {
                if (!player.IsActiveDamageCollider(other))
                    return false;

                bool wasAlive = player.CurrentHealth > 0f && !player.IsRespawning;
                if (m_cpuOwner != null)
                    player.DamageRapidHit(damage, m_cpuOwner);
                else
                    player.DamageRapidHit(damage, m_owner);
                if (wasAlive && m_owner != null)
                    m_owner.NotifyHitConfirmed(player.CurrentHealth <= 0f, true);
                HitAndDestroy(hitPoint);
                return true;
            }

            EnemyEntity enemy = other.GetComponent<EnemyEntity>();
            if (enemy == null)
                enemy = other.GetComponentInParent<EnemyEntity>();

            if (enemy != null)
            {
                bool wasAlive = enemy.CurrentHealth > 0f;
                bool defeated = wasAlive && damage >= enemy.CurrentHealth;
                if (m_cpuOwner != null)
                    enemy.Damage(damage, m_cpuOwner);
                else
                    enemy.Damage(damage, m_owner);
                if (wasAlive && m_owner != null)
                    m_owner.NotifyHitConfirmed(defeated);
                HitAndDestroy(hitPoint);
                return true;
            }

            EnemyCPUEntity cpu = other.GetComponent<EnemyCPUEntity>();
            if (cpu == null)
                cpu = other.GetComponentInParent<EnemyCPUEntity>();

            if (cpu != null)
            {
                if (cpu == m_cpuOwner)
                    return false;

                bool wasAlive = cpu.CPUCurrentHealth > 0f;
                if (m_cpuOwner != null)
                    cpu.Damage(damage, m_cpuOwner);
                else
                    cpu.Damage(damage, m_owner);
                if (wasAlive && m_owner != null)
                    m_owner.NotifyHitConfirmed(cpu.CPUCurrentHealth <= 0f, true);
                HitAndDestroy(hitPoint);
                return true;
            }

            DestructibleBuilding building = other.GetComponentInParent<DestructibleBuilding>();
            if (building != null)
            {
                building.Damage(damage, hitPoint);
                HitAndDestroy(hitPoint);
                return true;
            }

            if (ProjectileSweepUtility.IsBattlePlane(other))
            {
                HitAndDestroy(hitPoint);
                return true;
            }

            if (!other.isTrigger)
            {
                HitAndDestroy(hitPoint);
                return true;
            }

            return false;
        }

        private Transform GetOwnerTransform()
        {
            if (m_owner != null)
                return m_owner.transform;

            return m_cpuOwner != null ? m_cpuOwner.transform : null;
        }

        private void HitAndDestroy(Vector3 hitPoint)
        {
            if (m_hasHit) return;

            m_hasHit = true;
            SpawnHitEffect(hitPoint);
            DestroyProjectile();
        }

        private void SpawnHitEffect(Vector3 hitPoint)
        {
            global::AudioManager.Instance?.PlayCueAtPosition(
                global::AudioCue.HitExplosion,
                hitPoint,
                0.35f);

            if (hitEffectPrefab == null) return;

            RuntimeObjectPool.Spawn(hitEffectPrefab, hitPoint, GetHitEffectRotation());
        }

        private Quaternion GetHitEffectRotation()
        {
            Vector3 effectDirection = m_lastHitNormal;
            if (effectDirection.sqrMagnitude <= 0.0001f)
                effectDirection = -m_lastTravelDirection;
            if (effectDirection.sqrMagnitude <= 0.0001f)
                return Quaternion.identity;

            return Quaternion.LookRotation(effectDirection.normalized, Vector3.up);
        }

        private Vector3 ResolveHitPoint(Collider other)
        {
            if (other == null)
                return transform.position;

            Vector3 closestPoint = GetClosestPointSafe(other, transform.position);
            if (float.IsNaN(closestPoint.x) ||
                float.IsNaN(closestPoint.y) ||
                float.IsNaN(closestPoint.z))
            {
                return transform.position;
            }

            return closestPoint;
        }

        private static Vector3 GetClosestPointSafe(Collider collider, Vector3 point)
        {
            if (CanUseClosestPoint(collider))
                return collider.ClosestPoint(point);

            Bounds bounds = collider.bounds;
            if (bounds.size.sqrMagnitude > 0.0001f)
                return bounds.ClosestPoint(point);

            return point;
        }

        private static bool CanUseClosestPoint(Collider collider)
        {
            if (collider == null)
                return false;

            if (collider is BoxCollider ||
                collider is SphereCollider ||
                collider is CapsuleCollider)
            {
                return true;
            }

            if (collider is MeshCollider meshCollider)
                return meshCollider.convex;

            return false;
        }

        private Vector3 ResolveHitNormal(Vector3 hitPoint)
        {
            Vector3 normal = transform.position - hitPoint;
            if (normal.sqrMagnitude > 0.0001f)
                return normal.normalized;

            if (m_lastTravelDirection.sqrMagnitude > 0.0001f)
                return -m_lastTravelDirection.normalized;

            return -m_moveDirection;
        }

        public void OnPoolGet()
        {
            m_owner = null;
            m_cpuOwner = null;
            m_timer = 0f;
            m_hasHit = false;
            damage = m_baseDamage;
            lifeTime = m_baseLifeTime;
            m_lastPosition = transform.position;
            CaptureMoveDirection();
            ClearTrails();
        }

        private void CaptureMoveDirection()
        {
            m_moveDirection = transform.forward;
            if (m_moveDirection.sqrMagnitude <= 0.0001f)
                m_moveDirection = Vector3.forward;
            else
                m_moveDirection.Normalize();

            m_lastTravelDirection = m_moveDirection;
            m_lastHitNormal = -m_moveDirection;
        }

        private void ClearTrails()
        {
            if (m_trailRenderers == null)
                m_trailRenderers = GetComponentsInChildren<TrailRenderer>(true);

            for (int i = 0; i < m_trailRenderers.Length; i++)
            {
                if (m_trailRenderers[i] == null)
                    continue;

                m_trailRenderers[i].Clear();
            }
        }

        private bool CanProcessHits()
        {
            return m_networkAuthority == null || m_networkAuthority.CanProcessHits;
        }

        private void ResolveNetworkOwner()
        {
            if (m_owner == null && m_networkAuthority != null)
                m_owner = m_networkAuthority.ResolvePlayerOwner();
        }

        private void DestroyProjectile()
        {
            if (m_networkAuthority != null && m_networkAuthority.TryDespawn())
                return;

            OnDestroyEntity();
        }

        public void OnPoolRelease()
        {
        }
    }
}
