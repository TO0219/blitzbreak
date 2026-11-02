using System.Collections.Generic;
using UnityEngine;

namespace MyGame
{
    [System.Serializable]
    public class BattleBuildingDestructionSettings
    {
        public float BuildingHealth = 12f;
        public int PieceCount = 20;
        public float PieceDisableDelay = 5f;
        public float PieceShrinkDuration = 0.65f;
        public bool EnablePieceCollisions = false;
        public float DetachImpulse = 5.5f;
        public float UpwardImpulse = 0.75f;
    }

    public static class BattleBuildingDestructionInstaller
    {
        private static readonly string[] BuildingNamePrefixes =
        {
            "ClusterTower_",
            "Building_",
            "office_building_",
            "factory_building_",
            "factory_chimney",
            "mid_house_",
            "small_house_",
            "basement_"
        };
        private const string VisualOnlyBuildingRootPrefix = "FieldOutsideBuildings";

        public static void Install(
            BattleBuildingDestructionSettings settings,
            Transform excludedBuildingRoot = null)
        {
            settings ??= new BattleBuildingDestructionSettings();

            MeshFilter[] meshFilters = Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Exclude);
            HashSet<GameObject> processed = new HashSet<GameObject>();
            List<GameObject> buildingRoots = new List<GameObject>();
            int seed = 1000;

            for (int i = 0; i < meshFilters.Length; i++)
            {
                MeshFilter meshFilter = meshFilters[i];
                if (meshFilter == null || meshFilter.sharedMesh == null)
                    continue;

                GameObject obj = meshFilter.gameObject;
                if (obj == null)
                    continue;

                if (IsUnderExcludedRoot(obj.transform, excludedBuildingRoot))
                    continue;
                if (IsUnderVisualOnlyBuildingRoot(obj.transform))
                    continue;

                Transform buildingRoot = FindBuildingRoot(meshFilter.transform, excludedBuildingRoot);
                if (buildingRoot == null)
                    continue;

                GameObject rootObject = buildingRoot.gameObject;
                if (processed.Contains(rootObject))
                    continue;

                if (rootObject.GetComponentInParent<DestructibleBuilding>() != null)
                    continue;

                buildingRoots.Add(rootObject);
                processed.Add(rootObject);
            }

            buildingRoots.Sort((a, b) =>
                string.CompareOrdinal(
                    GetHierarchyPath(a.transform),
                    GetHierarchyPath(b.transform)));

            for (int i = 0; i < buildingRoots.Count; i++)
            {
                GameObject rootObject = buildingRoots[i];
                Mesh combinedMesh = global::MeshCombineUtility.CombineMeshesToLocal(rootObject);
                Renderer renderer = rootObject.GetComponentInChildren<Renderer>(true);
                if (combinedMesh == null || combinedMesh.vertexCount == 0)
                {
                    Debug.LogWarning($"Battle building destruction skipped mesh fracture for '{GetHierarchyPath(rootObject.transform)}' because no readable mesh could be combined.");
                }

                WrapBuildingObject(
                    rootObject,
                    combinedMesh,
                    renderer != null ? renderer.sharedMaterial : null,
                    seed++,
                    i,
                    settings);
            }
        }

        private static bool IsUnderExcludedRoot(Transform target, Transform excludedRoot)
        {
            return target != null &&
                   excludedRoot != null &&
                   (target == excludedRoot || target.IsChildOf(excludedRoot));
        }

        private static bool IsUnderVisualOnlyBuildingRoot(Transform target)
        {
            Transform current = target;
            while (current != null)
            {
                if (current.name.StartsWith(
                        VisualOnlyBuildingRootPrefix,
                        System.StringComparison.Ordinal))
                {
                    return true;
                }

                current = current.parent;
            }

            return false;
        }

        private static Transform FindBuildingRoot(Transform target, Transform excludedRoot)
        {
            Transform current = target;
            Transform root = null;
            while (current != null && current != excludedRoot)
            {
                if (IsBuildingObjectName(current.name))
                {
                    root = current;
                }

                current = current.parent;
            }

            return root;
        }

        private static bool IsBuildingObjectName(string objectName)
        {
            for (int i = 0; i < BuildingNamePrefixes.Length; i++)
            {
                if (objectName.StartsWith(BuildingNamePrefixes[i], System.StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        private static void WrapBuildingObject(
            GameObject sourceObject,
            Mesh sourceMesh,
            Material sourceMaterial,
            int randomSeed,
            int networkId,
            BattleBuildingDestructionSettings settings)
        {
            Transform sourceTransform = sourceObject.transform;
            Transform originalParent = sourceTransform.parent;
            int siblingIndex = sourceTransform.GetSiblingIndex();
            Vector3 localPosition = sourceTransform.localPosition;
            Quaternion localRotation = sourceTransform.localRotation;
            Vector3 localScale = sourceTransform.localScale;
            string originalName = sourceObject.name;

            GameObject root = new GameObject("DestructibleBuilding_" + originalName);
            Transform rootTransform = root.transform;
            rootTransform.SetParent(originalParent, false);
            rootTransform.SetSiblingIndex(siblingIndex);
            rootTransform.localPosition = localPosition;
            rootTransform.localRotation = localRotation;
            rootTransform.localScale = localScale;

            sourceTransform.SetParent(rootTransform, false);
            sourceTransform.localPosition = Vector3.zero;
            sourceTransform.localRotation = Quaternion.identity;
            sourceTransform.localScale = Vector3.one;
            sourceObject.name = "Intact_" + originalName;

            EnsureHitCollider(sourceObject, sourceMesh);

            DestructibleBuilding building = root.AddComponent<DestructibleBuilding>();
            building.Initialize(
                settings.BuildingHealth,
                sourceObject,
                sourceMesh,
                sourceMaterial,
                randomSeed,
                settings.PieceCount,
                settings.PieceDisableDelay,
                settings.PieceShrinkDuration,
                settings.EnablePieceCollisions,
                settings.DetachImpulse,
                settings.UpwardImpulse,
                networkId);
        }

        private static string GetHierarchyPath(Transform target)
        {
            if (target == null)
                return string.Empty;

            string path = target.name;
            Transform current = target.parent;
            while (current != null)
            {
                path = current.name + "/" + path;
                current = current.parent;
            }

            return path;
        }

        private static void EnsureHitCollider(GameObject sourceObject, Mesh sourceMesh)
        {
            if (sourceObject.GetComponent<Collider>() != null)
                return;

            if (sourceMesh == null || !sourceMesh.isReadable)
            {
                AddRendererBoundsBoxCollider(sourceObject);
                return;
            }

            MeshCollider meshCollider = sourceObject.AddComponent<MeshCollider>();
            meshCollider.sharedMesh = sourceMesh;
            meshCollider.convex = false;
        }

        private static void AddBoxCollider(GameObject sourceObject, Mesh sourceMesh)
        {
            BoxCollider boxCollider = sourceObject.AddComponent<BoxCollider>();
            if (sourceMesh == null)
                return;

            Bounds bounds = sourceMesh.bounds;
            boxCollider.center = bounds.center;
            boxCollider.size = bounds.size;
        }

        private static void AddRendererBoundsBoxCollider(GameObject sourceObject)
        {
            BoxCollider boxCollider = sourceObject.AddComponent<BoxCollider>();
            Renderer[] renderers = sourceObject.GetComponentsInChildren<Renderer>(true);
            if (renderers == null || renderers.Length == 0)
                return;

            Bounds worldBounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                worldBounds.Encapsulate(renderers[i].bounds);
            }

            Matrix4x4 worldToLocal = sourceObject.transform.worldToLocalMatrix;
            Vector3 localCenter = worldToLocal.MultiplyPoint3x4(worldBounds.center);
            Vector3 localSize = sourceObject.transform.InverseTransformVector(worldBounds.size);
            localSize = new Vector3(
                Mathf.Abs(localSize.x),
                Mathf.Abs(localSize.y),
                Mathf.Abs(localSize.z));

            boxCollider.center = localCenter;
            boxCollider.size = localSize;
        }
    }
}
