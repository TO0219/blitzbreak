using UnityEngine;
using UnityEngine.UI;

namespace MyGame
{
    public class MinimapCircleRawImage : RawImage
    {
        [SerializeField, Range(12, 128)] private int segments = 64;
        [SerializeField, Range(0f, 0.99f)] private float innerRadius;

        protected override void OnPopulateMesh(VertexHelper vertexHelper)
        {
            vertexHelper.Clear();

            Rect rect = GetPixelAdjustedRect();
            Vector2 center = rect.center;
            float radiusX = rect.width * 0.5f;
            float radiusY = rect.height * 0.5f;
            Color32 vertexColor = color;
            Rect uvRectValue = uvRect;

            if (innerRadius <= 0.001f)
            {
                vertexHelper.AddVert(
                    center,
                    vertexColor,
                    new Vector2(
                        uvRectValue.x + uvRectValue.width * 0.5f,
                        uvRectValue.y + uvRectValue.height * 0.5f)
                );

                for (int i = 0; i <= segments; i++)
                {
                    AddCircleVertex(
                        vertexHelper,
                        center,
                        radiusX,
                        radiusY,
                        1f,
                        i,
                        vertexColor,
                        uvRectValue);
                }

                for (int i = 1; i <= segments; i++)
                    vertexHelper.AddTriangle(0, i, i + 1);

                return;
            }

            for (int i = 0; i <= segments; i++)
            {
                AddCircleVertex(
                    vertexHelper,
                    center,
                    radiusX,
                    radiusY,
                    1f,
                    i,
                    vertexColor,
                    uvRectValue);
                AddCircleVertex(
                    vertexHelper,
                    center,
                    radiusX,
                    radiusY,
                    innerRadius,
                    i,
                    vertexColor,
                    uvRectValue);
            }

            for (int i = 0; i < segments; i++)
            {
                int outer = i * 2;
                int inner = outer + 1;
                int nextOuter = outer + 2;
                int nextInner = outer + 3;

                vertexHelper.AddTriangle(outer, nextOuter, inner);
                vertexHelper.AddTriangle(nextOuter, nextInner, inner);
            }
        }

        private void AddCircleVertex(
            VertexHelper vertexHelper,
            Vector2 center,
            float radiusX,
            float radiusY,
            float radiusScale,
            int index,
            Color32 vertexColor,
            Rect uvRectValue)
        {
            float angle = (Mathf.PI * 2f * index) / segments;
            float x = Mathf.Cos(angle);
            float y = Mathf.Sin(angle);
            Vector2 position =
                center +
                new Vector2(
                    x * radiusX * radiusScale,
                    y * radiusY * radiusScale);
            Vector2 uv = new(
                uvRectValue.x + uvRectValue.width * (0.5f + x * 0.5f * radiusScale),
                uvRectValue.y + uvRectValue.height * (0.5f + y * 0.5f * radiusScale)
            );
            vertexHelper.AddVert(position, vertexColor, uv);
        }
    }
}
