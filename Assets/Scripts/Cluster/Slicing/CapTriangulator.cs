using System.Collections.Generic;
using UnityEngine;

public struct CapEdge
{
    public Vector3 a;
    public Vector3 b;

    public CapEdge(Vector3 a, Vector3 b)
    {
        this.a = a;
        this.b = b;
    }
}

public static class CapTriangulator
{
    private const float EPSILON = 0.001f;

    public static void AddCaps(
        List<CapEdge> capEdges,
        Vector3 planeNormalWorld,
        Matrix4x4 worldToLocal,
        MeshBuilder positiveBuilder,
        MeshBuilder negativeBuilder)
    {
        if (capEdges == null || capEdges.Count == 0) return;

        List<List<Vector3>> loops = BuildLoops(capEdges);
        if (loops.Count == 0)
        {
            AddLooseCapFromEdges(
                capEdges,
                planeNormalWorld,
                worldToLocal,
                positiveBuilder,
                negativeBuilder);
            return;
        }

        for (int i = 0; i < loops.Count; i++)
        {
            AddSingleLoopCap(
                loops[i],
                planeNormalWorld,
                worldToLocal,
                positiveBuilder,
                negativeBuilder);
        }
    }

    private static void AddLooseCapFromEdges(
        List<CapEdge> capEdges,
        Vector3 planeNormalWorld,
        Matrix4x4 worldToLocal,
        MeshBuilder positiveBuilder,
        MeshBuilder negativeBuilder)
    {
        List<Vector3> points = new List<Vector3>();
        for (int i = 0; i < capEdges.Count; i++)
        {
            AddUniquePoint(points, capEdges[i].a);
            AddUniquePoint(points, capEdges[i].b);
        }

        if (points.Count < 3)
            return;

        points = BuildConvexHull(points, planeNormalWorld);
        if (points.Count < 3)
            return;

        AddSingleLoopCap(
            points,
            planeNormalWorld,
            worldToLocal,
            positiveBuilder,
            negativeBuilder);
    }

    private static void AddUniquePoint(List<Vector3> points, Vector3 point)
    {
        for (int i = 0; i < points.Count; i++)
        {
            if (NearlyEqual(points[i], point))
            {
                return;
            }
        }

        points.Add(point);
    }

    private class CapPoint
    {
        public Vector3 world;
        public Vector2 projected;
    }

    private static void RemoveProjectedNearDuplicates(List<CapPoint> points)
    {
        for (int i = points.Count - 1; i > 0; i--)
        {
            if ((points[i].projected - points[i - 1].projected).sqrMagnitude <= EPSILON * EPSILON)
            {
                points.RemoveAt(i);
            }
        }

        if (points.Count >= 2)
        {
            if ((points[0].projected - points[points.Count - 1].projected).sqrMagnitude <= EPSILON * EPSILON)
            {
                points.RemoveAt(points.Count - 1);
            }
        }
    }

    private static void RemoveProjectedCollinear(List<CapPoint> points)
    {
        bool removed = true;

        while (removed && points.Count >= 3)
        {
            removed = false;

            for (int i = 0; i < points.Count; i++)
            {
                int prev = (i - 1 + points.Count) % points.Count;
                int next = (i + 1) % points.Count;

                Vector2 a = points[prev].projected;
                Vector2 b = points[i].projected;
                Vector2 c = points[next].projected;

                Vector2 ab = (b - a).normalized;
                Vector2 bc = (c - b).normalized;

                float cross = ab.x * bc.y - ab.y * bc.x;

                if (Mathf.Abs(cross) <= 0.0001f)
                {
                    points.RemoveAt(i);
                    removed = true;
                    break;
                }
            }
        }
    }

    private static void AddSingleLoopCap(
        List<Vector3> loopWorld,
        Vector3 planeNormalWorld,
        Matrix4x4 worldToLocal,
        MeshBuilder positiveBuilder,
        MeshBuilder negativeBuilder)
    {
        if (loopWorld == null || loopWorld.Count < 3) return;

        List<Vector3> cleaned = RemoveSequentialDuplicates(loopWorld);
        if (cleaned.Count < 3) return;

        if ((cleaned[0] - cleaned[cleaned.Count - 1]).sqrMagnitude <= EPSILON * EPSILON)
        {
            cleaned.RemoveAt(cleaned.Count - 1);
        }

        if (cleaned.Count < 3) return;

        Vector3 tangent = Vector3.Cross(planeNormalWorld, Vector3.up);
        if (tangent.sqrMagnitude < 0.000001f)
        {
            tangent = Vector3.Cross(planeNormalWorld, Vector3.right);
        }
        tangent.Normalize();

        Vector3 bitangent = Vector3.Cross(planeNormalWorld, tangent).normalized;
        Vector3 projectionOrigin = cleaned[0];

        List<CapPoint> capPoints = new List<CapPoint>();
        for (int i = 0; i < cleaned.Count; i++)
        {
            Vector3 d = cleaned[i] - projectionOrigin;
            Vector2 p = new Vector2(
                Vector3.Dot(d, tangent),
                Vector3.Dot(d, bitangent));

            capPoints.Add(new CapPoint
            {
                world = cleaned[i],
                projected = p
            });
        }

        RemoveProjectedNearDuplicates(capPoints);
        RemoveProjectedCollinear(capPoints);

        if (capPoints.Count < 3) return;

        List<Vector2> polygon = new List<Vector2>(capPoints.Count);
        List<Vector3> worldLoop = new List<Vector3>(capPoints.Count);
        for (int i = 0; i < capPoints.Count; i++)
        {
            polygon.Add(capPoints[i].projected);
            worldLoop.Add(capPoints[i].world);
        }

        EnsureCCW(polygon, worldLoop);

        List<int> capTriangles = EarClip(polygon);
        int expectedTriangleIndexCount = Mathf.Max(0, polygon.Count - 2) * 3;
        if (capTriangles.Count != expectedTriangleIndexCount)
        {
            capTriangles = BuildFanFallback(polygon);
        }

        Vector3 localPlaneNormal = worldToLocal.MultiplyVector(planeNormalWorld).normalized;

        for (int i = 0; i < capTriangles.Count; i += 3)
        {
            int i0 = capTriangles[i];
            int i1 = capTriangles[i + 1];
            int i2 = capTriangles[i + 2];

            Vector3 v0 = worldToLocal.MultiplyPoint3x4(worldLoop[i0]);
            Vector3 v1 = worldToLocal.MultiplyPoint3x4(worldLoop[i1]);
            Vector3 v2 = worldToLocal.MultiplyPoint3x4(worldLoop[i2]);

            Vector2 uv0 = polygon[i0];
            Vector2 uv1 = polygon[i1];
            Vector2 uv2 = polygon[i2];

            positiveBuilder.AddTriangle(
                v0, v2, v1,
                -localPlaneNormal, -localPlaneNormal, -localPlaneNormal,
                uv0, uv2, uv1);

            negativeBuilder.AddTriangle(
                v0, v1, v2,
                localPlaneNormal, localPlaneNormal, localPlaneNormal,
                uv0, uv1, uv2);
        }
    }

    private static List<List<Vector3>> BuildLoops(List<CapEdge> edges)
    {
        List<CapEdge> remaining = new List<CapEdge>(edges);
        List<List<Vector3>> loops = new List<List<Vector3>>();

        while (remaining.Count > 0)
        {
            CapEdge start = remaining[0];
            remaining.RemoveAt(0);

            List<Vector3> loop = new List<Vector3>();
            loop.Add(start.a);
            loop.Add(start.b);

            Vector3 current = start.b;
            bool closed = false;
            int guard = 0;

            while (remaining.Count > 0 && guard < 10000)
            {
                guard++;

                int nextIndex = -1;
                bool flip = false;

                for (int i = 0; i < remaining.Count; i++)
                {
                    if (NearlyEqual(current, remaining[i].a))
                    {
                        nextIndex = i;
                        flip = false;
                        break;
                    }

                    if (NearlyEqual(current, remaining[i].b))
                    {
                        nextIndex = i;
                        flip = true;
                        break;
                    }
                }

                if (nextIndex < 0)
                {
                    break;
                }

                CapEdge next = remaining[nextIndex];
                remaining.RemoveAt(nextIndex);

                Vector3 nextPoint = flip ? next.a : next.b;
                loop.Add(nextPoint);
                current = nextPoint;

                if (NearlyEqual(current, loop[0]))
                {
                    closed = true;
                    break;
                }
            }

            if (closed && loop.Count >= 4)
            {
                loops.Add(loop);
            }
        }

        return loops;
    }

    private static List<Vector3> RemoveSequentialDuplicates(List<Vector3> points)
    {
        List<Vector3> result = new List<Vector3>();
        if (points == null || points.Count == 0) return result;

        result.Add(points[0]);

        for (int i = 1; i < points.Count; i++)
        {
            if (!NearlyEqual(points[i], result[result.Count - 1]))
            {
                result.Add(points[i]);
            }
        }

        return result;
    }

    private static bool NearlyEqual(Vector3 a, Vector3 b)
    {
        return (a - b).sqrMagnitude <= EPSILON * EPSILON;
    }

    private static void EnsureCCW(List<Vector2> poly2D, List<Vector3> worldLoop)
    {
        if (SignedArea(poly2D) < 0f)
        {
            poly2D.Reverse();
            worldLoop.Reverse();
        }
    }

    private static List<int> EarClip(List<Vector2> polygon)
    {
        List<int> result = new List<int>();
        if (polygon.Count < 3) return result;

        List<int> verts = new List<int>();
        for (int i = 0; i < polygon.Count; i++)
        {
            verts.Add(i);
        }

        int guard = 0;
        while (verts.Count > 2 && guard < 10000)
        {
            guard++;
            bool earFound = false;

            for (int i = 0; i < verts.Count; i++)
            {
                int prev = verts[(i - 1 + verts.Count) % verts.Count];
                int curr = verts[i];
                int next = verts[(i + 1) % verts.Count];

                Vector2 a = polygon[prev];
                Vector2 b = polygon[curr];
                Vector2 c = polygon[next];

                if (!IsConvex(a, b, c))
                    continue;

                bool contains = false;
                for (int j = 0; j < verts.Count; j++)
                {
                    int p = verts[j];
                    if (p == prev || p == curr || p == next) continue;

                    if (PointInTriangle(polygon[p], a, b, c))
                    {
                        contains = true;
                        break;
                    }
                }

                if (contains) continue;

                result.Add(prev);
                result.Add(curr);
                result.Add(next);
                verts.RemoveAt(i);
                earFound = true;
                break;
            }

            if (!earFound)
            {
                break;
            }
        }

        return result;
    }

    private static List<int> BuildFanFallback(List<Vector2> polygon)
    {
        List<int> result = new List<int>();
        if (polygon == null || polygon.Count < 3) return result;

        for (int i = 1; i < polygon.Count - 1; i++)
        {
            result.Add(0);
            result.Add(i);
            result.Add(i + 1);
        }

        return result;
    }

    private static List<Vector3> BuildConvexHull(List<Vector3> worldPoints, Vector3 planeNormalWorld)
    {
        List<Vector3> result = new List<Vector3>();
        if (worldPoints == null || worldPoints.Count < 3) return result;

        Vector3 tangent = Vector3.Cross(planeNormalWorld, Vector3.up);
        if (tangent.sqrMagnitude < 0.000001f)
        {
            tangent = Vector3.Cross(planeNormalWorld, Vector3.right);
        }
        tangent.Normalize();

        Vector3 bitangent = Vector3.Cross(planeNormalWorld, tangent).normalized;
        Vector3 origin = worldPoints[0];

        List<CapPoint> points = new List<CapPoint>();
        for (int i = 0; i < worldPoints.Count; i++)
        {
            Vector3 d = worldPoints[i] - origin;
            points.Add(new CapPoint
            {
                world = worldPoints[i],
                projected = new Vector2(
                    Vector3.Dot(d, tangent),
                    Vector3.Dot(d, bitangent))
            });
        }

        points.Sort((a, b) =>
        {
            int x = a.projected.x.CompareTo(b.projected.x);
            return x != 0 ? x : a.projected.y.CompareTo(b.projected.y);
        });

        RemoveProjectedNearDuplicates(points);
        if (points.Count < 3) return result;

        List<CapPoint> hull = new List<CapPoint>();
        for (int i = 0; i < points.Count; i++)
        {
            while (hull.Count >= 2 &&
                   Cross(
                       hull[hull.Count - 1].projected - hull[hull.Count - 2].projected,
                       points[i].projected - hull[hull.Count - 1].projected) <= 0f)
            {
                hull.RemoveAt(hull.Count - 1);
            }

            hull.Add(points[i]);
        }

        int lowerCount = hull.Count;
        for (int i = points.Count - 2; i >= 0; i--)
        {
            while (hull.Count > lowerCount &&
                   Cross(
                       hull[hull.Count - 1].projected - hull[hull.Count - 2].projected,
                       points[i].projected - hull[hull.Count - 1].projected) <= 0f)
            {
                hull.RemoveAt(hull.Count - 1);
            }

            hull.Add(points[i]);
        }

        if (hull.Count > 1)
        {
            hull.RemoveAt(hull.Count - 1);
        }

        for (int i = 0; i < hull.Count; i++)
        {
            result.Add(hull[i].world);
        }

        return result;
    }

    private static float SignedArea(List<Vector2> poly)
    {
        float area = 0f;
        for (int i = 0; i < poly.Count; i++)
        {
            Vector2 a = poly[i];
            Vector2 b = poly[(i + 1) % poly.Count];
            area += a.x * b.y - b.x * a.y;
        }
        return area * 0.5f;
    }

    private static bool IsConvex(Vector2 a, Vector2 b, Vector2 c)
    {
        return Cross(b - a, c - b) > 0f;
    }

    private static float Cross(Vector2 a, Vector2 b)
    {
        return a.x * b.y - a.y * b.x;
    }

    private static bool PointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        float c1 = Cross(b - a, p - a);
        float c2 = Cross(c - b, p - b);
        float c3 = Cross(a - c, p - c);

        bool hasNeg = (c1 < 0f) || (c2 < 0f) || (c3 < 0f);
        bool hasPos = (c1 > 0f) || (c2 > 0f) || (c3 > 0f);

        return !(hasNeg && hasPos);
    }
}
