using Unity.Netcode;
using UnityEngine;
using System.Collections.Generic;

namespace MyGame
{
    public class BaseEnemyEntity : Entity
    {
        private struct WanderReservation
        {
            public BaseEnemyEntity Owner;
            public Vector3 Position;
            public float Radius;
            public float ExpiresAt;
        }

        private static readonly List<WanderReservation> WanderReservations =
            new List<WanderReservation>(64);

        public enum EnemyState
        {
            Wander,
            Chase,
            Attack
        }

        protected EnemyState m_state;
        protected EnemyEntity enemyEntity;

        [SerializeField] protected float moveSpeed = 2f;
        [SerializeField] protected float rotateSpeed = 5f;
        [SerializeField] protected float chaseRange = 10f;
        [SerializeField] protected float attackRange = 3f;
        [SerializeField] protected float infightRange = 1f;
        [SerializeField, Min(0.05f)] protected float targetSearchInterval = 0.2f;

        [SerializeField] protected GameObject bullet;
        [SerializeField] protected Transform enemyMuzzleTransform;
        [SerializeField] protected float fireInterval = 1f;

        [SerializeField] protected bool enableWander = true;
        [SerializeField] protected float wanderRadius = 8f;
        [SerializeField] protected float wanderInterval = 6f;
        [SerializeField, Min(0f)] protected float wanderWaitTime = 2f;

        [Header("Wander Obstacle")]
        [SerializeField] private LayerMask wanderBuildingMask = Physics.DefaultRaycastLayers;
        [SerializeField] private LayerMask movementObstacleMask = Physics.DefaultRaycastLayers;
        [SerializeField, Min(0.05f)] private float movementSkinWidth = 0.05f;
        [SerializeField, Min(1)] protected int wanderDestinationSearchAttempts = 16;
        [SerializeField, Min(0.05f)] private float wanderDestinationCheckRadius = 1.0f;
        [SerializeField, Min(0.1f)] private float wanderDestinationSeparationRadius = 2.5f;
        [SerializeField, Min(0.1f)] private float wanderDestinationReservationRadius = 3.5f;
        [SerializeField, Min(0.05f)] protected float wanderObstacleProbeRadius = 0.65f;
        [SerializeField, Min(0.05f)] protected float wanderObstacleProbeHeight = 0.8f;
        [SerializeField, Min(0.1f)] private float wanderObstacleProbeDistance = 3.0f;
        [SerializeField, Min(0.1f)] private float wanderAvoidanceStrength = 1.2f;
        [SerializeField, Min(0.1f)] private float wanderSeparationRadius = 2.0f;
        [SerializeField, Min(0f)] private float wanderSeparationStrength = 0.9f;
        [SerializeField, Min(0.1f)] protected float wanderStuckResetTime = 1.5f;
        [SerializeField, Min(0.001f)] protected float wanderStuckMoveThreshold = 0.05f;

        [SerializeField] protected bool dropBuff = false;

        protected float m_fireTimer;
        protected Transform m_target;
        protected Vector3 m_wanderTarget;
        protected float m_wanderTimer;
        protected float m_wanderWaitTimer;
        protected GameRuleEntity m_gameRuleEntity;
        private readonly Collider[] m_targetSearchHits = new Collider[64];
        private readonly RaycastHit[] m_wanderObstacleHits = new RaycastHit[16];
        private readonly RaycastHit[] m_movementObstacleHits = new RaycastHit[16];
        private readonly Collider[] m_wanderDestinationHits = new Collider[16];
        private readonly Collider[] m_wanderAllyHits = new Collider[32];
        private float m_targetSearchTimer;
        private Vector3 m_lastWanderPosition;
        private float m_wanderStuckTimer;
        private int m_wanderAvoidanceSide = 1;
        private bool m_hasWanderReservation;

        public Transform ChaseTarget => m_target;
        public float MoveSpeed => moveSpeed;
        public float RotateSpeed => rotateSpeed;

        protected bool CanRunEnemyLogic()
        {
            if (enemyEntity == null)
                enemyEntity = GetComponent<EnemyEntity>();

            if (enemyEntity != null && !enemyEntity.CanSimulate)
                return false;

            if (m_gameRuleEntity == null)
            {
                m_gameRuleEntity = FindAnyObjectByType<GameRuleEntity>();
            }

            return m_gameRuleEntity == null || m_gameRuleEntity.CanPlay;
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            ReleaseWanderReservation();
        }

        protected virtual void Wander()
        {
            if (!enableWander)
            {
                ReleaseWanderReservation();
                m_wanderWaitTimer = 0f;
                return;
            }

            if (m_wanderWaitTimer > 0f)
            {
                m_wanderWaitTimer -= Time.deltaTime;
                return;
            }

            m_wanderTimer += Time.deltaTime;
            UpdateWanderStuckTimer();

            if (m_wanderTimer >= wanderInterval || m_wanderStuckTimer >= wanderStuckResetTime)
            {
                PickNewWanderTarget();
                m_wanderTimer = 0f;
                m_wanderStuckTimer = 0f;
            }

            Vector3 direction = m_wanderTarget - transform.position;
            direction.y = 0f;

            if (direction.sqrMagnitude < 0.5f)
            {
                BeginWanderWait();
                m_wanderTimer = 0f;
                return;
            }

            Vector3 moveDirection = GetWanderMoveDirection(direction.normalized);
            if (moveDirection.sqrMagnitude < 0.001f)
                return;

            Quaternion targetRotation = Quaternion.LookRotation(moveDirection);

            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRotation,
                rotateSpeed * Time.deltaTime
            );

            MoveWithinSpawnArea(moveDirection * moveSpeed * Time.deltaTime);
        }

        protected virtual void Chase(Vector3 direction)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);

            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRotation,
                rotateSpeed * Time.deltaTime
            );

            MoveWithinSpawnArea(transform.forward * moveSpeed * Time.deltaTime);
        }

        protected virtual void Fire()
        {
            if (bullet == null || enemyMuzzleTransform == null || m_target == null)
                return;

            Vector3 dir =
                (GetTargetAimPosition() - enemyMuzzleTransform.position).normalized;

            bool networked =
                NetworkManager.Singleton != null &&
                NetworkManager.Singleton.IsListening;
            if (networked && !NetworkManager.Singleton.IsServer)
                return;

            enemyEntity?.PlayAttackAnimation();

            GameObject bulletObj = networked
                ? Instantiate(
                    bullet,
                    enemyMuzzleTransform.position,
                    Quaternion.LookRotation(dir))
                : RuntimeObjectPool.Spawn(
                    bullet,
                    enemyMuzzleTransform.position,
                    Quaternion.LookRotation(dir));

            Rigidbody rb = bulletObj.GetComponent<Rigidbody>();

            if (rb != null)
            {
                EnemyBulletEntity bulletEntity =
                    bulletObj.GetComponent<EnemyBulletEntity>();

                if (bulletEntity != null)
                {
                    bulletEntity.Setup(this);
                    rb.linearVelocity = dir * bulletEntity.Speed;
                }
            }

            if (networked)
            {
                NetworkObject networkObject =
                    bulletObj.GetComponent<NetworkObject>();
                if (networkObject != null)
                    networkObject.Spawn(true);
                else
                    Destroy(bulletObj);
            }

            global::AudioManager.Instance?.PlayCueAtPosition(
                global::AudioCue.EnemyGroundShot,
                enemyMuzzleTransform.position,
                0.75f);
        }

        protected virtual void PickNewWanderTarget()
        {
            int searchAttempts = Mathf.Max(1, wanderDestinationSearchAttempts);
            for (int i = 0; i < searchAttempts; i++)
            {
                Vector2 random = Random.insideUnitCircle * wanderRadius;
                Vector3 candidate = transform.position + new Vector3(
                    random.x,
                    0f,
                    random.y
                );

                candidate = ClampToSpawnArea(candidate);
                if (IsValidWanderDestination(candidate))
                {
                    m_wanderTarget = candidate;
                    ReserveWanderDestination(candidate);
                    ResetWanderProgress();
                    return;
                }
            }

            for (int i = 0; i < searchAttempts; i++)
            {
                Vector2 random = Random.insideUnitCircle * wanderRadius;
                Vector3 candidate = transform.position + new Vector3(
                    random.x,
                    0f,
                    random.y
                );

                candidate = ClampToSpawnArea(candidate);
                if (!IsWanderDestinationInsideBuilding(candidate))
                {
                    m_wanderTarget = candidate;
                    ReserveWanderDestination(candidate);
                    ResetWanderProgress();
                    return;
                }
            }

            SetEmergencyWanderTarget();
        }

        private void SetEmergencyWanderTarget()
        {
            Vector2 randomDirection = Random.insideUnitCircle;
            if (randomDirection.sqrMagnitude < 0.001f)
                randomDirection = Vector2.right * m_wanderAvoidanceSide;

            randomDirection.Normalize();
            float fallbackDistance = Mathf.Max(1f, wanderRadius);
            Vector3 candidate = transform.position + new Vector3(
                randomDirection.x * fallbackDistance,
                0f,
                randomDirection.y * fallbackDistance);
            candidate = ClampToSpawnArea(candidate);

            Vector3 offset = candidate - transform.position;
            offset.y = 0f;
            if (offset.sqrMagnitude < 0.5f &&
                TryGetSpawnArea(out Vector3 spawnCenter, out _))
            {
                candidate = spawnCenter;
                candidate.y = transform.position.y;
            }

            m_wanderTarget = candidate;
            ReserveWanderDestination(candidate);
            ResetWanderProgress();
            m_wanderAvoidanceSide *= -1;
        }

        private Vector3 GetWanderMoveDirection(Vector3 desiredDirection)
        {
            if (desiredDirection.sqrMagnitude < 0.001f)
                return Vector3.zero;

            desiredDirection.y = 0f;
            desiredDirection.Normalize();

            Vector3 separatedDirection = ApplyWanderSeparation(desiredDirection);
            if (!TryFindBuildingInDirection(separatedDirection, out _))
                return separatedDirection;

            Vector3 right = Vector3.Cross(Vector3.up, separatedDirection).normalized;
            Vector3 firstSide = right * m_wanderAvoidanceSide;
            Vector3 secondSide = -firstSide;

            Vector3 firstCandidate =
                (separatedDirection + firstSide * wanderAvoidanceStrength).normalized;
            Vector3 secondCandidate =
                (separatedDirection + secondSide * wanderAvoidanceStrength).normalized;

            bool firstClear = !TryFindBuildingInDirection(firstCandidate, out _);
            bool secondClear = !TryFindBuildingInDirection(secondCandidate, out _);

            if (firstClear)
                return firstCandidate;

            if (secondClear)
            {
                m_wanderAvoidanceSide *= -1;
                return secondCandidate;
            }

            Vector3 sideOnly = firstSide.normalized;
            if (!TryFindBuildingInDirection(sideOnly, out _))
                return sideOnly;

            m_wanderAvoidanceSide *= -1;
            sideOnly = secondSide.normalized;
            return !TryFindBuildingInDirection(sideOnly, out _) ? sideOnly : Vector3.zero;
        }

        private Vector3 ApplyWanderSeparation(Vector3 desiredDirection)
        {
            Vector3 separation = GetWanderSeparationDirection();
            if (separation.sqrMagnitude < 0.001f || wanderSeparationStrength <= 0f)
                return desiredDirection;

            Vector3 separatedDirection =
                (desiredDirection + separation * wanderSeparationStrength).normalized;

            return separatedDirection.sqrMagnitude >= 0.001f
                ? separatedDirection
                : desiredDirection;
        }

        private bool TryFindBuildingInDirection(Vector3 direction, out RaycastHit nearestHit)
        {
            nearestHit = default;
            direction.y = 0f;

            if (direction.sqrMagnitude < 0.001f)
                return false;

            direction.Normalize();
            Vector3 origin = transform.position + Vector3.up * wanderObstacleProbeHeight;
            int hitCount = Physics.SphereCastNonAlloc(
                origin,
                wanderObstacleProbeRadius,
                direction,
                m_wanderObstacleHits,
                wanderObstacleProbeDistance,
                wanderBuildingMask,
                QueryTriggerInteraction.Ignore);

            float nearestDistance = float.PositiveInfinity;
            bool found = false;

            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = m_wanderObstacleHits[i];
                Collider hitCollider = hit.collider;
                if (hitCollider == null || IsOwnCollider(hitCollider))
                    continue;

                if (!IsActiveBuildingCollider(hitCollider))
                    continue;

                if (hit.distance >= nearestDistance)
                    continue;

                nearestDistance = hit.distance;
                nearestHit = hit;
                found = true;
            }

            return found;
        }

        private bool IsWanderDestinationInsideBuilding(Vector3 destination)
        {
            Vector3 center = destination + Vector3.up * wanderObstacleProbeHeight;
            int hitCount = Physics.OverlapSphereNonAlloc(
                center,
                wanderDestinationCheckRadius,
                m_wanderDestinationHits,
                wanderBuildingMask,
                QueryTriggerInteraction.Ignore);

            for (int i = 0; i < hitCount; i++)
            {
                Collider hitCollider = m_wanderDestinationHits[i];
                if (hitCollider == null || IsOwnCollider(hitCollider))
                    continue;

                if (IsActiveBuildingCollider(hitCollider))
                    return true;
            }

            return false;
        }

        private bool IsValidWanderDestination(Vector3 destination)
        {
            return !IsWanderDestinationInsideBuilding(destination) &&
                !IsWanderDestinationCrowded(destination) &&
                !IsWanderDestinationReserved(destination);
        }

        private bool IsWanderDestinationCrowded(Vector3 destination)
        {
            float radius = Mathf.Max(0.1f, wanderDestinationSeparationRadius);
            int hitCount = Physics.OverlapSphereNonAlloc(
                destination + Vector3.up * wanderObstacleProbeHeight,
                radius,
                m_wanderAllyHits,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Collide);

            for (int i = 0; i < hitCount; i++)
            {
                Collider hitCollider = m_wanderAllyHits[i];
                if (hitCollider == null || IsOwnCollider(hitCollider))
                    continue;

                if (IsOtherEnemyCollider(hitCollider))
                    return true;
            }

            return false;
        }

        private bool IsWanderDestinationReserved(Vector3 destination)
        {
            CleanupWanderReservations();

            float radius = Mathf.Max(0.1f, wanderDestinationReservationRadius);
            float radiusSqr = radius * radius;
            Vector3 flatDestination = destination;
            flatDestination.y = 0f;

            for (int i = 0; i < WanderReservations.Count; i++)
            {
                WanderReservation reservation = WanderReservations[i];
                if (reservation.Owner == this)
                    continue;

                Vector3 reservedPosition = reservation.Position;
                reservedPosition.y = 0f;
                float combinedRadius = Mathf.Max(radius, reservation.Radius);
                float combinedRadiusSqr = Mathf.Max(radiusSqr, combinedRadius * combinedRadius);

                if ((flatDestination - reservedPosition).sqrMagnitude <= combinedRadiusSqr)
                    return true;
            }

            return false;
        }

        private void ReserveWanderDestination(Vector3 destination)
        {
            CleanupWanderReservations();
            ReleaseWanderReservation();

            WanderReservations.Add(new WanderReservation
            {
                Owner = this,
                Position = destination,
                Radius = Mathf.Max(0.1f, wanderDestinationReservationRadius),
                ExpiresAt = Time.time + Mathf.Max(2f, wanderInterval + 2f)
            });

            m_hasWanderReservation = true;
        }

        private void ReleaseWanderReservation()
        {
            if (!m_hasWanderReservation)
                return;

            for (int i = WanderReservations.Count - 1; i >= 0; i--)
            {
                if (WanderReservations[i].Owner == this)
                    WanderReservations.RemoveAt(i);
            }

            m_hasWanderReservation = false;
        }

        private static void CleanupWanderReservations()
        {
            float now = Time.time;
            for (int i = WanderReservations.Count - 1; i >= 0; i--)
            {
                WanderReservation reservation = WanderReservations[i];
                if (reservation.Owner == null ||
                    !reservation.Owner.isActiveAndEnabled ||
                    reservation.ExpiresAt <= now)
                {
                    WanderReservations.RemoveAt(i);
                }
            }
        }

        private Vector3 GetWanderSeparationDirection()
        {
            float radius = Mathf.Max(0.1f, wanderSeparationRadius);
            int hitCount = Physics.OverlapSphereNonAlloc(
                transform.position,
                radius,
                m_wanderAllyHits,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Collide);

            Vector3 separation = Vector3.zero;

            for (int i = 0; i < hitCount; i++)
            {
                Collider hitCollider = m_wanderAllyHits[i];
                if (hitCollider == null || IsOwnCollider(hitCollider))
                    continue;

                if (!IsOtherEnemyCollider(hitCollider))
                    continue;

                Vector3 away = transform.position - hitCollider.bounds.center;
                away.y = 0f;
                float distanceSqr = away.sqrMagnitude;
                if (distanceSqr < 0.001f)
                    continue;

                separation += away.normalized / Mathf.Max(0.1f, distanceSqr);
            }

            separation.y = 0f;
            return separation.sqrMagnitude >= 0.001f
                ? separation.normalized
                : Vector3.zero;
        }

        private bool IsActiveBuildingCollider(Collider hitCollider)
        {
            DestructibleBuilding building =
                hitCollider.GetComponentInParent<DestructibleBuilding>();

            return building != null && !building.IsDestroyed;
        }

        private bool IsOwnCollider(Collider hitCollider)
        {
            return hitCollider.transform.root == transform.root;
        }

        private bool IsOtherEnemyCollider(Collider hitCollider)
        {
            EnemyEntity otherEnemy = hitCollider.GetComponentInParent<EnemyEntity>();
            return otherEnemy != null && otherEnemy != enemyEntity;
        }

        private void UpdateWanderStuckTimer()
        {
            Vector3 currentPosition = transform.position;
            currentPosition.y = 0f;
            Vector3 lastPosition = m_lastWanderPosition;
            lastPosition.y = 0f;

            float moveThreshold = Mathf.Max(0.001f, wanderStuckMoveThreshold);
            if ((currentPosition - lastPosition).sqrMagnitude <= moveThreshold * moveThreshold)
            {
                m_wanderStuckTimer += Time.deltaTime;
                return;
            }

            m_wanderStuckTimer = 0f;
            m_lastWanderPosition = transform.position;
        }

        private void ResetWanderProgress()
        {
            m_lastWanderPosition = transform.position;
            m_wanderStuckTimer = 0f;
        }

        private void BeginWanderWait()
        {
            ReleaseWanderReservation();
            m_wanderWaitTimer = Mathf.Max(0f, wanderWaitTime);

            if (m_wanderWaitTimer <= 0f)
                PickNewWanderTarget();
        }

        private Vector3 GetTargetAimPosition()
        {
            if (m_target == null)
                return transform.position;

            PlayerEntity player = m_target.GetComponentInParent<PlayerEntity>();
            if (player != null)
                return player.GetActiveTargetAimPoint();

            EnemyCPUEntity cpu = m_target.GetComponentInParent<EnemyCPUEntity>();
            if (cpu != null)
            {
                Collider cpuCollider = cpu.GetComponent<Collider>();
                if (cpuCollider == null)
                    cpuCollider = cpu.GetComponentInChildren<Collider>();

                if (cpuCollider != null)
                    return cpuCollider.bounds.center;

                return cpu.transform.position + Vector3.up;
            }

            Collider targetCollider = m_target.GetComponent<Collider>();
            if (targetCollider == null)
                targetCollider = m_target.GetComponentInParent<Collider>();

            if (targetCollider != null)
                return targetCollider.bounds.center;

            return m_target.position;
        }

        protected virtual void SearchTarget()
        {
            if (m_target != null && !IsValidChaseTarget(m_target))
                m_target = null;

            m_targetSearchTimer -= Time.deltaTime;
            if (m_targetSearchTimer > 0f)
                return;

            m_targetSearchTimer = Mathf.Max(0.05f, targetSearchInterval);

            if (TryFindTargetByActorSearch(out Transform actorTarget))
            {
                m_target = actorTarget;
                return;
            }

            float nearestDistance = Mathf.Infinity;
            Transform nearestTarget = null;

            int hitCount = Physics.OverlapSphereNonAlloc(
                transform.position,
                chaseRange * 4f,
                m_targetSearchHits,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Collide);

            for (int i = 0; i < hitCount; i++)
            {
                Collider col = m_targetSearchHits[i];
                if (col == null)
                    continue;

                if (col.transform.root == transform.root)
                    continue;

                EnemyCPUEntity enemyCPU = col.GetComponentInParent<EnemyCPUEntity>();
                PlayerEntity player = col.GetComponentInParent<PlayerEntity>();

                if (enemyCPU == null && player == null)
                    continue;

                float distance = GetHorizontalDistance(transform.position, col.transform.position);
                if (distance > chaseRange)
                    continue;

                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearestTarget = col.transform;
                }
            }

            m_target = nearestTarget;
        }

        protected bool HasUsableTarget()
        {
            return m_target != null && IsValidChaseTarget(m_target);
        }

        protected void RefreshTarget()
        {
            SearchTarget();
        }

        protected bool IsValidChaseTarget(Transform target)
        {
            if (target == null || !target.gameObject.activeInHierarchy)
                return false;

            PlayerEntity player = target.GetComponentInParent<PlayerEntity>();
            if (player != null)
                return player.isActiveAndEnabled &&
                       !player.IsRespawning &&
                       player.CurrentHealth > 0f;

            EnemyCPUEntity cpu = target.GetComponentInParent<EnemyCPUEntity>();
            if (cpu != null)
                return cpu.isActiveAndEnabled &&
                       !cpu.IsRespawning &&
                       cpu.CPUCurrentHealth > 0f;

            return false;
        }

        private bool TryFindTargetByActorSearch(out Transform target)
        {
            target = null;
            float nearestDistance = Mathf.Infinity;

            PlayerEntity[] players =
                FindObjectsByType<PlayerEntity>(FindObjectsInactive.Exclude);
            for (int i = 0; i < players.Length; i++)
            {
                PlayerEntity player = players[i];
                if (player == null || !IsValidChaseTarget(player.transform))
                    continue;

                float distance = GetHorizontalDistance(transform.position, player.transform.position);
                if (distance > chaseRange || distance >= nearestDistance)
                    continue;

                nearestDistance = distance;
                target = player.transform;
            }

            EnemyCPUEntity[] cpus =
                FindObjectsByType<EnemyCPUEntity>(FindObjectsInactive.Exclude);
            for (int i = 0; i < cpus.Length; i++)
            {
                EnemyCPUEntity cpu = cpus[i];
                if (cpu == null || !IsValidChaseTarget(cpu.transform))
                    continue;

                float distance = GetHorizontalDistance(transform.position, cpu.transform.position);
                if (distance > chaseRange || distance >= nearestDistance)
                    continue;

                nearestDistance = distance;
                target = cpu.transform;
            }

            return target != null;
        }

        protected virtual Vector3 GetDirection(Vector3 targetPos, Vector3 transformPos)
        {
            Vector3 dir = targetPos - transformPos;
            dir.y = 0f;
            return dir;
        }

        private float GetHorizontalDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }

        protected void MoveWithinSpawnArea(Vector3 delta)
        {
            if (delta.sqrMagnitude <= 0f)
                return;

            Vector3 currentPosition = transform.position;
            Vector3 targetPosition = ClampToSpawnArea(currentPosition + delta);
            Vector3 clampedDelta = targetPosition - currentPosition;
            if (clampedDelta.sqrMagnitude <= 0f)
                return;

            transform.position = GetObstacleSafeMovePosition(currentPosition, clampedDelta);
        }

        private Vector3 GetObstacleSafeMovePosition(Vector3 currentPosition, Vector3 delta)
        {
            Vector3 horizontalDelta = delta;
            horizontalDelta.y = 0f;
            float distance = horizontalDelta.magnitude;
            if (distance <= 0.001f)
                return currentPosition + delta;

            if (!TryGetMovementCapsule(
                    currentPosition,
                    out Vector3 bottom,
                    out Vector3 top,
                    out float radius))
            {
                return currentPosition + delta;
            }

            Vector3 direction = horizontalDelta / distance;
            float nearestDistance = GetNearestMovementObstacleDistance(
                bottom,
                top,
                radius,
                direction,
                distance);

            if (TryGetMovementFootprintCapsule(
                    currentPosition,
                    out Vector3 footprintBottom,
                    out Vector3 footprintTop,
                    out float footprintRadius))
            {
                nearestDistance = Mathf.Min(
                    nearestDistance,
                    GetNearestMovementObstacleDistance(
                        footprintBottom,
                        footprintTop,
                        footprintRadius,
                        direction,
                        distance));
            }

            if (float.IsPositiveInfinity(nearestDistance))
                return currentPosition + delta;

            float safeDistance = Mathf.Max(0f, nearestDistance - movementSkinWidth);
            Vector3 safeDelta = direction * Mathf.Min(distance, safeDistance);
            safeDelta.y = delta.y;
            return currentPosition + safeDelta;
        }

        private float GetNearestMovementObstacleDistance(
            Vector3 bottom,
            Vector3 top,
            float radius,
            Vector3 direction,
            float distance)
        {
            int hitCount = Physics.CapsuleCastNonAlloc(
                bottom,
                top,
                radius,
                direction,
                m_movementObstacleHits,
                distance + movementSkinWidth,
                movementObstacleMask,
                QueryTriggerInteraction.Ignore);

            float nearestDistance = float.PositiveInfinity;
            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = m_movementObstacleHits[i];
                Collider hitCollider = hit.collider;
                if (hitCollider == null || !IsBlockingObstacle(hitCollider))
                    continue;

                if (hit.distance < nearestDistance)
                    nearestDistance = hit.distance;
            }

            return nearestDistance;
        }

        private bool TryGetMovementCapsule(
            Vector3 position,
            out Vector3 bottom,
            out Vector3 top,
            out float radius)
        {
            if (TryGetOwnColliderBounds(out Bounds bounds))
            {
                Vector3 offset = position - transform.position;
                bounds.center += offset;
                radius = Mathf.Max(0.05f, Mathf.Min(bounds.extents.x, bounds.extents.z));
                float bottomY = bounds.min.y + radius;
                float topY = bounds.max.y - radius;

                if (topY < bottomY)
                    topY = bottomY;

                bottom = new Vector3(bounds.center.x, bottomY, bounds.center.z);
                top = new Vector3(bounds.center.x, topY, bounds.center.z);
                return true;
            }

            radius = Mathf.Max(0.05f, wanderObstacleProbeRadius);
            bottom = position + Vector3.up * radius;
            top = position + Vector3.up * Mathf.Max(radius, wanderObstacleProbeHeight);
            return true;
        }

        protected virtual bool TryGetMovementFootprintCapsule(
            Vector3 position,
            out Vector3 bottom,
            out Vector3 top,
            out float radius)
        {
            radius = Mathf.Max(0.05f, wanderObstacleProbeRadius);
            float height = Mathf.Max(radius, wanderObstacleProbeHeight);
            bottom = position + Vector3.up * radius;
            top = position + Vector3.up * height;
            return true;
        }

        private bool TryGetOwnColliderBounds(out Bounds bounds)
        {
            Collider[] colliders = GetComponentsInChildren<Collider>(true);
            bool hasBounds = false;
            bounds = default;

            for (int i = 0; i < colliders.Length; i++)
            {
                Collider ownCollider = colliders[i];
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

        private bool IsBlockingObstacle(Collider hitCollider)
        {
            if (hitCollider == null || hitCollider.isTrigger || IsOwnCollider(hitCollider))
                return false;

            DestructibleBuilding building =
                hitCollider.GetComponentInParent<DestructibleBuilding>();
            if (building != null)
                return !building.IsDestroyed;

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

        protected Vector3 ClampToSpawnArea(Vector3 position)
        {
            if (!TryGetSpawnArea(out Vector3 center, out float radius))
                return position;

            Vector3 offset = position - center;
            offset.y = 0f;

            if (offset.sqrMagnitude <= radius * radius)
                return position;

            Vector3 clampedOffset = offset.normalized * radius;
            return new Vector3(
                center.x + clampedOffset.x,
                position.y,
                center.z + clampedOffset.z
            );
        }

        protected bool TryGetSpawnArea(out Vector3 center, out float radius)
        {
            if (enemyEntity == null)
                enemyEntity = GetComponent<EnemyEntity>();

            if (enemyEntity == null)
            {
                center = default;
                radius = 0f;
                return false;
            }

            return enemyEntity.TryGetSpawnArea(out center, out radius);
        }
    }
}
