using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

namespace MyGame
{
    public class NearbyThreatIndicatorUIEntity : MaskableGraphic
    {
        [SerializeField] private float detectionRadius = 30f;
        [SerializeField] private float scanInterval = 0.15f;
        [SerializeField] private float indicatorRadius = 260f;
        [SerializeField] private float damageIndicatorRadius = 330f;
        [SerializeField] private Vector2 threatIndicatorSize = new(46f, 46f);
        [SerializeField] private Vector2 damageIndicatorSize = new(82f, 62f);
        [SerializeField] private float positionLerp = 18f;
        [SerializeField] private float screenEdgePadding = 64f;
        [SerializeField] private Color borderColor = new(0.2f, 1f, 0.25f, 1f);
        [SerializeField] private Color warningColor = new(1f, 0.18f, 0.16f, 1f);
        [SerializeField] private Color damageOuterColor = new(1f, 0.08f, 0.02f, 1f);
        [SerializeField] private Color damageInnerColor = new(1f, 0.82f, 0.16f, 1f);
        [SerializeField] private float pulseSpeed = 5f;
        [SerializeField] private float damageDirectionDuration = 1.2f;

        private PlayerEntity m_localPlayer;
        private bool m_hasNearbyThreat;
        private float m_scanTimer;
        private Vector3 m_nearestThreatPosition;
        private float m_damageDirectionRemaining;
        private Vector3 m_damageSourcePosition;
        private readonly List<EnemyCPUEntity> m_cachedCpus = new(4);
        private readonly List<PlayerEntity> m_cachedPlayers = new(4);

        protected override void Awake()
        {
            base.Awake();
            raycastTarget = false;
            NormalizeRectTransform();
        }

        private void Update()
        {
            if (m_damageDirectionRemaining > 0f)
            {
                m_damageDirectionRemaining = Mathf.Max(
                    0f,
                    m_damageDirectionRemaining - Time.deltaTime);

                if (m_damageDirectionRemaining <= 0f)
                {
                    m_hasNearbyThreat = false;
                    m_scanTimer = 0f;
                    SetVerticesDirty();
                    return;
                }

                m_nearestThreatPosition = m_damageSourcePosition;
                m_hasNearbyThreat = true;
                UpdateIndicatorTransform();
                SetVerticesDirty();
                return;
            }

            m_scanTimer -= Time.deltaTime;
            if (m_scanTimer <= 0f)
            {
                m_scanTimer = scanInterval;
                bool foundThreat = HasThreatNearby();
                if (m_hasNearbyThreat != foundThreat)
                {
                    m_hasNearbyThreat = foundThreat;
                    SetVerticesDirty();
                }
            }

            if (m_hasNearbyThreat)
            {
                UpdateIndicatorTransform();
                SetVerticesDirty();
            }
        }

        public void ShowDamageDirection(Vector3 worldSourcePosition)
        {
            if (m_localPlayer == null || !IsLocalOrSoloPlayer(m_localPlayer))
                m_localPlayer = FindLocalPlayer();

            m_damageSourcePosition = worldSourcePosition;
            m_nearestThreatPosition = worldSourcePosition;
            m_damageDirectionRemaining = Mathf.Max(0.1f, damageDirectionDuration);
            m_hasNearbyThreat = m_localPlayer != null;
            if (m_hasNearbyThreat)
                UpdateIndicatorTransform();
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vertexHelper)
        {
            vertexHelper.Clear();
            if (!m_hasNearbyThreat)
                return;

            Rect rect = GetPixelAdjustedRect();
            float pulse = 0.72f + Mathf.Sin(Time.time * pulseSpeed) * 0.28f;

            if (IsShowingDamageDirection())
            {
                float damagePulse = 0.82f + Mathf.Sin(Time.time * pulseSpeed * 1.45f) * 0.18f;
                Color outerDamageColor = damageOuterColor;
                outerDamageColor.a *= damagePulse;
                Color innerDamageColor = damageInnerColor;
                innerDamageColor.a *= damagePulse;

                AddDamageChevron(vertexHelper, rect, 8f, outerDamageColor);
                AddDamageChevron(vertexHelper, Shrink(rect, 12f), 4f, innerDamageColor);
                return;
            }

            Color outerColor = borderColor;
            outerColor.a *= pulse;
            Color innerColor = warningColor;
            innerColor.a *= pulse;

            AddWarningTriangle(vertexHelper, rect, outerColor);
            AddWarningTriangle(vertexHelper, Shrink(rect, 5f), innerColor);
        }

        private bool HasThreatNearby()
        {
            if (m_localPlayer == null || !IsLocalOrSoloPlayer(m_localPlayer))
                m_localPlayer = FindLocalPlayer();

            if (m_localPlayer == null)
                return false;

            Vector3 localPosition = m_localPlayer.transform.position;
            float radiusSqr = detectionRadius * detectionRadius;
            float nearestSqr = float.MaxValue;
            bool foundThreat = false;

            RefreshParticipantCache();

            for (int i = 0; i < m_cachedCpus.Count; i++)
            {
                EnemyCPUEntity cpu = m_cachedCpus[i];
                if (cpu == null || cpu.CPUCurrentHealth <= 0f)
                    continue;

                float distanceSqr = (cpu.transform.position - localPosition).sqrMagnitude;
                if (distanceSqr <= radiusSqr && distanceSqr < nearestSqr)
                {
                    nearestSqr = distanceSqr;
                    m_nearestThreatPosition = cpu.transform.position;
                    foundThreat = true;
                }
            }

            for (int i = 0; i < m_cachedPlayers.Count; i++)
            {
                PlayerEntity player = m_cachedPlayers[i];
                if (player == null || player == m_localPlayer || player.CurrentHealth <= 0f)
                    continue;

                NetworkObject netObj = player.GetComponent<NetworkObject>();
                if (netObj == null || !netObj.IsSpawned || netObj.IsOwner)
                    continue;

                float distanceSqr = (player.transform.position - localPosition).sqrMagnitude;
                if (distanceSqr <= radiusSqr && distanceSqr < nearestSqr)
                {
                    nearestSqr = distanceSqr;
                    m_nearestThreatPosition = player.transform.position;
                    foundThreat = true;
                }
            }

            return foundThreat;
        }

        private void UpdateIndicatorTransform()
        {
            if (m_localPlayer == null)
                return;

            Vector2 direction = GetThreatScreenDirection();
            if (direction.sqrMagnitude <= 0.0001f)
                direction = Vector2.up;

            direction.Normalize();

            bool showingDamage = IsShowingDamageDirection();
            Vector2 indicatorSize = showingDamage ? damageIndicatorSize : threatIndicatorSize;
            Vector2 targetPosition = direction * (showingDamage ? damageIndicatorRadius : indicatorRadius);
            targetPosition = ClampToParentRect(targetPosition, indicatorSize);
            float lerpRate = 1f - Mathf.Exp(-positionLerp * Time.deltaTime);
            rectTransform.anchoredPosition = Vector2.Lerp(rectTransform.anchoredPosition, targetPosition, lerpRate);
            rectTransform.sizeDelta = indicatorSize;

            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
            rectTransform.localRotation = Quaternion.Euler(0f, 0f, angle);
        }

        private Vector2 GetThreatScreenDirection()
        {
            Vector3 toThreat = m_nearestThreatPosition - m_localPlayer.transform.position;
            toThreat.y = 0f;

            if (toThreat.sqrMagnitude <= 0.0001f)
                return Vector2.up;

            Transform basis = m_localPlayer.CameraTransform != null
                ? m_localPlayer.CameraTransform
                : (Camera.main != null ? Camera.main.transform : m_localPlayer.transform);
            Vector3 forward = basis.forward;
            Vector3 right = basis.right;
            forward.y = 0f;
            right.y = 0f;

            if (forward.sqrMagnitude <= 0.0001f || right.sqrMagnitude <= 0.0001f)
            {
                forward = m_localPlayer.transform.forward;
                right = m_localPlayer.transform.right;
                forward.y = 0f;
                right.y = 0f;
            }

            forward.Normalize();
            right.Normalize();

            return new Vector2(
                Vector3.Dot(toThreat.normalized, right),
                Vector3.Dot(toThreat.normalized, forward)
            );
        }

        private PlayerEntity FindLocalPlayer()
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            {
                NetworkObject localPlayerObject = NetworkManager.Singleton.LocalClient?.PlayerObject;
                if (localPlayerObject != null)
                {
                    PlayerEntity localPlayer = localPlayerObject.GetComponent<PlayerEntity>();
                    if (localPlayer != null)
                        return localPlayer;
                }
            }

            RefreshParticipantCache();
            for (int i = 0; i < m_cachedPlayers.Count; i++)
            {
                if (IsLocalOrSoloPlayer(m_cachedPlayers[i]))
                    return m_cachedPlayers[i];
            }

            return null;
        }

        private void NormalizeRectTransform()
        {
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
        }

        private Vector2 ClampToParentRect(Vector2 targetPosition, Vector2 indicatorSize)
        {
            RectTransform parentRect = rectTransform.parent as RectTransform;
            if (parentRect == null)
                return targetPosition;

            Rect rect = parentRect.rect;
            float halfWidth = indicatorSize.x * 0.5f;
            float halfHeight = indicatorSize.y * 0.5f;
            float maxX = Mathf.Max(0f, rect.width * 0.5f - halfWidth - screenEdgePadding);
            float maxY = Mathf.Max(0f, rect.height * 0.5f - halfHeight - screenEdgePadding);

            return new Vector2(
                Mathf.Clamp(targetPosition.x, -maxX, maxX),
                Mathf.Clamp(targetPosition.y, -maxY, maxY));
        }

        private void RefreshParticipantCache()
        {
            if (EntityManager.Instance != null)
            {
                EntityManager.Instance.GetEntities(m_cachedCpus);
                EntityManager.Instance.GetEntities(m_cachedPlayers);
                return;
            }

            m_cachedCpus.Clear();
            EnemyCPUEntity[] cpus = FindObjectsByType<EnemyCPUEntity>();
            for (int i = 0; i < cpus.Length; i++)
                m_cachedCpus.Add(cpus[i]);

            m_cachedPlayers.Clear();
            PlayerEntity[] players = FindObjectsByType<PlayerEntity>();
            for (int i = 0; i < players.Length; i++)
                m_cachedPlayers.Add(players[i]);
        }

        private static bool IsLocalOrSoloPlayer(PlayerEntity player)
        {
            if (player == null)
                return false;

            NetworkObject netObj = player.GetComponent<NetworkObject>();
            return netObj == null || !netObj.IsSpawned || netObj.IsOwner;
        }

        private static Rect Shrink(Rect rect, float amount)
        {
            return new Rect(rect.x + amount, rect.y + amount, rect.width - amount * 2f, rect.height - amount * 2f);
        }

        private static void AddWarningTriangle(VertexHelper vertexHelper, Rect rect, Color color)
        {
            int startIndex = vertexHelper.currentVertCount;
            vertexHelper.AddVert(new Vector3(rect.center.x, rect.yMax), color, new Vector2(0.5f, 1f));
            vertexHelper.AddVert(new Vector3(rect.xMax, rect.yMin), color, new Vector2(1f, 0f));
            vertexHelper.AddVert(new Vector3(rect.xMin, rect.yMin), color, Vector2.zero);
            vertexHelper.AddTriangle(startIndex, startIndex + 1, startIndex + 2);
        }

        private bool IsShowingDamageDirection()
        {
            return m_damageDirectionRemaining > 0f;
        }

        private static void AddDamageChevron(VertexHelper vertexHelper, Rect rect, float thickness, Color color)
        {
            Vector2 apex = new(rect.center.x, rect.yMax);
            Vector2 left = new(rect.xMin, rect.yMin);
            Vector2 right = new(rect.xMax, rect.yMin);

            AddLineQuad(vertexHelper, apex, left, thickness, color);
            AddLineQuad(vertexHelper, apex, right, thickness, color);
        }

        private static void AddLineQuad(VertexHelper vertexHelper, Vector2 start, Vector2 end, float thickness, Color color)
        {
            Vector2 direction = end - start;
            if (direction.sqrMagnitude <= 0.0001f)
                return;

            Vector2 normal = new Vector2(-direction.y, direction.x).normalized * (thickness * 0.5f);
            int startIndex = vertexHelper.currentVertCount;
            vertexHelper.AddVert(start + normal, color, Vector2.zero);
            vertexHelper.AddVert(start - normal, color, Vector2.right);
            vertexHelper.AddVert(end - normal, color, Vector2.one);
            vertexHelper.AddVert(end + normal, color, Vector2.up);
            vertexHelper.AddTriangle(startIndex, startIndex + 1, startIndex + 2);
            vertexHelper.AddTriangle(startIndex, startIndex + 2, startIndex + 3);
        }
    }
}
