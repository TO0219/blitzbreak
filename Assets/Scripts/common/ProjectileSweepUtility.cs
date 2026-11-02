using UnityEngine;

namespace MyGame
{
    internal static class ProjectileSweepUtility
    {
        private static readonly RaycastHit[] PlaneRaycastHits = new RaycastHit[16];

        public static int Cast(
            Vector3 start,
            Vector3 end,
            float radius,
            RaycastHit[] results)
        {
            if (results == null || results.Length == 0)
                return 0;

            Vector3 delta = end - start;
            float distance = delta.magnitude;
            if (distance <= 0.001f)
                return 0;

            int hitCount = Physics.SphereCastNonAlloc(
                start,
                Mathf.Max(0.01f, radius),
                delta / distance,
                results,
                distance,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Collide);

            hitCount = AppendBattlePlaneRaycastHits(
                start,
                delta / distance,
                distance,
                results,
                hitCount);

            SortByDistance(results, hitCount);
            return hitCount;
        }

        private static int AppendBattlePlaneRaycastHits(
            Vector3 start,
            Vector3 direction,
            float distance,
            RaycastHit[] results,
            int currentCount)
        {
            if (results == null || currentCount >= results.Length)
                return currentCount;

            int rayHitCount = Physics.RaycastNonAlloc(
                start,
                direction,
                PlaneRaycastHits,
                distance,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Collide);

            for (int i = 0; i < rayHitCount && currentCount < results.Length; i++)
            {
                Collider collider = PlaneRaycastHits[i].collider;
                if (!IsBattlePlane(collider) ||
                    ContainsCollider(results, currentCount, collider))
                {
                    continue;
                }

                results[currentCount] = PlaneRaycastHits[i];
                currentCount++;
            }

            return currentCount;
        }

        private static bool ContainsCollider(
            RaycastHit[] hits,
            int count,
            Collider collider)
        {
            if (collider == null)
                return false;

            for (int i = 0; i < count; i++)
            {
                if (hits[i].collider == collider)
                    return true;
            }

            return false;
        }

        public static void SortByDistance(RaycastHit[] hits, int count)
        {
            for (int i = 1; i < count; i++)
            {
                RaycastHit current = hits[i];
                int j = i - 1;

                while (j >= 0 && hits[j].distance > current.distance)
                {
                    hits[j + 1] = hits[j];
                    j--;
                }

                hits[j + 1] = current;
            }
        }

        public static bool ShouldIgnore(Collider collider, Transform projectile, Transform owner)
        {
            if (collider == null)
                return true;

            if (IsBattlePlane(collider))
                return false;

            if (IsWorldBorder(collider))
                return true;

            Transform hitRoot = collider.transform.root;

            if (projectile != null && hitRoot == projectile.root)
                return true;

            if (owner != null && hitRoot == owner.root)
                return true;

            return collider.GetComponentInParent<PlayerBulletEntity>() != null ||
                collider.GetComponentInParent<EnemyBulletEntity>() != null ||
                collider.GetComponentInParent<EnemyCPUBulletEntity>() != null ||
                collider.GetComponentInParent<GatlingBulletEntity>() != null ||
                collider.GetComponentInParent<MissileBulletEntity>() != null;
        }

        public static bool IsWorldBorder(Collider collider)
        {
            if (collider == null)
                return false;

            if (collider.GetComponentInParent<WorldBorderEntity>() != null)
                return true;

            Transform current = collider.transform;
            while (current != null)
            {
                if (current.name == "__WorldBorder")
                    return true;

                current = current.parent;
            }

            return false;
        }

        public static bool IsBattlePlane(Collider collider)
        {
            if (collider == null)
                return false;

            Transform current = collider.transform;
            while (current != null)
            {
                if (current.name == "Plane" ||
                    current.name.StartsWith("Plane (", System.StringComparison.Ordinal))
                {
                    return true;
                }

                current = current.parent;
            }

            return false;
        }
    }
}
