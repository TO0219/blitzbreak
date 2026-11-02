using System.Collections.Generic;

namespace MyGame
{
    public class SelectorNode : BTNode
    {
        private List<BTNode> m_nodes;

        public SelectorNode(List<BTNode> nodes)
        {
            m_nodes = nodes;
        }

        public override NodeState Evaluate()
        {
            foreach (BTNode node in m_nodes)
            {
                NodeState state = node.Evaluate();

                if (state == NodeState.Success)
                {
                    return NodeState.Success;
                }

                if (state == NodeState.Running)
                {
                    return NodeState.Running;
                }
            }

            return NodeState.Failure;
        }
    }
}

