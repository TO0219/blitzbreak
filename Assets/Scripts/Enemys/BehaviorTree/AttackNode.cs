namespace MyGame
{
    public class AttackNode : BTNode
    {
        private EnemyCPUEntity m_enemy;

        public AttackNode(EnemyCPUEntity enemy)
        {
            m_enemy = enemy;
        }

        public override NodeState Evaluate()
        {
            if (m_enemy.CPUTarget == null)
                return NodeState.Failure;

            if (m_enemy.TargetIsItems)
                return NodeState.Failure;

            float distance = m_enemy.DistanceToTarget();

            if (distance <= m_enemy.CPU_ChaseRange)
            {
                m_enemy.AttackMove();

                if (m_enemy.CanFire())
                {
                    m_enemy.FireWeapon();
                }

                return NodeState.Success;
            }

            return NodeState.Failure;
        }
    }
}
