using UnityEngine;
using UnityEngine.UI;

namespace MyGame
{
    [RequireComponent(typeof(CanvasRenderer))]
    public class LowHealthEdgeGraphic : MaskableGraphic
    {
        [SerializeField, Range(0.02f, 0.5f)] private float edgeWidthRate = 0.18f;
        [SerializeField, Range(0f, 1f)] private float cornerBoost = 0.38f;

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();

            Rect rect = rectTransform.rect;
            float edgeX = Mathf.Min(rect.width * edgeWidthRate, rect.width * 0.34f);
            float edgeY = Mathf.Min(rect.height * edgeWidthRate, rect.height * 0.34f);

            Color cornerColor = WithAlpha(color, color.a * (0.86f + cornerBoost * 0.12f));
            Color edgeColor = WithAlpha(color, color.a * 0.62f);
            Color smallColor = WithAlpha(color, color.a * 0.44f);

            AddSplash(vh, new Vector2(rect.xMin + edgeX * 0.28f, rect.yMin + edgeY * 0.22f), edgeX * 1.26f, edgeY * 1.14f, 0.12f, cornerColor, 0.24f);
            AddSplash(vh, new Vector2(rect.xMax - edgeX * 0.25f, rect.yMin + edgeY * 0.25f), edgeX * 1.18f, edgeY * 1.18f, 0.75f, cornerColor, 0.21f);
            AddSplash(vh, new Vector2(rect.xMin + edgeX * 0.24f, rect.yMax - edgeY * 0.23f), edgeX * 1.12f, edgeY * 1.22f, 1.34f, cornerColor, 0.22f);
            AddSplash(vh, new Vector2(rect.xMax - edgeX * 0.31f, rect.yMax - edgeY * 0.2f), edgeX * 1.3f, edgeY * 1.08f, 2.02f, cornerColor, 0.24f);

            AddSplash(vh, new Vector2(rect.center.x - rect.width * 0.21f, rect.yMin + edgeY * 0.18f), edgeX * 0.9f, edgeY * 0.46f, 2.71f, edgeColor, 0.2f);
            AddSplash(vh, new Vector2(rect.center.x + rect.width * 0.17f, rect.yMin + edgeY * 0.14f), edgeX * 0.78f, edgeY * 0.38f, 3.34f, smallColor, 0.24f);
            AddSplash(vh, new Vector2(rect.center.x - rect.width * 0.08f, rect.yMax - edgeY * 0.16f), edgeX * 0.86f, edgeY * 0.42f, 4.12f, edgeColor, 0.22f);
            AddSplash(vh, new Vector2(rect.center.x + rect.width * 0.29f, rect.yMax - edgeY * 0.2f), edgeX * 0.74f, edgeY * 0.4f, 5.01f, smallColor, 0.2f);

            AddSplash(vh, new Vector2(rect.xMin + edgeX * 0.16f, rect.center.y - rect.height * 0.2f), edgeX * 0.42f, edgeY * 0.76f, 5.83f, edgeColor, 0.22f);
            AddSplash(vh, new Vector2(rect.xMin + edgeX * 0.19f, rect.center.y + rect.height * 0.17f), edgeX * 0.38f, edgeY * 0.7f, 6.41f, smallColor, 0.24f);
            AddSplash(vh, new Vector2(rect.xMax - edgeX * 0.17f, rect.center.y - rect.height * 0.11f), edgeX * 0.44f, edgeY * 0.84f, 7.08f, edgeColor, 0.2f);
            AddSplash(vh, new Vector2(rect.xMax - edgeX * 0.2f, rect.center.y + rect.height * 0.24f), edgeX * 0.34f, edgeY * 0.64f, 7.67f, smallColor, 0.26f);
        }

        private static void AddSplash(
            VertexHelper vh,
            Vector2 center,
            float radiusX,
            float radiusY,
            float seed,
            Color centerColor,
            float irregularity)
        {
            const int Segments = 18;
            int index = vh.currentVertCount;
            AddVertex(vh, center, centerColor);

            Color outerColor = WithAlpha(centerColor, 0f);
            for (int i = 0; i <= Segments; i++)
            {
                float angle = (Mathf.PI * 2f * i / Segments) + seed;
                float wave =
                    Mathf.Sin((i + 1) * 1.73f + seed * 2.1f) * 0.62f +
                    Mathf.Sin((i + 3) * 2.47f + seed * 0.7f) * 0.38f;
                float radiusScale = Mathf.Max(0.55f, 1f + wave * irregularity);
                Vector2 point = center + new Vector2(
                    Mathf.Cos(angle) * radiusX * radiusScale,
                    Mathf.Sin(angle) * radiusY * radiusScale);
                AddVertex(vh, point, outerColor);
            }

            for (int i = 1; i <= Segments; i++)
            {
                vh.AddTriangle(index, index + i, index + i + 1);
            }
        }

        private static Color WithAlpha(Color source, float alpha)
        {
            return new Color(source.r, source.g, source.b, Mathf.Clamp01(alpha));
        }

        private static void AddVertex(VertexHelper vh, Vector2 position, Color color)
        {
            UIVertex vertex = UIVertex.simpleVert;
            vertex.position = position;
            vertex.color = color;
            vh.AddVert(vertex);
        }
    }
}
