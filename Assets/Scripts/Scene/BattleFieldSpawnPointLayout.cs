using UnityEngine;

namespace MyGame
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class BattleFieldSpawnPointLayout : MonoBehaviour
    {
        private const int CornerCount = 4;

        [SerializeField] private Renderer floorRenderer;
        [SerializeField] private Transform[] spawnPoints = new Transform[CornerCount];
        [SerializeField, Min(0f)] private float edgePadding = 12f;
        [SerializeField] private float heightOffset = 1.25f;
        [SerializeField] private bool alignToCenter = true;
        [SerializeField] private bool updateInEditMode = true;

        private void OnEnable()
        {
            ApplyLayoutIfNeeded();
        }

        private void Awake()
        {
            ApplyLayoutIfNeeded();
        }

        private void OnValidate()
        {
            ApplyLayoutIfNeeded();
        }

        private void Update()
        {
            if (Application.isPlaying || !updateInEditMode)
                return;

            ApplyLayoutIfNeeded();
        }

        [ContextMenu("Apply Spawn Point Layout")]
        public void ApplyLayout()
        {
            if (floorRenderer == null || spawnPoints == null)
                return;

            Bounds bounds = floorRenderer.bounds;
            float width = bounds.size.x;
            float depth = bounds.size.z;
            float xInset = Mathf.Min(edgePadding, width * 0.49f);
            float zInset = Mathf.Min(edgePadding, depth * 0.49f);
            float y = bounds.max.y + heightOffset;

            Vector3 center = bounds.center;
            center.y = y;

            Vector3[] cornerPositions =
            {
                new Vector3(bounds.min.x + xInset, y, bounds.max.z - zInset),
                new Vector3(bounds.max.x - xInset, y, bounds.max.z - zInset),
                new Vector3(bounds.max.x - xInset, y, bounds.min.z + zInset),
                new Vector3(bounds.min.x + xInset, y, bounds.min.z + zInset)
            };

            int count = Mathf.Min(CornerCount, spawnPoints.Length);
            for (int i = 0; i < count; i++)
            {
                Transform spawnPoint = spawnPoints[i];
                if (spawnPoint == null)
                    continue;

                Vector3 targetPosition = cornerPositions[i];
                if ((spawnPoint.position - targetPosition).sqrMagnitude > 0.0001f)
                    spawnPoint.position = targetPosition;

                if (!alignToCenter)
                    continue;

                Vector3 direction = center - targetPosition;
                direction.y = 0f;
                if (direction.sqrMagnitude <= 0.0001f)
                    continue;

                Quaternion targetRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
                if (Quaternion.Angle(spawnPoint.rotation, targetRotation) > 0.01f)
                    spawnPoint.rotation = targetRotation;
            }
        }

        private void ApplyLayoutIfNeeded()
        {
            if (floorRenderer == null || spawnPoints == null)
                return;

            ApplyLayout();
        }
    }
}
