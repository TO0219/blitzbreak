using UnityEngine;
using System.Collections.Generic;

namespace MyGame
{

    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class RoadCornerMeshV2 : MonoBehaviour
    {
        [Header("コーナー設定")]
        public float radius = 1.0f;

        [Range(4, 32)]
        public int segments = 12;

        [Tooltip("厚さ（Y方向）")]
        public float thickness = 0.1f;

        public CornerDirection direction = CornerDirection.NW;
        public enum CornerDirection { NW, NE, SW, SE }

        void Awake() => GenerateMesh();

#if UNITY_EDITOR
        void OnValidate()
        {
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this != null) GenerateMesh();
            };
        }
#endif

        public void GenerateMesh()
        {
            float startAngle = GetStartAngle(direction);

            Vector3 c0, c1, c2;
            GetCorners(direction, radius, out c0, out c1, out c2);

            int arcCount = segments + 1;
            int faceVertCount = 3 + arcCount;

            // 上面・下面の頂点
            Vector3[] topVerts = new Vector3[faceVertCount];
            Vector3[] botVerts = new Vector3[faceVertCount];

            topVerts[0] = c0 + Vector3.up * thickness;
            topVerts[1] = c1 + Vector3.up * thickness;
            topVerts[2] = c2 + Vector3.up * thickness;
            botVerts[0] = c0;
            botVerts[1] = c1;
            botVerts[2] = c2;

            for (int i = 0; i <= segments; i++)
            {
                float t = (float)i / segments;
                float angle = Mathf.Deg2Rad * (startAngle + t * 90f);
                float x = Mathf.Cos(angle) * radius;
                float z = Mathf.Sin(angle) * radius;
                Vector3 p = new Vector3(x, 0f, z);
                topVerts[3 + i] = p + Vector3.up * thickness;
                botVerts[3 + i] = p;
            }

            // 全頂点結合（表面のみ：上面 + 下面）
            Vector3[] originalVerts = new Vector3[faceVertCount * 2];
            Vector2[] uvs = new Vector2[faceVertCount * 2];
            for (int i = 0; i < faceVertCount; i++)
            {
                originalVerts[i] = topVerts[i];
                originalVerts[faceVertCount + i] = botVerts[i];
                uvs[i] = new Vector2(
                    (topVerts[i].x / radius + 1f) * 0.5f,
                    (topVerts[i].z / radius + 1f) * 0.5f
                );
                uvs[faceVertCount + i] = uvs[i];
            }

            List<int> triList = new List<int>();

            // 上面（表が上を向くように）
            triList.Add(0); triList.Add(3); triList.Add(1);
            for (int i = 0; i < segments; i++)
            {
                triList.Add(0); triList.Add(3 + i + 1); triList.Add(3 + i);
            }
            triList.Add(0); triList.Add(2); triList.Add(3 + segments);

            // 下面（表が下を向くように）
            int B = faceVertCount;
            triList.Add(B + 0); triList.Add(B + 1); triList.Add(B + 3);
            for (int i = 0; i < segments; i++)
            {
                triList.Add(B + 0); triList.Add(B + 3 + i); triList.Add(B + 3 + i + 1);
            }
            triList.Add(B + 0); triList.Add(B + 3 + segments); triList.Add(B + 2);

            // 側面（外周を繋ぐ）
            // 外周順: c1(1) → arc[0..seg](3..3+seg) → c2(2) → c0(0) → c1(1)
            int[] perim = new int[faceVertCount + 1];
            perim[0] = 1;
            for (int i = 0; i <= segments; i++) perim[1 + i] = 3 + i;
            perim[arcCount + 1] = 2;
            perim[arcCount + 2] = 0;

            int perimLen = arcCount + 3; // c1, arc*N+1, c2, c0
            for (int i = 0; i < perimLen - 1; i++)
            {
                int t0 = perim[i];
                int t1 = perim[i + 1];
                int b0 = t0 + faceVertCount;
                int b1 = t1 + faceVertCount;
                triList.Add(t0); triList.Add(t1); triList.Add(b0);
                triList.Add(t1); triList.Add(b1); triList.Add(b0);
            }

            // ここまでが「表面」のみの三角形（裏面は別頂点として複製する）
            int[] frontTris = triList.ToArray();

            // 裏面用に頂点を複製（同じ位置だが法線計算用に別インデックス）
            int originalCount = originalVerts.Length;
            Vector3[] backVerts = (Vector3[])originalVerts.Clone();
            Vector2[] backUVs = (Vector2[])uvs.Clone();

            // 裏面トライアングル：インデックスをシフトしつつ巻き順を反転
            int[] backTris = new int[frontTris.Length];
            for (int i = 0; i < frontTris.Length; i += 3)
            {
                backTris[i + 0] = frontTris[i + 0] + originalCount;
                backTris[i + 1] = frontTris[i + 2] + originalCount;
                backTris[i + 2] = frontTris[i + 1] + originalCount;
            }

            // 頂点・UV・トライアングルを最終結合
            Vector3[] finalVerts = new Vector3[originalCount * 2];
            Vector2[] finalUVs = new Vector2[originalCount * 2];
            originalVerts.CopyTo(finalVerts, 0);
            backVerts.CopyTo(finalVerts, originalCount);
            uvs.CopyTo(finalUVs, 0);
            backUVs.CopyTo(finalUVs, originalCount);

            int[] finalTris = new int[frontTris.Length + backTris.Length];
            frontTris.CopyTo(finalTris, 0);
            backTris.CopyTo(finalTris, frontTris.Length);

            Mesh mesh = new Mesh();
            mesh.name = "RoadCornerV2";
            mesh.vertices = finalVerts;
            mesh.uv = finalUVs;
            mesh.triangles = finalTris;
            mesh.RecalculateNormals();

            GetComponent<MeshFilter>().mesh = mesh;
        }

        void GetCorners(CornerDirection dir, float r, out Vector3 c0, out Vector3 c1, out Vector3 c2)
        {
            switch (dir)
            {
                case CornerDirection.NE:
                    c0 = new Vector3(r, 0, r);
                    c1 = new Vector3(r, 0, 0);
                    c2 = new Vector3(0, 0, r);
                    break;
                case CornerDirection.NW:
                    c0 = new Vector3(-r, 0, r);
                    c1 = new Vector3(0, 0, r);
                    c2 = new Vector3(-r, 0, 0);
                    break;
                case CornerDirection.SW:
                    c0 = new Vector3(-r, 0, -r);
                    c1 = new Vector3(-r, 0, 0);
                    c2 = new Vector3(0, 0, -r);
                    break;
                default:
                    c0 = new Vector3(r, 0, -r);
                    c1 = new Vector3(0, 0, -r);
                    c2 = new Vector3(r, 0, 0);
                    break;
            }
        }

        float GetStartAngle(CornerDirection dir)
        {
            switch (dir)
            {
                case CornerDirection.NE: return 0f;
                case CornerDirection.NW: return 90f;
                case CornerDirection.SW: return 180f;
                case CornerDirection.SE: return 270f;
                default: return 0f;
            }
        }
    }
}