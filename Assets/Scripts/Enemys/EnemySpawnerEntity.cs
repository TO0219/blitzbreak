using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace MyGame
{
    public class EnemySpawnerEntity : MonoBehaviour
    {
        [Header("Spawn")]
        [SerializeField] private GameObject enemyPrefab;
        [SerializeField, Min(0)] private int initialSpawnCount = 3;
        [SerializeField] private float spawnInterval = 5f;
        [SerializeField] private int maxSpawnCount = 5;
        [SerializeField] private float spawnRadius = 10f;

        [Header("Spawn Ratio")]
        [SerializeField, Min(0f)] private float groundEnemySpawnWeight = 1f;
        [SerializeField, Min(0f)] private float airEnemySpawnWeight = 0f;
        [SerializeField, Min(0f)] private float infightEnemySpawnWeight = 0f;
        [SerializeField, Min(0f)] private float shooterEnemySpawnWeight = 1f;

        [Header("Spawn Type Filter")]
        [SerializeField] private bool groundInfightEnemiesSpawn = true;
        [SerializeField] private bool groundShooterEnemiesSpawn = true;
        [SerializeField] private bool airInfightEnemiesSpawn = true;
        [SerializeField] private bool airShooterEnemiesSpawn = true;

        [Header("Spawn Distribution")]
        [SerializeField] private bool useEvenSpawnDistribution = true;
        [SerializeField, Range(0f, 1f)] private float spawnPositionJitter = 0.2f;

        [Header("Spawn Height")]
        [SerializeField] private float spawnHeight = 3f;

        [Header("Spawn Collision Check")]
        [SerializeField, Min(1)] private int spawnPositionAttempts = 24;
        [SerializeField, Min(0.05f)] private float spawnClearanceRadius = 0.65f;
        [SerializeField, Min(0.1f)] private float spawnClearanceHeight = 2f;
        [SerializeField] private LayerMask spawnObstacleLayer = ~0;
        [SerializeField] private LayerMask spawnGroundLayer = ~0;
        [SerializeField, Min(0.1f)] private float spawnGroundRayDistance = 30f;
        [SerializeField, Min(0.5f)] private float spawnBuildingBoundsProbeRadius = 12f;
        [SerializeField, Min(0.5f)] private float spawnEnclosureProbeDistance = 5f;
        [SerializeField, Range(3, 8)] private int spawnEnclosureHitThreshold = 4;

        [Header("Scene Gizmo")]
        [SerializeField] private Color spawnAreaGizmoColor = new Color(0.2f, 0.75f, 1f, 0.25f);
        [SerializeField] private Color spawnAreaWireGizmoColor = new Color(0.2f, 0.75f, 1f, 1f);

        private float m_timer;
        private int m_currentCount;
        private int m_spawnSequenceIndex;
        private GameRuleEntity m_gameRuleEntity;
        private readonly List<EnemyEntity> m_spawnedEnemies =
            new List<EnemyEntity>();
        private readonly List<Vector3> m_nearbyEnemyPositions =
            new List<Vector3>();
        private readonly Collider[] m_spawnBoundsHits = new Collider[64];
        private readonly RaycastHit[] m_spawnEnclosureHits = new RaycastHit[16];

        public Vector3 SpawnCenter => transform.position;
        public float SpawnRadius => spawnRadius;

        private void Start()
        {
            if (IsOnlineClient())
                return;

            int spawnCount = Mathf.Min(initialSpawnCount, maxSpawnCount);
            for (int i = 0; i < spawnCount; i++)
            {
                SpawnEnemy();
            }
        }

        private void Update()
        {
            if (IsOnlineClient())
                return;

            if (!CanSpawnDuringBattle())
                return;

            m_timer += Time.deltaTime;

            if (m_timer >= spawnInterval)
            {
                m_timer = 0f;

                SpawnEnemy();
            }
        }

        private bool CanSpawnDuringBattle()
        {
            if (m_gameRuleEntity == null)
            {
                m_gameRuleEntity = FindAnyObjectByType<GameRuleEntity>();
            }

            return m_gameRuleEntity == null || m_gameRuleEntity.CanPlay;
        }

        private void SpawnEnemy()
        {
            if (enemyPrefab == null)
                return;

            PruneSpawnedEnemies();
            if (m_currentCount >= maxSpawnCount)
                return;

            if (!TryChooseEnemyType(out bool airEnemy, out bool infightEnemy))
                return;

            if (!TryFindSpawnPosition(out Vector3 spawnPos))
                return;

            GameObject enemyObj = Instantiate(
                enemyPrefab,
                spawnPos,
                Quaternion.identity
            );

            EnemyEntity enemy =
                enemyObj.GetComponent<EnemyEntity>();

            if (enemy == null)
            {
                Destroy(enemyObj);
                return;
            }

            enemy.SetSpawner(this);

            //enemy.SetAir(airEnemy);
            //enemy.SetInfight(infightEnemy);

            if (NetworkManager.Singleton != null &&
                NetworkManager.Singleton.IsListening)
            {
                NetworkEnemyController networkController =
                    enemyObj.GetComponent<NetworkEnemyController>();
                NetworkObject networkObject =
                    enemyObj.GetComponent<NetworkObject>();

                if (networkController == null || networkObject == null)
                {
                    Debug.LogError(
                        "Online Enemy requires NetworkEnemyController and NetworkObject.");
                    Destroy(enemyObj);
                    return;
                }

                networkController.ConfigureServer(airEnemy, infightEnemy);
                networkObject.Spawn(true);
            }
            else
            {
                enemy.Configure(airEnemy, infightEnemy);
            }

            m_spawnedEnemies.Add(enemy);
            m_currentCount = m_spawnedEnemies.Count;
        }

        private bool TryChooseEnemyType(out bool airEnemy, out bool infightEnemy)
        {
            airEnemy = false;
            infightEnemy = false;

            float groundInfightWeight = GetEnemyTypeWeight(
                groundInfightEnemiesSpawn,
                groundEnemySpawnWeight,
                infightEnemySpawnWeight);
            float groundShooterWeight = GetEnemyTypeWeight(
                groundShooterEnemiesSpawn,
                groundEnemySpawnWeight,
                shooterEnemySpawnWeight);
            float airInfightWeight = GetEnemyTypeWeight(
                airInfightEnemiesSpawn,
                airEnemySpawnWeight,
                infightEnemySpawnWeight);
            float airShooterWeight = GetEnemyTypeWeight(
                airShooterEnemiesSpawn,
                airEnemySpawnWeight,
                shooterEnemySpawnWeight);
            float totalWeight =
                groundInfightWeight +
                groundShooterWeight +
                airInfightWeight +
                airShooterWeight;

            if (totalWeight <= 0f)
                return false;

            float pick = Random.value * totalWeight;
            if (TryConsumeWeight(ref pick, groundInfightWeight))
            {
                infightEnemy = true;
                return true;
            }

            if (TryConsumeWeight(ref pick, groundShooterWeight))
                return true;

            if (TryConsumeWeight(ref pick, airInfightWeight))
            {
                airEnemy = true;
                infightEnemy = true;
                return true;
            }

            airEnemy = true;
            return true;
        }

        private static float GetEnemyTypeWeight(
            bool canSpawn,
            float movementWeight,
            float combatWeight)
        {
            if (!canSpawn)
                return 0f;

            return Mathf.Max(0f, movementWeight) * Mathf.Max(0f, combatWeight);
        }

        private static bool TryConsumeWeight(ref float pick, float weight)
        {
            if (weight <= 0f)
                return false;

            if (pick < weight)
                return true;

            pick -= weight;
            return false;
        }

        private static bool IsOnlineClient()
        {
            return NetworkManager.Singleton != null &&
                   NetworkManager.Singleton.IsListening &&
                   !NetworkManager.Singleton.IsServer;
        }

        private bool TryFindSpawnPosition(out Vector3 spawnPosition)
        {
            int attempts = Mathf.Max(1, spawnPositionAttempts);
            bool foundPosition = false;
            float bestSeparationSqr = float.NegativeInfinity;
            spawnPosition = default;
            RefreshNearbyEnemyPositions();

            for (int i = 0; i < attempts; i++)
            {
                Vector2 random = useEvenSpawnDistribution
                    ? GetEvenSpawnOffset(m_spawnSequenceIndex++)
                    : Random.insideUnitCircle * Mathf.Max(0f, spawnRadius);
                Vector3 candidate =
                    transform.position +
                    new Vector3(random.x, spawnHeight, random.y);

                if (!TryResolveSpawnPosition(candidate, out Vector3 resolvedPosition))
                    continue;

                float separationSqr =
                    GetNearestEnemyDistanceSqr(resolvedPosition);
                if (foundPosition && separationSqr <= bestSeparationSqr)
                    continue;

                foundPosition = true;
                bestSeparationSqr = separationSqr;
                spawnPosition = resolvedPosition;
            }

            return foundPosition;
        }

        private void RefreshNearbyEnemyPositions()
        {
            m_nearbyEnemyPositions.Clear();
            float radius = Mathf.Max(0f, spawnRadius);
            float radiusSqr = radius * radius;
            EnemyEntity[] enemies = FindObjectsByType<EnemyEntity>();

            for (int i = 0; i < enemies.Length; i++)
            {
                EnemyEntity enemy = enemies[i];
                if (enemy == null ||
                    enemy.IsDestroy ||
                    enemy.CurrentHealth <= 0f)
                {
                    continue;
                }

                Vector3 enemyPosition = enemy.transform.position;
                Vector3 offset = enemyPosition - transform.position;
                offset.y = 0f;
                if (offset.sqrMagnitude <= radiusSqr)
                    m_nearbyEnemyPositions.Add(enemyPosition);
            }
        }

        private float GetNearestEnemyDistanceSqr(Vector3 position)
        {
            float nearestDistanceSqr = float.PositiveInfinity;

            for (int i = 0; i < m_nearbyEnemyPositions.Count; i++)
            {
                Vector3 offset = m_nearbyEnemyPositions[i] - position;
                offset.y = 0f;
                nearestDistanceSqr = Mathf.Min(
                    nearestDistanceSqr,
                    offset.sqrMagnitude);
            }

            return nearestDistanceSqr;
        }

        private void PruneSpawnedEnemies()
        {
            for (int i = m_spawnedEnemies.Count - 1; i >= 0; i--)
            {
                EnemyEntity enemy = m_spawnedEnemies[i];
                if (enemy == null ||
                    enemy.IsDestroy ||
                    enemy.CurrentHealth <= 0f)
                {
                    m_spawnedEnemies.RemoveAt(i);
                }
            }

            m_currentCount = m_spawnedEnemies.Count;
        }

        private Vector2 GetEvenSpawnOffset(int index)
        {
            float radius = Mathf.Max(0f, spawnRadius);
            if (radius <= 0f)
                return Vector2.zero;

            const float goldenAngle = 2.39996323f;
            const float radialSequenceStep = 0.754877666f;
            int sampleIndex = Mathf.Max(0, index);
            float radialSequence = Mathf.Repeat(
                (sampleIndex + 1) * radialSequenceStep,
                1f);
            float normalizedRadius = Mathf.Sqrt(radialSequence);
            float angle = sampleIndex * goldenAngle;

            Vector2 offset = new Vector2(
                Mathf.Cos(angle),
                Mathf.Sin(angle)) * normalizedRadius * radius;

            float jitter = Mathf.Clamp01(spawnPositionJitter);
            if (jitter > 0f)
            {
                float jitterRadius = radius / Mathf.Max(1f, Mathf.Sqrt(maxSpawnCount)) * jitter;
                offset += Random.insideUnitCircle * jitterRadius;
                if (offset.sqrMagnitude > radius * radius)
                    offset = offset.normalized * radius;
            }

            return offset;
        }

        private bool TryResolveSpawnPosition(
            Vector3 candidate,
            out Vector3 spawnPosition)
        {
            spawnPosition = candidate;

            Vector3 groundedCheckCenter = candidate;
            if (TryFindGroundBelow(candidate, out RaycastHit groundHit))
            {
                if (!IsValidSpawnGround(groundHit.collider))
                    return false;

                float height = Mathf.Max(
                    spawnClearanceRadius * 2f,
                    spawnClearanceHeight);
                groundedCheckCenter = groundHit.point + Vector3.up * (height * 0.5f);
                spawnPosition = groundHit.point + Vector3.up * Mathf.Max(0f, spawnHeight);
            }

            if (!IsSpawnPositionClear(candidate))
                return false;

            if (!IsSpawnPositionClear(
                    groundedCheckCenter,
                    groundHit.collider))
            {
                return false;
            }

            if (IsInsideBlockingBounds(
                    groundedCheckCenter,
                    groundHit.collider))
            {
                return false;
            }

            return !IsEnclosedByObstacles(groundedCheckCenter, groundHit.collider);
        }

        private bool TryFindGroundBelow(Vector3 position, out RaycastHit groundHit)
        {
            groundHit = default;
            Vector3 origin =
                position + Vector3.up * Mathf.Max(0.1f, spawnClearanceHeight);
            float distance =
                Mathf.Max(0.1f, spawnGroundRayDistance + spawnClearanceHeight);
            RaycastHit[] hits = Physics.RaycastAll(
                origin,
                Vector3.down,
                distance,
                spawnGroundLayer,
                QueryTriggerInteraction.Ignore);

            if (hits == null || hits.Length == 0)
                return false;

            float nearestDistance = float.PositiveInfinity;
            for (int i = 0; i < hits.Length; i++)
            {
                RaycastHit hit = hits[i];
                if (hit.collider == null)
                    continue;

                if (hit.distance >= nearestDistance)
                    continue;

                nearestDistance = hit.distance;
                groundHit = hit;
            }

            return groundHit.collider != null;
        }

        private bool IsSpawnPositionClear(
            Vector3 position,
            Collider supportingGround = null)
        {
            float radius = Mathf.Max(0.05f, spawnClearanceRadius);
            float height = Mathf.Max(radius * 2f, spawnClearanceHeight);
            float capsuleOffset = Mathf.Max(0f, height * 0.5f - radius);
            Vector3 bottom = position + Vector3.down * capsuleOffset;
            Vector3 top = position + Vector3.up * capsuleOffset;

            Collider[] hits = Physics.OverlapCapsule(
                bottom,
                top,
                radius,
                spawnObstacleLayer,
                QueryTriggerInteraction.Ignore
            );

            for (int i = 0; i < hits.Length; i++)
            {
                if (hits[i] == supportingGround)
                    continue;

                if (IsBlockingSpawnObstacle(hits[i]))
                    return false;
            }

            return true;
        }

        private bool IsInsideBlockingBounds(
            Vector3 position,
            Collider supportingGround)
        {
            int hitCount = Physics.OverlapSphereNonAlloc(
                position,
                Mathf.Max(0.5f, spawnBuildingBoundsProbeRadius),
                m_spawnBoundsHits,
                spawnObstacleLayer,
                QueryTriggerInteraction.Ignore);

            Vector2 positionXZ = new Vector2(position.x, position.z);
            for (int i = 0; i < hitCount; i++)
            {
                Collider hit = m_spawnBoundsHits[i];
                m_spawnBoundsHits[i] = null;

                if (hit == null || hit == supportingGround)
                    continue;

                if (!IsBlockingSpawnObstacle(hit))
                    continue;

                if (HasGroundNameInHierarchy(hit.transform))
                    continue;

                Bounds bounds = hit.bounds;
                if (position.y < bounds.min.y || position.y > bounds.max.y)
                    continue;

                Vector2 min = new Vector2(bounds.min.x, bounds.min.z);
                Vector2 max = new Vector2(bounds.max.x, bounds.max.z);
                if (positionXZ.x >= min.x &&
                    positionXZ.x <= max.x &&
                    positionXZ.y >= min.y &&
                    positionXZ.y <= max.y)
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsEnclosedByObstacles(
            Vector3 position,
            Collider supportingGround)
        {
            float rayDistance = Mathf.Max(0.5f, spawnEnclosureProbeDistance);
            Vector3 origin = position + Vector3.up * Mathf.Max(0.1f, spawnClearanceRadius);
            int blockedDirections = 0;

            for (int i = 0; i < 8; i++)
            {
                float angle = i * Mathf.PI * 0.25f;
                Vector3 direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));

                int hitCount = Physics.RaycastNonAlloc(
                    origin,
                    direction,
                    m_spawnEnclosureHits,
                    rayDistance,
                    spawnObstacleLayer,
                    QueryTriggerInteraction.Ignore);

                if (HasBlockingHit(hitCount, supportingGround))
                    blockedDirections++;
            }

            return blockedDirections >= spawnEnclosureHitThreshold;
        }

        private bool HasBlockingHit(int hitCount, Collider supportingGround)
        {
            for (int i = 0; i < hitCount; i++)
            {
                Collider hit = m_spawnEnclosureHits[i].collider;
                m_spawnEnclosureHits[i] = default;

                if (hit == null || hit == supportingGround)
                    continue;

                if (!IsBlockingSpawnObstacle(hit))
                    continue;

                if (HasGroundNameInHierarchy(hit.transform))
                    continue;

                return true;
            }

            return false;
        }

        private bool IsValidSpawnGround(Collider hitCollider)
        {
            if (hitCollider == null || hitCollider.isTrigger)
                return false;

            if (IsActiveSpawnBuilding(hitCollider))
                return false;

            if (hitCollider.GetComponentInParent<DestructibleBuilding>() != null)
                return false;

            if (hitCollider.GetComponentInParent<EnemyEntity>() != null)
                return false;

            if (hitCollider.GetComponentInParent<EnemyCPUEntity>() != null)
                return false;

            if (hitCollider.GetComponentInParent<PlayerEntity>() != null)
                return false;

            if (hitCollider.GetComponentInParent<ItemEntity>() != null)
                return false;

            if (hitCollider.GetComponentInParent<BuffItemEntity>() != null)
                return false;

            return HasGroundNameInHierarchy(hitCollider.transform);
        }

        private static bool HasGroundNameInHierarchy(Transform target)
        {
            Transform current = target;
            while (current != null)
            {
                string objectName = current.name;
                if (objectName.StartsWith(
                        "Ground",
                        System.StringComparison.OrdinalIgnoreCase) ||
                    objectName.StartsWith(
                        "Plane",
                        System.StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                current = current.parent;
            }

            return false;
        }

        private bool IsBlockingSpawnObstacle(Collider hitCollider)
        {
            if (hitCollider == null || hitCollider.isTrigger)
                return false;

            if (IsActiveSpawnBuilding(hitCollider))
                return true;

            if (hitCollider.GetComponentInParent<DestructibleBuilding>() != null)
                return false;

            if (hitCollider.GetComponentInParent<EnemyEntity>() != null)
                return false;

            if (hitCollider.GetComponentInParent<EnemyCPUEntity>() != null)
                return false;

            if (hitCollider.GetComponentInParent<PlayerEntity>() != null)
                return false;

            if (hitCollider.GetComponentInParent<ItemEntity>() != null)
                return false;

            if (hitCollider.GetComponentInParent<BuffItemEntity>() != null)
                return false;

            return true;
        }

        private bool IsActiveSpawnBuilding(Collider hitCollider)
        {
            if (hitCollider == null)
                return false;

            DestructibleBuilding building =
                hitCollider.GetComponentInParent<DestructibleBuilding>();
            return building != null && !building.IsDestroyed;
        }

        public void NotifyEnemyDead()
        {
            PruneSpawnedEnemies();
        }

        private void OnDrawGizmos()
        {
            DrawSpawnAreaGizmo(false);
        }

        private void OnDrawGizmosSelected()
        {
            DrawSpawnAreaGizmo(true);
        }

        private void DrawSpawnAreaGizmo(bool selected)
        {
            if (spawnRadius <= 0f)
                return;

            Color fillColor = spawnAreaGizmoColor;
            Color wireColor = spawnAreaWireGizmoColor;

            if (!selected)
            {
                fillColor.a *= 0.35f;
                wireColor.a *= 0.55f;
            }

            Vector3 center = transform.position;
            Vector3 size = new Vector3(spawnRadius * 2f, 0.02f, spawnRadius * 2f);

            Gizmos.color = fillColor;
            Gizmos.DrawCube(center, size);

            Gizmos.color = wireColor;
            DrawCircle(center, spawnRadius, 64);
            Gizmos.DrawLine(center, center + Vector3.up * Mathf.Max(0.1f, spawnHeight));
        }

        private void DrawCircle(Vector3 center, float radius, int segments)
        {
            Vector3 previousPoint = center + Vector3.right * radius;

            for (int i = 1; i <= segments; i++)
            {
                float angle = i / (float)segments * Mathf.PI * 2f;
                Vector3 nextPoint = center + new Vector3(
                    Mathf.Cos(angle) * radius,
                    0f,
                    Mathf.Sin(angle) * radius
                );

                Gizmos.DrawLine(previousPoint, nextPoint);
                previousPoint = nextPoint;
            }
        }
    }
}
