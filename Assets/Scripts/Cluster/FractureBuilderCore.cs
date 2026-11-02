using System;
using System.Collections.Generic;
using UnityEngine;

public enum FractureMode
{
    Voxel,
    Voronoi,
    Slice
}

public enum FractureQualityPreset
{
    Custom,
    Light,
    Medium,
    Heavy
}

public enum FracturePieceColliderMode
{
    Auto,
    Box,
    ConvexMesh
}

[Serializable]
public class VoxelFractureSettings
{
    public int gridX = 6;
    public int gridY = 6;
    public int gridZ = 6;
}

[Serializable]
public class VoronoiFractureSettings
{
    public FractureQualityPreset qualityPreset = FractureQualityPreset.Medium;
    public int seedCount = 16;
    public int gridX = 12;
    public int gridY = 12;
    public int gridZ = 12;
    public int randomSeed = 0;
    public float minPieceVolume = 0.001f;
    public float minAxisSize = 0.05f;
    public float minAxisRatio = 0.08f;
    public float maxAspectRatio = 14f;
    public FracturePieceColliderMode colliderMode = FracturePieceColliderMode.Auto;
    public int autoMeshColliderMaxTriangles = 160;
    public float autoMeshColliderMinAxisSize = 0.3f;
    public float autoMeshColliderMinVolume = 0.03f;

    public void ApplyQualityPreset()
    {
        switch (qualityPreset)
        {
            case FractureQualityPreset.Light:
                seedCount = 8;
                minPieceVolume = 0.0025f;
                minAxisSize = 0.06f;
                minAxisRatio = 0.08f;
                maxAspectRatio = 16f;
                colliderMode = FracturePieceColliderMode.Auto;
                autoMeshColliderMaxTriangles = 80;
                autoMeshColliderMinAxisSize = 0.4f;
                autoMeshColliderMinVolume = 0.05f;
                break;

            case FractureQualityPreset.Medium:
                seedCount = 14;
                minPieceVolume = 0.0006f;
                minAxisSize = 0.035f;
                minAxisRatio = 0.045f;
                maxAspectRatio = 24f;
                colliderMode = FracturePieceColliderMode.Auto;
                autoMeshColliderMaxTriangles = 128;
                autoMeshColliderMinAxisSize = 0.28f;
                autoMeshColliderMinVolume = 0.025f;
                break;

            case FractureQualityPreset.Heavy:
                seedCount = 24;
                minPieceVolume = 0.00015f;
                minAxisSize = 0.02f;
                minAxisRatio = 0.02f;
                maxAspectRatio = 36f;
                colliderMode = FracturePieceColliderMode.Auto;
                autoMeshColliderMaxTriangles = 192;
                autoMeshColliderMinAxisSize = 0.18f;
                autoMeshColliderMinVolume = 0.01f;
                break;
        }
    }
}

[Serializable]
public class SliceFractureSettings
{
    public FractureQualityPreset qualityPreset = FractureQualityPreset.Medium;
    public int sliceCount = 4;
    public int gridX = 12;
    public int gridY = 12;
    public int gridZ = 12;
    public int randomSeed = 0;
    public float angleJitter = 25f;
    public Vector3 axisBias = Vector3.up;

    public float minPieceVolume = 0.001f;
    public float minAxisSize = 0.05f;
    public float minAxisRatio = 0.1f;
    public float maxAspectRatio = 10f;
    public int maxRetryPerSlice = 8;
    public bool mergeSmallPieces = true;
    public int maxSmallPieceMergePasses = 32;
    public FracturePieceColliderMode colliderMode = FracturePieceColliderMode.Auto;
    public int autoMeshColliderMaxTriangles = 128;
    public float autoMeshColliderMinAxisSize = 0.35f;
    public float autoMeshColliderMinVolume = 0.04f;

    public void ApplyQualityPreset()
    {
        switch (qualityPreset)
        {
            case FractureQualityPreset.Light:
                sliceCount = 5;
                minPieceVolume = 0.0025f;
                minAxisSize = 0.06f;
                minAxisRatio = 0.12f;
                maxAspectRatio = 12f;
                maxRetryPerSlice = 10;
                mergeSmallPieces = true;
                maxSmallPieceMergePasses = 24;
                colliderMode = FracturePieceColliderMode.Auto;
                autoMeshColliderMaxTriangles = 64;
                autoMeshColliderMinAxisSize = 0.5f;
                autoMeshColliderMinVolume = 0.08f;
                break;

            case FractureQualityPreset.Medium:
                sliceCount = 9;
                minPieceVolume = 0.0008f;
                minAxisSize = 0.04f;
                minAxisRatio = 0.07f;
                maxAspectRatio = 18f;
                maxRetryPerSlice = 18;
                mergeSmallPieces = true;
                maxSmallPieceMergePasses = 32;
                colliderMode = FracturePieceColliderMode.Auto;
                autoMeshColliderMaxTriangles = 128;
                autoMeshColliderMinAxisSize = 0.35f;
                autoMeshColliderMinVolume = 0.04f;
                break;

            case FractureQualityPreset.Heavy:
                sliceCount = 14;
                minPieceVolume = 0.00015f;
                minAxisSize = 0.02f;
                minAxisRatio = 0.035f;
                maxAspectRatio = 28f;
                maxRetryPerSlice = 28;
                mergeSmallPieces = true;
                maxSmallPieceMergePasses = 48;
                colliderMode = FracturePieceColliderMode.Auto;
                autoMeshColliderMaxTriangles = 192;
                autoMeshColliderMinAxisSize = 0.25f;
                autoMeshColliderMinVolume = 0.02f;
                break;
        }
    }
}

public class FractureBuildContext
{
    public GameObject sourceObject;
    public Bounds localBounds;
    public Collider[] sourceColliders;
    public bool useColliderFilter;
    public float surfaceTolerance;
    public float pieceSpacing;
    public Material pieceMaterial;
}

public class FractureCell
{
    public Vector3 localCenter;
    public Vector3 localSize;
    public Vector3 worldCenter;
}

public class FracturePieceData
{
    public int pieceId;
    public List<FractureCell> cells = new List<FractureCell>();
    public Bounds localBounds;
}

public static class FractureBuilderCore
{
    public static List<FracturePieceData> GeneratePieces(
        FractureMode mode,
        FractureBuildContext context,
        VoxelFractureSettings voxelSettings,
        VoronoiFractureSettings voronoiSettings,
        SliceFractureSettings sliceSettings)
    {
        switch (mode)
        {
            case FractureMode.Voxel:
                return GenerateVoxelPieces(context, voxelSettings);

            case FractureMode.Voronoi:
                return GenerateVoronoiPieces(context, voronoiSettings);

            case FractureMode.Slice:
                return GenerateSlicePieces(context, sliceSettings);
        }

        return new List<FracturePieceData>();
    }

    public static List<FracturePieceData> GenerateVoxelPieces(
        FractureBuildContext context,
        VoxelFractureSettings settings)
    {
        List<FractureCell> cells = GenerateCells(
            context,
            settings.gridX,
            settings.gridY,
            settings.gridZ);

        List<FracturePieceData> pieces = new List<FracturePieceData>(cells.Count);

        for (int i = 0; i < cells.Count; i++)
        {
            FracturePieceData piece = new FracturePieceData();
            piece.pieceId = i;
            piece.cells.Add(cells[i]);
            piece.localBounds = new Bounds(cells[i].localCenter, cells[i].localSize);
            pieces.Add(piece);
        }

        return pieces;
    }

    public static List<FracturePieceData> GenerateVoronoiPieces(
        FractureBuildContext context,
        VoronoiFractureSettings settings)
    {
        List<FractureCell> cells = GenerateCells(
            context,
            settings.gridX,
            settings.gridY,
            settings.gridZ);

        List<FracturePieceData> pieces = new List<FracturePieceData>();
        if (cells.Count == 0) return pieces;

        int seedCount = Mathf.Clamp(settings.seedCount, 1, cells.Count);
        System.Random rng = new System.Random(settings.randomSeed);

        List<int> available = new List<int>(cells.Count);
        for (int i = 0; i < cells.Count; i++)
        {
            available.Add(i);
        }

        List<Vector3> seeds = new List<Vector3>(seedCount);
        for (int i = 0; i < seedCount; i++)
        {
            int pick = rng.Next(available.Count);
            int cellIndex = available[pick];
            available.RemoveAt(pick);
            seeds.Add(cells[cellIndex].localCenter);
        }

        Dictionary<int, FracturePieceData> groups = new Dictionary<int, FracturePieceData>();

        for (int i = 0; i < cells.Count; i++)
        {
            int nearest = 0;
            float nearestDist = float.MaxValue;

            for (int s = 0; s < seeds.Count; s++)
            {
                float dist = (cells[i].localCenter - seeds[s]).sqrMagnitude;
                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    nearest = s;
                }
            }

            if (!groups.TryGetValue(nearest, out FracturePieceData piece))
            {
                piece = new FracturePieceData();
                piece.pieceId = nearest;
                groups.Add(nearest, piece);
            }

            piece.cells.Add(cells[i]);
        }

        int nextId = 0;
        foreach (var kv in groups)
        {
            kv.Value.pieceId = nextId++;
            kv.Value.localBounds = CalculateBounds(kv.Value.cells);
            pieces.Add(kv.Value);
        }

        return pieces;
    }

    public static List<FracturePieceData> GenerateSlicePieces(
        FractureBuildContext context,
        SliceFractureSettings settings)
    {
        List<FractureCell> cells = GenerateCells(
            context,
            settings.gridX,
            settings.gridY,
            settings.gridZ);

        List<FracturePieceData> pieces = new List<FracturePieceData>();
        if (cells.Count == 0) return pieces;

        int planeCount = Mathf.Clamp(settings.sliceCount, 1, 16);
        System.Random rng = new System.Random(settings.randomSeed);

        Vector3 bias = settings.axisBias.sqrMagnitude > 0.0001f
            ? settings.axisBias.normalized
            : Vector3.up;

        List<Plane> planes = new List<Plane>(planeCount);
        for (int i = 0; i < planeCount; i++)
        {
            Quaternion jitter = Quaternion.Euler(
                RandomRange(rng, -settings.angleJitter, settings.angleJitter),
                RandomRange(rng, -settings.angleJitter, settings.angleJitter),
                RandomRange(rng, -settings.angleJitter, settings.angleJitter));

            Vector3 normal = (jitter * bias).normalized;

            Vector3 point = new Vector3(
                RandomRange(rng, context.localBounds.min.x, context.localBounds.max.x),
                RandomRange(rng, context.localBounds.min.y, context.localBounds.max.y),
                RandomRange(rng, context.localBounds.min.z, context.localBounds.max.z));

            planes.Add(new Plane(normal, point));
        }

        Dictionary<ulong, FracturePieceData> groups = new Dictionary<ulong, FracturePieceData>();

        for (int i = 0; i < cells.Count; i++)
        {
            ulong mask = 0UL;
            Vector3 p = cells[i].localCenter;

            for (int j = 0; j < planes.Count; j++)
            {
                if (planes[j].GetSide(p))
                {
                    mask |= (1UL << j);
                }
            }

            if (!groups.TryGetValue(mask, out FracturePieceData piece))
            {
                piece = new FracturePieceData();
                groups.Add(mask, piece);
            }

            piece.cells.Add(cells[i]);
        }

        int nextId = 0;
        foreach (var kv in groups)
        {
            kv.Value.pieceId = nextId++;
            kv.Value.localBounds = CalculateBounds(kv.Value.cells);
            pieces.Add(kv.Value);
        }

        return pieces;
    }

    public static GameObject CreatePieceObject(
        Transform parent,
        FracturePieceData piece,
        Material pieceMaterial,
        float pieceSpacing)
    {
        GameObject go = new GameObject("Piece_" + piece.pieceId);
        go.transform.SetParent(parent, false);

        Vector3 center = piece.localBounds.center;
        go.transform.localPosition = center;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;

        Mesh mesh = BuildCombinedCellMesh(piece, center, pieceSpacing);

        MeshFilter mf = go.AddComponent<MeshFilter>();
        mf.sharedMesh = mesh;

        MeshRenderer mr = go.AddComponent<MeshRenderer>();
        if (pieceMaterial != null)
        {
            mr.sharedMaterial = pieceMaterial;
        }

        BoxCollider bc = go.AddComponent<BoxCollider>();
        bc.center = Vector3.zero;
        bc.size = piece.localBounds.size;

        Rigidbody rb = go.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        go.AddComponent<ClusterPiece>();

        return go;
    }

    public static Bounds CalculateLocalBounds(GameObject source)
    {
        Renderer[] renderers = source.GetComponentsInChildren<Renderer>();
        if (renderers == null || renderers.Length == 0)
        {
            return new Bounds(Vector3.zero, Vector3.one);
        }

        Transform root = source.transform;
        bool initialized = false;
        Bounds localBounds = new Bounds();

        for (int i = 0; i < renderers.Length; i++)
        {
            Bounds wb = renderers[i].bounds;
            Vector3 c = wb.center;
            Vector3 e = wb.extents;

            Vector3[] corners = new Vector3[8]
            {
                c + new Vector3( e.x,  e.y,  e.z),
                c + new Vector3( e.x,  e.y, -e.z),
                c + new Vector3( e.x, -e.y,  e.z),
                c + new Vector3( e.x, -e.y, -e.z),
                c + new Vector3(-e.x,  e.y,  e.z),
                c + new Vector3(-e.x,  e.y, -e.z),
                c + new Vector3(-e.x, -e.y,  e.z),
                c + new Vector3(-e.x, -e.y, -e.z),
            };

            for (int j = 0; j < corners.Length; j++)
            {
                Vector3 localPoint = root.InverseTransformPoint(corners[j]);

                if (!initialized)
                {
                    localBounds = new Bounds(localPoint, Vector3.zero);
                    initialized = true;
                }
                else
                {
                    localBounds.Encapsulate(localPoint);
                }
            }
        }

        return localBounds;
    }

    public static List<FractureCell> GenerateCells(
        FractureBuildContext context,
        int gridX,
        int gridY,
        int gridZ)
    {
        List<FractureCell> result = new List<FractureCell>();

        Vector3 size = context.localBounds.size;
        Vector3 cellSize = new Vector3(
            size.x / gridX,
            size.y / gridY,
            size.z / gridZ);

        Vector3 min = context.localBounds.min;

        for (int x = 0; x < gridX; x++)
        {
            for (int y = 0; y < gridY; y++)
            {
                for (int z = 0; z < gridZ; z++)
                {
                    Vector3 localCenter = new Vector3(
                        min.x + cellSize.x * (x + 0.5f),
                        min.y + cellSize.y * (y + 0.5f),
                        min.z + cellSize.z * (z + 0.5f));

                    Vector3 worldCenter = context.sourceObject.transform.TransformPoint(localCenter);

                    if (context.useColliderFilter &&
                        context.sourceColliders != null &&
                        context.sourceColliders.Length > 0)
                    {
                        bool accepted = false;

                        for (int i = 0; i < context.sourceColliders.Length; i++)
                        {
                            Collider col = context.sourceColliders[i];
                            if (col == null) continue;

                            Vector3 closest = col.ClosestPoint(worldCenter);
                            float dist = Vector3.Distance(worldCenter, closest);

                            if (dist <= context.surfaceTolerance ||
                                closest == worldCenter)
                            {
                                accepted = true;
                                break;
                            }
                        }

                        if (!accepted)
                        {
                            continue;
                        }
                    }

                    FractureCell cell = new FractureCell();
                    cell.localCenter = localCenter;
                    cell.localSize = cellSize;
                    cell.worldCenter = worldCenter;
                    result.Add(cell);
                }
            }
        }

        return result;
    }

    private static Bounds CalculateBounds(List<FractureCell> cells)
    {
        if (cells == null || cells.Count == 0)
        {
            return new Bounds(Vector3.zero, Vector3.zero);
        }

        Bounds b = new Bounds(cells[0].localCenter, cells[0].localSize);

        for (int i = 1; i < cells.Count; i++)
        {
            Bounds cb = new Bounds(cells[i].localCenter, cells[i].localSize);
            b.Encapsulate(cb.min);
            b.Encapsulate(cb.max);
        }

        return b;
    }

    private static float RandomRange(System.Random rng, float min, float max)
    {
        return (float)(min + (max - min) * rng.NextDouble());
    }

    private static Mesh BuildCombinedCellMesh(
        FracturePieceData piece,
        Vector3 pivotCenter,
        float pieceSpacing)
    {
        Vector3[] cubeVertices = new Vector3[]
        {
            new Vector3(-0.5f, -0.5f, -0.5f),
            new Vector3( 0.5f, -0.5f, -0.5f),
            new Vector3( 0.5f,  0.5f, -0.5f),
            new Vector3(-0.5f,  0.5f, -0.5f),
            new Vector3(-0.5f, -0.5f,  0.5f),
            new Vector3( 0.5f, -0.5f,  0.5f),
            new Vector3( 0.5f,  0.5f,  0.5f),
            new Vector3(-0.5f,  0.5f,  0.5f),
        };

        int[] cubeTriangles = new int[]
        {
            0, 2, 1, 0, 3, 2,
            1, 2, 6, 1, 6, 5,
            5, 6, 7, 5, 7, 4,
            4, 7, 3, 4, 3, 0,
            3, 7, 6, 3, 6, 2,
            4, 0, 1, 4, 1, 5
        };

        List<Vector3> vertices = new List<Vector3>(piece.cells.Count * 8);
        List<int> triangles = new List<int>(piece.cells.Count * 36);

        for (int i = 0; i < piece.cells.Count; i++)
        {
            FractureCell cell = piece.cells[i];

            Vector3 scaledSize = new Vector3(
                Mathf.Max(0.001f, cell.localSize.x - pieceSpacing),
                Mathf.Max(0.001f, cell.localSize.y - pieceSpacing),
                Mathf.Max(0.001f, cell.localSize.z - pieceSpacing));

            int baseIndex = vertices.Count;
            Vector3 localCenter = cell.localCenter - pivotCenter;

            for (int v = 0; v < cubeVertices.Length; v++)
            {
                Vector3 vert = Vector3.Scale(cubeVertices[v], scaledSize) + localCenter;
                vertices.Add(vert);
            }

            for (int t = 0; t < cubeTriangles.Length; t++)
            {
                triangles.Add(baseIndex + cubeTriangles[t]);
            }
        }

        Mesh mesh = new Mesh();
        if (vertices.Count > 65000)
        {
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        }

        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        return mesh;
    }
}
