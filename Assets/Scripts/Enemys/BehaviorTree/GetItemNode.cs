namespace MyGame
{
    public class GetItemNode : BTNode
    {
        private EnemyCPUEntity m_enemy;

        public GetItemNode(EnemyCPUEntity enemy)
        {
            m_enemy = enemy;
        }

        public override NodeState Evaluate()
        {
            if (m_enemy.CPUTarget == null)
                return NodeState.Failure;

            if (!m_enemy.TargetIsItems)
                return NodeState.Failure;

            m_enemy.LookTarget();
            m_enemy.MoveToTarget();
            m_enemy.CounterAttackWhileMovingToItem();

            return NodeState.Running;
        }
    }
}
