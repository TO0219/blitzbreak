using System;
using System.Collections.Generic;
using System.Reflection;
using Unity.Netcode;
using UnityEngine;

namespace MyGame
{
    public enum DestructibleFractureMode
    {
        VoronoiThenSlice,
        SliceOnly
    }

    public class DestructibleBuilding : MonoBehaviour
    {
        private static readonly Dictionary<int, DestructibleBuilding> NetworkBuildings =
            new Dictionary<int, DestructibleBuilding>();

        [SerializeField] private float maxHealth = 12f;
        [SerializeField] private GameObject intactRoot;
        [SerializeField] private GameObject fracturedRoot;
        [SerializeField] private Mesh sourceMesh;
        [SerializeField] private Material pieceMaterial;
        [SerializeField] private int randomSeed;
        [SerializeField] private int sliceCount = 6;
        [SerializeField] private float pieceDisableDelay = 5f;
        [SerializeField] private float pieceShrinkDuration = 0.65f;
        [SerializeField] private bool enablePieceCollisions = false;
        [SerializeField] private float detachImpulse = 5.5f;
        [SerializeField] private float upwardImpulse = 0.75f;
        [SerializeField] private DestructibleFractureMode fractureMode = DestructibleFractureMode.VoronoiThenSlice;
        [SerializeField] private global::ConnectedClusterDestruction cluster;
        [SerializeField] private int networkId = -1;

        private float currentHealth;
        private bool destroyed;
        private bool fractureBuilt;

        public bool IsDestroyed => destroyed;
        public int NetworkId => networkId;

        private void Awake()
        {
            currentHealth = maxHealth;

            if (fracturedRoot != null)
            {
                fracturedRoot.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            if (networkId >= 0 &&
                NetworkBuildings.TryGetValue(networkId, out DestructibleBuilding building) &&
                building == this)
            {
                NetworkBuildings.Remove(networkId);
            }
        }

        public void Initialize(
            float health,
            GameObject intactObject,
            Mesh mesh,
            Material material,
            int seed,
            int generatedSliceCount,
            float disableDelay,
            float shrinkDuration,
            bool generatedEnablePieceCollisions,
            float generatedDetachImpulse,
            float generatedUpwardImpulse,
            int generatedNetworkId,
            DestructibleFractureMode generatedFractureMode = DestructibleFractureMode.VoronoiThenSlice)
        {
            maxHealth = Mathf.Max(1f, health);
            currentHealth = maxHealth;
            intactRoot = intactObject;
            sourceMesh = mesh;
            pieceMaterial = material;
            randomSeed = seed;
            sliceCount = Mathf.Max(2, generatedSliceCount);
            pieceDisableDelay = Mathf.Max(0.1f, disableDelay);
            pieceShrinkDuration = Mathf.Max(0.01f, shrinkDuration);
            enablePieceCollisions = generatedEnablePieceCollisions;
            detachImpulse = Mathf.Max(0f, generatedDetachImpulse);
            upwardImpulse = Mathf.Max(0f, generatedUpwardImpulse);
            fractureMode = generatedFractureMode;
            networkId = generatedNetworkId;
            destroyed = false;
            fractureBuilt = false;
            NetworkBuildings[networkId] = this;

            if (fracturedRoot != null)
            {
                fracturedRoot.SetActive(false);
            }
        }

        public void Damage(float damage, Vector3 hitPoint)
        {
            DamageInternal(
                damage,
                hitPoint,
                global::AudioCue.BuildingCollapseA,
                false);
        }

        public void DamageByPlayerImpact(float damage, Vector3 hitPoint)
        {
            DamageInternal(
                damage,
                hitPoint,
                global::AudioCue.BuildingCollapseB,
                true);
        }

        private void DamageInternal(
            float damage,
            Vector3 hitPoint,
            global::AudioCue breakSound,
            bool isPlayerImpact)
        {
            if (destroyed)
                return;

            if (NetworkManager.Singleton != null &&
                NetworkManager.Singleton.IsListening &&
                !NetworkManager.Singleton.IsServer)
            {
                return;
            }

            currentHealth -= Mathf.Max(0f, damage);
            if (currentHealth > 0f)
                return;

            destroyed = true;
            BreakAt(hitPoint, breakSound);

            if (NetworkManager.Singleton != null &&
                NetworkManager.Singleton.IsListening &&
                NetworkManager.Singleton.IsServer &&
                NetworkBattleManager.Instance != null)
            {
                NetworkBattleManager.Instance.BroadcastBuildingDestroyed(
                    networkId,
                    hitPoint,
                    isPlayerImpact);
            }
        }

        public static bool TryBreakFromNetwork(int id, Vector3 hitPoint)
        {
            if (!NetworkBuildings.TryGetValue(id, out DestructibleBuilding building) ||
                building == null)
            {
                return false;
            }

            building.BreakFromNetwork(hitPoint);
            return true;
        }

        public static bool TryBreakFromNetwork(
            int id,
            Vector3 hitPoint,
            bool isPlayerImpact)
        {
            if (!NetworkBuildings.TryGetValue(id, out DestructibleBuilding building) ||
                building == null)
            {
                return false;
            }

            building.BreakFromNetwork(hitPoint, isPlayerImpact);
            return true;
        }

        public static bool TryDamageFromNetwork(
            int id,
            float damage,
            Vector3 hitPoint)
        {
            if (!NetworkBuildings.TryGetValue(id, out DestructibleBuilding building) ||
                building == null)
            {
                return false;
            }

            building.Damage(damage, hitPoint);
            return true;
        }

        public static bool TryDamageByPlayerImpactFromNetwork(
            int id,
            float damage,
            Vector3 hitPoint)
        {
            if (!NetworkBuildings.TryGetValue(id, out DestructibleBuilding building) ||
                building == null)
            {
                return false;
            }

            building.DamageByPlayerImpact(damage, hitPoint);
            return true;
        }

        private void BreakFromNetwork(Vector3 hitPoint)
        {
            BreakFromNetwork(hitPoint, false);
        }

        private void BreakFromNetwork(Vector3 hitPoint, bool isPlayerImpact)
        {
            if (destroyed)
                return;

            destroyed = true;
            currentHealth = 0f;
            BreakAt(
                hitPoint,
                isPlayerImpact
                    ? global::AudioCue.BuildingCollapseB
                    : global::AudioCue.BuildingCollapseA);
        }

        private void BreakAt(Vector3 hitPoint, global::AudioCue breakSound)
        {
            BuildFractureIfNeeded();
            global::AudioManager.Instance?.PlayCueAtPosition(breakSound, hitPoint);

            if (intactRoot != null)
            {
                intactRoot.SetActive(false);
            }

            if (fracturedRoot != null)
            {
                fracturedRoot.SetActive(true);
            }

            if (cluster != null)
            {
                cluster.BuildCluster();
                cluster.BreakAll(hitPoint);
                return;
            }

            Destroy(gameObject);
        }

        private void BuildFractureIfNeeded()
        {
            if (fractureBuilt)
                return;

            fractureBuilt = true;

            Mesh mesh = ResolveSourceMesh();
            if (mesh == null)
                return;

            if (fracturedRoot == null)
            {
                fracturedRoot = new GameObject("FracturedRoot");
                fracturedRoot.transform.SetParent(transform, false);
            }

            fracturedRoot.SetActive(false);

            Material material = ResolvePieceMaterial();
            List<GameObject> pieceObjects = fractureMode == DestructibleFractureMode.SliceOnly
                ? CreateSlicePieceObjects(mesh, material)
                : CreateVoronoiPieceObjects(mesh, material);

            if (fractureMode != DestructibleFractureMode.SliceOnly &&
                pieceObjects.Count < 2)
            {
                ClearFracturedRootChildren();
                Debug.LogWarning($"DestructibleBuilding true mesh voronoi produced {pieceObjects.Count} piece(s) on {name}. Falling back to true mesh slice, not boxel.");
                pieceObjects = CreateSlicePieceObjects(mesh, material);
            }

            if (pieceObjects.Count < 2)
            {
                ClearFracturedRootChildren();
                Debug.LogWarning($"DestructibleBuilding could not generate mesh fracture pieces on {name}. Boxel fallback is disabled.");
                return;
            }

            for (int i = 0; i < pieceObjects.Count; i++)
            {
                ConfigurePiece(pieceObjects[i]);
            }

            cluster = fracturedRoot.GetComponent<global::ConnectedClusterDestruction>();
            if (cluster == null)
            {
                cluster = fracturedRoot.AddComponent<global::ConnectedClusterDestruction>();
            }

            ConfigureCluster(cluster);
        }

        private Mesh ResolveSourceMesh()
        {
            if (sourceMesh != null && sourceMesh.isReadable)
                return sourceMesh;

            GameObject combineRoot = intactRoot != null ? intactRoot : gameObject;
            Mesh combinedMesh = global::MeshCombineUtility.CombineMeshesToLocal(combineRoot);
            if (combinedMesh != null && combinedMesh.vertexCount > 0)
            {
                sourceMesh = combinedMesh;
                return sourceMesh;
            }

            MeshFilter meshFilter = intactRoot != null
                ? intactRoot.GetComponentInChildren<MeshFilter>(true)
                : GetComponentInChildren<MeshFilter>(true);

            sourceMesh = meshFilter != null && meshFilter.sharedMesh != null && meshFilter.sharedMesh.isReadable
                ? meshFilter.sharedMesh
                : null;
            return sourceMesh;
        }

        private Material ResolvePieceMaterial()
        {
            if (pieceMaterial != null)
                return pieceMaterial;

            Renderer renderer = intactRoot != null
                ? intactRoot.GetComponentInChildren<Renderer>(true)
                : GetComponentInChildren<Renderer>(true);

            pieceMaterial = renderer != null ? renderer.sharedMaterial : null;
            return pieceMaterial;
        }

        private List<GameObject> CreateVoronoiPieceObjects(Mesh mesh, Material material)
        {
            List<GameObject> pieceObjects = new List<GameObject>();
            if (mesh == null || !mesh.isReadable)
            {
                return pieceObjects;
            }

            VoronoiFractureSettings settings = CreateVoronoiFractureSettings();

            try
            {
                List<SliceFracturePiece> pieces = global::VoronoiFractureGenerator.Generate(mesh, settings);
                if (pieces == null)
                    return pieceObjects;

                for (int i = 0; i < pieces.Count; i++)
                {
                    GameObject pieceObject = global::MeshPieceObjectFactory.CreatePieceObject(
                        fracturedRoot.transform,
                        pieces[i].mesh,
                        i,
                        material,
                        settings.colliderMode,
                        settings.autoMeshColliderMaxTriangles,
                        settings.autoMeshColliderMinAxisSize,
                        settings.autoMeshColliderMinVolume,
                        false);

                    if (pieceObject != null)
                    {
                        pieceObjects.Add(pieceObject);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"DestructibleBuilding true mesh voronoi failed on {name}. Boxel fallback is disabled. {ex.Message}");
            }

            return pieceObjects;
        }

        private List<GameObject> CreateSlicePieceObjects(Mesh mesh, Material material)
        {
            List<GameObject> pieceObjects = new List<GameObject>();
            if (mesh == null || !mesh.isReadable)
            {
                return pieceObjects;
            }

            SliceFractureSettings settings = CreateSliceFractureSettings();

            try
            {
                List<SliceFracturePiece> pieces = global::SliceFractureGenerator.Generate(mesh, settings);
                if (pieces == null)
                    return pieceObjects;

                for (int i = 0; i < pieces.Count; i++)
                {
                    GameObject pieceObject = global::MeshPieceObjectFactory.CreatePieceObject(
                        fracturedRoot.transform,
                        pieces[i].mesh,
                        i,
                        material,
                        settings.colliderMode,
                        settings.autoMeshColliderMaxTriangles,
                        settings.autoMeshColliderMinAxisSize,
                        settings.autoMeshColliderMinVolume,
                        false);

                    if (pieceObject != null)
                    {
                        pieceObjects.Add(pieceObject);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"DestructibleBuilding true mesh slice failed on {name}. Boxel fallback is disabled. {ex.Message}");
            }

            return pieceObjects;
        }

        private void ConfigurePiece(GameObject pieceObject)
        {
            if (pieceObject == null)
                return;

            global::ClusterPiece piece = pieceObject.GetComponent<global::ClusterPiece>();
            if (piece == null)
                return;

            piece.autoDisable = true;
            piece.disableAfterSeconds = pieceDisableDelay;
            piece.shrinkDuration = pieceShrinkDuration;
            piece.enableCollisionWhenDetached = enablePieceCollisions;
        }

        private void ClearFracturedRootChildren()
        {
            if (fracturedRoot == null)
                return;

            for (int i = fracturedRoot.transform.childCount - 1; i >= 0; i--)
            {
                Destroy(fracturedRoot.transform.GetChild(i).gameObject);
            }
        }

        private VoronoiFractureSettings CreateVoronoiFractureSettings()
        {
            return new VoronoiFractureSettings
            {
                qualityPreset = FractureQualityPreset.Custom,
                seedCount = Mathf.Max(20, sliceCount),
                randomSeed = randomSeed,
                minPieceVolume = 0.0001f,
                minAxisSize = 0.03f,
                minAxisRatio = 0f,
                maxAspectRatio = 0f,
                colliderMode = FracturePieceColliderMode.Auto,
                autoMeshColliderMaxTriangles = 128,
                autoMeshColliderMinAxisSize = 0.25f,
                autoMeshColliderMinVolume = 0.02f
            };
        }

        private SliceFractureSettings CreateSliceFractureSettings()
        {
            return new SliceFractureSettings
            {
                qualityPreset = FractureQualityPreset.Custom,
                sliceCount = Mathf.Max(2, sliceCount - 1),
                randomSeed = randomSeed,
                angleJitter = 55f,
                axisBias = Vector3.up,
                minPieceVolume = 0.0001f,
                minAxisSize = 0.03f,
                minAxisRatio = 0f,
                maxAspectRatio = 0f,
                maxRetryPerSlice = 32,
                mergeSmallPieces = true,
                maxSmallPieceMergePasses = 48,
                colliderMode = FracturePieceColliderMode.Auto,
                autoMeshColliderMaxTriangles = 128,
                autoMeshColliderMinAxisSize = 0.25f,
                autoMeshColliderMinVolume = 0.02f
            };
        }

        private void ConfigureCluster(global::ConnectedClusterDestruction targetCluster)
        {
            SetClusterField(targetCluster, "autoCollectChildren", true);
            SetClusterField(targetCluster, "autoMarkLowestAsSupport", true);
            SetClusterField(targetCluster, "autoTuneParameters", true);
            SetClusterField(targetCluster, "detachImpulse", detachImpulse);
            SetClusterField(targetCluster, "upwardImpulse", upwardImpulse);
            SetClusterField(targetCluster, "allowDamagedSupportDetach", true);
            SetClusterField(targetCluster, "drawNeighborGizmos", false);
            SetClusterField(targetCluster, "drawBrokenLinks", false);
            SetClusterField(targetCluster, "logDebugInfo", false);
        }

        private static void SetClusterField<T>(global::ConnectedClusterDestruction targetCluster, string fieldName, T value)
        {
            FieldInfo field = typeof(global::ConnectedClusterDestruction)
                .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);

            if (field != null)
            {
                field.SetValue(targetCluster, value);
            }
        }
    }
}
