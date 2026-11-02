using UnityEngine;

namespace MyGame
{
    /// <summary>
    /// ミサイル型砲台の弾Entity
    /// 発射後に一番近くの敵に向かって追尾する
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class MissileBulletEntity : Entity, IRuntimePoolable
    {
        //********************************************************
        // 変数の定義
        //********************************************************

        /// <summary>ミサイルの速度</summary>
        [SerializeField] private float speed = 15f;

        /// <summary>追尾の回転速度</summary>
        [SerializeField] private float turnSpeed = 90f;

        /// <summary>ミサイルの寿命（秒）</summary>
        [SerializeField] private float lifeTime = 5f;

        /// <summary>ミサイルのダメージ量</summary>
        [SerializeField] private int damage = 3;
        [SerializeField] private float hitRadius = 0.22f;
        [SerializeField] private GameObject hitEffectPrefab;
        [SerializeField] private GameObject smokeEffectPrefab;

        /// <summary>ミサイルの所有者</summary>
        private PlayerEntity m_owner;
        private EnemyCPUEntity m_cpuOwner;
        private NetworkProjectileAuthority m_networkAuthority;

        /// <summary>追尾対象の敵</summary>
        private Transform m_target;

        /// <summary>寿命タイマー</summary>
        private float m_timer;
        private Vector3 m_lastPosition;
        private bool m_hasHit;
        private int m_baseDamage;
        private float m_baseLifeTime;
        private GameObject m_smokeEffect;
        private readonly RaycastHit[] m_sweepHits = new RaycastHit[32];

        private void Awake()
        {
            m_baseDamage = damage;
            m_baseLifeTime = lifeTime;
            m_networkAuthority = GetComponent<NetworkProjectileAuthority>();
        }

        //********************************************************
        // セットアップ
        //********************************************************

        /// <summary>
        /// ミサイルの初期設定
        /// MissileTurretから発射時に呼び出す
        /// </summary>
        /// <param name="owner">発射したプレイヤー</param>
        public void Setup(PlayerEntity owner, Transform target)
        {
            m_owner = owner;
            ApplyOwnerStats(owner);
            m_target = target;
        }

        public void SetupNetworkOwner(
            ulong ownerClientId,
            ulong targetNetworkObjectId,
            PlayerEntity knownOwner = null)
        {
            if (m_networkAuthority == null)
                m_networkAuthority = GetComponent<NetworkProjectileAuthority>();

            m_networkAuthority?.ConfigurePlayer(ownerClientId, targetNetworkObjectId);
            m_owner = knownOwner != null
                ? knownOwner
                : m_networkAuthority?.ResolvePlayerOwner();
            m_target = m_networkAuthority?.ResolveTarget();
            ApplyOwnerStats(m_owner);
        }

        public void Setup(EnemyCPUEntity owner, Transform target)
        {
            m_cpuOwner = owner;
            if (owner != null)
            {
                damage = Mathf.RoundToInt(
                    damage * owner.Stats.GetStat(StatKey.AttackPower));
                lifeTime *= owner.Stats.GetStat(StatKey.AttackRange);
            }

            m_target = target;
        }

        public override void InitEntity()
        {
            m_lastPosition = transform.position;
            SpawnSmokeEffect();
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
        /// 追尾対象に向かって進む
        /// </summary>
        public override void UpdateEntity()
        {
            if (m_hasHit) return;

            m_timer += Time.deltaTime;

            // 寿命が尽きたら消える
            if (m_timer >= lifeTime)
            {
                if (CanProcessHits())
                    DestroyProjectile();
                return;
            }

            if (m_target == null && m_networkAuthority != null)
                m_target = m_networkAuthority.ResolveTarget();

            // 発射時にロックした対象が残っている間だけ追尾する
            if (m_target != null)
            {
                Vector3 dir =
                    (GetTargetAimPoint(m_target) - transform.position).normalized;
                Quaternion targetRotation = Quaternion.LookRotation(dir);
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation,
                    targetRotation,
                    turnSpeed * Time.deltaTime
                );
            }

            // 前方に進む
            transform.position += transform.forward * speed * Time.deltaTime;
            if (CanProcessHits())
                CheckSweptHit();
            else
                m_lastPosition = transform.position;
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

            TryHandleHit(other, transform.position);
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
                    player.Damage(damage, m_cpuOwner);
                else
                    player.Damage(damage, m_owner);
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
                hitPoint);

            if (hitEffectPrefab == null) return;

            RuntimeObjectPool.Spawn(hitEffectPrefab, hitPoint, Quaternion.identity);
        }

        private void SpawnSmokeEffect()
        {
            if (smokeEffectPrefab == null) return;

            if (m_smokeEffect == null)
            {
                m_smokeEffect = Instantiate(
                    smokeEffectPrefab,
                    transform.position,
                    transform.rotation,
                    transform
                );
            }

            m_smokeEffect.transform.localPosition = Vector3.zero;
            m_smokeEffect.transform.localRotation = Quaternion.identity;
            m_smokeEffect.SetActive(true);
        }

        public void OnPoolGet()
        {
            m_owner = null;
            m_cpuOwner = null;
            m_target = null;
            m_timer = 0f;
            m_hasHit = false;
            damage = m_baseDamage;
            lifeTime = m_baseLifeTime;
            m_lastPosition = transform.position;
        }

        public void OnPoolRelease()
        {
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

        //********************************************************
        // 内部処理
        //********************************************************

        /// <summary>
        /// ロック判定と追尾に使う対象の中心位置を取得する
        /// </summary>
        /// <returns>Collider、Renderer、Transformの順に求めた中心位置</returns>
        public static Vector3 GetTargetAimPoint(Transform target)
        {
            if (target == null)
                return Vector3.zero;

            PlayerEntity player = target.GetComponentInParent<PlayerEntity>();
            if (player != null)
                return player.GetActiveTargetAimPoint();

            if (TryGetRendererBounds(target, out Bounds rendererBounds))
                return rendererBounds.center;

            if (TryGetColliderBounds(target, out Bounds colliderBounds))
                return colliderBounds.center;

            return target.position;
        }

        private static bool TryGetRendererBounds(Transform target, out Bounds bounds)
        {
            bounds = default;
            if (target == null)
                return false;

            Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
            bool hasBounds = false;
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null ||
                    !renderer.enabled ||
                    !renderer.gameObject.activeInHierarchy)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                    continue;
                }

                bounds.Encapsulate(renderer.bounds);
            }

            return hasBounds;
        }

        private static bool TryGetColliderBounds(Transform target, out Bounds bounds)
        {
            bounds = default;
            if (target == null)
                return false;

            Collider[] colliders = target.GetComponentsInChildren<Collider>(true);
            bool hasBounds = false;
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
                    continue;
                }

                bounds.Encapsulate(targetCollider.bounds);
            }

            return hasBounds;
        }
    }
}
