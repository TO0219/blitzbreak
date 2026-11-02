using System.Collections.Generic;
using UnityEngine;

namespace MyGame
{
    /// <summary>
    /// タイトル画面専用の演出CPU。
    /// 指定ルートを巡回し、範囲内の対象へ砲塔を向けて射撃する。
    /// </summary>
    public sealed class TitleCPUEntity : MonoBehaviour
    {
        [System.Serializable]
        private struct TankMaterialVariant
        {
            [SerializeField] private MachineType machineType;
            [SerializeField] private Material tankBaseMaterial;
            [SerializeField] private Material tankTrackMaterial;
            [SerializeField] private Material weaponBaseType1Material;
            [SerializeField] private Material weaponBaseType2Material;

            public MachineType MachineType => machineType;
            public Material TankBaseMaterial => tankBaseMaterial;
            public Material TankTrackMaterial => tankTrackMaterial;
            public Material WeaponBaseType1Material => weaponBaseType1Material;
            public Material WeaponBaseType2Material => weaponBaseType2Material;
        }

        private enum TitleWeaponType
        {
            AutoByName,
            Default,
            MachineGun,
            Missile
        }

        [Header("Machine")]
        [SerializeField] private int machineIndex;
        [SerializeField] private TankMaterialVariant[] tankMaterialVariants;

        [Header("Route")]
        [SerializeField] private Transform routeRoot;
        [SerializeField] private Transform[] routePoints;
        [SerializeField, Min(0)] private int startRouteIndex;
        [SerializeField] private bool startAtClosestRoutePoint = true;
        [SerializeField, Min(0.05f)] private float waypointArrivalDistance = 0.75f;
        [Tooltip("既存ルート線上に追加したポイントを中継点として認識する幅です。")]
        [SerializeField, Min(0.05f)] private float routeSegmentSnapDistance = 0.75f;
        [SerializeField, Min(0f)] private float moveSpeed = 4f;
        [SerializeField, Min(0f)] private float rotateSpeed = 120f;
        [Tooltip("急旋回中も維持する、通常移動速度に対する最低速度の割合です。")]
        [SerializeField, Range(0.05f, 1f)] private float minimumTurningSpeedRatio = 0.2f;
        [Tooltip("分岐で直前のポイントへ即座に戻るのを避けます。ほかに道がない場合だけ戻ります。")]
        [SerializeField] private bool avoidImmediateBacktracking = true;

        [Header("Body / Turret")]
        [SerializeField] private Transform bodyTransform;
        [SerializeField] private Transform turretTransform;
        [SerializeField] private Transform[] turretTransforms;
        [SerializeField] private Transform enemyMuzzleTransform;
        [SerializeField] private Transform defaultTurretModel;
        [SerializeField] private Transform machineGunTurretModel;
        [SerializeField] private Transform missileTurretModel;
        [SerializeField, Min(0f)] private float turretRotateSpeed = 180f;
        [SerializeField, Range(0f, 90f)] private float idleTurretSweepAngle = 25f;
        [SerializeField, Min(0f)] private float idleTurretSweepSpeed = 1.2f;

        [Header("Target")]
        [SerializeField, Min(0f)] private float attackRange = 10f;
        [SerializeField, Min(0.05f)] private float targetSearchInterval = 0.2f;
        [SerializeField] private LayerMask targetMask = ~0;
        [SerializeField] private bool targetPlayers = true;
        [SerializeField] private bool targetEnemies = true;
        [SerializeField] private bool targetBattleCpus = true;
        [SerializeField] private bool targetTitleCpus = true;
        [SerializeField] private Transform[] additionalTargets;

        [Header("Fire")]
        [SerializeField] private TitleWeaponType weaponType = TitleWeaponType.AutoByName;
        [SerializeField] private GameObject bullet;
        [SerializeField, Min(0.05f)] private float fireInterval = 0.6f;
        [SerializeField, Min(0f)] private float projectileSpeed = 15f;
        [SerializeField] private GameObject machineGunBullet;
        [SerializeField, Min(0.05f)] private float machineGunFireInterval = 0.12f;
        [SerializeField, Min(0f)] private float machineGunProjectileSpeed = 30f;
        [SerializeField] private GameObject missileBullet;
        [SerializeField, Min(0.05f)] private float missileFireInterval = 1.2f;
        [SerializeField, Min(0f)] private float missileProjectileSpeed = 20f;

        private Rigidbody m_rigidbody;
        private Collider[] m_ownColliders;
        private Collider[] m_targetHits = new Collider[64];
        private readonly HashSet<Transform> m_checkedTargets = new HashSet<Transform>();
        private readonly List<Transform> m_routeCandidates = new List<Transform>();
        private Transform m_target;
        private int m_routeIndex;
        private Transform m_previousRoutePoint;
        private float m_targetSearchTimer;
        private float m_fireTimer;
        private float m_idleTurretPhase;
        private Transform[] m_activeMuzzles;
        private GameObject m_activeBullet;
        private float m_activeFireInterval;
        private float m_activeProjectileSpeed;
        private int m_nextMuzzleIndex;
        private Transform[] m_activeTurretTransforms;
        private bool m_hasBaseMachineValues;
        private Vector3 m_baseScale;
        private float m_baseMoveSpeed;
        private float m_baseRotateSpeed;
        private float m_baseTurretRotateSpeed;
        private float m_baseAttackRange;
        private float m_baseFireInterval;
        private float m_baseProjectileSpeed;
        private float m_baseMachineGunFireInterval;
        private float m_baseMachineGunProjectileSpeed;
        private float m_baseMissileFireInterval;
        private float m_baseMissileProjectileSpeed;

        public int MachineIndex => machineIndex;
        public MachineProfile MachineProfile =>
            MachineProfileCatalog.GetProfile(machineIndex);

        private void Awake()
        {
            m_rigidbody = GetComponent<Rigidbody>();
            m_ownColliders = GetComponentsInChildren<Collider>(true);
            m_idleTurretPhase = Random.Range(0f, Mathf.PI * 2f);
            ApplyMachineIndex(machineIndex);

            if (m_rigidbody != null)
            {
                // タイトル演出用のルート移動を物理衝突で停止させない。
                m_rigidbody.useGravity = false;
                m_rigidbody.isKinematic = true;
                m_rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
                m_rigidbody.collisionDetectionMode =
                    CollisionDetectionMode.ContinuousSpeculative;
                m_rigidbody.constraints = RigidbodyConstraints.FreezeRotation;
            }
        }

        private void Start()
        {
            ConfigureWeapon();
            InitializeRouteIndex();
            RefreshTarget();
        }

        private void ConfigureWeapon()
        {
            m_activeTurretTransforms = CollectTurretTransforms();

            if (defaultTurretModel == null)
                defaultTurretModel =
                    FindDescendant(m_activeTurretTransforms, "turretModel");
            if (machineGunTurretModel == null)
                machineGunTurretModel =
                    FindDescendant(m_activeTurretTransforms, "GatlingTurretModel");
            if (missileTurretModel == null)
                missileTurretModel =
                    FindDescendant(m_activeTurretTransforms, "MissileTurretModel");

            TitleWeaponType selectedWeapon = ResolveWeaponType();
            SetModelActive(
                defaultTurretModel,
                selectedWeapon == TitleWeaponType.Default);
            SetModelActive(
                machineGunTurretModel,
                selectedWeapon == TitleWeaponType.MachineGun);
            SetModelActive(
                missileTurretModel,
                selectedWeapon == TitleWeaponType.Missile);

            Transform selectedModel = null;
            m_activeBullet = bullet;
            m_activeFireInterval = fireInterval;
            m_activeProjectileSpeed = projectileSpeed;

            if (selectedWeapon == TitleWeaponType.MachineGun)
            {
                selectedModel = machineGunTurretModel;
                if (machineGunBullet != null)
                    m_activeBullet = machineGunBullet;
                m_activeFireInterval = machineGunFireInterval;
                m_activeProjectileSpeed = machineGunProjectileSpeed;
            }
            else if (selectedWeapon == TitleWeaponType.Missile)
            {
                selectedModel = missileTurretModel;
                if (missileBullet != null)
                    m_activeBullet = missileBullet;
                m_activeFireInterval = missileFireInterval;
                m_activeProjectileSpeed = missileProjectileSpeed;
            }

            m_activeMuzzles = CollectMuzzles(selectedModel);
            if (m_activeMuzzles.Length <= 0 && enemyMuzzleTransform != null)
                m_activeMuzzles = new[] { enemyMuzzleTransform };

            m_nextMuzzleIndex = 0;
        }

        public void ApplyMachineIndex(int index)
        {
            CacheBaseMachineValues();

            machineIndex = MachineProfileCatalog.ClampIndex(index);
            MachineProfile profile = MachineProfileCatalog.GetProfile(machineIndex);

            transform.localScale = m_baseScale * profile.SizeMultiplier;
            moveSpeed = m_baseMoveSpeed * profile.MoveSpeedMultiplier;
            rotateSpeed = m_baseRotateSpeed * profile.MoveSpeedMultiplier;
            turretRotateSpeed =
                m_baseTurretRotateSpeed * profile.AttackSpeedMultiplier;
            attackRange = m_baseAttackRange * profile.SizeMultiplier;
            fireInterval = GetProfiledFireInterval(
                m_baseFireInterval,
                profile.AttackSpeedMultiplier);
            machineGunFireInterval = GetProfiledFireInterval(
                m_baseMachineGunFireInterval,
                profile.AttackSpeedMultiplier);
            missileFireInterval = GetProfiledFireInterval(
                m_baseMissileFireInterval,
                profile.AttackSpeedMultiplier);
            projectileSpeed =
                m_baseProjectileSpeed * profile.AttackPowerMultiplier;
            machineGunProjectileSpeed =
                m_baseMachineGunProjectileSpeed * profile.AttackPowerMultiplier;
            missileProjectileSpeed =
                m_baseMissileProjectileSpeed * profile.AttackPowerMultiplier;

            ApplyTankMaterialVariant(profile.Type);

            if (m_activeBullet != null)
                ConfigureWeapon();
        }

        private static float GetProfiledFireInterval(
            float baseInterval,
            float attackSpeedMultiplier)
        {
            return Mathf.Max(
                0.05f,
                baseInterval / Mathf.Max(0.01f, attackSpeedMultiplier));
        }

        private void CacheBaseMachineValues()
        {
            if (m_hasBaseMachineValues)
                return;

            m_baseScale = transform.localScale;
            m_baseMoveSpeed = moveSpeed;
            m_baseRotateSpeed = rotateSpeed;
            m_baseTurretRotateSpeed = turretRotateSpeed;
            m_baseAttackRange = attackRange;
            m_baseFireInterval = fireInterval;
            m_baseProjectileSpeed = projectileSpeed;
            m_baseMachineGunFireInterval = machineGunFireInterval;
            m_baseMachineGunProjectileSpeed = machineGunProjectileSpeed;
            m_baseMissileFireInterval = missileFireInterval;
            m_baseMissileProjectileSpeed = missileProjectileSpeed;
            m_hasBaseMachineValues = true;
        }

        private Transform[] CollectTurretTransforms()
        {
            List<Transform> turrets = new List<Transform>();
            AddTurretTransform(turrets, turretTransform);

            if (turretTransforms != null)
            {
                for (int i = 0; i < turretTransforms.Length; i++)
                    AddTurretTransform(turrets, turretTransforms[i]);
            }

            return turrets.Count > 0
                ? turrets.ToArray()
                : System.Array.Empty<Transform>();
        }

        private static void AddTurretTransform(
            List<Transform> turrets,
            Transform turret)
        {
            if (turret == null || turrets.Contains(turret))
                return;

            turrets.Add(turret);
        }

        private TitleWeaponType ResolveWeaponType()
        {
            if (weaponType != TitleWeaponType.AutoByName)
                return weaponType;

            if (name.Contains("(1)"))
                return TitleWeaponType.MachineGun;
            if (name.Contains("(2)"))
                return TitleWeaponType.Missile;

            return TitleWeaponType.Default;
        }

        private static void SetModelActive(Transform model, bool active)
        {
            if (model != null)
                model.gameObject.SetActive(active);
        }

        private void ApplyTankMaterialVariant(MachineType machineType)
        {
            if (!TryGetTankMaterialVariant(machineType, out TankMaterialVariant variant))
                return;

            ApplyMachineVariantMaterials(gameObject, variant);
        }

        private bool TryGetTankMaterialVariant(
            MachineType machineType,
            out TankMaterialVariant variant)
        {
            if (tankMaterialVariants != null)
            {
                for (int i = 0; i < tankMaterialVariants.Length; i++)
                {
                    TankMaterialVariant candidate = tankMaterialVariants[i];
                    if (candidate.MachineType == machineType)
                    {
                        variant = candidate;
                        return true;
                    }
                }
            }

            variant = default;
            return false;
        }

        private void ApplyMachineVariantMaterials(
            GameObject targetRoot,
            TankMaterialVariant variant)
        {
            if (targetRoot == null)
                return;

            Renderer[] renderers =
                targetRoot.GetComponentsInChildren<Renderer>(true);
            for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
            {
                Renderer targetRenderer = renderers[rendererIndex];
                if (targetRenderer == null)
                    continue;

                Material[] materials = targetRenderer.sharedMaterials;
                bool changed = false;
                string hierarchyName = GetHierarchyName(targetRenderer.transform);

                for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
                {
                    Material sourceMaterial = materials[materialIndex];
                    if (!TryResolveVariantMaterial(
                            sourceMaterial,
                            hierarchyName,
                            variant,
                            out Material replacement))
                    {
                        continue;
                    }

                    if (sourceMaterial == replacement)
                        continue;

                    materials[materialIndex] = replacement;
                    changed = true;
                }

                if (changed)
                    targetRenderer.sharedMaterials = materials;
            }
        }

        private static bool TryResolveVariantMaterial(
            Material sourceMaterial,
            string hierarchyName,
            TankMaterialVariant variant,
            out Material replacement)
        {
            string materialName =
                sourceMaterial != null ? sourceMaterial.name : string.Empty;

            if (MatchesMaterialPart(materialName, hierarchyName, "WeaponBaseType1"))
            {
                replacement = variant.WeaponBaseType1Material;
                return replacement != null;
            }

            if (MatchesMaterialPart(materialName, hierarchyName, "WeaponBaseType2"))
            {
                replacement = variant.WeaponBaseType2Material;
                return replacement != null;
            }

            if (MatchesMaterialPart(materialName, hierarchyName, "TankTrack") ||
                MatchesMaterialPart(materialName, hierarchyName, "TankTracks"))
            {
                replacement = variant.TankTrackMaterial;
                return replacement != null;
            }

            if (MatchesMaterialPart(materialName, hierarchyName, "TankBase"))
            {
                replacement = variant.TankBaseMaterial;
                return replacement != null;
            }

            replacement = null;
            return false;
        }

        private static bool MatchesMaterialPart(
            string materialName,
            string hierarchyName,
            string partName)
        {
            return ContainsIgnoreCase(materialName, partName) ||
                   ContainsIgnoreCase(hierarchyName, partName);
        }

        private static bool ContainsIgnoreCase(string text, string value)
        {
            return !string.IsNullOrEmpty(text) &&
                   text.IndexOf(value, System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string GetHierarchyName(Transform target)
        {
            if (target == null)
                return string.Empty;

            string result = target.name;
            Transform current = target.parent;
            while (current != null)
            {
                result = current.name + "/" + result;
                current = current.parent;
            }

            return result;
        }

        private static Transform FindDescendant(Transform root, string objectName)
        {
            if (root == null)
                return null;

            Transform[] descendants = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < descendants.Length; i++)
            {
                if (descendants[i].name == objectName)
                    return descendants[i];
            }

            return null;
        }

        private static Transform FindDescendant(
            Transform[] roots,
            string objectName)
        {
            if (roots == null)
                return null;

            for (int i = 0; i < roots.Length; i++)
            {
                Transform descendant = FindDescendant(roots[i], objectName);
                if (descendant != null)
                    return descendant;
            }

            return null;
        }

        private static Transform[] CollectMuzzles(Transform model)
        {
            if (model == null)
                return System.Array.Empty<Transform>();

            List<Transform> muzzles = new List<Transform>();
            Transform[] descendants =
                model.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < descendants.Length; i++)
            {
                string objectName = descendants[i].name;
                if (objectName == "Muzzle_L" ||
                    objectName == "Muzzle_R" ||
                    objectName == "Muzzle")
                {
                    muzzles.Add(descendants[i]);
                }
            }

            muzzles.Sort((left, right) =>
                string.CompareOrdinal(left.name, right.name));
            return muzzles.ToArray();
        }

        private void Update()
        {
            MoveAlongRoute();
            UpdateTarget();
            UpdateTurretAndFire();
        }

        private void OnDisable()
        {
            StopHorizontalVelocity();
        }

        private void InitializeRouteIndex()
        {
            int count = GetRoutePointCount();
            if (count <= 0)
            {
                m_routeIndex = 0;
                return;
            }

            m_routeIndex = Mathf.Clamp(startRouteIndex, 0, count - 1);
            if (!startAtClosestRoutePoint)
                return;

            float nearestDistanceSqr = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                Transform point = GetRoutePoint(i);
                if (point == null)
                    continue;

                Vector3 offset = point.position - transform.position;
                offset.y = 0f;
                if (offset.sqrMagnitude >= nearestDistanceSqr)
                    continue;

                nearestDistanceSqr = offset.sqrMagnitude;
                m_routeIndex = i;
            }
        }

        private void MoveAlongRoute()
        {
            if (moveSpeed <= 0f || GetRoutePointCount() <= 0)
            {
                StopHorizontalVelocity();
                return;
            }

            Vector3 direction;
            if (!TryGetRouteDirection(out direction))
            {
                // 全ポイントが同じ位置でも停止せず、一度前進して抜け出す。
                direction = transform.forward;
                direction.y = 0f;
            }

            if (direction.sqrMagnitude < 0.001f)
                direction = Vector3.forward;

            Vector3 desiredDirection = direction.normalized;
            Vector3 moveDirection = RotateBody(desiredDirection);
            float directionAlignment =
                Mathf.Clamp01(Vector3.Dot(moveDirection, desiredDirection));
            float facingAmount = Mathf.Lerp(
                Mathf.Clamp(minimumTurningSpeedRatio, 0.05f, 1f),
                1f,
                directionAlignment);
            float currentMoveSpeed = moveSpeed * facingAmount;

            if (m_rigidbody != null && !m_rigidbody.isKinematic)
            {
                Vector3 velocity = moveDirection * currentMoveSpeed;
                velocity.y = m_rigidbody.linearVelocity.y;
                m_rigidbody.linearVelocity = velocity;
            }
            else
            {
                transform.position +=
                    moveDirection * currentMoveSpeed * Time.deltaTime;
            }
        }

        private bool TryGetRouteDirection(out Vector3 direction)
        {
            direction = Vector3.zero;
            int count = GetRoutePointCount();
            float arrivalDistance = Mathf.Max(0.05f, waypointArrivalDistance);
            float arrivalDistanceSqr = arrivalDistance * arrivalDistance;

            // 重複地点や近すぎる地点を同一フレーム内で飛ばす。
            for (int attempt = 0; attempt <= count; attempt++)
            {
                Transform routePoint = GetCurrentRoutePoint();
                if (routePoint == null)
                    return false;

                direction = routePoint.position - transform.position;
                direction.y = 0f;
                if (direction.sqrMagnitude > arrivalDistanceSqr)
                    return true;

                AdvanceRouteIndex();
            }

            return false;
        }

        private Vector3 RotateBody(Vector3 direction)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                targetRotation,
                rotateSpeed * Time.deltaTime);

            Vector3 facingDirection = transform.forward;
            facingDirection.y = 0f;
            return facingDirection.sqrMagnitude >= 0.001f
                ? facingDirection.normalized
                : direction;
        }

        private void AdvanceRouteIndex()
        {
            Transform currentPoint = GetRoutePoint(m_routeIndex);
            TitleCPURoutePoint graphPoint =
                currentPoint != null ? currentPoint.GetComponent<TitleCPURoutePoint>() : null;
            if (TrySelectConnectedRoutePoint(graphPoint, currentPoint))
                return;

            int count = GetRoutePointCount();
            if (count <= 0)
                return;

            m_previousRoutePoint = currentPoint;
            for (int i = 0; i < count; i++)
            {
                m_routeIndex = (m_routeIndex + 1) % count;
                if (GetRoutePoint(m_routeIndex) != null)
                    return;
            }
        }

        private bool TrySelectConnectedRoutePoint(
            TitleCPURoutePoint graphPoint,
            Transform currentPoint)
        {
            if (graphPoint == null)
                return false;

            m_routeCandidates.Clear();
            for (int i = 0; i < graphPoint.ConnectionCount; i++)
            {
                TitleCPURoutePoint connection = graphPoint.GetConnection(i);
                AddRouteCandidate(
                    connection != null ? connection.transform : null,
                    currentPoint);
            }

            // Prefabの子を増減してもつながるよう、Hierarchy上の前後を自動接続する。
            int currentIndex = FindRoutePointIndex(currentPoint);
            AddSequentialRouteCandidate(currentIndex, -1, currentPoint);
            AddSequentialRouteCandidate(currentIndex, 1, currentPoint);

            if (m_routeCandidates.Count <= 0)
                return false;

            bool canAvoidBacktracking =
                avoidImmediateBacktracking &&
                m_routeCandidates.Count > 1 &&
                m_routeCandidates.Exists(point => point != m_previousRoutePoint);
            int candidateCount = 0;
            for (int i = 0; i < m_routeCandidates.Count; i++)
            {
                if (!canAvoidBacktracking ||
                    m_routeCandidates[i] != m_previousRoutePoint)
                {
                    candidateCount++;
                }
            }

            int selectedCandidate = Random.Range(0, candidateCount);
            for (int i = 0; i < m_routeCandidates.Count; i++)
            {
                Transform candidate = m_routeCandidates[i];
                if (canAvoidBacktracking && candidate == m_previousRoutePoint)
                    continue;

                if (selectedCandidate-- > 0)
                    continue;

                m_previousRoutePoint = currentPoint;
                m_routeIndex = FindRoutePointIndex(candidate);
                return true;
            }

            return false;
        }

        private void AddSequentialRouteCandidate(
            int currentIndex,
            int offset,
            Transform currentPoint)
        {
            int count = GetRoutePointCount();
            if (currentIndex < 0 || count <= 1)
                return;

            int index = (currentIndex + offset + count) % count;
            AddRouteCandidate(GetRoutePoint(index), currentPoint);
        }

        private void AddRouteCandidate(Transform point, Transform currentPoint)
        {
            point = FindNearestIntermediatePoint(currentPoint, point);
            if (point == null ||
                point == currentPoint ||
                FindRoutePointIndex(point) < 0 ||
                m_routeCandidates.Contains(point))
            {
                return;
            }

            m_routeCandidates.Add(point);
        }

        private Transform FindNearestIntermediatePoint(
            Transform startPoint,
            Transform endPoint)
        {
            if (startPoint == null || endPoint == null)
                return endPoint;

            Vector3 start = startPoint.position;
            Vector3 segment = endPoint.position - start;
            start.y = 0f;
            segment.y = 0f;
            float segmentLengthSqr = segment.sqrMagnitude;
            if (segmentLengthSqr < 0.001f)
                return endPoint;

            Transform nearestPoint = endPoint;
            float nearestProgress = 1f;
            float snapDistanceSqr =
                routeSegmentSnapDistance * routeSegmentSnapDistance;
            int count = GetRoutePointCount();
            for (int i = 0; i < count; i++)
            {
                Transform candidate = GetRoutePoint(i);
                if (candidate == null ||
                    candidate == startPoint ||
                    candidate == endPoint)
                {
                    continue;
                }

                Vector3 candidatePosition = candidate.position;
                candidatePosition.y = 0f;
                float progress = Vector3.Dot(
                    candidatePosition - start,
                    segment) / segmentLengthSqr;
                if (progress <= 0.001f || progress >= nearestProgress)
                    continue;

                Vector3 closestPosition = start + segment * progress;
                if ((candidatePosition - closestPosition).sqrMagnitude >
                    snapDistanceSqr)
                {
                    continue;
                }

                nearestProgress = progress;
                nearestPoint = candidate;
            }

            return nearestPoint;
        }

        private int FindRoutePointIndex(Transform point)
        {
            int count = GetRoutePointCount();
            for (int i = 0; i < count; i++)
            {
                if (GetRoutePoint(i) == point)
                    return i;
            }

            return -1;
        }

        private Transform GetCurrentRoutePoint()
        {
            int count = GetRoutePointCount();
            if (count <= 0)
                return null;

            m_routeIndex = Mathf.Clamp(m_routeIndex, 0, count - 1);
            Transform point = GetRoutePoint(m_routeIndex);
            if (point != null)
                return point;

            AdvanceRouteIndex();
            return GetRoutePoint(m_routeIndex);
        }

        private int GetRoutePointCount()
        {
            if (routePoints != null && routePoints.Length > 0)
                return routePoints.Length;

            return routeRoot != null ? routeRoot.childCount : 0;
        }

        private Transform GetRoutePoint(int index)
        {
            if (routePoints != null && routePoints.Length > 0)
                return index >= 0 && index < routePoints.Length ? routePoints[index] : null;

            return routeRoot != null && index >= 0 && index < routeRoot.childCount
                ? routeRoot.GetChild(index)
                : null;
        }

        private void UpdateTarget()
        {
            m_targetSearchTimer -= Time.deltaTime;
            if (m_targetSearchTimer > 0f && IsTargetInRange(m_target))
                return;

            RefreshTarget();
        }

        private void RefreshTarget()
        {
            m_targetSearchTimer = Mathf.Max(0.05f, targetSearchInterval);
            m_target = null;
            float nearestDistanceSqr = attackRange * attackRange;

            m_checkedTargets.Clear();
            int hitCount = CollectTargetHits();
            for (int i = 0; i < hitCount; i++)
            {
                Collider hit = m_targetHits[i];
                if (hit == null || IsOwnCollider(hit))
                    continue;

                Transform candidate = GetKnownCombatTarget(hit);
                TrySelectTarget(candidate, ref nearestDistanceSqr);
            }

            if (additionalTargets == null)
                return;

            for (int i = 0; i < additionalTargets.Length; i++)
                TrySelectTarget(additionalTargets[i], ref nearestDistanceSqr);
        }

        private int CollectTargetHits()
        {
            float range = Mathf.Max(0f, attackRange);
            while (true)
            {
                int hitCount = Physics.OverlapSphereNonAlloc(
                    transform.position,
                    range,
                    m_targetHits,
                    targetMask,
                    QueryTriggerInteraction.Collide);

                if (hitCount < m_targetHits.Length)
                    return hitCount;

                System.Array.Resize(ref m_targetHits, m_targetHits.Length * 2);
            }
        }

        private Transform GetKnownCombatTarget(Collider hit)
        {
            if (targetPlayers)
            {
                PlayerEntity player = hit.GetComponentInParent<PlayerEntity>();
                if (player != null && !player.IsRespawning && player.CurrentHealth > 0f)
                    return player.transform;
            }

            if (targetEnemies)
            {
                EnemyEntity enemy = hit.GetComponentInParent<EnemyEntity>();
                if (enemy != null && !enemy.IsDestroy && enemy.CurrentHealth > 0f)
                    return enemy.transform;
            }

            if (targetBattleCpus)
            {
                EnemyCPUEntity cpu = hit.GetComponentInParent<EnemyCPUEntity>();
                if (cpu != null && cpu.CPUCurrentHealth > 0f)
                    return cpu.transform;
            }

            if (targetTitleCpus)
            {
                TitleCPUEntity titleCpu = hit.GetComponentInParent<TitleCPUEntity>();
                if (titleCpu != null && titleCpu != this)
                    return titleCpu.transform;
            }

            return null;
        }

        private void TrySelectTarget(Transform candidate, ref float nearestDistanceSqr)
        {
            if (candidate == null ||
                candidate.root == transform.root ||
                !candidate.gameObject.activeInHierarchy ||
                !m_checkedTargets.Add(candidate))
            {
                return;
            }

            Vector3 offset = candidate.position - transform.position;
            float distanceSqr = offset.sqrMagnitude;
            if (distanceSqr > nearestDistanceSqr)
                return;

            nearestDistanceSqr = distanceSqr;
            m_target = candidate;
        }

        private bool IsTargetInRange(Transform candidate)
        {
            if (candidate == null || !candidate.gameObject.activeInHierarchy)
                return false;

            return (candidate.position - transform.position).sqrMagnitude <=
                   attackRange * attackRange;
        }

        private void UpdateTurretAndFire()
        {
            m_fireTimer += Time.deltaTime;
            if (!IsTargetInRange(m_target))
            {
                RotateTurretIdle();
                return;
            }

            Vector3 aimPosition = GetAimPosition(m_target);
            RotateTurretsToward(aimPosition);

            if (m_fireTimer < Mathf.Max(0.05f, m_activeFireInterval))
                return;

            FireAt(aimPosition);
            m_fireTimer = 0f;
        }

        private void RotateTurretsToward(Vector3 aimPosition)
        {
            Transform[] turrets = GetActiveTurretTransforms();
            if (turrets.Length <= 0)
            {
                RotateTransformToward(transform, aimPosition);
                return;
            }

            for (int i = 0; i < turrets.Length; i++)
                RotateTransformToward(turrets[i], aimPosition);
        }

        private void RotateTransformToward(
            Transform rotationTarget,
            Vector3 aimPosition)
        {
            if (rotationTarget == null)
                return;

            Vector3 aimDirection = aimPosition - rotationTarget.position;
            aimDirection.y = 0f;
            if (aimDirection.sqrMagnitude < 0.001f)
                return;

            Quaternion targetRotation =
                Quaternion.LookRotation(aimDirection.normalized, Vector3.up);
            rotationTarget.rotation = Quaternion.RotateTowards(
                rotationTarget.rotation,
                targetRotation,
                turretRotateSpeed * Time.deltaTime);
        }

        private void RotateTurretIdle()
        {
            Transform[] turrets = GetActiveTurretTransforms();
            if (turrets.Length <= 0)
                return;

            Vector3 baseDirection = transform.forward;
            baseDirection.y = 0f;
            if (baseDirection.sqrMagnitude < 0.001f)
                return;

            float sweepYaw = Mathf.Sin(
                Time.time * idleTurretSweepSpeed + m_idleTurretPhase) *
                idleTurretSweepAngle;
            Vector3 sweepDirection =
                Quaternion.AngleAxis(sweepYaw, Vector3.up) *
                baseDirection.normalized;
            Quaternion targetRotation =
                Quaternion.LookRotation(sweepDirection, Vector3.up);
            for (int i = 0; i < turrets.Length; i++)
            {
                Transform turret = turrets[i];
                if (turret == null)
                    continue;

                turret.rotation = Quaternion.RotateTowards(
                    turret.rotation,
                    targetRotation,
                    turretRotateSpeed * Time.deltaTime);
            }
        }

        private Transform[] GetActiveTurretTransforms()
        {
            if (m_activeTurretTransforms == null)
                m_activeTurretTransforms = CollectTurretTransforms();

            return m_activeTurretTransforms;
        }

        private void FireAt(Vector3 aimPosition)
        {
            Transform muzzle = GetNextMuzzle();
            if (m_activeBullet == null || muzzle == null)
                return;

            Vector3 direction = aimPosition - muzzle.position;
            if (direction.sqrMagnitude < 0.001f)
                return;

            direction.Normalize();
            GameObject projectile = Instantiate(
                m_activeBullet,
                muzzle.position,
                Quaternion.LookRotation(direction, Vector3.up));

            TitleCPUBulletEntity titleBullet =
                projectile.GetComponent<TitleCPUBulletEntity>();
            if (titleBullet == null)
            {
                EnemyCPUBulletEntity battleBullet =
                    projectile.GetComponent<EnemyCPUBulletEntity>();
                if (battleBullet != null)
                    battleBullet.enabled = false;

                GatlingBulletEntity gatlingBullet =
                    projectile.GetComponent<GatlingBulletEntity>();
                if (gatlingBullet != null)
                    gatlingBullet.enabled = false;

                MissileBulletEntity missileProjectile =
                    projectile.GetComponent<MissileBulletEntity>();
                if (missileProjectile != null)
                    missileProjectile.enabled = false;

                titleBullet = projectile.AddComponent<TitleCPUBulletEntity>();
            }

            float speed = Mathf.Max(0f, m_activeProjectileSpeed);
            titleBullet.SetSpeed(speed);
            titleBullet.Setup(this);

            Rigidbody projectileBody = projectile.GetComponent<Rigidbody>();
            if (projectileBody != null)
                projectileBody.linearVelocity = direction * speed;

            IgnoreOwnerCollisions(projectile);
        }

        private Transform GetNextMuzzle()
        {
            if (m_activeMuzzles == null || m_activeMuzzles.Length <= 0)
                return enemyMuzzleTransform;

            for (int i = 0; i < m_activeMuzzles.Length; i++)
            {
                int index = m_nextMuzzleIndex % m_activeMuzzles.Length;
                m_nextMuzzleIndex = (index + 1) % m_activeMuzzles.Length;
                if (m_activeMuzzles[index] != null)
                    return m_activeMuzzles[index];
            }

            return enemyMuzzleTransform;
        }

        private void IgnoreOwnerCollisions(GameObject projectile)
        {
            if (projectile == null || m_ownColliders == null)
                return;

            Collider[] projectileColliders =
                projectile.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < m_ownColliders.Length; i++)
            {
                Collider ownCollider = m_ownColliders[i];
                if (ownCollider == null)
                    continue;

                for (int j = 0; j < projectileColliders.Length; j++)
                {
                    if (projectileColliders[j] != null)
                        Physics.IgnoreCollision(ownCollider, projectileColliders[j], true);
                }
            }
        }

        private Vector3 GetAimPosition(Transform target)
        {
            Collider targetCollider = target != null
                ? target.GetComponentInChildren<Collider>()
                : null;
            return targetCollider != null ? targetCollider.bounds.center : target.position;
        }

        private bool IsOwnCollider(Collider hit)
        {
            return hit != null && hit.transform.root == transform.root;
        }

        private void StopHorizontalVelocity()
        {
            if (m_rigidbody == null || m_rigidbody.isKinematic)
                return;

            Vector3 velocity = m_rigidbody.linearVelocity;
            velocity.x = 0f;
            velocity.z = 0f;
            m_rigidbody.linearVelocity = velocity;
        }

        // TitleCPUは演出専用で、ダメージによって破壊されない。
        public void Damage(float _) { }
    }
}
