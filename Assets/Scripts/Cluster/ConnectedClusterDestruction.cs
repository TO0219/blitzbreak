using System.Collections.Generic;
using UnityEngine;

public class ConnectedClusterDestruction : MonoBehaviour
{
    [Header("Auto Build")]
    [SerializeField] private bool autoCollectChildren = true;
    [SerializeField] private float neighborDistance = 1.1f;
    [SerializeField] private bool autoMarkLowestAsSupport = true;
    [SerializeField] private float supportHeightTolerance = 0.05f;

    [Header("Auto Tune")]
    [SerializeField] private bool autoTuneParameters = true;
    [SerializeField] private float autoNeighborDistanceMultiplier = 0.08f;
    [SerializeField] private float autoSupportHeightToleranceMultiplier = 0.05f;
    [SerializeField] private float autoBreakRadiusMultiplier = 0.65f;
    [SerializeField] private float autoBaseLinkHp = 1f;
    [SerializeField] private float autoDamageToLinkHpMultiplier = 5f;
    [SerializeField] private float autoSupportBonusMultiplier = 1f;
    [SerializeField] private float minAutoNeighborDistance = 0.02f;
    [SerializeField] private float minAutoSupportHeightTolerance = 0.01f;
    [SerializeField] private float minAutoBreakRadius = 0.25f;

    [Header("Link Strength")]
    [SerializeField] private float baseLinkHp = 10f;
    [SerializeField] private float supportBonusMultiplier = 2f;

    [Header("Damage")]
    [SerializeField] private float breakRadius = 1.5f;
    [SerializeField] private float damageAmount = 6f;
    [SerializeField] private AnimationCurve damageFalloff = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);
    [SerializeField] private float detachImpulse = 2.0f;
    [SerializeField] private float upwardImpulse = 0.4f;
    [SerializeField] private bool allowDamagedSupportDetach = true;

    [Header("Debug")]
    [SerializeField] private bool drawNeighborGizmos = true;
    [SerializeField] private bool drawBrokenLinks = false;
    [SerializeField] private bool logDebugInfo = false;

    private readonly List<ClusterPiece> pieces = new List<ClusterPiece>();

    private class DamageResult
    {
        public int damagedLinks;
        public int brokenLinks;
        public readonly HashSet<int> piecesWithBrokenLinks = new HashSet<int>();
    }

    private void Awake()
    {
        BuildCluster();
    }

    public int PieceCount => pieces.Count;

    [ContextMenu("Build Cluster")]
    public void BuildCluster()
    {
        pieces.Clear();

        if (!autoCollectChildren && logDebugInfo)
        {
            Debug.LogWarning("ConnectedClusterDestruction requires child ClusterPiece collection. Forcing collection from children.");
        }

        ClusterPiece[] found = GetComponentsInChildren<ClusterPiece>(true);
        for (int i = 0; i < found.Length; i++)
        {
            if (found[i].transform == transform) continue;
            pieces.Add(found[i]);
        }

        for (int i = 0; i < pieces.Count; i++)
        {
            pieces[i].pieceId = i;
            pieces[i].CaptureInitialTransform();
            pieces[i].ResetRuntimeState();
        }

        AutoTuneParameters();

        if (autoMarkLowestAsSupport)
        {
            MarkLowestPiecesAsSupport();
        }

        BuildLinks();

        if (logDebugInfo)
        {
            if (pieces.Count == 0)
            {
                Debug.LogWarning(
                    "BuildCluster found no ClusterPiece children. " +
                    $"ClusterRoot={GetHierarchyPath(transform)}, " +
                    $"ChildCount={transform.childCount}");
            }
        }
    }

    private void AutoTuneParameters()
    {
        if (!autoTuneParameters) return;
        if (pieces.Count == 0) return;

        Bounds clusterBounds = CalculateClusterWorldBounds(out bool hasBounds);
        if (!hasBounds) return;

        float averagePieceMaxAxis = CalculateAveragePieceMaxAxis();
        if (averagePieceMaxAxis <= 0.0001f) return;

        neighborDistance = Mathf.Max(
            minAutoNeighborDistance,
            averagePieceMaxAxis * Mathf.Max(0.001f, autoNeighborDistanceMultiplier));

        supportHeightTolerance = Mathf.Max(
            minAutoSupportHeightTolerance,
            averagePieceMaxAxis * Mathf.Max(0.001f, autoSupportHeightToleranceMultiplier));

        baseLinkHp = Mathf.Max(0.001f, autoBaseLinkHp);
        supportBonusMultiplier = Mathf.Max(1f, autoSupportBonusMultiplier);

        float clusterMaxAxis = MaxComponent(clusterBounds.size);
        float tunedBreakRadius = Mathf.Max(
            averagePieceMaxAxis * Mathf.Max(0.001f, autoBreakRadiusMultiplier),
            clusterMaxAxis * 0.08f,
            neighborDistance * 2f,
            minAutoBreakRadius);

        breakRadius = tunedBreakRadius;
        damageAmount = Mathf.Max(baseLinkHp, baseLinkHp * Mathf.Max(1f, autoDamageToLinkHpMultiplier));

    }

    private Bounds CalculateClusterWorldBounds(out bool hasBounds)
    {
        hasBounds = false;
        Bounds result = new Bounds(transform.position, Vector3.zero);

        for (int i = 0; i < pieces.Count; i++)
        {
            Bounds bounds = GetPieceWorldBounds(pieces[i]);
            if (!IsUsableBounds(bounds)) continue;

            if (!hasBounds)
            {
                result = bounds;
                hasBounds = true;
            }
            else
            {
                result.Encapsulate(bounds.min);
                result.Encapsulate(bounds.max);
            }
        }

        return result;
    }

    private float CalculateAveragePieceMaxAxis()
    {
        float total = 0f;
        int count = 0;

        for (int i = 0; i < pieces.Count; i++)
        {
            Bounds bounds = GetPieceWorldBounds(pieces[i]);
            if (!IsUsableBounds(bounds)) continue;

            total += MaxComponent(bounds.size);
            count++;
        }

        if (count == 0) return 0f;
        return total / count;
    }

    private void BuildLinks()
    {
        float sqr = neighborDistance * neighborDistance;

        for (int i = 0; i < pieces.Count; i++)
        {
            Vector3 aPos = pieces[i].transform.position;
            Bounds aBounds = GetPieceWorldBounds(pieces[i]);

            for (int j = i + 1; j < pieces.Count; j++)
            {
                Vector3 bPos = pieces[j].transform.position;
                Bounds bBounds = GetPieceWorldBounds(pieces[j]);

                bool linked = false;

                float distSqr = (aPos - bPos).sqrMagnitude;
                if (distSqr <= sqr)
                {
                    linked = true;
                }
                else
                {
                    float boundsGap = BoundsGap(aBounds, bBounds);
                    if (boundsGap <= neighborDistance)
                    {
                        linked = true;
                    }
                }

                if (!linked) continue;

                float hp = CalculateLinkHp(pieces[i], pieces[j]);
                pieces[i].AddLink(j, hp);
                pieces[j].AddLink(i, hp);
            }
        }
    }

    private Bounds GetPieceWorldBounds(ClusterPiece piece)
    {
        Collider c = piece.col;
        if (c != null)
        {
            Bounds colliderBounds = c.bounds;
            if (IsUsableBounds(colliderBounds))
            {
                return colliderBounds;
            }

            if (c is BoxCollider box)
            {
                return TransformBounds(piece.transform.localToWorldMatrix, new Bounds(box.center, box.size));
            }

            if (c is MeshCollider meshCollider && meshCollider.sharedMesh != null)
            {
                return TransformBounds(piece.transform.localToWorldMatrix, meshCollider.sharedMesh.bounds);
            }
        }

        Renderer r = piece.GetComponent<Renderer>();
        if (r != null)
        {
            Bounds rendererBounds = r.bounds;
            if (IsUsableBounds(rendererBounds))
            {
                return rendererBounds;
            }
        }

        MeshFilter meshFilter = piece.GetComponent<MeshFilter>();
        if (meshFilter != null && meshFilter.sharedMesh != null)
        {
            return TransformBounds(piece.transform.localToWorldMatrix, meshFilter.sharedMesh.bounds);
        }

        return new Bounds(piece.transform.position, Vector3.zero);
    }

    private Bounds TransformBounds(Matrix4x4 matrix, Bounds localBounds)
    {
        Vector3 center = matrix.MultiplyPoint3x4(localBounds.center);
        Vector3 extents = localBounds.extents;

        Vector3 axisX = matrix.MultiplyVector(new Vector3(extents.x, 0f, 0f));
        Vector3 axisY = matrix.MultiplyVector(new Vector3(0f, extents.y, 0f));
        Vector3 axisZ = matrix.MultiplyVector(new Vector3(0f, 0f, extents.z));

        extents.x = Mathf.Abs(axisX.x) + Mathf.Abs(axisY.x) + Mathf.Abs(axisZ.x);
        extents.y = Mathf.Abs(axisX.y) + Mathf.Abs(axisY.y) + Mathf.Abs(axisZ.y);
        extents.z = Mathf.Abs(axisX.z) + Mathf.Abs(axisY.z) + Mathf.Abs(axisZ.z);

        return new Bounds(center, extents * 2f);
    }

    private bool IsUsableBounds(Bounds bounds)
    {
        return bounds.size.sqrMagnitude > 0.000001f;
    }

    private float MaxComponent(Vector3 value)
    {
        return Mathf.Max(value.x, Mathf.Max(value.y, value.z));
    }

    private float BoundsGap(Bounds a, Bounds b)
    {
        float dx = Mathf.Max(0f, Mathf.Max(a.min.x - b.max.x, b.min.x - a.max.x));
        float dy = Mathf.Max(0f, Mathf.Max(a.min.y - b.max.y, b.min.y - a.max.y));
        float dz = Mathf.Max(0f, Mathf.Max(a.min.z - b.max.z, b.min.z - a.max.z));

        return Mathf.Sqrt(dx * dx + dy * dy + dz * dz);
    }

    private float CalculateLinkHp(ClusterPiece a, ClusterPiece b)
    {
        float hp = baseLinkHp;
        hp *= 0.5f * (a.strengthMultiplier + b.strengthMultiplier);

        if (a.isSupport || b.isSupport)
        {
            hp *= supportBonusMultiplier;
        }

        return hp;
    }

    private void MarkLowestPiecesAsSupport()
    {
        if (pieces.Count == 0) return;

        float minY = float.MaxValue;

        for (int i = 0; i < pieces.Count; i++)
        {
            float y = GetPieceWorldBounds(pieces[i]).min.y;
            if (y < minY) minY = y;
        }

        for (int i = 0; i < pieces.Count; i++)
        {
            float y = GetPieceWorldBounds(pieces[i]).min.y;
            pieces[i].isSupport = y <= minY + supportHeightTolerance;
        }
    }

    public void DamageAt(Vector3 hitPoint)
    {
        if (pieces.Count == 0)
        {
            BuildCluster();
        }

        if (pieces.Count == 0)
        {
            if (logDebugInfo)
            {
                Debug.LogWarning("DamageAt skipped because this cluster has no ClusterPiece children.");
            }

            return;
        }

        DamageResult damageResult = DamageLinksNear(hitPoint, breakRadius, damageAmount);

        RecalculateConnectivity(hitPoint, damageResult.piecesWithBrokenLinks);
    }

    public void BreakAll(Vector3 hitPoint)
    {
        if (pieces.Count == 0)
        {
            BuildCluster();
        }

        List<int> detachedIds = new List<int>();
        for (int i = 0; i < pieces.Count; i++)
        {
            if (pieces[i] == null || pieces[i].isDetached)
                continue;

            pieces[i].isDetached = true;
            detachedIds.Add(i);
        }

        ActivateDetachedPieces(detachedIds, hitPoint);
    }

    private DamageResult DamageLinksNear(Vector3 hitPoint, float radius, float maxDamage)
    {
        DamageResult result = new DamageResult();
        float radiusSqr = radius * radius;

        for (int i = 0; i < pieces.Count; i++)
        {
            ClusterPiece a = pieces[i];

            for (int n = 0; n < a.links.Count; n++)
            {
                PieceLink link = a.links[n];
                int otherId = link.otherId;

                if (otherId <= i) continue;
                if (link.broken) continue;

                ClusterPiece b = pieces[otherId];

                float linkDistance = GetLinkDistanceToHit(a, b, hitPoint);
                float distSqr = linkDistance * linkDistance;
                if (distSqr > radiusSqr) continue;

                float t = Mathf.Clamp01(linkDistance / radius);
                float falloff = damageFalloff.Evaluate(t);
                float damage = maxDamage * falloff;
                bool wasBroken = link.broken;

                a.ApplyDamageToLink(otherId, damage);
                b.ApplyDamageToLink(i, damage);
                result.damagedLinks++;

                if (!wasBroken && link.broken)
                {
                    result.brokenLinks++;
                    result.piecesWithBrokenLinks.Add(i);
                    result.piecesWithBrokenLinks.Add(otherId);
                }
            }
        }

        return result;
    }

    private float GetLinkDistanceToHit(ClusterPiece a, ClusterPiece b, Vector3 hitPoint)
    {
        Bounds aBounds = GetPieceWorldBounds(a);
        Bounds bBounds = GetPieceWorldBounds(b);

        float aBoundsDistance = DistanceToBounds(aBounds, hitPoint);
        float bBoundsDistance = DistanceToBounds(bBounds, hitPoint);

        Vector3 mid = (a.transform.position + b.transform.position) * 0.5f;
        float midpointDistance = Vector3.Distance(mid, hitPoint);

        return Mathf.Min(midpointDistance, Mathf.Min(aBoundsDistance, bBoundsDistance));
    }

    private float DistanceToBounds(Bounds bounds, Vector3 point)
    {
        if (bounds.size.sqrMagnitude <= 0.000001f)
        {
            return Vector3.Distance(bounds.center, point);
        }

        return Vector3.Distance(bounds.ClosestPoint(point), point);
    }

    private void RecalculateConnectivity(Vector3 hitPoint, HashSet<int> piecesWithBrokenLinks)
    {
        bool[] visited = new bool[pieces.Count];
        Queue<int> queue = new Queue<int>();

        for (int i = 0; i < pieces.Count; i++)
        {
            if (pieces[i].isSupport && !pieces[i].isDetached)
            {
                if (allowDamagedSupportDetach &&
                    piecesWithBrokenLinks != null &&
                    piecesWithBrokenLinks.Contains(i))
                {
                    continue;
                }

                visited[i] = true;
                queue.Enqueue(i);
            }
        }

        while (queue.Count > 0)
        {
            int current = queue.Dequeue();
            ClusterPiece piece = pieces[current];

            for (int i = 0; i < piece.links.Count; i++)
            {
                PieceLink link = piece.links[i];
                int next = link.otherId;

                if (visited[next]) continue;
                if (link.broken) continue;
                if (pieces[next].isDetached) continue;

                visited[next] = true;
                queue.Enqueue(next);
            }
        }

        List<int> detachedNow = new List<int>();

        for (int i = 0; i < pieces.Count; i++)
        {
            if (pieces[i].isDetached) continue;
            if (visited[i]) continue;

            detachedNow.Add(i);
            pieces[i].isDetached = true;
        }

        ActivateDetachedPieces(detachedNow, hitPoint);
    }

    private void ActivateDetachedPieces(List<int> detachedIds, Vector3 hitPoint)
    {
        List<Collider> actorColliders = CollectActorColliders();

        for (int i = 0; i < detachedIds.Count; i++)
        {
            ClusterPiece piece = pieces[detachedIds[i]];
            piece.ActivatePhysics(actorColliders);

            if (piece.rb == null)
            {
                continue;
            }

            Vector3 dir = piece.transform.position - hitPoint;
            if (dir.sqrMagnitude < 0.0001f)
            {
                dir = Random.onUnitSphere;
            }

            dir.Normalize();
            dir.y += upwardImpulse;
            dir.Normalize();

            piece.rb.AddForce(dir * detachImpulse, ForceMode.Impulse);
            piece.rb.AddTorque(Random.insideUnitSphere * detachImpulse, ForceMode.Impulse);
        }
    }

    private static List<Collider> CollectActorColliders()
    {
        List<Collider> result = new List<Collider>();
        AddActorColliders(Object.FindObjectsByType<MyGame.PlayerEntity>(FindObjectsInactive.Exclude), result);
        AddActorColliders(Object.FindObjectsByType<MyGame.EnemyCPUEntity>(FindObjectsInactive.Exclude), result);
        AddActorColliders(Object.FindObjectsByType<MyGame.EnemyEntity>(FindObjectsInactive.Exclude), result);
        return result;
    }

    private static void AddActorColliders<T>(T[] actors, List<Collider> result) where T : Component
    {
        for (int i = 0; i < actors.Length; i++)
        {
            Collider[] colliders = actors[i].GetComponentsInChildren<Collider>(true);
            result.AddRange(colliders);
        }
    }

    private int CountLinks()
    {
        int total = 0;

        for (int i = 0; i < pieces.Count; i++)
        {
            total += pieces[i].links.Count;
        }

        return total / 2;
    }

    private int CountSupports()
    {
        int total = 0;

        for (int i = 0; i < pieces.Count; i++)
        {
            if (pieces[i].isSupport)
            {
                total++;
            }
        }

        return total;
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawNeighborGizmos) return;

        ClusterPiece[] all = GetComponentsInChildren<ClusterPiece>(true);
        if (all == null) return;

        for (int i = 0; i < all.Length; i++)
        {
            ClusterPiece a = all[i];
            if (a == null) continue;

            for (int n = 0; n < a.links.Count; n++)
            {
                PieceLink link = a.links[n];
                int id = link.otherId;
                if (id < 0 || id >= all.Length) continue;

                ClusterPiece b = all[id];
                if (b == null) continue;
                if (id <= a.pieceId) continue;

                bool linked = !link.broken;

                if (!drawBrokenLinks && !linked) continue;

                float ratio = link.maxHp > 0f ? link.hp / link.maxHp : 0f;
                Gizmos.color = linked ? Color.Lerp(Color.red, Color.cyan, ratio) : Color.red;
                Gizmos.DrawLine(a.transform.position, b.transform.position);
            }

            Gizmos.color = a.isSupport ? Color.green : Color.yellow;
            Gizmos.DrawSphere(a.transform.position, 0.05f);
        }
    }

    private string GetHierarchyPath(Transform target)
    {
        if (target == null) return "(null)";

        string path = target.name;
        Transform current = target.parent;

        while (current != null)
        {
            path = current.name + "/" + path;
            current = current.parent;
        }

        return path;
    }
}
