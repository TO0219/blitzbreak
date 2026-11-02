using UnityEngine;

namespace MyGame
{
    /// <summary>
    /// TitleCPU が移動できるルートの接続点。
    /// Connections に、この地点から次に進める地点を登録する。
    /// </summary>
    public sealed class TitleCPURoutePoint : MonoBehaviour
    {
        [Tooltip("追加の分岐先です。Hierarchy上の前後ポイントは自動で接続されます。")]
        [SerializeField] private TitleCPURoutePoint[] connections;

        public int ConnectionCount => connections != null ? connections.Length : 0;

        public TitleCPURoutePoint GetConnection(int index)
        {
            return connections != null && index >= 0 && index < connections.Length
                ? connections[index]
                : null;
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.1f, 0.85f, 1f, 0.9f);
            Gizmos.DrawSphere(transform.position, 0.18f);

            if (connections == null)
                return;

            for (int i = 0; i < connections.Length; i++)
            {
                TitleCPURoutePoint connection = connections[i];
                if (connection == null)
                    continue;

                Gizmos.DrawLine(transform.position, connection.transform.position);
            }
        }
#endif
    }
}
