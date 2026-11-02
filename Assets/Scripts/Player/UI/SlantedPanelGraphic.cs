using UnityEngine;
using UnityEngine.UI;

namespace MyGame
{
    [RequireComponent(typeof(CanvasRenderer))]
    public class SlantedPanelGraphic : MaskableGraphic
    {
        [SerializeField] private float slant = 42f;

        public float Slant
        {
            get => slant;
            set
            {
                slant = value;
                SetVerticesDirty();
            }
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();

            Rect rect = rectTransform.rect;
            float halfSlant = slant * 0.5f;

            Vector2 bottomLeft = new Vector2(rect.xMin - halfSlant, rect.yMin);
            Vector2 bottomRight = new Vector2(rect.xMax - halfSlant, rect.yMin);
            Vector2 topRight = new Vector2(rect.xMax + halfSlant, rect.yMax);
            Vector2 topLeft = new Vector2(rect.xMin + halfSlant, rect.yMax);

            UIVertex vertex = UIVertex.simpleVert;
            vertex.color = color;

            vertex.position = bottomLeft;
            vh.AddVert(vertex);
            vertex.position = bottomRight;
            vh.AddVert(vertex);
            vertex.position = topRight;
            vh.AddVert(vertex);
            vertex.position = topLeft;
            vh.AddVert(vertex);

            vh.AddTriangle(0, 1, 2);
            vh.AddTriangle(2, 3, 0);
        }
    }
}
