using UnityEngine;

public static class MeshPieceObjectFactory
{
    public static GameObject CreatePieceObject(
        Transform parent,
        Mesh sourceMesh,
        int pieceId,
        Material material,
        bool createConvexMeshCollider = true,
        bool activatePhysicsImmediately = false)
    {
        FracturePieceColliderMode colliderMode = createConvexMeshCollider
            ? FracturePieceColliderMode.ConvexMesh
            : FracturePieceColliderMode.Box;

        return CreatePieceObject(
            parent,
            sourceMesh,
            pieceId,
            material,
            colliderMode,
            128,
            0.35f,
            0.04f,
            activatePhysicsImmediately);
    }

    public static GameObject CreatePieceObject(
        Transform parent,
        Mesh sourceMesh,
        int pieceId,
        Material material,
        FracturePieceColliderMode colliderMode,
        int autoMeshColliderMaxTriangles,
        float autoMeshColliderMinAxisSize,
        float autoMeshColliderMinVolume,
        bool activatePhysicsImmediately = false)
    {
        if (sourceMesh == null)
        {
            return null;
        }

        Mesh mesh = Object.Instantiate(sourceMesh);
        mesh.RecalculateBounds();

        Bounds bounds = mesh.bounds;
        Vector3 pivot = bounds.center;

        Vector3[] vertices = mesh.vertices;
        for (int i = 0; i < vertices.Length; i++)
        {
            vertices[i] -= pivot;
        }

        mesh.vertices = vertices;
        mesh.RecalculateBounds();
        mesh.RecalculateNormals();

        GameObject go = new GameObject("Piece_" + pieceId);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pivot;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;

        MeshFilter mf = go.AddComponent<MeshFilter>();
        mf.sharedMesh = mesh;

        MeshRenderer mr = go.AddComponent<MeshRenderer>();
        if (material != null)
        {
            mr.sharedMaterial = material;
        }

        if (ShouldUseConvexMeshCollider(
            mesh,
            colliderMode,
            autoMeshColliderMaxTriangles,
            autoMeshColliderMinAxisSize,
            autoMeshColliderMinVolume))
        {
            MeshCollider mc = go.AddComponent<MeshCollider>();
            mc.sharedMesh = mesh;
            mc.convex = true;
        }
        else
        {
            BoxCollider bc = go.AddComponent<BoxCollider>();
            Bounds b = mesh.bounds;
            bc.center = b.center;
            bc.size = b.size;
        }

        Rigidbody rb = go.AddComponent<Rigidbody>();

        if (activatePhysicsImmediately)
        {
            rb.isKinematic = false;
            rb.useGravity = true;
        }
        else
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        ClusterPiece piece = go.AddComponent<ClusterPiece>();
        piece.CaptureInitialTransform();

        return go;
    }

    private static bool ShouldUseConvexMeshCollider(
        Mesh mesh,
        FracturePieceColliderMode colliderMode,
        int autoMeshColliderMaxTriangles,
        float autoMeshColliderMinAxisSize,
        float autoMeshColliderMinVolume)
    {
        if (colliderMode == FracturePieceColliderMode.Box)
        {
            return false;
        }

        if (colliderMode == FracturePieceColliderMode.ConvexMesh)
        {
            return true;
        }

        Bounds bounds = mesh.bounds;
        Vector3 size = bounds.size;
        float maxAxis = Mathf.Max(size.x, Mathf.Max(size.y, size.z));
        float volume = size.x * size.y * size.z;
        int triangleCount = mesh.triangles.Length / 3;

        if (triangleCount > Mathf.Max(0, autoMeshColliderMaxTriangles))
        {
            return false;
        }

        if (maxAxis < autoMeshColliderMinAxisSize)
        {
            return false;
        }

        if (volume < autoMeshColliderMinVolume)
        {
            return false;
        }

        return true;
    }
}
