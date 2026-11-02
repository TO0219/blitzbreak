using System.Collections.Generic;
using UnityEngine;

public static class MeshSlicer
{
    private const float EPSILON = 0.00001f;

    private struct VertexData
    {
        public Vector3 worldPos;
        public Vector3 worldNormal;
        public Vector2 uv;
        public float distance;
    }

    public static MeshSliceResult Slice(
        Mesh sourceMesh,
        Plane worldPlane,
        Matrix4x4 localToWorld,
        Matrix4x4 worldToLocal)
    {
        MeshSliceResult result = new MeshSliceResult();

        if (sourceMesh == null)
        {
            result.success = false;
            return result;
        }

        Vector3[] srcVertices = sourceMesh.vertices;
        Vector3[] srcNormals = sourceMesh.normals;
        Vector2[] srcUVs = sourceMesh.uv;
        int[] srcTriangles = sourceMesh.triangles;

        if (srcVertices == null || srcVertices.Length == 0 || srcTriangles == null || srcTriangles.Length == 0)
        {
            result.success = false;
            return result;
        }

        bool hasNormals = srcNormals != null && srcNormals.Length == srcVertices.Length;
        bool hasUVs = srcUVs != null && srcUVs.Length == srcVertices.Length;

        MeshBuilder positive = new MeshBuilder();
        MeshBuilder negative = new MeshBuilder();

        List<CapEdge> capEdges = new List<CapEdge>();

        for (int i = 0; i < srcTriangles.Length; i += 3)
        {
            VertexData v0 = BuildVertex(srcTriangles[i], srcVertices, srcNormals, srcUVs, hasNormals, hasUVs, localToWorld, worldPlane);
            VertexData v1 = BuildVertex(srcTriangles[i + 1], srcVertices, srcNormals, srcUVs, hasNormals, hasUVs, localToWorld, worldPlane);
            VertexData v2 = BuildVertex(srcTriangles[i + 2], srcVertices, srcNormals, srcUVs, hasNormals, hasUVs, localToWorld, worldPlane);

            ProcessTriangle(v0, v1, v2, worldToLocal, positive, negative, capEdges);
        }

        if (capEdges.Count >= 1)
        {
            CapTriangulator.AddCaps(
                capEdges,
                worldPlane.normal,
                worldToLocal,
                positive,
                negative);
        }

        if (positive.vertices.Count < 3 || negative.vertices.Count < 3)
        {
            result.success = false;
            return result;
        }

        result.positiveMesh = positive.ToMesh();
        result.negativeMesh = negative.ToMesh();
        result.success = true;
        return result;
    }

    private static VertexData BuildVertex(
        int index,
        Vector3[] srcVertices,
        Vector3[] srcNormals,
        Vector2[] srcUVs,
        bool hasNormals,
        bool hasUVs,
        Matrix4x4 localToWorld,
        Plane plane)
    {
        Vector3 localPos = srcVertices[index];
        Vector3 worldPos = localToWorld.MultiplyPoint3x4(localPos);

        Vector3 localNormal = hasNormals ? srcNormals[index] : Vector3.up;
        Vector3 worldNormal = localToWorld.MultiplyVector(localNormal).normalized;

        Vector2 uv = hasUVs ? srcUVs[index] : Vector2.zero;
        float dist = plane.GetDistanceToPoint(worldPos);

        return new VertexData
        {
            worldPos = worldPos,
            worldNormal = worldNormal,
            uv = uv,
            distance = dist
        };
    }

    private static void ProcessTriangle(
        VertexData a,
        VertexData b,
        VertexData c,
        Matrix4x4 worldToLocal,
        MeshBuilder positive,
        MeshBuilder negative,
        List<CapEdge> capEdges)
    {
        bool aPos = a.distance >= -EPSILON;
        bool bPos = b.distance >= -EPSILON;
        bool cPos = c.distance >= -EPSILON;

        int positiveCount = (aPos ? 1 : 0) + (bPos ? 1 : 0) + (cPos ? 1 : 0);

        if (positiveCount == 3)
        {
            AddTriangleToBuilder(positive, worldToLocal, a, b, c);
            return;
        }

        if (positiveCount == 0)
        {
            AddTriangleToBuilder(negative, worldToLocal, a, b, c);
            return;
        }

        if (positiveCount == 1)
        {
            VertexData p;
            VertexData n0;
            VertexData n1;

            if (aPos)
            {
                p = a; n0 = b; n1 = c;
            }
            else if (bPos)
            {
                p = b; n0 = c; n1 = a;
            }
            else
            {
                p = c; n0 = a; n1 = b;
            }

            VertexData i0 = Intersect(p, n0);
            VertexData i1 = Intersect(p, n1);

            AddTriangleToBuilder(positive, worldToLocal, p, i0, i1);

            AddTriangleToBuilder(negative, worldToLocal, n0, n1, i1);
            AddTriangleToBuilder(negative, worldToLocal, n0, i1, i0);

            capEdges.Add(new CapEdge(i0.worldPos, i1.worldPos));
            return;
        }

        if (positiveCount == 2)
        {
            VertexData n;
            VertexData p0;
            VertexData p1;

            if (!aPos)
            {
                n = a; p0 = b; p1 = c;
            }
            else if (!bPos)
            {
                n = b; p0 = c; p1 = a;
            }
            else
            {
                n = c; p0 = a; p1 = b;
            }

            VertexData i0 = Intersect(n, p0);
            VertexData i1 = Intersect(n, p1);

            AddTriangleToBuilder(negative, worldToLocal, n, i0, i1);

            AddTriangleToBuilder(positive, worldToLocal, p0, p1, i1);
            AddTriangleToBuilder(positive, worldToLocal, p0, i1, i0);

            capEdges.Add(new CapEdge(i0.worldPos, i1.worldPos));
        }
    }

    private static VertexData Intersect(VertexData from, VertexData to)
    {
        float denom = from.distance - to.distance;
        float t = Mathf.Abs(denom) > EPSILON ? from.distance / denom : 0.5f;
        t = Mathf.Clamp01(t);

        return new VertexData
        {
            worldPos = Vector3.Lerp(from.worldPos, to.worldPos, t),
            worldNormal = Vector3.Lerp(from.worldNormal, to.worldNormal, t).normalized,
            uv = Vector2.Lerp(from.uv, to.uv, t),
            distance = 0f
        };
    }

    private static void AddTriangleToBuilder(
        MeshBuilder builder,
        Matrix4x4 worldToLocal,
        VertexData a,
        VertexData b,
        VertexData c)
    {
        Vector3 la = worldToLocal.MultiplyPoint3x4(a.worldPos);
        Vector3 lb = worldToLocal.MultiplyPoint3x4(b.worldPos);
        Vector3 lc = worldToLocal.MultiplyPoint3x4(c.worldPos);

        Vector3 na = worldToLocal.MultiplyVector(a.worldNormal).normalized;
        Vector3 nb = worldToLocal.MultiplyVector(b.worldNormal).normalized;
        Vector3 nc = worldToLocal.MultiplyVector(c.worldNormal).normalized;

        builder.AddTriangle(
            la, lb, lc,
            na, nb, nc,
            a.uv, b.uv, c.uv);
    }
}