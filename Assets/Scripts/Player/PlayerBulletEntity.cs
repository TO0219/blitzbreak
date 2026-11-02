using Unity.Netcode;
using UnityEngine;

namespace MyGame
{
    public class PlayerBulletEntity : NetworkBehaviour, IRuntimePoolable
    {
        [SerializeField] private float speed = 20f;
        [SerializeField] private float lifeTime = 3f;
        [SerializeField] private float damage = 1;
        [SerializeField] private float hitRadius = 0.12f;
        [SerializeField] private GameObject hitEffectPrefab;

        private PlayerEntity m_owner;
        private ulong m_ownerClientId = ulong.MaxValue;
        private float m_timer;
        private Vector3 m_lastPosition;
        private bool m_hasHit;
        private float m_baseDamage;
        private float m_baseLifeTime;
        private readonly RaycastHit[] m_sweepHits = new RaycastHit[32];

        public float Speed => speed;

        public float Damage => damage;

        private void Awake()
        {
            m_baseDamage = damage;
            m_baseLifeTime = lifeTime;
            m_lastPosition = transform.position;
        }

        public void Setup(PlayerEntity owner)
        {
            m_owner = owner;
            ApplyOwnerStats(owner);

            NetworkObject ownerNetworkObject = owner != null ? owner.GetComponent<NetworkObject>() : null;
            if (ownerNetworkObject != null && ownerNetworkObject.IsSpawned)
            {
                m_ownerClientId = ownerNetworkObject.OwnerClientId;
            }
        }

        public void SetupNetworkOwner(
            ulong ownerClientId,
            PlayerEntity knownOwner = null)
        {
            m_ownerClientId = ownerClientId;
            m_owner = knownOwner != null ? knownOwner : FindOwnerPlayer();
            ApplyOwnerStats(m_owner);
        }

        private void ApplyOwnerStats(PlayerEntity owner)
        {
            if (owner == null) return;

            damage *= owner.Stats.GetStat(StatKey.AttackPower);
            lifeTime *= owner.Stats.GetStat(StatKey.AttackRange);
        }

        private void Update()
        {
            if (m_hasHit) return;

            transform.position += transform.forward * speed * Time.deltaTime;

            if (CanControlLifetime())
                CheckSweptHit();
            else
                m_lastPosition = transform.position;

            m_timer += Time.deltaTime;
            if (CanControlLifetime() && m_timer >= lifeTime)
            {
                DestroyBullet();
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!CanControlLifetime() || m_hasHit) return;

            TryHandleHit(other, transform.position);
        }

        private void CheckSweptHit()
        {
            int hitCount = ProjectileSweepUtility.Cast(
                m_lastPosition,
                transform.position,
                hitRadius,
                m_sweepHits);
            PlayerEntity owner = FindOwnerPlayer();
            Transform ownerTransform = owner != null ? owner.transform : null;

            for (int i = 0; i < hitCount; i++)
            {
                Collider hitCollider = m_sweepHits[i].collider;
                if (ProjectileSweepUtility.ShouldIgnore(hitCollider, transform, ownerTransform))
                    continue;

                if (TryHandleHit(hitCollider, m_sweepHits[i].point))
                    return;
            }

            m_lastPosition = transform.position;
        }

        private bool TryHandleHit(Collider other, Vector3 hitPoint)
        {
            PlayerEntity owner = FindOwnerPlayer();
            if (ProjectileSweepUtility.ShouldIgnore(other, transform, owner != null ? owner.transform : null))
                return false;

            PlayerEntity player = other.GetComponentInParent<PlayerEntity>();
            if (player != null && player != owner)
            {
                if (!player.IsActiveDamageCollider(other))
                    return false;

                bool wasAlive = player.CurrentHealth > 0f && !player.IsRespawning;
                player.Damage(damage, owner);
                if (wasAlive && owner != null)
                    owner.NotifyHitConfirmed(player.CurrentHealth <= 0f, true);
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
                enemy.Damage(damage, owner);
                if (wasAlive && owner != null)
                    owner.NotifyHitConfirmed(defeated);
                HitAndDestroy(hitPoint);
                return true;
            }

            EnemyCPUEntity cpu = other.GetComponent<EnemyCPUEntity>();
            if (cpu == null)
                cpu = other.GetComponentInParent<EnemyCPUEntity>();

            if (cpu != null)
            {
                bool wasAlive = cpu.CPUCurrentHealth > 0f;
                cpu.Damage(damage, owner);
                if (wasAlive && owner != null)
                    owner.NotifyHitConfirmed(cpu.CPUCurrentHealth <= 0f, true);
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

        private void HitAndDestroy(Vector3 hitPoint)
        {
            if (m_hasHit) return;

            m_hasHit = true;
            SpawnHitEffect(hitPoint);
            DestroyBullet();
        }

        private void SpawnHitEffect(Vector3 hitPoint)
        {
            global::AudioManager.Instance?.PlayCueAtPosition(
                global::AudioCue.HitExplosion,
                hitPoint,
                0.6f);

            if (hitEffectPrefab == null) return;

            RuntimeObjectPool.Spawn(hitEffectPrefab, hitPoint, Quaternion.identity);
        }

        private bool CanControlLifetime()
        {
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
                return true;

            if (!IsSpawned)
                return true;

            return IsServer;
        }

        private PlayerEntity FindOwnerPlayer()
        {
            if (m_owner != null)
                return m_owner;

            if (NetworkManager.Singleton == null)
                return null;

            if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(m_ownerClientId, out NetworkClient client))
                return null;

            if (client.PlayerObject == null)
                return null;

            m_owner = client.PlayerObject.GetComponent<PlayerEntity>();
            return m_owner;
        }

        private void DestroyBullet()
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening && IsSpawned)
            {
                if (IsServer)
                {
                    NetworkObject.Despawn(true);
                }

                return;
            }

            if (!RuntimeObjectPool.TryRelease(gameObject))
                Destroy(gameObject);
        }

        public void OnPoolGet()
        {
            m_owner = null;
            m_ownerClientId = ulong.MaxValue;
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
    }
}
