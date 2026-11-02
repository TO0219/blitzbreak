using UnityEngine;

namespace MyGame
{
    public class InfightEnemyEntity : BaseEnemyEntity
    {
        protected virtual void Start()
        {
            InitializeInfight();
        }

        protected void InitializeInfight()
        {
            SearchTarget();
            m_state = EnemyState.Wander;

            enemyEntity = GetComponent<EnemyEntity>();

            if (enemyEntity != null)
            {
                moveSpeed = enemyEntity.MoveSpeed;
                rotateSpeed = enemyEntity.RotateSpeed;
                chaseRange = enemyEntity.ChaseRange;
                infightRange = enemyEntity.InfightRange;
                bullet = enemyEntity.EnemyBulletPrefab;
                enemyMuzzleTransform = enemyEntity.EnemyMuzzleTransform;
                fireInterval = enemyEntity.FireInterval;
                enableWander = enemyEntity.EnableWander;
                wanderRadius = enemyEntity.WanderRadius;
                wanderInterval = enemyEntity.WanderInterval;
                wanderWaitTime = enemyEntity.WanderWaitTime;
                wanderDestinationSearchAttempts =
                    enemyEntity.WanderDestinationSearchAttempts;
                wanderStuckResetTime = enemyEntity.WanderStuckResetTime;
                wanderStuckMoveThreshold = enemyEntity.WanderStuckMoveThreshold;
            }

            if (enemyEntity != null && enemyEntity.IsAir)
            {
                Vector3 pos = transform.position;
                pos.y = 5f;   // 空中高度
                transform.position = pos;
            }

            PickNewWanderTarget();
        }

        protected virtual void Update()
        {
            if (!CanRunEnemyLogic())
                return;

            RefreshTarget();
            if (!HasUsableTarget())
            {
                Wander();
                return;
            }

            m_fireTimer += Time.deltaTime;

            Vector3 direction = GetDirection(m_target.transform.position, transform.position);
            float distance = direction.magnitude;

            if (distance <= infightRange)
            {
                m_state = EnemyState.Attack;
            }
            else if (distance <= chaseRange)
            {
                m_state = EnemyState.Chase;
            }
            else
            {
                m_state = EnemyState.Wander;
            }

            switch (m_state)
            {
                case EnemyState.Wander:
                    Wander();
                    break;

                case EnemyState.Chase:
                    Chase(direction);
                    if (m_fireTimer >= fireInterval)
                    {
                        Fire();
                        m_fireTimer = 0f;
                    }

                    break;

                case EnemyState.Attack:
                    if (m_fireTimer >= fireInterval)
                    {
                        Fire();
                        m_fireTimer = 0f;
                    }
                    break;
            }
        }
    }
}
