using UnityEngine;

namespace MyGame
{
    internal static class ProjectileVisualUtility
    {
        public static void ClearTrails(GameObject projectile)
        {
            if (projectile == null)
                return;

            TrailRenderer[] trails =
                projectile.GetComponentsInChildren<TrailRenderer>(true);
            for (int i = 0; i < trails.Length; i++)
            {
                if (trails[i] != null)
                    trails[i].Clear();
            }
        }
    }
}
