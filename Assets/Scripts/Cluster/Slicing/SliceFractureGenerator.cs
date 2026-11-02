using System.Collections.Generic;
using UnityEngine;

public class SliceFracturePiece
{
    public Mesh mesh;
    public Bounds bounds;
}

public static class SliceFractureGenerator
{
    private static readonly bool LogInternalSliceWarnings = false;

    private class WorkPiece
    {
        public Mesh mesh;
        public Bounds bounds;
        public bool lockedFromSlicing;
    }

    public static List<SliceFracturePiece> Generate(
    Mesh sourceMesh,
    SliceFractureSettings settings)
    {
        List<SliceFracturePiece> result = new List<SliceFracturePiece>();

        if (sourceMesh == null)
        {
            Debug.LogError("SliceFractureGenerator: sourceMesh is null");
            return result;
        }

        settings.ApplyQualityPreset();

        List<WorkPiece> workPieces = new List<WorkPiece>();

        Mesh initialMesh = Object.Instantiate(sourceMesh);
        initialMesh.RecalculateBounds();

        workPieces.Add(new WorkPiece
        {
            mesh = initialMesh,
            bounds = initialMesh.bounds
        });

        System.Random rng = new System.Random(settings.randomSeed);

        int targetSplits = Mathf.Max(0, settings.sliceCount);

        for (int splitIndex = 0; splitIndex < targetSplits; splitIndex++)
        {
            int pieceIndex = FindLargestSliceCandidateIndex(workPieces, settings);
            if (pieceIndex < 0)
            {
                LogInternalWarning("No slice candidate found");
                break;
            }

            WorkPiece targetPiece = workPieces[pieceIndex];
            if (targetPiece.mesh == null)
            {
                LogInternalWarning("Target piece mesh is null");
                break;
            }

            if (!CanSliceBounds(targetPiece.bounds, settings))
            {
                LogInternalWarning("CanSliceBounds == false");
                targetPiece.lockedFromSlicing = true;
                continue;
            }

            if (!TrySlicePiece(targetPiece, settings, rng, out WorkPiece positive, out WorkPiece negative))
            {
                LogInternalWarning($"Split {splitIndex}: failed all retries");
                targetPiece.lockedFromSlicing = true;
                continue;
            }

            Mesh previousMesh = targetPiece.mesh;
            workPieces.RemoveAt(pieceIndex);
            DestroyMesh(previousMesh);

            workPieces.Add(positive);
            workPieces.Add(negative);
        }

        if (settings.mergeSmallPieces)
        {
            MergeSmallPieces(workPieces, settings);
        }

        for (int i = 0; i < workPieces.Count; i++)
        {
            if (workPieces[i].mesh == null) continue;

            result.Add(new SliceFracturePiece
            {
                mesh = workPieces[i].mesh,
                bounds = workPieces[i].bounds
            });
        }

        return result;
    }

    private static void LogInternalWarning(string message)
    {
        if (LogInternalSliceWarnings)
            Debug.LogWarning(message);
    }


    private static bool TrySlicePiece(
        WorkPiece targetPiece,
        SliceFractureSettings settings,
        System.Random rng,
        out WorkPiece positive,
        out WorkPiece negative)
    {
        positive = null;
        negative = null;

        for (int retry = 0; retry < settings.maxRetryPerSlice; retry++)
        {
            Plane localPlane = BuildRandomLocalPlane(targetPiece.bounds, settings, rng);

            MeshSliceResult sliceResult = MeshSlicer.Slice(
                targetPiece.mesh,
                localPlane,
                Matrix4x4.identity,
                Matrix4x4.identity);

            if (!sliceResult.success ||
                sliceResult.positiveMesh == null ||
                sliceResult.negativeMesh == null)
            {
                DestroySliceResult(sliceResult);
                continue;
            }

            sliceResult.positiveMesh.RecalculateBounds();
            sliceResult.negativeMesh.RecalculateBounds();

            Bounds pb = sliceResult.positiveMesh.bounds;
            Bounds nb = sliceResult.negativeMesh.bounds;

            if (!IsValidPiece(pb, settings) || !IsValidPiece(nb, settings))
            {
                DestroySliceResult(sliceResult);
                continue;
            }

            positive = new WorkPiece
            {
                mesh = sliceResult.positiveMesh,
                bounds = pb
            };

            negative = new WorkPiece
            {
                mesh = sliceResult.negativeMesh,
                bounds = nb
            };

            return true;
        }

        return false;
    }

    private static bool CanSliceBounds(Bounds bounds, SliceFractureSettings settings)
    {
        Vector3 s = bounds.size;
        float minAxis = Mathf.Min(s.x, Mathf.Min(s.y, s.z));
        float maxAxis = Mathf.Max(s.x, Mathf.Max(s.y, s.z));
        float midAxis = s.x + s.y + s.z - minAxis - maxAxis;
        float volume = s.x * s.y * s.z;
        float flatAxisThreshold = Mathf.Max(0.000001f, settings.minAxisSize * 0.2f);

        if (minAxis <= flatAxisThreshold)
        {
            float areaLike = maxAxis * midAxis;
            return maxAxis >= settings.minAxisSize * 2f &&
                   midAxis >= settings.minAxisSize * 0.4f &&
                   areaLike >= settings.minPieceVolume * 2f;
        }

        return maxAxis >= settings.minAxisSize * 2f &&
               volume >= settings.minPieceVolume * 2f;
    }

    private static bool IsValidPiece(Bounds bounds, SliceFractureSettings settings)
    {
        Vector3 s = bounds.size;
        float volume = s.x * s.y * s.z;
        float minAxis = Mathf.Min(s.x, Mathf.Min(s.y, s.z));
        float maxAxis = Mathf.Max(s.x, Mathf.Max(s.y, s.z));
        float midAxis = s.x + s.y + s.z - minAxis - maxAxis;

        if (maxAxis < settings.minAxisSize)
        {
            return false;
        }

        if (midAxis < settings.minAxisSize * 0.2f)
        {
            return false;
        }

        float flatAxisThreshold = Mathf.Max(0.000001f, settings.minAxisSize * 0.2f);
        if (minAxis <= flatAxisThreshold)
        {
            if (settings.maxAspectRatio > 0f && midAxis > 0.000001f)
            {
                return maxAxis / midAxis <= settings.maxAspectRatio * 2f;
            }

            return true;
        }

        if (volume < settings.minPieceVolume)
        {
            return false;
        }

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

    private static int FindLargestSliceCandidateIndex(List<WorkPiece> pieces, SliceFractureSettings settings)
    {
        if (pieces == null || pieces.Count == 0) return -1;

        int bestIndex = -1;
        float bestVolume = -1f;

        for (int i = 0; i < pieces.Count; i++)
        {
            if (pieces[i] == null || pieces[i].mesh == null) continue;
            if (pieces[i].lockedFromSlicing) continue;
            if (!CanSliceBounds(pieces[i].bounds, settings)) continue;

            Vector3 s = pieces[i].bounds.size;
            float volume = s.x * s.y * s.z;

            if (volume > bestVolume)
            {
                bestVolume = volume;
                bestIndex = i;
            }
        }

        return bestIndex;
    }

    private static void MergeSmallPieces(List<WorkPiece> pieces, SliceFractureSettings settings)
    {
        if (pieces == null || pieces.Count <= 1) return;

        int maxPasses = Mathf.Max(0, settings.maxSmallPieceMergePasses);

        for (int pass = 0; pass < maxPasses; pass++)
        {
            int smallIndex = FindSmallPieceIndex(pieces, settings);
            if (smallIndex < 0) break;

            int neighborIndex = FindNearestPieceIndex(pieces, smallIndex);
            if (neighborIndex < 0) break;

            WorkPiece merged = MergePieces(pieces[smallIndex], pieces[neighborIndex]);
            if (merged == null) break;

            int first = Mathf.Max(smallIndex, neighborIndex);
            int second = Mathf.Min(smallIndex, neighborIndex);

            DestroyMesh(pieces[first].mesh);
            pieces.RemoveAt(first);

            DestroyMesh(pieces[second].mesh);
            pieces.RemoveAt(second);

            pieces.Add(merged);
        }
    }

    private static int FindSmallPieceIndex(List<WorkPiece> pieces, SliceFractureSettings settings)
    {
        int bestIndex = -1;
        float bestVolume = float.MaxValue;

        for (int i = 0; i < pieces.Count; i++)
        {
            if (pieces[i] == null || pieces[i].mesh == null) continue;
            if (!ShouldMergePiece(pieces[i].bounds, settings)) continue;

            Vector3 size = pieces[i].bounds.size;
            float volume = size.x * size.y * size.z;

            if (volume < bestVolume)
            {
                bestVolume = volume;
                bestIndex = i;
            }
        }

        return bestIndex;
    }

    private static bool ShouldMergePiece(Bounds bounds, SliceFractureSettings settings)
    {
        if (!IsValidPiece(bounds, settings))
        {
            return true;
        }

        Vector3 size = bounds.size;
        float volume = size.x * size.y * size.z;
        float minAxis = Mathf.Min(size.x, Mathf.Min(size.y, size.z));
        float maxAxis = Mathf.Max(size.x, Mathf.Max(size.y, size.z));
        float midAxis = size.x + size.y + size.z - minAxis - maxAxis;
        float axisRatio = maxAxis > 0.000001f ? minAxis / maxAxis : 0f;
        float flatAxisThreshold = Mathf.Max(0.000001f, settings.minAxisSize * 0.2f);

        if (minAxis <= flatAxisThreshold)
        {
            if (maxAxis < settings.minAxisSize * 1.1f)
            {
                return true;
            }

            if (midAxis < settings.minAxisSize * 0.3f)
            {
                return true;
            }

            return false;
        }

        if (volume < settings.minPieceVolume * 1.5f)
        {
            return true;
        }

        if (maxAxis < settings.minAxisSize * 1.1f)
        {
            return true;
        }

        if (settings.minAxisRatio > 0f && axisRatio < settings.minAxisRatio * 1.1f)
        {
            return true;
        }

        return false;
    }

    private static int FindNearestPieceIndex(List<WorkPiece> pieces, int sourceIndex)
    {
        int bestIndex = -1;
        float bestDistance = float.MaxValue;
        Bounds sourceBounds = pieces[sourceIndex].bounds;

        for (int i = 0; i < pieces.Count; i++)
        {
            if (i == sourceIndex) continue;
            if (pieces[i] == null || pieces[i].mesh == null) continue;

            float boundsGap = BoundsGap(sourceBounds, pieces[i].bounds);
            float centerDistance = (sourceBounds.center - pieces[i].bounds.center).sqrMagnitude;
            float distance = boundsGap <= 0.0001f ? centerDistance * 0.001f : boundsGap * boundsGap;

            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestIndex = i;
            }
        }

        return bestIndex;
    }

    private static WorkPiece MergePieces(WorkPiece a, WorkPiece b)
    {
        if (a == null || b == null || a.mesh == null || b.mesh == null) return null;

        CombineInstance[] combines = new CombineInstance[2];
        combines[0].mesh = a.mesh;
        combines[0].transform = Matrix4x4.identity;
        combines[1].mesh = b.mesh;
        combines[1].transform = Matrix4x4.identity;

        Mesh mergedMesh = new Mesh();
        mergedMesh.name = "MergedSlicePiece";

        if (a.mesh.vertexCount + b.mesh.vertexCount > 65000)
        {
            mergedMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        }

        mergedMesh.CombineMeshes(combines, true, false, false);
        mergedMesh.RecalculateNormals();
        mergedMesh.RecalculateBounds();

        return new WorkPiece
        {
            mesh = mergedMesh,
            bounds = mergedMesh.bounds
        };
    }

    private static float BoundsGap(Bounds a, Bounds b)
    {
        float dx = Mathf.Max(0f, Mathf.Max(a.min.x - b.max.x, b.min.x - a.max.x));
        float dy = Mathf.Max(0f, Mathf.Max(a.min.y - b.max.y, b.min.y - a.max.y));
        float dz = Mathf.Max(0f, Mathf.Max(a.min.z - b.max.z, b.min.z - a.max.z));

        return Mathf.Sqrt(dx * dx + dy * dy + dz * dz);
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

    private static Plane BuildRandomLocalPlane(
        Bounds bounds,
        SliceFractureSettings settings,
        System.Random rng)
    {
        Vector3 bias = settings.axisBias.sqrMagnitude > 0.0001f
            ? settings.axisBias.normalized
            : Vector3.up;

        Quaternion jitter = Quaternion.Euler(
            RandomRange(rng, -settings.angleJitter, settings.angleJitter),
            RandomRange(rng, -settings.angleJitter, settings.angleJitter),
            RandomRange(rng, -settings.angleJitter, settings.angleJitter));

        Vector3 normal = (jitter * bias).normalized;

        Vector3 center = bounds.center;
        Vector3 extent = bounds.extents * 0.35f;

        Vector3 point = new Vector3(
            RandomRange(rng, center.x - extent.x, center.x + extent.x),
            RandomRange(rng, center.y - extent.y, center.y + extent.y),
            RandomRange(rng, center.z - extent.z, center.z + extent.z)
        );

        return new Plane(normal, point);
    }

    private static float RandomRange(System.Random rng, float min, float max)
    {
        return (float)(min + (max - min) * rng.NextDouble());
    }
}
