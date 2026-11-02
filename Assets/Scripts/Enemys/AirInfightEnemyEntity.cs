using UnityEngine;

namespace MyGame
{
    public class AirInfightEnemyEntity : InfightEnemyEntity
    {
        [SerializeField] private float hoverHeight = 3f;
        [SerializeField] private float hoverAdjustSpeed = 1f;
        [SerializeField] private float groundCheckDistance = 20f;
        [SerializeField] private LayerMask groundCheckMask = Physics.DefaultRaycastLayers;
        [SerializeField, Min(0.05f)] private float contactCheckInterval = 0.05f;
        [SerializeField, Min(0f)] private float contactDamage = 30f;
        [SerializeField, Min(0.05f)] private float contactRadius = 1.6f;
        [SerializeField, Min(0f)] private float contactBoundsPadding = 0.35f;
        [SerializeField] private LayerMask contactTargetMask = ~0;

        private readonly RaycastHit[] m_groundHits = new RaycastHit[16];
        private readonly Collider[] m_contactHits = new Collider[16];
        private bool m_hasDetonated;

        protected override void Start()
        {
            InitializeInfight();
            fireInterval = contactCheckInterval;
            SnapAboveGround();
        }

        protected override void Fire()
        {
            if (m_hasDetonated || m_target == null)
                return;

            if (!TryGetContactTarget(out PlayerEntity player, out EnemyCPUEntity cpu))
                return;

            m_hasDetonated = true;

            if (player != null)
                player.Damage(contactDamage, transform.position);
            else if (cpu != null)
                cpu.Damage(contactDamage, (PlayerEntity)null);

            DetonateSelf();
        }

        protected override void Chase(Vector3 direction)
        {
            if (direction.sqrMagnitude < 0.001f)
            {
                Fire();
                return;
            }

            Vector3 lookDir = direction;
            lookDir.y = 0f;

            if (lookDir.sqrMagnitude > 0.001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(lookDir);

                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    targetRotation,
                    rotateSpeed * Time.deltaTime
                );
            }

            // 横移動のみ
            Vector3 moveDir = direction;
            moveDir.y = 0f;

            if (moveDir.sqrMagnitude > 0.001f)
            {
                MoveWithinSpawnArea(moveDir.normalized * moveSpeed * Time.deltaTime);
            }

            AdjustHeight(hoverHeight);
            Fire();
        }

        protected override void Wander()
        {
            base.Wander();

            AdjustHeight(hoverHeight);
        }

        protected override Vector3 GetDirection(Vector3 targetPos, Vector3 transformPos)
        {
            Vector3 direction = targetPos - transformPos;
            //direction.y = 2f;
            return direction;
        }

        protected override bool TryGetMovementFootprintCapsule(
            Vector3 position,
            out Vector3 bottom,
            out Vector3 top,
            out float radius)
        {
            radius = Mathf.Max(0.05f, wanderObstacleProbeRadius);
            float baseY = position.y - Mathf.Max(0f, hoverHeight);
            float height = Mathf.Max(radius, wanderObstacleProbeHeight);
            bottom = new Vector3(position.x, baseY + radius, position.z);
            top = new Vector3(position.x, baseY + height, position.z);
            return true;
        }

        private bool TryGetContactTarget(
            out PlayerEntity player,
            out EnemyCPUEntity cpu)
        {
            player = null;
            cpu = null;
            Bounds contactBounds = GetContactBounds();
            contactBounds.Expand(contactBoundsPadding * 2f);

            int hitCount = Physics.OverlapSphereNonAlloc(
                contactBounds.center,
                GetContactSearchRadius(contactBounds),
                m_contactHits,
                contactTargetMask,
                QueryTriggerInteraction.Collide);

            for (int i = 0; i < hitCount; i++)
            {
                Collider hit = m_contactHits[i];
                if (hit == null || hit.transform.root == transform.root)
                    continue;

                PlayerEntity hitPlayer = hit.GetComponentInParent<PlayerEntity>();
                if (IsValidContactPlayer(hitPlayer) &&
                    IsCurrentTarget(hitPlayer.transform) &&
                    IsTouchingContactBounds(contactBounds, hit))
                {
                    player = hitPlayer;
                    return true;
                }

                EnemyCPUEntity hitCpu = hit.GetComponentInParent<EnemyCPUEntity>();
                if (IsValidContactCpu(hitCpu) &&
                    IsCurrentTarget(hitCpu.transform) &&
                    IsTouchingContactBounds(contactBounds, hit))
                {
                    cpu = hitCpu;
                    return true;
                }
            }

            return false;
        }

        private Bounds GetContactBounds()
        {
            Collider bodyCollider = GetComponent<Collider>();
            if (bodyCollider == null)
                bodyCollider = GetComponentInChildren<Collider>();

            if (bodyCollider != null)
                return bodyCollider.bounds;

            return new Bounds(transform.position, Vector3.one * contactRadius);
        }

        private float GetContactSearchRadius(Bounds contactBounds)
        {
            return Mathf.Max(0.05f, contactRadius) +
                   Mathf.Max(contactBounds.extents.x, contactBounds.extents.z);
        }

        private bool IsTouchingContactBounds(Bounds contactBounds, Collider hit)
        {
            if (hit == null)
                return false;

            Bounds expandedBounds = contactBounds;
            expandedBounds.Expand(contactRadius * 2f);
            return expandedBounds.Intersects(hit.bounds);
        }

        private bool IsCurrentTarget(Transform candidate)
        {
            return candidate != null &&
                   m_target != null &&
                   candidate.root == m_target.root;
        }

        private static bool IsValidContactPlayer(PlayerEntity player)
        {
            return player != null &&
                   !player.IsRespawning &&
                   player.CurrentHealth > 0f;
        }

        private static bool IsValidContactCpu(EnemyCPUEntity cpu)
        {
            return cpu != null && cpu.CPUCurrentHealth > 0f;
        }

        private void SuppressSelfDetonationDrops()
        {
            if (enemyEntity == null)
                enemyEntity = GetComponent<EnemyEntity>();

            if (enemyEntity != null)
                enemyEntity.SuppressNextItemDrop();
        }

        private void DetonateSelf()
        {
            if (enemyEntity == null)
                enemyEntity = GetComponent<EnemyEntity>();

            if (enemyEntity == null)
            {
                Destroy(gameObject);
                return;
            }

            SpawnSelfDetonationEffect();
            SuppressSelfDetonationDrops();
            enemyEntity.Damage(
                Mathf.Max(1f, enemyEntity.CurrentHealth),
                (PlayerEntity)null);
        }

        private void SpawnSelfDetonationEffect()
        {
            if (enemyEntity == null)
                enemyEntity = GetComponent<EnemyEntity>();

            GameObject effectPrefab = enemyEntity != null
                ? enemyEntity.AirInfightSelfDetonationEffectPrefab
                : null;
            if (effectPrefab == null)
                return;

            RuntimeObjectPool.Spawn(
                effectPrefab,
                GetContactBounds().center,
                Quaternion.identity);
        }

        private void AdjustHeight(float targetHeight)
        {
            if (TryGetGroundHit(out RaycastHit hit))
            {
                Vector3 pos = transform.position;

                float targetY = hit.point.y + targetHeight;

                pos.y = Mathf.Lerp(
                    pos.y,
                    targetY,
                    hoverAdjustSpeed * Time.deltaTime
                );

                transform.position = pos;
            }
        }

        private void SnapAboveGround()
        {
            if (!TryGetGroundHit(out RaycastHit hit))
                return;

            Vector3 pos = transform.position;
            pos.y = Mathf.Max(pos.y, hit.point.y + hoverHeight);
            transform.position = pos;
        }

        private bool TryGetGroundHit(out RaycastHit groundHit)
        {
            groundHit = default;
            Vector3 origin =
                transform.position + Vector3.up * Mathf.Max(0.1f, hoverHeight);
            float distance =
                Mathf.Max(0.1f, groundCheckDistance + hoverHeight);
            int hitCount = Physics.RaycastNonAlloc(
                origin,
                Vector3.down,
                m_groundHits,
                distance,
                groundCheckMask,
                QueryTriggerInteraction.Ignore);

            float nearestDistance = float.PositiveInfinity;
            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = m_groundHits[i];
                Collider hitCollider = hit.collider;
                if (hitCollider == null || hitCollider.transform.root == transform.root)
                    continue;

                if (hitCollider.GetComponentInParent<EnemyEntity>() != null)
                    continue;

                if (hitCollider.GetComponentInParent<EnemyCPUEntity>() != null)
                    continue;

                if (hitCollider.GetComponentInParent<PlayerEntity>() != null)
                    continue;

                if (hit.distance >= nearestDistance)
                    continue;

                nearestDistance = hit.distance;
                groundHit = hit;
            }

            return groundHit.collider != null;
        }
    }
}
