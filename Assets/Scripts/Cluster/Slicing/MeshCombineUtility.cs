using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public static class MeshCombineUtility
{
    private const string CityModelFolder = "Assets/Versatile Studio Assets/Demo City By Versatile Studio/Models/";

    public static Mesh CombineMeshesToLocal(GameObject sourceRoot, bool logUnreadableMeshes = true)
    {
        if (sourceRoot == null) return null;

        MeshFilter[] meshFilters = sourceRoot.GetComponentsInChildren<MeshFilter>(true);
        if (meshFilters == null || meshFilters.Length == 0) return null;

        List<CombineInstance> combines = new List<CombineInstance>();
        Matrix4x4 rootWorldToLocal = sourceRoot.transform.worldToLocalMatrix;

        for (int i = 0; i < meshFilters.Length; i++)
        {
            MeshFilter mf = meshFilters[i];
            if (mf == null || mf.sharedMesh == null) continue;

            MeshRenderer mr = mf.GetComponent<MeshRenderer>();
            if (mr == null) continue;

            Mesh mesh = mf.sharedMesh;
            if (!mesh.isReadable)
            {
#if UNITY_EDITOR
                Mesh assetMesh = TryCombineReadableCityAsset(sourceRoot);
                if (assetMesh != null)
                {
                    return assetMesh;
                }
#endif

                if (logUnreadableMeshes)
                {
                    Debug.LogWarning($"MeshCombineUtility skipped unreadable mesh '{mesh.name}' on '{mf.name}'. Enable Read/Write to use true mesh fracture.");
                }

                continue;
            }

            int subMeshCount = Mathf.Max(1, mesh.subMeshCount);
            for (int sub = 0; sub < subMeshCount; sub++)
            {
                CombineInstance ci = new CombineInstance();
                ci.mesh = mesh;
                ci.subMeshIndex = sub;

                // sourceRoot ローカル空間へ変換
                ci.transform = rootWorldToLocal * mf.transform.localToWorldMatrix;
                combines.Add(ci);
            }
        }

        if (combines.Count == 0) return null;

        Mesh combined = new Mesh();
        combined.name = sourceRoot.name + "_CombinedForSlice";

        if (NeedUInt32IndexFormat(combines))
        {
            combined.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        }

        // 1つのsubmeshにまとめる
        combined.CombineMeshes(combines.ToArray(), true, true, false);

        if (combined.normals == null || combined.normals.Length == 0)
        {
            combined.RecalculateNormals();
        }

        if (combined.uv == null || combined.uv.Length == 0)
        {
            Vector2[] uvs = new Vector2[combined.vertexCount];
            combined.uv = uvs;
        }

        combined.RecalculateBounds();
        return combined;
    }

#if UNITY_EDITOR
    private static Mesh TryCombineReadableCityAsset(GameObject sourceRoot)
    {
        string assetPath = CityModelFolder + StripCloneSuffix(sourceRoot.name) + ".fbx";
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
        if (prefab == null)
            return null;

        ModelImporter importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
        if (importer != null && !importer.isReadable)
        {
            importer.isReadable = true;
            importer.SaveAndReimport();
        }

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        if (instance == null)
            return null;

        instance.hideFlags = HideFlags.HideAndDontSave;
        Mesh combined = CombineReadableMeshesOnly(instance);
        Object.DestroyImmediate(instance);
        return combined;
    }

    private static Mesh CombineReadableMeshesOnly(GameObject sourceRoot)
    {
        MeshFilter[] meshFilters = sourceRoot.GetComponentsInChildren<MeshFilter>(true);
        if (meshFilters == null || meshFilters.Length == 0) return null;

        List<CombineInstance> combines = new List<CombineInstance>();
        Matrix4x4 rootWorldToLocal = sourceRoot.transform.worldToLocalMatrix;

        for (int i = 0; i < meshFilters.Length; i++)
        {
            MeshFilter mf = meshFilters[i];
            if (mf == null || mf.sharedMesh == null || !mf.sharedMesh.isReadable) continue;

            MeshRenderer mr = mf.GetComponent<MeshRenderer>();
            if (mr == null) continue;

            Mesh mesh = mf.sharedMesh;
            int subMeshCount = Mathf.Max(1, mesh.subMeshCount);
            for (int sub = 0; sub < subMeshCount; sub++)
            {
                combines.Add(new CombineInstance
                {
                    mesh = mesh,
                    subMeshIndex = sub,
                    transform = rootWorldToLocal * mf.transform.localToWorldMatrix
                });
            }
        }

        if (combines.Count == 0) return null;

        Mesh combined = new Mesh();
        combined.name = sourceRoot.name + "_ReadableAssetCombinedForFracture";

        if (NeedUInt32IndexFormat(combines))
        {
            combined.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        }

        combined.CombineMeshes(combines.ToArray(), true, true, false);
        if (combined.normals == null || combined.normals.Length == 0)
        {
            combined.RecalculateNormals();
        }

        if (combined.uv == null || combined.uv.Length == 0)
        {
            combined.uv = new Vector2[combined.vertexCount];
        }

        combined.RecalculateBounds();
        return combined;
    }

    private static string StripCloneSuffix(string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName))
            return string.Empty;

        int suffixIndex = objectName.IndexOf(" (", System.StringComparison.Ordinal);
        if (suffixIndex >= 0)
        {
            objectName = objectName.Substring(0, suffixIndex);
        }

        if (objectName.StartsWith("Intact_", System.StringComparison.Ordinal))
        {
            objectName = objectName.Substring("Intact_".Length);
        }

        if (objectName.StartsWith("DestructibleBuilding_", System.StringComparison.Ordinal))
        {
            objectName = objectName.Substring("DestructibleBuilding_".Length);
        }

        return objectName;
    }
#endif

    private static bool NeedUInt32IndexFormat(List<CombineInstance> combines)
    {
        long totalVertexCount = 0;

        for (int i = 0; i < combines.Count; i++)
        {
            if (combines[i].mesh == null) continue;
            totalVertexCount += combines[i].mesh.vertexCount;
            if (totalVertexCount > 65000)
            {
                return true;
            }
        }

        return false;
    }
}
