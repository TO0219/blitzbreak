using UnityEngine;

namespace MyGame
{
    public enum NodeState
    {
        Success,
        Failure,
        Running
    }

    public abstract class BTNode
    {
        public abstract NodeState Evaluate();
    }
}
