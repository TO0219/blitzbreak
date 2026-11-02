using System.Collections.Generic;
using UnityEngine;

namespace MyGame
{
    /// <summary>
    /// タイトル背景専用の飛行モデル。戦闘処理を持たず、指定ルートだけを巡回する。
    /// </summary>
    public sealed class TitleAircraftEntity : MonoBehaviour
    {
        [Header("Route")]
        [SerializeField] private Transform routeRoot;
        [SerializeField] private Transform[] routePoints;
        [SerializeField, Min(0)] private int startRouteIndex;
        [SerializeField] private bool startAtClosestRoutePoint = true;
        [SerializeField, Min(0.1f)] private float waypointArrivalDistance = 1.5f;
        [SerializeField] private bool avoidImmediateBacktracking = true;

        [Header("Flight")]
        [SerializeField, Min(0f)] private float moveSpeed = 10f;
        [SerializeField, Min(0f)] private float rotateSpeed = 120f;
        [SerializeField, Range(0f, 60f)] private float maxBankAngle = 20f;
        [SerializeField, Min(0.1f)] private float turnAnticipationDistance = 12f;

        [Header("Player Air Model")]
        [SerializeField] private Transform[] visualModels;
        [SerializeField] private RuntimeAnimatorController[] animatorControllers;
        [SerializeField, Min(0)] private int startingModelIndex;
        [SerializeField] private bool changeModelEachLap = true;
        [Tooltip("この番号のルートポイントへ到着した時にモデルを変更します。0は最初のポイントです。")]
        [SerializeField, Min(0)] private int modelChangeRouteIndex;
        [SerializeField, Min(0f)] private float animationSpeed = 1f;

        private readonly List<Transform> m_candidates = new List<Transform>();
        private int m_routeIndex;
        private Transform m_previousPoint;
        private int m_activeModelIndex;
        private int m_fighterModelIndex = -1;
        private TrailRenderer[] m_fighterTrails;
        private GameObject[] m_fighterVerniers;
        private static Material s_airTrailMaterial;
        private static Material s_fighterVernierCoreMaterial;
        private static Material s_fighterVernierOuterMaterial;

        private void Awake()
        {
            PrepareVisualModels();
        }

        private void Start()
        {
            InitializeRouteIndex();
        }

        private void Update()
        {
            MoveAlongRoute();
        }

        private void PrepareVisualModels()
        {
            if (visualModels == null || visualModels.Length <= 0)
                return;

            for (int i = 0; i < visualModels.Length; i++)
                PrepareVisualModel(i);

            PrepareFighterEffects();

            m_activeModelIndex = Mathf.Clamp(
                startingModelIndex,
                0,
                visualModels.Length - 1);
            SetActiveModel(m_activeModelIndex);
        }

        private void PrepareVisualModel(int index)
        {
            Transform model = visualModels[index];
            if (model == null)
                return;

            Collider[] colliders =
                model.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
                colliders[i].enabled = false;

            RuntimeAnimatorController controller =
                animatorControllers != null &&
                index < animatorControllers.Length
                    ? animatorControllers[index]
                    : null;
            Animator animator = model.GetComponent<Animator>();
            if (animator == null)
                animator = model.GetComponentInChildren<Animator>(true);
            if (animator == null && controller != null)
                animator = model.gameObject.AddComponent<Animator>();

            if (animator == null)
                return;

            if (controller != null)
                animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.speed = animationSpeed;
            animator.enabled = true;
        }

        private void SetActiveModel(int activeIndex)
        {
            for (int i = 0; i < visualModels.Length; i++)
            {
                if (visualModels[i] != null)
                    visualModels[i].gameObject.SetActive(i == activeIndex);
            }

            SetFighterEffectsActive(activeIndex == m_fighterModelIndex);
        }

        private void PrepareFighterEffects()
        {
            m_fighterModelIndex = FindFighterModelIndex();
            if (m_fighterModelIndex < 0)
                return;

            Transform fighterRoot = visualModels[m_fighterModelIndex];
            Vector3 trailCenter = new Vector3(0f, 0f, -0.435f);
            m_fighterTrails = new[]
            {
                CreateAirTrail(
                    fighterRoot,
                    "AirMovementTrail_L",
                    trailCenter + Vector3.left * 0.35f),
                CreateAirTrail(
                    fighterRoot,
                    "AirMovementTrail_R",
                    trailCenter + Vector3.right * 0.35f)
            };

            Transform leftVernier = GetOrCreateChild(
                fighterRoot,
                "Vernier_L",
                new Vector3(-0.087f, 0f, -0.47f));
            Transform rightVernier = GetOrCreateChild(
                fighterRoot,
                "Vernier_R",
                new Vector3(0.088f, 0f, -0.47f));
            m_fighterVerniers = new[]
            {
                CreateFighterVernier(leftVernier),
                CreateFighterVernier(rightVernier)
            };
        }

        private int FindFighterModelIndex()
        {
            for (int i = 0; i < visualModels.Length; i++)
            {
                if (visualModels[i] != null &&
                    visualModels[i].name == "Fighter")
                {
                    return i;
                }
            }

            return -1;
        }

        private static Transform GetOrCreateChild(
            Transform parent,
            string childName,
            Vector3 localPosition)
        {
            Transform child = parent.Find(childName);
            if (child == null)
            {
                GameObject childObject = new GameObject(childName);
                child = childObject.transform;
                child.SetParent(parent, false);
            }

            child.localPosition = localPosition;
            child.localRotation = Quaternion.identity;
            return child;
        }

        private static TrailRenderer CreateAirTrail(
            Transform fighterRoot,
            string trailName,
            Vector3 localPosition)
        {
            Transform trailTransform = GetOrCreateChild(
                fighterRoot,
                trailName,
                localPosition);
            TrailRenderer trail =
                trailTransform.GetComponent<TrailRenderer>();
            if (trail == null)
            {
                trail = trailTransform.gameObject
                    .AddComponent<TrailRenderer>();
            }

            trail.time = 0.4f;
            trail.widthMultiplier = 0.08f;
            trail.widthCurve = new AnimationCurve(
                new Keyframe(0f, 1f),
                new Keyframe(1f, 0f));
            trail.startColor = new Color(0.62f, 0.82f, 1f, 0.42f);
            trail.endColor = new Color(0.72f, 0.5f, 1f, 0f);
            trail.minVertexDistance = 0.05f;
            trail.numCornerVertices = 2;
            trail.numCapVertices = 2;
            trail.alignment = LineAlignment.View;
            trail.textureMode = LineTextureMode.Stretch;
            trail.shadowCastingMode =
                UnityEngine.Rendering.ShadowCastingMode.Off;
            trail.receiveShadows = false;
            trail.sharedMaterial = GetAirTrailMaterial();
            trail.emitting = false;
            trail.Clear();
            return trail;
        }

        private static Material GetAirTrailMaterial()
        {
            if (s_airTrailMaterial != null)
                return s_airTrailMaterial;

            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null)
                return null;

            s_airTrailMaterial = new Material(shader)
            {
                name = "Title Fighter Air Trail",
                hideFlags = HideFlags.HideAndDontSave
            };
            return s_airTrailMaterial;
        }

        private static GameObject CreateFighterVernier(Transform vernierRoot)
        {
            Transform existingVisual = vernierRoot.Find("VernierVisual");
            if (existingVisual != null)
                return existingVisual.gameObject;

            GameObject visualRoot = new GameObject("VernierVisual");
            visualRoot.transform.SetParent(vernierRoot, false);
            CreateFighterVernierCone(
                visualRoot.transform,
                "OuterFlame",
                0.22f,
                0.038f,
                GetFighterVernierMaterial(false));
            CreateFighterVernierCone(
                visualRoot.transform,
                "CoreFlame",
                0.13f,
                0.018f,
                GetFighterVernierMaterial(true));
            visualRoot.SetActive(false);
            return visualRoot;
        }

        private static void CreateFighterVernierCone(
            Transform parent,
            string objectName,
            float length,
            float radius,
            Material material)
        {
            const int segments = 16;
            GameObject coneObject = new GameObject(objectName);
            coneObject.transform.SetParent(parent, false);

            Vector3[] vertices = new Vector3[segments + 2];
            int[] triangles = new int[segments * 6];
            vertices[0] = Vector3.back * length;
            vertices[1] = Vector3.zero;

            for (int i = 0; i < segments; i++)
            {
                float angle = Mathf.PI * 2f * i / segments;
                vertices[i + 2] = new Vector3(
                    Mathf.Cos(angle) * radius,
                    Mathf.Sin(angle) * radius,
                    0f);
            }

            int triangleIndex = 0;
            for (int i = 0; i < segments; i++)
            {
                int current = i + 2;
                int next = i == segments - 1 ? 2 : current + 1;
                triangles[triangleIndex++] = 0;
                triangles[triangleIndex++] = current;
                triangles[triangleIndex++] = next;
                triangles[triangleIndex++] = 1;
                triangles[triangleIndex++] = next;
                triangles[triangleIndex++] = current;
            }

            Mesh mesh = new Mesh
            {
                name = objectName + "Mesh",
                vertices = vertices,
                triangles = triangles,
                hideFlags = HideFlags.HideAndDontSave
            };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            MeshFilter meshFilter = coneObject.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = mesh;
            MeshRenderer meshRenderer =
                coneObject.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = material;
            meshRenderer.shadowCastingMode =
                UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
        }

        private static Material GetFighterVernierMaterial(bool core)
        {
            Material cachedMaterial = core
                ? s_fighterVernierCoreMaterial
                : s_fighterVernierOuterMaterial;
            if (cachedMaterial != null)
                return cachedMaterial;

            Shader shader = Shader.Find("Particles/Standard Unlit");
            if (shader == null)
                return null;

            Color color = core
                ? new Color(0.92f, 0.98f, 1f, 0.95f)
                : new Color(0.3f, 0.48f, 1f, 0.62f);
            Material material = new Material(shader)
            {
                name = core
                    ? "Title Fighter Vernier Core"
                    : "Title Fighter Vernier Outer",
                color = color,
                hideFlags = HideFlags.HideAndDontSave
            };

            if (material.HasProperty("_Mode"))
                material.SetFloat("_Mode", 2f);
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);
            if (material.HasProperty("_EmissionColor"))
                material.SetColor("_EmissionColor", color);

            if (core)
                s_fighterVernierCoreMaterial = material;
            else
                s_fighterVernierOuterMaterial = material;
            return material;
        }

        private void SetFighterEffectsActive(bool isActive)
        {
            if (m_fighterTrails != null)
            {
                for (int i = 0; i < m_fighterTrails.Length; i++)
                {
                    TrailRenderer trail = m_fighterTrails[i];
                    if (trail == null)
                        continue;

                    trail.emitting = false;
                    trail.Clear();
                    trail.emitting = isActive;
                }
            }

            if (m_fighterVerniers == null)
                return;

            for (int i = 0; i < m_fighterVerniers.Length; i++)
            {
                if (m_fighterVerniers[i] != null)
                    m_fighterVerniers[i].SetActive(isActive);
            }
        }

        private void SwitchToNextModel()
        {
            if (!changeModelEachLap ||
                visualModels == null ||
                visualModels.Length <= 1)
            {
                return;
            }

            int nextIndex = m_activeModelIndex;
            for (int i = 0; i < visualModels.Length; i++)
            {
                nextIndex = (nextIndex + 1) % visualModels.Length;
                if (visualModels[nextIndex] != null)
                {
                    m_activeModelIndex = nextIndex;
                    SetActiveModel(m_activeModelIndex);
                    return;
                }
            }
        }

        private void InitializeRouteIndex()
        {
            int count = GetRoutePointCount();
            if (count <= 0)
                return;

            m_routeIndex = Mathf.Clamp(startRouteIndex, 0, count - 1);
            if (!startAtClosestRoutePoint)
                return;

            float nearestDistanceSqr = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                Transform point = GetRoutePoint(i);
                if (point == null)
                    continue;

                float distanceSqr =
                    (point.position - transform.position).sqrMagnitude;
                if (distanceSqr >= nearestDistanceSqr)
                    continue;

                nearestDistanceSqr = distanceSqr;
                m_routeIndex = i;
            }
        }

        private void MoveAlongRoute()
        {
            if (moveSpeed <= 0f)
                return;

            Transform point = GetCurrentRoutePoint();
            if (point == null)
                return;

            Vector3 direction = point.position - transform.position;
            float arrivalDistance = Mathf.Max(
                Mathf.Max(0.1f, waypointArrivalDistance),
                turnAnticipationDistance);
            int attempts = GetRoutePointCount();
            while (direction.sqrMagnitude <= arrivalDistance * arrivalDistance &&
                   attempts-- > 0)
            {
                AdvanceRoute();
                point = GetCurrentRoutePoint();
                if (point == null)
                    return;
                direction = point.position - transform.position;
            }

            if (direction.sqrMagnitude < 0.001f)
                return;

            Vector3 moveDirection = direction.normalized;
            RotateAircraft(moveDirection);

            // 経路点が切り替わっても進路が折れないよう、滑らかに旋回した機首方向へ進む。
            Vector3 flightDirection = rotateSpeed > 0f
                ? transform.forward
                : moveDirection;
            transform.position +=
                flightDirection.normalized * moveSpeed * Time.deltaTime;
        }

        private void RotateAircraft(Vector3 direction)
        {
            Vector3 flatCurrent =
                Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            Vector3 flatTarget =
                Vector3.ProjectOnPlane(direction, Vector3.up);
            float bank = 0f;
            if (flatCurrent.sqrMagnitude > 0.001f &&
                flatTarget.sqrMagnitude > 0.001f)
            {
                float yaw = Vector3.SignedAngle(
                    flatCurrent.normalized,
                    flatTarget.normalized,
                    Vector3.up);
                bank = -Mathf.Clamp(yaw / 45f, -1f, 1f) * maxBankAngle;
            }

            Quaternion targetRotation =
                Quaternion.LookRotation(direction, Vector3.up) *
                Quaternion.Euler(0f, 0f, bank);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                targetRotation,
                rotateSpeed * Time.deltaTime);
        }

        private void AdvanceRoute()
        {
            int reachedRouteIndex = m_routeIndex;
            Transform current = GetRoutePoint(m_routeIndex);
            TitleCPURoutePoint graphPoint =
                current != null
                    ? current.GetComponent<TitleCPURoutePoint>()
                    : null;
            if (TrySelectConnectedPoint(graphPoint, current))
            {
                RecordRouteAdvance(reachedRouteIndex);
                return;
            }

            int count = GetRoutePointCount();
            if (count <= 0)
                return;

            m_previousPoint = current;
            for (int i = 0; i < count; i++)
            {
                m_routeIndex = (m_routeIndex + 1) % count;
                if (GetRoutePoint(m_routeIndex) != null)
                {
                    RecordRouteAdvance(reachedRouteIndex);
                    return;
                }
            }
        }

        private void RecordRouteAdvance(int reachedRouteIndex)
        {
            int count = GetRoutePointCount();
            if (!changeModelEachLap || count <= 0)
                return;

            int changeIndex = Mathf.Clamp(
                modelChangeRouteIndex,
                0,
                count - 1);
            if (reachedRouteIndex != changeIndex)
                return;

            SwitchToNextModel();
        }

        private bool TrySelectConnectedPoint(
            TitleCPURoutePoint graphPoint,
            Transform current)
        {
            if (graphPoint == null)
                return false;

            m_candidates.Clear();
            for (int i = 0; i < graphPoint.ConnectionCount; i++)
            {
                TitleCPURoutePoint connection = graphPoint.GetConnection(i);
                AddCandidate(
                    connection != null ? connection.transform : null,
                    current);
            }

            int currentIndex = FindRoutePointIndex(current);
            AddSequentialCandidate(currentIndex, -1, current);
            AddSequentialCandidate(currentIndex, 1, current);
            if (m_candidates.Count <= 0)
                return false;

            bool excludePrevious =
                avoidImmediateBacktracking &&
                m_candidates.Exists(candidate =>
                    candidate != m_previousPoint);
            int selectableCount = 0;
            for (int i = 0; i < m_candidates.Count; i++)
            {
                if (!excludePrevious || m_candidates[i] != m_previousPoint)
                    selectableCount++;
            }

            int selected = Random.Range(0, selectableCount);
            for (int i = 0; i < m_candidates.Count; i++)
            {
                Transform candidate = m_candidates[i];
                if (excludePrevious && candidate == m_previousPoint)
                    continue;
                if (selected-- > 0)
                    continue;

                m_previousPoint = current;
                m_routeIndex = FindRoutePointIndex(candidate);
                return true;
            }

            return false;
        }

        private void AddSequentialCandidate(
            int currentIndex,
            int offset,
            Transform current)
        {
            int count = GetRoutePointCount();
            if (currentIndex < 0 || count <= 1)
                return;

            int index = (currentIndex + offset + count) % count;
            AddCandidate(GetRoutePoint(index), current);
        }

        private void AddCandidate(Transform point, Transform current)
        {
            if (point == null ||
                point == current ||
                FindRoutePointIndex(point) < 0 ||
                m_candidates.Contains(point))
            {
                return;
            }

            m_candidates.Add(point);
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

            AdvanceRoute();
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
            {
                return index >= 0 && index < routePoints.Length
                    ? routePoints[index]
                    : null;
            }

            return routeRoot != null &&
                   index >= 0 &&
                   index < routeRoot.childCount
                ? routeRoot.GetChild(index)
                : null;
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
    }
}
