using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace MyGame
{
    public class EnemyEntity : Entity
    {
        [SerializeField] private bool isInfight = false;
        [SerializeField] private bool isBoss = false;
        [SerializeField] private bool isAir = false;

        [SerializeField] private int maxHp = 1;

        [Header("Defeat Rewards")]
        [SerializeField, Tooltip("白色の通常敵を倒したときに獲得するポイント")]
        private int normalEnemyPointValue = 150;
        [SerializeField, Tooltip("色付きの通常敵を倒したときに獲得するポイント")]
        private int pointValue = 100;
        [SerializeField] private float gaugeValue = 20f;

        [Header("Defeat Recovery")]
        [SerializeField] private float normalDefeatHeal = 10f;
        [SerializeField] private float recoveryEnemyDefeatHeal = 100f;

        [SerializeField] private float moveSpeed = 2f;
        [SerializeField] private float rotateSpeed = 5f;
        [SerializeField] private float chaseRange = 20f;
        [SerializeField] private float attackRange = 5f;
        [SerializeField] private float infightRange = 2;
        [SerializeField] private float fireInterval = 1f;
        [SerializeField] private bool enableWander = true;
        [SerializeField] private float wanderRadius = 8f;
        [SerializeField] private float wanderInterval = 6f;
        [SerializeField, Min(0f)] private float wanderWaitTime = 2f;
        [SerializeField, Min(1)] private int wanderDestinationSearchAttempts = 16;
        [SerializeField, Min(0.1f)] private float wanderStuckResetTime = 1.5f;
        [SerializeField, Min(0.001f)] private float wanderStuckMoveThreshold = 0.05f;

        [Header("Enemy Type Prefabs")]
        [SerializeField, Tooltip("地上・近距離タイプ用。モデルとColliderを含むPrefab")]
        private GameObject groundInfightPrefab;
        [SerializeField, Tooltip("地上・遠距離タイプ用。モデルとColliderを含むPrefab")]
        private GameObject groundShooterPrefab;
        [SerializeField, Tooltip("飛行・近距離タイプ用。モデルとColliderを含むPrefab")]
        private GameObject airInfightPrefab;
        [SerializeField, Tooltip("飛行・遠距離タイプ用。モデルとColliderを含むPrefab")]
        private GameObject airShooterPrefab;
        [SerializeField] private GameObject airInfightSelfDetonationEffectPrefab;
        [SerializeField, Tooltip("タイプ別Prefabの生成先。未設定ならEnemy直下")]
        private Transform appearanceRoot;

        [SerializeField] private GameObject[] buffItemPrefabs;
        [SerializeField] private GameObject turretItemPrefab;
        [SerializeField] private GameObject EnemyBullet;
        [SerializeField] private Transform enemyMuzzleTransform;

        [Header("Drop Visual")]
        [SerializeField] private Color buffDropEnemyColor = new Color(1f, 0.65f, 0.1f, 1f);
        [SerializeField] private Color turretDropEnemyColor = new Color(0.15f, 0.75f, 1f, 1f);
        [SerializeField] private Color recoveryEnemyColor = new Color(0.2f, 1f, 0.35f, 1f);

        [Header("Drop Scatter")]
        [SerializeField] private float dropSpawnHeight = 0.9f;
        [SerializeField] private float dropGroundProbeHeight = 2f;
        [SerializeField] private float dropGroundProbeDistance = 150f;
        [SerializeField] private float dropMaximumGroundY = 2f;
        [SerializeField] private float dropScatterMinSpeed = 2.2f;
        [SerializeField] private float dropScatterMaxSpeed = 4.0f;
        [SerializeField] private float dropScatterUpSpeed = 4.5f;

        [Header("Death Destruction")]
        [SerializeField] private bool enableDeathDestruction = true;
        [SerializeField, Min(2)] private int deathDestructionPieceCount = 4;
        [SerializeField, Min(0.1f)] private float deathPieceDisableDelay = 1.1f;
        [SerializeField, Min(0.01f)] private float deathPieceShrinkDuration = 0.25f;
        [SerializeField, Min(0f)] private float deathPieceDetachImpulse = 4.2f;
        [SerializeField, Min(0f)] private float deathPieceUpwardImpulse = 0.7f;

        private float m_currentHp;
        private bool m_setup = false;
        private EnemySpawnerEntity m_spawner;
        private int m_dropScatterIndex;
        private bool m_willDropBuffItem;
        private bool m_willDropTurretItem;
        private bool m_isDead;
        private bool m_suppressNextItemDrop;
        private DropType m_dropType;
        private NetworkEnemyController m_networkController;
        private bool m_canSimulate = true;
        private GameObject m_activeAppearance;
        private Transform m_activeMuzzleTransform;
        private EnemyMoveAnimationController m_moveAnimationController;
        private readonly RaycastHit[] m_dropGroundHits = new RaycastHit[32];
        private static Material s_buffDropMaterial;
        private static Material s_turretDropMaterial;
        private static Material s_recoveryDropMaterial;
        private bool m_deathDestructionSpawned;
        private bool m_hasDeathDropPosition;
        private Vector3 m_deathDropPosition;

        private enum DropType
        {
            None,
            Buff,
            Turret,
            Recovery
        }

        public bool IsAir => isAir;
        public bool IsInfight => isInfight;
        public bool CanSimulate => m_canSimulate;
        public int DropTypeIndex => (int)m_dropType;
        public float CurrentHealth => m_currentHp;
        public float MoveSpeed => moveSpeed;
        public float RotateSpeed => rotateSpeed;
        public float ChaseRange => chaseRange;
        public float AttackRange => attackRange;
        public float InfightRange => infightRange;
        public float FireInterval => fireInterval;
        public bool EnableWander => enableWander;
        public float WanderRadius => wanderRadius;
        public float WanderInterval => wanderInterval;
        public float WanderWaitTime => wanderWaitTime;
        public int WanderDestinationSearchAttempts => wanderDestinationSearchAttempts;
        public float WanderStuckResetTime => wanderStuckResetTime;
        public float WanderStuckMoveThreshold => wanderStuckMoveThreshold;
        public GameObject EnemyBulletPrefab => EnemyBullet;
        public GameObject AirInfightSelfDetonationEffectPrefab =>
            airInfightSelfDetonationEffectPrefab;
        public Transform EnemyMuzzleTransform =>
            m_activeMuzzleTransform != null
                ? m_activeMuzzleTransform
                : enemyMuzzleTransform;

        private void Awake()
        {
            m_currentHp = maxHp;
        }

        public void Configure(bool air, bool infight)
        {
            Configure(air, infight, -1);
        }

        private void Configure(bool air, bool infight, int dropType)
        {
            isAir = air;
            isInfight = infight;
            m_currentHp = maxHp;
            m_isDead = false;
            m_deathDestructionSpawned = false;
            m_hasDeathDropPosition = false;
            m_suppressNextItemDrop = false;

            if (ShouldForceNormalDropType())
                SetDropType((int)DropType.None);
            else if (dropType >= 0)
                SetDropType(dropType);
            else
                PrepareItemDrops();
            SetupModel();
            ApplyDropTypeColor();
            SetupPhysics();
            Setup();
        }

        private void Setup()
        {
            if (m_setup)
                return;

            m_setup = true;

            if (isBoss)
            {
                gameObject.AddComponent<BossEnemyEntity>();
            }
            else if (isInfight)
            {
                if (isAir)
                {
                    gameObject.AddComponent<AirInfightEnemyEntity>();
                }
                else
                {
                    gameObject.AddComponent<GroundInfightEnemyEntity>();
                }
            }
            else
            {
                if (isAir)
                {
                    gameObject.AddComponent<AirShooterEnemyEntity>();
                }
                else
                {
                    gameObject.AddComponent<GroundShooterEnemyEntity>();
                }
            }
        }

        private void SetupModel()
        {
            if (m_activeAppearance != null)
            {
                m_activeAppearance.SetActive(false);
                Destroy(m_activeAppearance);
                m_activeAppearance = null;
                m_activeMuzzleTransform = null;
                m_moveAnimationController = null;
            }

            GameObject selectedPrefab = GetPrefabForCurrentType();
            if (selectedPrefab == null)
                return;

            Transform parent = appearanceRoot != null
                ? appearanceRoot
                : transform;
            m_activeAppearance = Instantiate(
                selectedPrefab,
                parent,
                false);
            m_activeAppearance.name = selectedPrefab.name;
            m_activeMuzzleTransform =
                FindChildByName(m_activeAppearance.transform, "enemyMuzzleTransform");
            m_moveAnimationController =
                m_activeAppearance.GetComponentInChildren<EnemyMoveAnimationController>(true);
        }

        public void PlayAttackAnimation()
        {
            if (m_moveAnimationController == null && m_activeAppearance != null)
            {
                m_moveAnimationController =
                    m_activeAppearance.GetComponentInChildren<EnemyMoveAnimationController>(true);
            }

            m_moveAnimationController?.PlayAttack();
        }

        private static Transform FindChildByName(Transform root, string childName)
        {
            if (root == null)
                return null;

            if (root.name == childName)
                return root;

            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindChildByName(root.GetChild(i), childName);
                if (found != null)
                    return found;
            }

            return null;
        }

        private GameObject GetPrefabForCurrentType()
        {
            if (isAir)
                return isInfight ? airInfightPrefab : airShooterPrefab;

            return isInfight ? groundInfightPrefab : groundShooterPrefab;
        }

        private void SetupPhysics()
        {
            Rigidbody rb = GetComponent<Rigidbody>();

            if (rb == null)
                return;

            if (!m_canSimulate)
            {
                StopReplicaPhysics(rb);
                return;
            }

            if (isAir)
            {
                rb.useGravity = false;
                rb.isKinematic = true;
            }
            else
            {
                rb.useGravity = true;
                rb.isKinematic = false;
            }
        }

        public void Damage(float value, PlayerEntity attacker)
        {
            if (!m_canSimulate || m_isDead || value <= 0f)
                return;

            m_currentHp -= value;
            SyncNetworkState();

            if (m_currentHp <= 0)
            {
                Die(attacker);
            }
        }

        public void Damage(float value, EnemyCPUEntity attacker)
        {
            if (!m_canSimulate || m_isDead || value <= 0f)
                return;

            m_currentHp -= value;
            SyncNetworkState();

            if (m_currentHp <= 0)
            {
                Die(attacker);
            }
        }

        //一時的に同期無しのDamage関数を追加
        public void Damage(float value)
        {
            if (!m_canSimulate || m_isDead || value <= 0f)
                return;

            m_currentHp -= value;
            SyncNetworkState();

            if (m_currentHp <= 0)
            {
                CaptureDeathDropPosition();
                PlayDeathSound();
                m_isDead = true;

                if (m_spawner != null)
                    m_spawner.NotifyEnemyDead();

                TrySpawnQueuedDropItems();
                DestroyAfterDeath();
            }
        }

        private void Die(PlayerEntity attacker)
        {
            if (m_isDead)
                return;

            CaptureDeathDropPosition();
            m_isDead = true;
            PlayDeathSound();

            if (attacker != null)
            {
                attacker.AddScore(GetDefeatPointValue());
                attacker.AddAirGauge(gaugeValue);
                attacker.RecoverHealth(GetDefeatHealAmount());
                attacker.RegisterDefeat();
            }

            if (m_spawner != null)
            {
                m_spawner.NotifyEnemyDead();
            }

            TrySpawnQueuedDropItems();
            DestroyAfterDeath();
        }

        private void Die(EnemyCPUEntity attacker)
        {
            if (m_isDead)
                return;

            CaptureDeathDropPosition();
            m_isDead = true;
            PlayDeathSound();

            if (attacker != null)
            {
                attacker.AddScore(GetDefeatPointValue());
                attacker.RecoverHealth(GetDefeatHealAmount());
                attacker.RegisterDefeat();
            }

            if (m_spawner != null)
            {
                m_spawner.NotifyEnemyDead();
            }

            TrySpawnQueuedDropItems();
            DestroyAfterDeath();
        }

        private void PlayDeathSound()
        {
            global::AudioManager.Instance?.PlayCueAtPosition(
                global::AudioCue.EnemyDeath,
                transform.position);
        }

        public void SuppressNextItemDrop()
        {
            m_suppressNextItemDrop = true;
        }

        public void SetNetworkController(NetworkEnemyController controller)
        {
            m_networkController = controller;
        }

        public void ConfigureNetworkReplica(bool isServer)
        {
            m_canSimulate = isServer;

            if (!isServer)
            {
                Rigidbody rb = GetComponent<Rigidbody>();
                if (rb != null)
                    StopReplicaPhysics(rb);
            }
        }

        private static void StopReplicaPhysics(Rigidbody rb)
        {
            if (rb == null)
                return;

            if (!rb.isKinematic)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            rb.useGravity = false;
            rb.isKinematic = true;
        }

        public void ApplyNetworkState(NetworkEnemyState state)
        {
            if (!m_setup)
                Configure(state.IsAir, state.IsInfight, state.DropType);

            m_currentHp = state.Health;
        }

        private void SyncNetworkState()
        {
            m_networkController?.SyncFromServer();
        }

        private void DestroyAfterDeath()
        {
            PlayDeathDestructionEffect();

            if (m_networkController != null && m_networkController.IsSpawned)
            {
                m_networkController.DespawnFromServer();
                return;
            }

            OnDestroyEntity();
        }

        public void PlayDeathDestructionEffect()
        {
            if (m_deathDestructionSpawned || !enableDeathDestruction)
                return;

            m_deathDestructionSpawned = true;

            if (m_activeAppearance == null)
                return;

            Mesh sourceMesh = BuildDeathSourceMesh(m_activeAppearance);
            List<global::SliceFracturePiece> pieces = null;
            if (sourceMesh != null)
            {
                try
                {
                    pieces = global::SliceFractureGenerator.Generate(
                        sourceMesh,
                        CreateDeathSliceSettings());
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"Enemy death destruction failed on {name}: {ex.Message}");
                }

                Destroy(sourceMesh);
            }

            Transform sourceTransform = m_activeAppearance.transform;
            GameObject effectRoot = new GameObject("EnemyDeathDestruction");
            effectRoot.transform.SetPositionAndRotation(
                sourceTransform.position,
                sourceTransform.rotation);
            effectRoot.transform.localScale = sourceTransform.lossyScale;

            Material pieceMaterial = ResolveDeathPieceMaterial();
            List<Collider> ignoredColliders = new List<Collider>();
            ignoredColliders.AddRange(GetComponentsInChildren<Collider>(true));

            Vector3 explosionCenter = GetDeathDestructionCenter();
            Bounds sizeLimitBounds = GetDeathColliderBounds();
            int requestedPieceCount = Mathf.Max(2, deathDestructionPieceCount);
            if (pieces != null && pieces.Count >= requestedPieceCount)
            {
                int maxPieces = Mathf.Min(pieces.Count, requestedPieceCount);
                for (int i = 0; i < maxPieces; i++)
                {
                    GameObject pieceObject = global::MeshPieceObjectFactory.CreatePieceObject(
                        effectRoot.transform,
                        pieces[i].mesh,
                        i,
                        pieceMaterial,
                        global::FracturePieceColliderMode.Box,
                        64,
                        0.15f,
                        0.001f,
                        false);

                    ConstrainDeathPieceSize(pieceObject, explosionCenter, sizeLimitBounds);
                    ConfigureDeathPiece(pieceObject, explosionCenter, ignoredColliders);
                }
            }
            else
            {
                CreateFallbackDeathPieces(
                    effectRoot.transform,
                    pieceMaterial,
                    explosionCenter,
                    sizeLimitBounds,
                    ignoredColliders);
            }

            Destroy(
                effectRoot,
                deathPieceDisableDelay + deathPieceShrinkDuration + 0.4f);
        }

        private Mesh BuildDeathSourceMesh(GameObject sourceRoot)
        {
            Mesh staticMesh = global::MeshCombineUtility.CombineMeshesToLocal(sourceRoot, false);
            Mesh skinnedMesh = CombineSkinnedMeshesToLocal(sourceRoot);

            if (staticMesh == null)
                return skinnedMesh;

            if (skinnedMesh == null)
                return staticMesh;

            CombineInstance[] combines =
            {
                new CombineInstance
                {
                    mesh = staticMesh,
                    transform = Matrix4x4.identity
                },
                new CombineInstance
                {
                    mesh = skinnedMesh,
                    transform = Matrix4x4.identity
                }
            };

            Mesh combined = new Mesh
            {
                name = sourceRoot.name + "_CombinedForDeathSlice"
            };

            if (staticMesh.vertexCount + skinnedMesh.vertexCount > 65000)
                combined.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

            combined.CombineMeshes(combines, true, true, false);
            combined.RecalculateBounds();
            combined.RecalculateNormals();

            Destroy(staticMesh);
            Destroy(skinnedMesh);
            return combined.vertexCount > 0 ? combined : null;
        }

        private Mesh CombineSkinnedMeshesToLocal(GameObject sourceRoot)
        {
            if (sourceRoot == null)
                return null;

            SkinnedMeshRenderer[] skinnedRenderers =
                sourceRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            if (skinnedRenderers == null || skinnedRenderers.Length <= 0)
                return null;

            List<CombineInstance> combines = new List<CombineInstance>();
            List<Mesh> bakedMeshes = new List<Mesh>();
            Matrix4x4 rootWorldToLocal = sourceRoot.transform.worldToLocalMatrix;

            for (int i = 0; i < skinnedRenderers.Length; i++)
            {
                SkinnedMeshRenderer skinnedRenderer = skinnedRenderers[i];
                if (skinnedRenderer == null || skinnedRenderer.sharedMesh == null)
                    continue;

                Mesh bakedMesh = new Mesh();
                skinnedRenderer.BakeMesh(bakedMesh);
                bakedMeshes.Add(bakedMesh);

                int subMeshCount = Mathf.Max(1, bakedMesh.subMeshCount);
                for (int subMeshIndex = 0; subMeshIndex < subMeshCount; subMeshIndex++)
                {
                    combines.Add(new CombineInstance
                    {
                        mesh = bakedMesh,
                        subMeshIndex = subMeshIndex,
                        transform = rootWorldToLocal * skinnedRenderer.transform.localToWorldMatrix
                    });
                }
            }

            if (combines.Count <= 0)
            {
                DestroyMeshes(bakedMeshes);
                return null;
            }

            Mesh combined = new Mesh
            {
                name = sourceRoot.name + "_SkinnedCombinedForDeathSlice"
            };

            combined.CombineMeshes(combines.ToArray(), true, true, false);
            combined.RecalculateBounds();
            combined.RecalculateNormals();
            DestroyMeshes(bakedMeshes);
            return combined.vertexCount > 0 ? combined : null;
        }

        private static void DestroyMeshes(List<Mesh> meshes)
        {
            for (int i = 0; i < meshes.Count; i++)
            {
                if (meshes[i] != null)
                    Destroy(meshes[i]);
            }
        }

        private void CreateFallbackDeathPieces(
            Transform effectRoot,
            Material pieceMaterial,
            Vector3 explosionCenter,
            Bounds sizeLimitBounds,
            IReadOnlyList<Collider> ignoredColliders)
        {
            Bounds bounds = sizeLimitBounds;
            Vector3 size = bounds.size;
            if (size.sqrMagnitude <= 0.0001f)
                size = Vector3.one;

            int pieceCount = Mathf.Max(2, deathDestructionPieceCount);
            for (int i = 0; i < pieceCount; i++)
            {
                Mesh fallbackMesh = CreateFallbackDeathPieceMesh(i);
                GameObject pieceObject = global::MeshPieceObjectFactory.CreatePieceObject(
                    effectRoot,
                    fallbackMesh,
                    i,
                    pieceMaterial,
                    global::FracturePieceColliderMode.Box,
                    64,
                    0.15f,
                    0.001f,
                    false);
                Destroy(fallbackMesh);
                if (pieceObject == null)
                    continue;

                pieceObject.transform.position = GetFallbackPiecePosition(bounds, i);
                pieceObject.transform.rotation =
                    effectRoot.rotation * GetFallbackPieceRotation(i);
                pieceObject.transform.localScale =
                    ConvertWorldScaleToLocal(
                        GetFallbackPieceScale(size, i),
                        effectRoot.lossyScale);

                global::ClusterPiece piece = pieceObject.GetComponent<global::ClusterPiece>();
                if (piece == null)
                    piece = pieceObject.AddComponent<global::ClusterPiece>();

                piece.CaptureInitialTransform();
                ConstrainDeathPieceSize(pieceObject, explosionCenter, sizeLimitBounds);
                ConfigureDeathPiece(pieceObject, explosionCenter, ignoredColliders);
            }
        }

        private static Mesh CreateFallbackDeathPieceMesh(int index)
        {
            float x = 0.5f * (0.85f + 0.05f * (index % 3));
            float y = 0.5f * (0.75f + 0.07f * ((index + 1) % 3));
            float z = 0.5f * (0.8f + 0.06f * ((index + 2) % 3));
            float skew = index % 2 == 0 ? 0.12f : -0.12f;

            Vector3[] vertices =
            {
                new Vector3(-x, 0f, skew),
                new Vector3(x, 0f, -skew),
                new Vector3(0f, y, z),
                new Vector3(0f, -y, z * 0.75f),
                new Vector3(skew, y * 0.35f, -z),
                new Vector3(-skew, -y * 0.45f, -z * 0.85f)
            };

            int[] triangles =
            {
                0, 2, 4,
                2, 1, 4,
                1, 3, 4,
                3, 0, 4,
                2, 0, 5,
                1, 2, 5,
                3, 1, 5,
                0, 3, 5
            };

            Mesh mesh = new Mesh
            {
                name = "EnemyFallbackDeathShard_" + index
            };
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            mesh.RecalculateNormals();
            return mesh;
        }

        private static Quaternion GetFallbackPieceRotation(int index)
        {
            return Quaternion.Euler(
                17f * (index + 1),
                41f * (index + 2),
                -23f * (index + 3));
        }

        private Bounds GetDeathDestructionBounds()
        {
            Renderer[] renderers = m_activeAppearance != null
                ? m_activeAppearance.GetComponentsInChildren<Renderer>(true)
                : GetComponentsInChildren<Renderer>(true);

            bool hasBounds = false;
            Bounds bounds = new Bounds(transform.position, Vector3.one);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null)
                    continue;

                if (!hasBounds)
                {
                    bounds = renderers[i].bounds;
                    hasBounds = true;
                    continue;
                }

                bounds.Encapsulate(renderers[i].bounds);
            }

            return bounds;
        }

        private Bounds GetDeathColliderBounds()
        {
            Collider[] colliders = m_activeAppearance != null
                ? m_activeAppearance.GetComponentsInChildren<Collider>(true)
                : GetComponentsInChildren<Collider>(true);
            bool hasBounds = false;
            Bounds bounds = GetDeathDestructionBounds();

            for (int i = 0; i < colliders.Length; i++)
            {
                Collider targetCollider = colliders[i];
                if (targetCollider == null || targetCollider.isTrigger)
                    continue;

                if (!hasBounds)
                {
                    bounds = targetCollider.bounds;
                    hasBounds = true;
                    continue;
                }

                bounds.Encapsulate(targetCollider.bounds);
            }

            return bounds;
        }

        private void ConstrainDeathPieceSize(
            GameObject pieceObject,
            Vector3 explosionCenter,
            Bounds sizeLimitBounds)
        {
            if (pieceObject == null)
                return;

            Bounds pieceBounds = GetRendererBounds(pieceObject);
            float pieceMaxAxis = GetMaxAxis(pieceBounds.size);
            float limitMaxAxis = GetMaxAxis(sizeLimitBounds.size);
            float appliedScale = 1f;
            if (pieceMaxAxis > 0.0001f && limitMaxAxis > 0.0001f)
            {
                float maxPieceAxis = limitMaxAxis * 0.42f;
                appliedScale = Mathf.Min(1f, maxPieceAxis / pieceMaxAxis);
                pieceObject.transform.localScale *= appliedScale;
            }

            Vector3 centerOffset = pieceObject.transform.position - explosionCenter;
            pieceObject.transform.position = explosionCenter + centerOffset * appliedScale;
        }

        private static Bounds GetRendererBounds(GameObject target)
        {
            Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
            if (renderers == null || renderers.Length <= 0)
                return new Bounds(target.transform.position, Vector3.zero);

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                if (renderers[i] != null)
                    bounds.Encapsulate(renderers[i].bounds);
            }

            return bounds;
        }

        private static float GetMaxAxis(Vector3 size)
        {
            return Mathf.Max(size.x, Mathf.Max(size.y, size.z));
        }

        private static Vector3 GetFallbackPiecePosition(Bounds bounds, int index)
        {
            Vector3 offset = new Vector3(
                (index % 2 == 0 ? -0.25f : 0.25f) * bounds.size.x,
                (index / 2 == 0 ? -0.2f : 0.2f) * bounds.size.y,
                (index % 4 < 2 ? -0.2f : 0.2f) * bounds.size.z);

            return bounds.center + offset;
        }

        private static Vector3 GetFallbackPieceScale(Vector3 sourceSize, int index)
        {
            return new Vector3(
                Mathf.Max(0.08f, sourceSize.x * 0.34f),
                Mathf.Max(0.08f, sourceSize.y * 0.34f),
                Mathf.Max(0.08f, sourceSize.z * (index % 2 == 0 ? 0.32f : 0.38f)));
        }

        private static Vector3 ConvertWorldScaleToLocal(
            Vector3 worldScale,
            Vector3 parentLossyScale)
        {
            return new Vector3(
                DivideScaleAxis(worldScale.x, parentLossyScale.x),
                DivideScaleAxis(worldScale.y, parentLossyScale.y),
                DivideScaleAxis(worldScale.z, parentLossyScale.z));
        }

        private static float DivideScaleAxis(float value, float parentAxis)
        {
            if (Mathf.Abs(parentAxis) <= 0.0001f)
                return value;

            return value / Mathf.Abs(parentAxis);
        }

        private global::SliceFractureSettings CreateDeathSliceSettings()
        {
            return new global::SliceFractureSettings
            {
                qualityPreset = global::FractureQualityPreset.Custom,
                sliceCount = Mathf.Max(1, deathDestructionPieceCount - 1),
                randomSeed = Random.Range(1, int.MaxValue),
                angleJitter = 50f,
                axisBias = Vector3.up,
                minPieceVolume = 0.00001f,
                minAxisSize = 0.005f,
                minAxisRatio = 0f,
                maxAspectRatio = 0f,
                maxRetryPerSlice = 24,
                mergeSmallPieces = true,
                maxSmallPieceMergePasses = 16,
                colliderMode = global::FracturePieceColliderMode.Box,
                autoMeshColliderMaxTriangles = 64,
                autoMeshColliderMinAxisSize = 0.15f,
                autoMeshColliderMinVolume = 0.001f
            };
        }

        private Material ResolveDeathPieceMaterial()
        {
            Renderer renderer = m_activeAppearance != null
                ? m_activeAppearance.GetComponentInChildren<Renderer>(true)
                : GetComponentInChildren<Renderer>(true);

            return renderer != null ? renderer.sharedMaterial : null;
        }

        private Vector3 GetDeathDestructionCenter()
        {
            Bounds bounds = GetDeathDestructionBounds();
            if (bounds.size.sqrMagnitude > 0.0001f)
                return bounds.center;

            Collider targetCollider = GetComponentInChildren<Collider>(true);
            return targetCollider != null ? targetCollider.bounds.center : transform.position;
        }

        private void ConfigureDeathPiece(
            GameObject pieceObject,
            Vector3 explosionCenter,
            IReadOnlyList<Collider> ignoredColliders)
        {
            if (pieceObject == null)
                return;

            global::ClusterPiece piece = pieceObject.GetComponent<global::ClusterPiece>();
            if (piece == null)
                return;

            piece.autoDisable = true;
            piece.disableAfterSeconds = deathPieceDisableDelay;
            piece.shrinkDuration = deathPieceShrinkDuration;
            piece.enableCollisionWhenDetached = false;
            piece.ActivatePhysics(ignoredColliders);

            if (piece.rb == null)
                return;

            Vector3 direction = piece.transform.position - explosionCenter;
            if (direction.sqrMagnitude < 0.0001f)
                direction = Random.onUnitSphere;

            direction.Normalize();
            direction.y += deathPieceUpwardImpulse;
            direction.Normalize();

            piece.rb.AddForce(
                direction * deathPieceDetachImpulse,
                ForceMode.Impulse);
            piece.rb.AddTorque(
                Random.insideUnitSphere * deathPieceDetachImpulse,
                ForceMode.Impulse);
        }

        public void SetSpawner(EnemySpawnerEntity spawner)
        {
            m_spawner = spawner;
        }

        public bool TryGetSpawnArea(out Vector3 center, out float radius)
        {
            if (m_spawner == null)
            {
                center = default;
                radius = 0f;
                return false;
            }

            center = m_spawner.SpawnCenter;
            radius = m_spawner.SpawnRadius;
            return radius > 0f;
        }

        public void SetAir(bool value)
        {
            isAir = value;
        }

        public void SetInfight(bool value)
        {
            isInfight = value;
        }

        /// <summary>
        /// 砲台アイテムをスポーンする
        /// 敵の位置にItemEntityのPrefabを生成する
        /// </summary>
        private void TrySpawnTurretItem()
        {
            if (!m_willDropTurretItem)
                return;

            m_willDropTurretItem = false;
            SpawnTurretItem();
        }

        private void SpawnTurretItem()
        {
            if (turretItemPrefab == null)
            {
                Debug.LogWarning("[EnemyEntity] turretItemPrefabが設定されていません。");
                return;
            }

            bool isOnline =
                NetworkManager.Singleton != null &&
                NetworkManager.Singleton.IsListening;
            if (isOnline && !NetworkManager.Singleton.IsServer)
                return;

            // 敵の位置にアイテムをスポーン
            if (turretItemPrefab.GetComponent<ItemEntity>() == null)
            {
                Debug.LogWarning($"[EnemyEntity] turretItemPrefab must reference the TurretItem root with ItemEntity. Current reference: {turretItemPrefab.name}");
                return;
            }

            Vector3 spawnPosition = GetDropSpawnPosition();
            GameObject itemObject = Instantiate(
                turretItemPrefab,
                spawnPosition,
                Quaternion.identity
            );

            ItemEntity item = itemObject.GetComponent<ItemEntity>();
            if (item == null)
            {
                Debug.LogWarning("[EnemyEntity] ItemEntityがPrefabにアタッチされていません。");
                return;
            }

            // ランダムでミサイルかガトリングを設定
            string[] turretTypes = { "missile", "gatling", "railgun" };
            item.SetItemId(turretTypes[Random.Range(0, turretTypes.Length)]);
            item.ApplyDropScatter(GetNextDropScatterVelocity());

            if (!isOnline)
                return;

            NetworkObject networkObject = itemObject.GetComponent<NetworkObject>();
            NetworkBuffItemController networkController =
                itemObject.GetComponent<NetworkBuffItemController>();
            if (networkObject == null || networkController == null)
            {
                Debug.LogError(
                    "[EnemyEntity] Online turret item requires NetworkObject and NetworkBuffItemController.");
                Destroy(itemObject);
                return;
            }

            networkController.ConfigureWeaponServer(item.ItemId);
            networkController.ConfigureSpawnPositionServer(itemObject.transform.position);
            networkObject.Spawn(true);
        }

        ///<summary>
        ///バフアイテムをスポーンさせる
        ///敵の位置にBuffItemEntityのPrefabを生成する
        ///</summary>
        private void TrySpawnBuffItem()
        {
            if (!m_willDropBuffItem)
                return;

            m_willDropBuffItem = false;
            SpawnBuffItem();
        }

        private void SpawnBuffItem()
        {
            bool isOnline =
                NetworkManager.Singleton != null &&
                NetworkManager.Singleton.IsListening;
            if (isOnline && !NetworkManager.Singleton.IsServer)
                return;

            GameObject buffItemPrefab = GetRandomBuffItemPrefab();
            if(buffItemPrefab == null)
            {
                Debug.LogWarning("[EnemyEntity] buffItemPrefabが設定されていません。");
                return;
            }

            //敵の位置にアイテムをスポーン
            Vector3 spawnPosition = GetDropSpawnPosition();
            GameObject itemObject = Instantiate(
                buffItemPrefab,
                spawnPosition,
                Quaternion.identity
                );

            if(itemObject.GetComponent<BuffItemEntity>() == null)
            {
                Debug.LogWarning("[EnemyEntity] BuffItemEntityがPrefabにアタッチされていません。");
                return;
            }

            BuffItemEntity scatterBuffItem = itemObject.GetComponent<BuffItemEntity>();
            if (scatterBuffItem != null)
                scatterBuffItem.ApplyScatter(GetNextDropScatterVelocity());

            if (isOnline)
            {
                NetworkObject networkObject =
                    itemObject.GetComponent<NetworkObject>();
                if (networkObject == null)
                {
                    Debug.LogError(
                        "[EnemyEntity] Online buff item requires NetworkObject.");
                    Destroy(itemObject);
                    return;
                }

                NetworkBuffItemController networkController =
                    itemObject.GetComponent<NetworkBuffItemController>();
                networkController?.ConfigureSpawnPositionServer(itemObject.transform.position);
                networkObject.Spawn(true);
            }
        }

        private void TrySpawnQueuedDropItems()
        {
            if (m_suppressNextItemDrop)
            {
                m_suppressNextItemDrop = false;
                m_willDropBuffItem = false;
                m_willDropTurretItem = false;
                return;
            }

            switch (m_dropType)
            {
                case DropType.Buff:
                    m_willDropBuffItem = false;
                    SpawnBuffItem();
                    break;
                case DropType.Turret:
                    m_willDropTurretItem = false;
                    SpawnTurretItem();
                    break;
            }
        }

        private void PrepareItemDrops()
        {
            SetDropType(Random.Range(0, 4));
        }

        private void SetDropType(int value)
        {
            m_dropType = (DropType)Mathf.Clamp(value, 0, 3);
            m_willDropBuffItem = m_dropType == DropType.Buff;
            m_willDropTurretItem = m_dropType == DropType.Turret;
        }

        private bool ShouldForceNormalDropType()
        {
            return isAir && isInfight;
        }

        private void ApplyDropTypeColor()
        {
            if (m_dropType == DropType.None)
                return;

            Color dropColor;
            Material dropMaterial;
            switch (m_dropType)
            {
                case DropType.Buff:
                    dropColor = buffDropEnemyColor;
                    dropMaterial = GetDropMaterial(ref s_buffDropMaterial, "EnemyBuffDropMaterial", dropColor);
                    break;
                case DropType.Turret:
                    dropColor = turretDropEnemyColor;
                    dropMaterial = GetDropMaterial(ref s_turretDropMaterial, "EnemyTurretDropMaterial", dropColor);
                    break;
                case DropType.Recovery:
                    dropColor = recoveryEnemyColor;
                    dropMaterial = GetDropMaterial(ref s_recoveryDropMaterial, "EnemyRecoveryDropMaterial", dropColor);
                    break;
                default:
                    return;
            }

            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            MaterialPropertyBlock properties = new MaterialPropertyBlock();

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer targetRenderer = renderers[i];
                if (targetRenderer == null || dropMaterial == null)
                    continue;

                Material[] materials = targetRenderer.sharedMaterials;
                for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
                    materials[materialIndex] = dropMaterial;

                targetRenderer.sharedMaterials = materials;
                targetRenderer.GetPropertyBlock(properties);
                properties.SetColor("_BaseColor", dropColor);
                properties.SetColor("_Color", dropColor);
                targetRenderer.SetPropertyBlock(properties);
            }
        }

        private static Material GetDropMaterial(
            ref Material cachedMaterial,
            string materialName,
            Color color)
        {
            if (cachedMaterial != null)
                return cachedMaterial;

            Shader shader = Shader.Find("Standard");
            if (shader == null)
                shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                shader = Shader.Find("Sprites/Default");
            if (shader == null)
                return null;

            cachedMaterial = new Material(shader)
            {
                name = materialName,
                color = color
            };

            if (cachedMaterial.HasProperty("_Color"))
                cachedMaterial.SetColor("_Color", color);
            if (cachedMaterial.HasProperty("_BaseColor"))
                cachedMaterial.SetColor("_BaseColor", color);
            if (cachedMaterial.HasProperty("_MainTex"))
                cachedMaterial.SetTexture("_MainTex", Texture2D.whiteTexture);
            if (cachedMaterial.HasProperty("_BaseMap"))
                cachedMaterial.SetTexture("_BaseMap", Texture2D.whiteTexture);

            return cachedMaterial;
        }

        private float GetDefeatHealAmount()
        {
            return m_dropType == DropType.Recovery
                ? Mathf.Max(0f, recoveryEnemyDefeatHeal)
                : Mathf.Max(0f, normalDefeatHeal);
        }

        private int GetDefeatPointValue()
        {
            return m_dropType == DropType.None
                ? Mathf.Max(0, normalEnemyPointValue)
                : Mathf.Max(0, pointValue);
        }

        private GameObject GetRandomBuffItemPrefab()
        {
            if (buffItemPrefabs == null || buffItemPrefabs.Length <= 0)
                return null;

            int startIndex = Random.Range(0, buffItemPrefabs.Length);
            for (int i = 0; i < buffItemPrefabs.Length; i++)
            {
                int index = (startIndex + i) % buffItemPrefabs.Length;
                if (buffItemPrefabs[index] != null)
                    return buffItemPrefabs[index];
            }

            return null;
        }

        private Vector3 GetDropSpawnPosition()
        {
            Vector3 sourcePosition = GetDropSourcePosition();
            if (TryGetDropGroundPosition(sourcePosition, out Vector3 groundPosition))
                return groundPosition + Vector3.up * dropSpawnHeight;

            return sourcePosition + Vector3.up * dropSpawnHeight;
        }

        private Vector3 GetDropSourcePosition()
        {
            return m_hasDeathDropPosition ? m_deathDropPosition : transform.position;
        }

        private void CaptureDeathDropPosition()
        {
            m_deathDropPosition = transform.position;
            m_hasDeathDropPosition = true;
        }

        private bool TryGetDropGroundPosition(
            Vector3 sourcePosition,
            out Vector3 groundPosition)
        {
            Vector3 rayOrigin = sourcePosition + Vector3.up * Mathf.Max(0f, dropGroundProbeHeight);
            float rayDistance = Mathf.Max(0.1f, dropGroundProbeHeight + dropGroundProbeDistance);
            int hitCount = Physics.RaycastNonAlloc(
                rayOrigin,
                Vector3.down,
                m_dropGroundHits,
                rayDistance,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore);

            groundPosition = sourcePosition;
            float bestGroundY = float.PositiveInfinity;
            float bestDistance = float.PositiveInfinity;
            bool hasGround = false;

            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = m_dropGroundHits[i];
                Collider hitCollider = hit.collider;
                if (hitCollider == null)
                    continue;
                if (hit.normal.y < 0.35f)
                    continue;
                if (dropMaximumGroundY > 0f && hit.point.y > dropMaximumGroundY)
                    continue;
                if (hitCollider.transform.root == transform.root)
                    continue;
                if (hitCollider.GetComponentInParent<DestructibleBuilding>() != null)
                    continue;
                if (ProjectileSweepUtility.IsWorldBorder(hitCollider) &&
                    !ProjectileSweepUtility.IsBattlePlane(hitCollider))
                {
                    continue;
                }
                if (hitCollider.GetComponentInParent<PlayerEntity>() != null)
                    continue;
                if (hitCollider.GetComponentInParent<EnemyEntity>() != null)
                    continue;
                if (hitCollider.GetComponentInParent<EnemyCPUEntity>() != null)
                    continue;
                if (hitCollider.GetComponentInParent<BuffItemEntity>() != null)
                    continue;
                if (hitCollider.GetComponentInParent<ItemEntity>() != null)
                    continue;
                if (hit.point.y > bestGroundY + 0.01f)
                    continue;
                if (Mathf.Abs(hit.point.y - bestGroundY) <= 0.01f &&
                    hit.distance >= bestDistance)
                    continue;

                bestGroundY = hit.point.y;
                bestDistance = hit.distance;
                groundPosition = hit.point;
                hasGround = true;
            }

            return hasGround;
        }

        private Vector3 GetNextDropScatterVelocity()
        {
            float goldenAngle = 137.5f;
            float angle = Random.Range(0f, 360f) + m_dropScatterIndex * goldenAngle;
            m_dropScatterIndex++;

            Vector3 direction = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
            float speed = Random.Range(dropScatterMinSpeed, dropScatterMaxSpeed);

            return direction.normalized * speed + Vector3.up * dropScatterUpSpeed;
        }
    }
}
