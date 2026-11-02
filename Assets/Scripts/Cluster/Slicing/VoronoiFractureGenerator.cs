using System.Collections.Generic;
using UnityEngine;

public static class VoronoiFractureGenerator
{
    private const float PlaneEpsilon = 0.00001f;

    public static List<SliceFracturePiece> Generate(
        Mesh sourceMesh,
        VoronoiFractureSettings settings)
    {
        List<SliceFracturePiece> result = new List<SliceFracturePiece>();

        if (sourceMesh == null)
        {
            Debug.LogError("VoronoiFractureGenerator: sourceMesh is null");
            return result;
        }

        if (!sourceMesh.isReadable)
        {
            Debug.LogWarning($"VoronoiFractureGenerator skipped because mesh '{sourceMesh.name}' is not readable. Enable Read/Write in import settings to use true mesh fracture.");
            return result;
        }

        settings.ApplyQualityPreset();

        Mesh workingSource = Object.Instantiate(sourceMesh);
        if (!workingSource.isReadable)
        {
            Debug.LogWarning($"VoronoiFractureGenerator skipped because mesh copy '{workingSource.name}' is not readable.");
            DestroyMesh(workingSource);
            return result;
        }

        workingSource.RecalculateBounds();

        List<Vector3> seeds = GenerateSeeds(workingSource, settings);

        for (int i = 0; i < seeds.Count; i++)
        {
            Mesh cellMesh = Object.Instantiate(workingSource);
            cellMesh.name = $"VoronoiCell_{i}";
            if (!cellMesh.isReadable)
            {
                DestroyMesh(cellMesh);
                continue;
            }

            cellMesh.RecalculateBounds();

            bool valid = true;

            for (int j = 0; j < seeds.Count; j++)
            {
                if (i == j) continue;

                Vector3 toSeed = seeds[i] - seeds[j];
                if (toSeed.sqrMagnitude <= 0.000001f) continue;

                Vector3 normal = toSeed.normalized;
                Vector3 midpoint = (seeds[i] + seeds[j]) * 0.5f;
                Plane keepSeedSidePlane = new Plane(normal, midpoint);

                int meshSide = ClassifyMeshVertices(cellMesh, keepSeedSidePlane);
                if (meshSide > 0)
                {
                    continue;
                }

                if (meshSide < 0)
                {
                    valid = false;
                    break;
                }

                MeshSliceResult sliceResult = MeshSlicer.Slice(
                    cellMesh,
                    keepSeedSidePlane,
                    Matrix4x4.identity,
                    Matrix4x4.identity);

                if (!sliceResult.success || sliceResult.positiveMesh == null)
                {
                    DestroySliceResult(sliceResult);
                    valid = false;
                    break;
                }

                DestroyMesh(cellMesh);
                DestroyMesh(sliceResult.negativeMesh);

                cellMesh = sliceResult.positiveMesh;
                cellMesh.name = $"VoronoiCell_{i}";
                cellMesh.RecalculateBounds();
            }

            if (!valid || cellMesh == null)
            {
                DestroyMesh(cellMesh);
                continue;
            }

            cellMesh.RecalculateBounds();
            if (!IsValidPiece(cellMesh.bounds, settings))
            {
                DestroyMesh(cellMesh);
                continue;
            }

            result.Add(new SliceFracturePiece
            {
                mesh = cellMesh,
                bounds = cellMesh.bounds
            });
        }

        DestroyMesh(workingSource);

        return result;
    }

    private static List<Vector3> GenerateSeeds(Mesh mesh, VoronoiFractureSettings settings)
    {
        int seedCount = Mathf.Max(1, settings.seedCount);
        List<Vector3> seeds = new List<Vector3>(seedCount);
        System.Random rng = new System.Random(settings.randomSeed);
        Vector3[] vertices = mesh.vertices;
        int[] triangles = mesh.triangles;

        if (vertices != null && vertices.Length > 0 && triangles != null && triangles.Length >= 3)
        {
            List<int> validTriangles = new List<int>();
            List<float> cumulativeAreas = new List<float>();
            float totalArea = 0f;

            for (int i = 0; i < triangles.Length; i += 3)
            {
                Vector3 a = vertices[triangles[i]];
                Vector3 b = vertices[triangles[i + 1]];
                Vector3 c = vertices[triangles[i + 2]];
                float area = Vector3.Cross(b - a, c - a).magnitude * 0.5f;
                if (area <= 0.000001f)
                    continue;

                totalArea += area;
                validTriangles.Add(i);
                cumulativeAreas.Add(totalArea);
            }

            if (validTriangles.Count > 0 && totalArea > 0f)
            {
                for (int i = 0; i < seedCount; i++)
                {
                    float pick = RandomRange(rng, 0f, totalArea);
                    int triangleListIndex = FindAreaIndex(cumulativeAreas, pick);
                    int triangleIndex = validTriangles[triangleListIndex];

                    Vector3 a = vertices[triangles[triangleIndex]];
                    Vector3 b = vertices[triangles[triangleIndex + 1]];
                    Vector3 c = vertices[triangles[triangleIndex + 2]];

                    float u = RandomRange(rng, 0f, 1f);
                    float v = RandomRange(rng, 0f, 1f);
                    if (u + v > 1f)
                    {
                        u = 1f - u;
                        v = 1f - v;
                    }

                    seeds.Add(a + (b - a) * u + (c - a) * v);
                }

                return seeds;
            }
        }

        Bounds bounds = mesh.bounds;

        for (int i = 0; i < seedCount; i++)
        {
            Vector3 seed = new Vector3(
                RandomRange(rng, bounds.min.x, bounds.max.x),
                RandomRange(rng, bounds.min.y, bounds.max.y),
                RandomRange(rng, bounds.min.z, bounds.max.z));

            seeds.Add(seed);
        }

        return seeds;
    }

    private static int FindAreaIndex(List<float> cumulativeAreas, float pick)
    {
        for (int i = 0; i < cumulativeAreas.Count; i++)
        {
            if (pick <= cumulativeAreas[i])
            {
                return i;
            }
        }

        return cumulativeAreas.Count - 1;
    }

    private static int ClassifyMeshVertices(Mesh mesh, Plane plane)
    {
        if (mesh == null || mesh.vertexCount == 0) return -1;
        if (!mesh.isReadable) return -1;

        Vector3[] vertices = mesh.vertices;
        bool hasPositive = false;
        bool hasNegative = false;

        for (int i = 0; i < vertices.Length; i++)
        {
            float distance = plane.GetDistanceToPoint(vertices[i]);

            if (distance >= -PlaneEpsilon)
            {
                hasPositive = true;
            }

            if (distance <= PlaneEpsilon)
            {
                hasNegative = true;
            }
        }

        if (hasPositive && hasNegative) return 0;
        return hasPositive ? 1 : -1;
    }

    private static bool IsValidPiece(Bounds bounds, VoronoiFractureSettings settings)
    {
        Vector3 size = bounds.size;
        float volume = size.x * size.y * size.z;
        float minAxis = Mathf.Min(size.x, Mathf.Min(size.y, size.z));
        float maxAxis = Mathf.Max(size.x, Mathf.Max(size.y, size.z));
        float midAxis = size.x + size.y + size.z - minAxis - maxAxis;

        if (maxAxis < settings.minAxisSize) return false;
        if (midAxis < settings.minAxisSize * 0.2f) return false;

        float flatAxisThreshold = Mathf.Max(0.000001f, settings.minAxisSize * 0.2f);
        if (minAxis <= flatAxisThreshold)
        {
            if (settings.maxAspectRatio > 0f && midAxis > 0.000001f)
            {
                return maxAxis / midAxis <= settings.maxAspectRatio * 2f;
            }

            return true;
        }

        if (volume < settings.minPieceVolume) return false;

        float axisRatio = minAxis / maxAxis;
        if (settings.minAxisRatio > 0f && axisRatio < settings.minAxisRatio)
        {
            return false;
        }

        float aspectRatio = maxAxis / minAxis;
        if (settings.maxAspectRatio > 0f && aspectRatio > settings.maxAspectRatio)
        {
            return false;
        }

        return true;
    }

    private static void DestroySliceResult(MeshSliceResult result)
    {
        if (result == null) return;

        DestroyMesh(result.positiveMesh);
        DestroyMesh(result.negativeMesh);
    }

    private static void DestroyMesh(Mesh mesh)
    {
        if (mesh == null) return;

        if (Application.isPlaying)
        {
            Object.Destroy(mesh);
        }
        else
        {
            Object.DestroyImmediate(mesh);
        }
    }

    private static float RandomRange(System.Random rng, float min, float max)
    {
        return (float)(min + (max - min) * rng.NextDouble());
    }
}
