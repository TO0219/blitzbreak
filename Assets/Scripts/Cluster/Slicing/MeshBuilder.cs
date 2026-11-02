using System.Collections.Generic;
using UnityEngine;

public class MeshBuilder
{
    public readonly List<Vector3> vertices = new List<Vector3>();
    public readonly List<Vector3> normals = new List<Vector3>();
    public readonly List<Vector2> uvs = new List<Vector2>();
    public readonly List<int> triangles = new List<int>();

    public int AddVertex(Vector3 v, Vector3 n, Vector2 uv)
    {
        int index = vertices.Count;
        vertices.Add(v);
        normals.Add(n);
        uvs.Add(uv);
        return index;
    }

    public void AddTriangle(
        Vector3 v0, Vector3 v1, Vector3 v2,
        Vector3 n0, Vector3 n1, Vector3 n2,
        Vector2 uv0, Vector2 uv1, Vector2 uv2)
    {
        int i0 = AddVertex(v0, n0, uv0);
        int i1 = AddVertex(v1, n1, uv1);
        int i2 = AddVertex(v2, n2, uv2);

        triangles.Add(i0);
        triangles.Add(i1);
        triangles.Add(i2);
    }

    public Mesh ToMesh()
    {
        Mesh mesh = new Mesh();

        if (vertices.Count > 65000)
        {
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        }

        mesh.SetVertices(vertices);
        mesh.SetNormals(normals);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(triangles, 0);

        if (normals.Count == 0)
        {
            mesh.RecalculateNormals();
        }

        mesh.RecalculateBounds();
        return mesh;
    }
}