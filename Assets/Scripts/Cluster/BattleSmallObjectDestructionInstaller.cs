using System.Collections.Generic;
using UnityEngine;

namespace MyGame
{
    [System.Serializable]
    public class BattleSmallObjectDestructionSettings
    {
        public float Health = 1f;
        public int PieceCount = 6;
        public float PieceDisableDelay = 3f;
        public float PieceShrinkDuration = 0.45f;
        public bool EnablePieceCollisions = true;
        public float DetachImpulse = 4.5f;
        public float UpwardImpulse = 0.8f;
        public float ContactDamage = 999f;
        public float MinImpactSpeed = 0.4f;
        public int NetworkIdOffset = 10000;
    }

    public static class BattleSmallObjectDestructionInstaller
    {
        public static void Install(
            Transform smallObjectRoot,
            BattleSmallObjectDestructionSettings settings)
        {
            if (smallObjectRoot == null)
                return;

            settings ??= new BattleSmallObjectDestructionSettings();

            List<GameObject> targets = CollectDirectChildTargets(smallObjectRoot);
            targets.Sort((a, b) =>
                string.CompareOrdinal(
                    GetHierarchyPath(a.transform),
                    GetHierarchyPath(b.transform)));

            int seed = 7000;
            for (int i = 0; i < targets.Count; i++)
            {
                GameObject target = targets[i];
                if (target == null ||
                    target.GetComponentInParent<DestructibleBuilding>() != null)
                {
                    continue;
                }

                Mesh combinedMesh = global::MeshCombineUtility.CombineMeshesToLocal(target, false);
                if (combinedMesh == null || combinedMesh.vertexCount == 0)
                {
                    Debug.LogWarning($"Small object destruction skipped '{GetHierarchyPath(target.transform)}' because no readable mesh could be combined. Enable Read/Write to use slice destruction.");
                    continue;
                }

                Renderer renderer = target.GetComponentInChildren<Renderer>(true);
                WrapSmallObject(
                    target,
                    combinedMesh,
                    renderer != null ? renderer.sharedMaterial : null,
                    seed++,
                    settings.NetworkIdOffset + i,
                    settings);
            }
        }

        private static List<GameObject> CollectDirectChildTargets(Transform root)
        {
            List<GameObject> targets = new List<GameObject>();
            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                if (child == null ||
                    !child.gameObject.activeInHierarchy ||
                    child.GetComponentInChildren<Renderer>(true) == null)
                {
                    continue;
                }

                targets.Add(child.gameObject);
            }

            return targets;
        }

        private static void WrapSmallObject(
            GameObject sourceObject,
            Mesh sourceMesh,
            Material sourceMaterial,
            int randomSeed,
            int networkId,
            BattleSmallObjectDestructionSettings settings)
        {
            Transform sourceTransform = sourceObject.transform;
            Transform originalParent = sourceTransform.parent;
            int siblingIndex = sourceTransform.GetSiblingIndex();
            Vector3 localPosition = sourceTransform.localPosition;
            Quaternion localRotation = sourceTransform.localRotation;
            Vector3 localScale = sourceTransform.localScale;
            string originalName = sourceObject.name;

            GameObject root = new GameObject("DestructibleSmallObject_" + originalName);
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

            DestructibleBuilding destructible = root.AddComponent<DestructibleBuilding>();
            destructible.Initialize(
                settings.Health,
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
                networkId,
                DestructibleFractureMode.SliceOnly);

            PlayerImpactDestructibleTrigger trigger =
                sourceObject.GetComponent<PlayerImpactDestructibleTrigger>();
            if (trigger == null)
            {
                trigger = sourceObject.AddComponent<PlayerImpactDestructibleTrigger>();
            }

            trigger.Initialize(
                destructible,
                settings.ContactDamage,
                settings.MinImpactSpeed);
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
    }
}
