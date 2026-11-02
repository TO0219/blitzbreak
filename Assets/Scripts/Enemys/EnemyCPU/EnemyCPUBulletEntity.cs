using UnityEngine;
using Unity.Netcode;

namespace MyGame
{
    public class EnemyCPUBulletEntity : Entity, IRuntimePoolable
    {
        [SerializeField] private float speed = 15f;
        [SerializeField] private float lifeTime = 3f;
        [SerializeField] private float damage = 1;
        [SerializeField] private float hitRadius = 0.45f;
        [SerializeField] private GameObject hitEffectPrefab;

        private float m_timer;
        private EnemyCPUEntity m_owner;
        private Rigidbody m_rigidbody;
        private Vector3 m_lastPosition;
        private bool m_hasHit;
        private float m_baseDamage;
        private float m_baseLifeTime;
        private NetworkProjectileAuthority m_networkAuthority;
        private readonly RaycastHit[] m_sweepHits = new RaycastHit[32];

        private void Awake()
        {
            m_baseDamage = damage;
            m_baseLifeTime = lifeTime;
            m_networkAuthority = GetComponent<NetworkProjectileAuthority>();
        }

        public float Speed => speed;

        public override void InitEntity()
        {
            m_rigidbody = GetComponent<Rigidbody>();
            m_lastPosition = transform.position;
            ProjectileVisualUtility.ClearTrails(gameObject);
        }

        public float Damage => damage;

        public void Setup(EnemyCPUEntity owner)
        {
            m_owner = owner;
        }

        public override void UpdateEntity()
        {
            if (m_hasHit) return;

            if (!CanProcessHits())
            {
                if (m_rigidbody != null)
                    m_rigidbody.isKinematic = true;

                transform.position += transform.forward * speed * Time.deltaTime;
                m_lastPosition = transform.position;
                return;
            }

            if (m_rigidbody == null)
                transform.position += transform.forward * speed * Time.deltaTime;

            CheckSweptHit();

            m_timer += Time.deltaTime;

            if (m_timer >= lifeTime)
            {
                DestroyProjectile();
            }
        }

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
                if (TryHandleHit(m_sweepHits[i].collider, m_sweepHits[i].point))
                    return;
            }

            m_lastPosition = transform.position;
        }

        private bool TryHandleHit(Collider other, Vector3 hitPoint)
        {
            if (ProjectileSweepUtility.ShouldIgnore(
                other,
                transform,
                m_owner != null ? m_owner.transform : null))
                return false;

            PlayerEntity player = other.GetComponent<PlayerEntity>();
            if (player == null)
                player = other.GetComponentInParent<PlayerEntity>();

            if (player != null)
            {
                if (!player.IsActiveDamageCollider(other))
                    return false;

                HitPlayer(player, hitPoint);
                return true;
            }

            EnemyCPUEntity cpu = other.GetComponent<EnemyCPUEntity>();
            if (cpu == null)
                cpu = other.GetComponentInParent<EnemyCPUEntity>();

            if (cpu != null)
            {
                HitEnemyCPU(cpu, hitPoint);
                return true;
            }

            EnemyEntity enemy = other.GetComponent<EnemyEntity>();
            if (enemy == null)
                enemy = other.GetComponentInParent<EnemyEntity>();

            if (enemy != null)
            {
                HitEnemy(enemy, hitPoint);
                return true;
            }

            DestructibleBuilding building = other.GetComponentInParent<DestructibleBuilding>();
            if (building != null)
            {
                HitBuilding(building, hitPoint);
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

        private void HitPlayer(PlayerEntity player, Vector3 hitPoint)
        {
            if (player == null || m_hasHit) return;

            m_hasHit = true;
            player.Damage(damage, m_owner);
            SpawnHitEffect(hitPoint);
            DestroyProjectile();
        }

        private void HitEnemyCPU(EnemyCPUEntity enemy, Vector3 hitPoint)
        {
            if (enemy == null || m_hasHit) return;

            m_hasHit = true;
            enemy.Damage(damage, m_owner);
            SpawnHitEffect(hitPoint);
            DestroyProjectile();
        }

        private void HitEnemy(EnemyEntity enemy, Vector3 hitPoint)
        {
            if (enemy == null || m_hasHit) return;

            m_hasHit = true;
            enemy.Damage(damage, m_owner);
            SpawnHitEffect(hitPoint);
            DestroyProjectile();
        }

        private void HitBuilding(DestructibleBuilding building, Vector3 hitPoint)
        {
            if (building == null || m_hasHit) return;

            m_hasHit = true;
            building.Damage(damage, hitPoint);
            SpawnHitEffect(hitPoint);
            DestroyProjectile();
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
                0.45f);

            if (hitEffectPrefab == null) return;

            RuntimeObjectPool.Spawn(hitEffectPrefab, hitPoint, Quaternion.identity);
        }

        public void SetDamage(float dam)
        {
            damage = dam;
        }

        public void OnPoolGet()
        {
            m_owner = null;
            m_timer = 0f;
            m_hasHit = false;
            damage = m_baseDamage;
            lifeTime = m_baseLifeTime;
            m_lastPosition = transform.position;
            ProjectileVisualUtility.ClearTrails(gameObject);
        }

        public void OnPoolRelease()
        {
        }

        private bool CanProcessHits()
        {
            return m_networkAuthority == null || m_networkAuthority.CanProcessHits;
        }

        private void DestroyProjectile()
        {
            if (m_networkAuthority != null && m_networkAuthority.TryDespawn())
                return;

            OnDestroyEntity();
        }
    }
}
