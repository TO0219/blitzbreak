using UnityEngine;

namespace MyGame
{
    /// <summary>
    /// 子FlightPointをSceneビューへ周回ルートとして表示する。
    /// </summary>
    public sealed class TitleAircraftRouteVisualizer : MonoBehaviour
    {
        [SerializeField] private Color routeColor =
            new Color(1f, 0.65f, 0.1f, 0.95f);
        [SerializeField, Min(0.05f)] private float pointRadius = 0.4f;
        [SerializeField, Min(0.05f)] private float arrowSize = 0.8f;
        [SerializeField] private bool closeLoop = true;

        private void OnDrawGizmos()
        {
            int count = transform.childCount;
            if (count <= 0)
                return;

            Gizmos.color = routeColor;
            for (int i = 0; i < count; i++)
            {
                Transform point = transform.GetChild(i);
                if (point == null)
                    continue;

                Gizmos.DrawSphere(point.position, pointRadius);

                int nextIndex = i + 1;
                if (nextIndex >= count)
                {
                    if (!closeLoop)
                        continue;
                    nextIndex = 0;
                }

                Transform nextPoint = transform.GetChild(nextIndex);
                if (nextPoint == null)
                    continue;

                DrawArrow(point.position, nextPoint.position);
            }
        }

        private void DrawArrow(Vector3 start, Vector3 end)
        {
            Gizmos.DrawLine(start, end);

            Vector3 direction = end - start;
            if (direction.sqrMagnitude < 0.001f)
                return;

            direction.Normalize();
            Vector3 arrowPosition = Vector3.Lerp(start, end, 0.65f);
            Vector3 side = Vector3.Cross(direction, Vector3.up);
            if (side.sqrMagnitude < 0.001f)
                side = Vector3.Cross(direction, Vector3.right);
            side.Normalize();

            Vector3 back = -direction * arrowSize;
            Vector3 wing = side * arrowSize * 0.45f;
            Gizmos.DrawLine(arrowPosition, arrowPosition + back + wing);
            Gizmos.DrawLine(arrowPosition, arrowPosition + back - wing);
        }
    }
}
