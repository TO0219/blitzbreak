using System.Collections.Generic;
using UnityEngine;

namespace MyGame
{
    [System.Serializable]
    public class StatData
    {
        private Dictionary<string, float> m_stats =
                new Dictionary<string, float>();

        public float GetStat(string key)
        {
            if (m_stats.TryGetValue(key, out float value))
            {
                return value;
            }

            return 1f;
        }

        public void SetStat(string key, float value)
        {
            m_stats[key] = value;
        }

        public void MultiplyStat(string key, float value)
        {
            float current =
                GetStat(key);

            m_stats[key] =
                current * value;
        }
    }
    public static class StatKey
    {
        public const string AttackPower = "attackPower";

        public const string AttackRange = "attackRange";

        public const string FireInterval = "attackSpeed";

        public const string DamageTaken = "durability";

        public const string MoveSpeed = "moveSpeed";

    }
}