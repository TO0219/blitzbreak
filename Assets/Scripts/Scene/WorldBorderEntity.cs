using UnityEngine;

namespace MyGame
{
    [DisallowMultipleComponent]
    public class WorldBorderEntity : MonoBehaviour
    {
        private const string RootName = "__WorldBorder";
        private const string BorderShaderName = "Sprites/Default";

        [SerializeField] private Renderer sourceRenderer;
        [SerializeField] private float borderHeight = 18f;
        [SerializeField] private float wallThickness = 0.25f;
        [SerializeField] private float lineThickness = 0.08f;
        [SerializeField] private float verticalLineSpacing = 10f;
        [SerializeField] private float horizontalLineSpacing = 3f;
        [SerializeField] private float floorOffset = 0.05f;
        [SerializeField] private bool addWallColliders = true;
        [SerializeField] private Color wallColor = new Color(0.08f, 0.55f, 1f, 0.18f);
        [SerializeField] private Color lineColor = new Color(0.2f, 0.95f, 1f, 0.85f);
        [SerializeField] private float pulseSpeed = 2f;
        [SerializeField, Range(0f, 1f)] private float pulseAmount = 0.2f;

        private Material m_wallMaterial;
        private Material m_lineMaterial;

        public bool TryGetSourceBounds(out Bounds bounds)
        {
            if (sourceRenderer == null)
                sourceRenderer = GetComponent<Renderer>();

            if (sourceRenderer == null)
            {
                bounds = default;
                return false;
            }

            bounds = sourceRenderer.bounds;
            return bounds.size.x > 0f && bounds.size.z > 0f;
        }

        private void Reset()
        {
            sourceRenderer = GetComponent<Renderer>();
        }

        private void Awake()
        {
            BuildBorder();
        }

        private void Update()
        {
            if (m_lineMaterial == null || pulseAmount <= 0f)
                return;

            Color pulsedColor = lineColor;
            float pulse = 1f + Mathf.Sin(Time.time * pulseSpeed) * pulseAmount;
            pulsedColor.a = Mathf.Clamp01(lineColor.a * pulse);
            m_lineMaterial.color = pulsedColor;
        }

        private void OnDestroy()
        {
            DestroyMaterial(m_wallMaterial);
            DestroyMaterial(m_lineMaterial);
        }

        [ContextMenu("Rebuild World Border")]
        private void BuildBorder()
        {
            if (sourceRenderer == null)
                sourceRenderer = GetComponent<Renderer>();

            if (sourceRenderer == null)
                return;

            DestroyMaterial(m_wallMaterial);
            DestroyMaterial(m_lineMaterial);
            m_wallMaterial = null;
            m_lineMaterial = null;

            ClearExistingBorder();

            Bounds bounds = sourceRenderer.bounds;
            float minX = bounds.min.x;
            float maxX = bounds.max.x;
            float minZ = bounds.min.z;
            float maxZ = bounds.max.z;
            float centerX = bounds.center.x;
            float centerZ = bounds.center.z;
            float width = Mathf.Max(0.1f, maxX - minX);
            float depth = Mathf.Max(0.1f, maxZ - minZ);
            float bottomY = bounds.max.y + floorOffset;
            float centerY = bottomY + borderHeight * 0.5f;
            float topY = bottomY + borderHeight;

            Transform root = new GameObject(RootName).transform;
            root.SetParent(transform, true);

            m_wallMaterial = CreateMaterial(wallColor);
            m_lineMaterial = CreateMaterial(lineColor);

            CreateBox(root, "NorthWall", new Vector3(centerX, centerY, maxZ), new Vector3(width, borderHeight, wallThickness), m_wallMaterial, addWallColliders);
            CreateBox(root, "SouthWall", new Vector3(centerX, centerY, minZ), new Vector3(width, borderHeight, wallThickness), m_wallMaterial, addWallColliders);
            CreateBox(root, "EastWall", new Vector3(maxX, centerY, centerZ), new Vector3(wallThickness, borderHeight, depth), m_wallMaterial, addWallColliders);
            CreateBox(root, "WestWall", new Vector3(minX, centerY, centerZ), new Vector3(wallThickness, borderHeight, depth), m_wallMaterial, addWallColliders);

            BuildNorthSouthLines(root, minX, maxX, minZ, maxZ, bottomY, topY, width);
            BuildEastWestLines(root, minX, maxX, minZ, maxZ, bottomY, topY, depth);
        }

        private void BuildNorthSouthLines(Transform root, float minX, float maxX, float minZ, float maxZ, float bottomY, float topY, float width)
        {
            float lineDepth = wallThickness * 1.35f;

            for (float x = minX; x <= maxX + 0.01f; x += verticalLineSpacing)
            {
                CreateBox(root, "NorthVerticalLine", new Vector3(x, bottomY + borderHeight * 0.5f, maxZ), new Vector3(lineThickness, borderHeight, lineDepth), m_lineMaterial, false);
                CreateBox(root, "SouthVerticalLine", new Vector3(x, bottomY + borderHeight * 0.5f, minZ), new Vector3(lineThickness, borderHeight, lineDepth), m_lineMaterial, false);
            }

            AddNorthSouthHorizontalLine(root, minX, maxX, minZ, maxZ, bottomY, width, lineDepth);
            for (float y = bottomY + horizontalLineSpacing; y < topY - 0.01f; y += horizontalLineSpacing)
                AddNorthSouthHorizontalLine(root, minX, maxX, minZ, maxZ, y, width, lineDepth);
            AddNorthSouthHorizontalLine(root, minX, maxX, minZ, maxZ, topY, width, lineDepth);
        }

        private void BuildEastWestLines(Transform root, float minX, float maxX, float minZ, float maxZ, float bottomY, float topY, float depth)
        {
            float lineDepth = wallThickness * 1.35f;

            for (float z = minZ; z <= maxZ + 0.01f; z += verticalLineSpacing)
            {
                CreateBox(root, "EastVerticalLine", new Vector3(maxX, bottomY + borderHeight * 0.5f, z), new Vector3(lineDepth, borderHeight, lineThickness), m_lineMaterial, false);
                CreateBox(root, "WestVerticalLine", new Vector3(minX, bottomY + borderHeight * 0.5f, z), new Vector3(lineDepth, borderHeight, lineThickness), m_lineMaterial, false);
            }

            AddEastWestHorizontalLine(root, minX, maxX, minZ, maxZ, bottomY, depth, lineDepth);
            for (float y = bottomY + horizontalLineSpacing; y < topY - 0.01f; y += horizontalLineSpacing)
                AddEastWestHorizontalLine(root, minX, maxX, minZ, maxZ, y, depth, lineDepth);
            AddEastWestHorizontalLine(root, minX, maxX, minZ, maxZ, topY, depth, lineDepth);
        }

        private void AddNorthSouthHorizontalLine(Transform root, float minX, float maxX, float minZ, float maxZ, float y, float width, float lineDepth)
        {
            float centerX = (minX + maxX) * 0.5f;
            CreateBox(root, "NorthHorizontalLine", new Vector3(centerX, y, maxZ), new Vector3(width, lineThickness, lineDepth), m_lineMaterial, false);
            CreateBox(root, "SouthHorizontalLine", new Vector3(centerX, y, minZ), new Vector3(width, lineThickness, lineDepth), m_lineMaterial, false);
        }

        private void AddEastWestHorizontalLine(Transform root, float minX, float maxX, float minZ, float maxZ, float y, float depth, float lineDepth)
        {
            float centerZ = (minZ + maxZ) * 0.5f;
            CreateBox(root, "EastHorizontalLine", new Vector3(maxX, y, centerZ), new Vector3(lineDepth, lineThickness, depth), m_lineMaterial, false);
            CreateBox(root, "WestHorizontalLine", new Vector3(minX, y, centerZ), new Vector3(lineDepth, lineThickness, depth), m_lineMaterial, false);
        }

        private void CreateBox(Transform root, string objectName, Vector3 position, Vector3 scale, Material material, bool keepCollider)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = objectName;
            box.transform.position = position;
            box.transform.localScale = scale;
            box.transform.SetParent(root, true);

            MeshRenderer renderer = box.GetComponent<MeshRenderer>();
            if (renderer != null)
                renderer.sharedMaterial = material;

            Collider collider = box.GetComponent<Collider>();
            if (collider != null && !keepCollider)
            {
                if (Application.isPlaying)
                    Destroy(collider);
                else
                    DestroyImmediate(collider);
            }
        }

        private Material CreateMaterial(Color color)
        {
            Shader shader = Shader.Find(BorderShaderName);
            if (shader == null)
            {
                Debug.LogError(
                    $"[WorldBorderEntity] Required shader '{BorderShaderName}' was not found.",
                    this);
                return null;
            }

            Material material = new Material(shader);
            material.name = "WorldBorderMaterial";
            material.color = color;
            material.renderQueue = 3000;
            return material;
        }

        private void ClearExistingBorder()
        {
            Transform existing = transform.Find(RootName);
            if (existing == null)
                return;

            if (Application.isPlaying)
                Destroy(existing.gameObject);
            else
                DestroyImmediate(existing.gameObject);
        }

        private void DestroyMaterial(Material material)
        {
            if (material == null)
                return;

            if (Application.isPlaying)
                Destroy(material);
            else
                DestroyImmediate(material);
        }
    }
}
