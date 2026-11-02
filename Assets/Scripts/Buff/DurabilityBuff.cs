using UnityEngine;

namespace MyGame
{
    public class DurabilityBuff : BuffBase
    {
        private readonly float durabilityMultiplier;

        public DurabilityBuff(float durabilityMultiplier)
            : base(StatKey.DamageTaken, "Durability", 0f, 5)
        {
            this.durabilityMultiplier = Mathf.Max(0.01f, durabilityMultiplier);
        }

        public override void OnApply(Entity target)
        {
            if (target == null) return;

            target.Stats.MultiplyStat(StatKey.DamageTaken, 1f / durabilityMultiplier);
        }

        public override void OnTick(Entity target, float deltaTime) { }

        public override void OnRemove(Entity target)
        {
            if (target == null) return;

            target.Stats.MultiplyStat(StatKey.DamageTaken, durabilityMultiplier);
        }
    }
}
