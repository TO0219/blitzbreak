namespace MyGame
{
    public class WanderNode : BTNode
    {
        private EnemyCPUEntity m_enemy;

        public WanderNode(EnemyCPUEntity enemy)
        {
            m_enemy = enemy;
        }

        public override NodeState Evaluate()
        {
            m_enemy.WanderMove();

            return NodeState.Running;
        }
    }
}
