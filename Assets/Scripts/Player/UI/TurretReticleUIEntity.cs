using UnityEngine;
using UnityEngine.UI;

namespace MyGame
{
    [ExecuteAlways]
    public class TurretReticleUIEntity : Entity
    {
        [SerializeField] private RectTransform reticleRoot;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Image horizontalLine;
        [SerializeField] private Image verticalLine;
        [SerializeField] private Image centerDot;
        [SerializeField] private Image hitMarkerSlashA;
        [SerializeField] private Image hitMarkerSlashB;
        [SerializeField] private RectTransform missileLockRingRoot;
        [SerializeField] private Graphic missileLockRing;
        [SerializeField] private RectTransform missileLockTargetBoxRoot;
        [SerializeField] private Color normalColor = new Color(0.18f, 0.95f, 1f, 1f);
        [SerializeField] private Color targetColor = new Color(1f, 0.18f, 0.12f, 1f);
        [SerializeField] private Color lockRingColor = new Color(0.18f, 0.95f, 1f, 0.82f);
        [SerializeField] private Color lockedRingColor = new Color(1f, 0.3f, 0.2f, 0.95f);
        [SerializeField] private Color lockBoxAcquireColor = new Color(0.18f, 0.95f, 1f, 0.95f);
        [SerializeField] private Color lockBoxLockedColor = new Color(1f, 0.22f, 0.16f, 1f);
        [SerializeField] private Color hitMarkerColor = Color.white;
        [SerializeField] private Color defeatedMarkerColor = new Color(1f, 0.25f, 0.2f, 1f);
        [SerializeField] private float hitMarkerDuration = 0.16f;
        [SerializeField] private float missileLockBoxTargetSize = 74f;
        [SerializeField] private float missileLockBoxStartScale = 1.65f;
        [SerializeField] private float missileLockBoxAcquireTime = 0.28f;
        [SerializeField] private float missileLockBoxFollowSpeed = 18f;
        [SerializeField] private float maxDistance = 200f;
        [SerializeField] private float fallbackDistance = 80f;
        [SerializeField] private float screenPadding = 20f;
        [SerializeField] private bool clampToScreen = true;
        [SerializeField] private LayerMask hitMask = ~0;

        private Canvas m_canvas;
        private RectTransform m_canvasRect;
        private float m_hitMarkerRemaining;
        private bool m_defeatedHit;
        private Transform m_missileLockVisualTarget;
        private Vector2 m_missileLockBoxPosition;
        private float m_missileLockBoxAnimT;
        private bool m_missileLockBoxHasPosition;
        private Image[] m_missileLockBoxImages;
        private readonly RaycastHit[] m_raycastHits = new RaycastHit[64];

        public override void InitEntity()
        {
            EnsureLayout();
        }

        public void UpdateReticle(PlayerEntity player)
        {
            EnsureLayout();
            UpdateHitMarker();

            if (player == null)
            {
                SetVisible(false);
                return;
            }

            Camera targetCamera = ResolveCamera(player);
            if (targetCamera == null || !TryGetAimRay(player, out Ray aimRay))
            {
                SetVisible(false);
                return;
            }

            bool showBaseReticle = ShouldShowBaseReticle(player);
            if (!showBaseReticle)
            {
                SetBaseReticleVisible(false);
                SetVisible(true);
                UpdateMissileLockRing(player, targetCamera);
                return;
            }

            bool hasTarget;
            bool hasHit;
            Vector3 worldPoint;
            if (player.TryGetAirAimPoint(out Vector3 airAimPoint, out Collider airHitCollider))
            {
                worldPoint = airAimPoint;
                hasTarget = IsTargetCollider(airHitCollider);
                hasHit = true;
            }
            else
            {
                hasHit = TryGetHitPoint(aimRay, player.transform, out worldPoint, out hasTarget);
            }

            if (!hasHit)
                worldPoint = aimRay.origin + aimRay.direction * fallbackDistance;

            Vector3 screenPoint = targetCamera.WorldToScreenPoint(worldPoint);
            if (screenPoint.z <= 0.01f)
            {
                SetVisible(false);
                return;
            }

            if (!TryResolveCanvas())
            {
                SetVisible(false);
                return;
            }

            Camera uiCamera = m_canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : m_canvas.worldCamera;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(m_canvasRect, screenPoint, uiCamera, out Vector2 localPoint))
            {
                SetVisible(false);
                return;
            }

            if (clampToScreen)
                localPoint = ClampToCanvas(localPoint);

            reticleRoot.anchoredPosition = localPoint;
            SetColor(hasTarget ? targetColor : normalColor);
            SetBaseReticleVisible(true);
            SetVisible(true);
            UpdateMissileLockRing(player, targetCamera);
        }

        public void ShowHitMarker(bool defeated)
        {
            EnsureLayout();
            m_hitMarkerRemaining = Mathf.Max(0.05f, hitMarkerDuration);
            m_defeatedHit = defeated;
            UpdateHitMarker();
        }

        public void SetVisible(bool visible)
        {
            EnsureLayout();

            if (canvasGroup != null)
            {
                canvasGroup.alpha = visible ? 1f : 0f;
                canvasGroup.blocksRaycasts = false;
                canvasGroup.interactable = false;
            }
            else if (reticleRoot != null)
            {
                reticleRoot.gameObject.SetActive(visible);
            }

            if (!visible && missileLockRingRoot != null)
                missileLockRingRoot.gameObject.SetActive(false);
            if (!visible && missileLockTargetBoxRoot != null)
                missileLockTargetBoxRoot.gameObject.SetActive(false);
        }

        private static bool ShouldShowBaseReticle(PlayerEntity player)
        {
            if (player == null)
                return false;

            return !player.IsAirMode ||
                   player.CurrentAirModeStock == 1 ||
                   player.CurrentAirModeStock == 3;
        }

        private void SetBaseReticleVisible(bool visible)
        {
            SetGraphicVisible(horizontalLine, visible);
            SetGraphicVisible(verticalLine, visible);
            SetGraphicVisible(centerDot, visible);
            if (!visible)
                SetHitMarkerActive(false);
        }

        private static void SetGraphicVisible(Graphic graphic, bool visible)
        {
            if (graphic != null && graphic.gameObject.activeSelf != visible)
                graphic.gameObject.SetActive(visible);
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            EnsureLayout();
        }

        private void OnValidate()
        {
            if (!Application.isPlaying)
                EnsureLayout();
        }

        private void EnsureLayout()
        {
            if (reticleRoot == null)
                reticleRoot = GetComponent<RectTransform>();

            if (reticleRoot == null)
                return;

            reticleRoot.anchorMin = new Vector2(0.5f, 0.5f);
            reticleRoot.anchorMax = new Vector2(0.5f, 0.5f);
            reticleRoot.pivot = new Vector2(0.5f, 0.5f);
            reticleRoot.sizeDelta = new Vector2(64f, 64f);

            if (canvasGroup == null)
                canvasGroup = GetComponent<CanvasGroup>();

            horizontalLine = FindLine("TurretReticleHorizontal", new Vector2(52f, 5f), Vector2.zero);

            verticalLine = FindLine("TurretReticleVertical", new Vector2(5f, 52f), Vector2.zero);

            centerDot = FindLine("TurretReticleCenter", new Vector2(10f, 10f), Vector2.zero);

            hitMarkerSlashA = FindLine("HitMarkerSlashA", new Vector2(30f, 3f), Vector2.zero);

            hitMarkerSlashB = FindLine("HitMarkerSlashB", new Vector2(30f, 3f), Vector2.zero);

            SetHitMarkerActive(m_hitMarkerRemaining > 0f);
        }

        private void UpdateHitMarker()
        {
            if (m_hitMarkerRemaining > 0f && Application.isPlaying)
                m_hitMarkerRemaining = Mathf.Max(0f, m_hitMarkerRemaining - Time.deltaTime);

            bool visible = m_hitMarkerRemaining > 0f;
            SetHitMarkerActive(visible);
            if (!visible)
                return;

            Color color = m_defeatedHit ? defeatedMarkerColor : hitMarkerColor;
            if (hitMarkerSlashA != null)
                hitMarkerSlashA.color = color;
            if (hitMarkerSlashB != null)
                hitMarkerSlashB.color = color;

            float progress = 1f - Mathf.Clamp01(m_hitMarkerRemaining / Mathf.Max(0.05f, hitMarkerDuration));
            float scale = Mathf.Lerp(1.25f, 1f, progress);
            if (hitMarkerSlashA != null)
                hitMarkerSlashA.rectTransform.localScale = Vector3.one * scale;
            if (hitMarkerSlashB != null)
                hitMarkerSlashB.rectTransform.localScale = Vector3.one * scale;
        }

        private void SetHitMarkerActive(bool active)
        {
            if (hitMarkerSlashA != null && hitMarkerSlashA.gameObject.activeSelf != active)
                hitMarkerSlashA.gameObject.SetActive(active);
            if (hitMarkerSlashB != null && hitMarkerSlashB.gameObject.activeSelf != active)
                hitMarkerSlashB.gameObject.SetActive(active);
        }

        private void UpdateMissileLockRing(PlayerEntity player, Camera targetCamera)
        {
            if (missileLockRingRoot == null)
                return;

            TurretManagerEntity turretManager =
                player != null ? player.GetComponent<TurretManagerEntity>() : null;
            bool isVisible = turretManager != null && turretManager.IsMissileActive;
            missileLockRingRoot.gameObject.SetActive(isVisible);
            if (!isVisible)
            {
                ResetMissileLockTargetBox();
                return;
            }

            float diameter =
                Screen.height *
                turretManager.MissileLockScreenRadius *
                2f;
            missileLockRingRoot.anchorMin = new Vector2(0.5f, 0.5f);
            missileLockRingRoot.anchorMax = new Vector2(0.5f, 0.5f);
            missileLockRingRoot.pivot = new Vector2(0.5f, 0.5f);
            missileLockRingRoot.anchoredPosition = Vector2.zero;
            missileLockRingRoot.sizeDelta = new Vector2(diameter, diameter);

            if (missileLockRing != null)
            {
                bool hasLock = turretManager.TryGetMissileLockTarget(out _);
                missileLockRing.color =
                    hasLock ? lockedRingColor : lockRingColor;
                missileLockRing.raycastTarget = false;
            }

            UpdateMissileLockTargetBox(turretManager, targetCamera, diameter * 0.5f);
        }

        private void UpdateMissileLockTargetBox(
            TurretManagerEntity turretManager,
            Camera targetCamera,
            float ringRadius)
        {
            CacheMissileLockTargetBox();
            if (missileLockTargetBoxRoot == null ||
                turretManager == null ||
                targetCamera == null ||
                !TryResolveCanvas() ||
                !turretManager.TryGetMissileLockTarget(out Transform target) ||
                target == null)
            {
                ResetMissileLockTargetBox();
                return;
            }

            Vector3 aimPoint = MissileBulletEntity.GetTargetAimPoint(target);
            Vector3 screenPoint = targetCamera.WorldToScreenPoint(aimPoint);
            if (screenPoint.z <= 0.01f)
            {
                ResetMissileLockTargetBox();
                return;
            }

            Camera uiCamera = m_canvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null
                : m_canvas.worldCamera;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    m_canvasRect,
                    screenPoint,
                    uiCamera,
                    out Vector2 targetLocalPoint))
            {
                ResetMissileLockTargetBox();
                return;
            }

            if (m_missileLockVisualTarget != target)
            {
                m_missileLockVisualTarget = target;
                m_missileLockBoxAnimT = 0f;
                m_missileLockBoxHasPosition = false;
            }

            Vector2 reticlePosition = reticleRoot != null
                ? reticleRoot.anchoredPosition
                : Vector2.zero;
            Vector2 toTarget = targetLocalPoint - reticlePosition;
            Vector2 startPosition = reticlePosition;
            if (toTarget.sqrMagnitude > 0.001f)
                startPosition += toTarget.normalized * Mathf.Max(1f, ringRadius);
            else
                startPosition += Vector2.up * Mathf.Max(1f, ringRadius);

            float acquireTime = Mathf.Max(0.01f, missileLockBoxAcquireTime);
            m_missileLockBoxAnimT = Mathf.Clamp01(
                m_missileLockBoxAnimT + Time.deltaTime / acquireTime);
            float eased = SmoothStep01(m_missileLockBoxAnimT);
            Vector2 acquirePosition = Vector2.Lerp(startPosition, targetLocalPoint, eased);

            if (!m_missileLockBoxHasPosition)
            {
                m_missileLockBoxPosition = acquirePosition;
                m_missileLockBoxHasPosition = true;
            }
            else if (m_missileLockBoxAnimT < 1f)
            {
                m_missileLockBoxPosition = acquirePosition;
            }
            else
            {
                float followRate = 1f - Mathf.Exp(
                    -Mathf.Max(0.01f, missileLockBoxFollowSpeed) * Time.deltaTime);
                m_missileLockBoxPosition = Vector2.Lerp(
                    m_missileLockBoxPosition,
                    targetLocalPoint,
                    followRate);
            }

            float startSize =
                Mathf.Max(1f, missileLockBoxTargetSize) *
                Mathf.Max(1f, missileLockBoxStartScale);
            float size = Mathf.Lerp(
                startSize,
                Mathf.Max(1f, missileLockBoxTargetSize),
                eased);

            missileLockTargetBoxRoot.gameObject.SetActive(true);
            missileLockTargetBoxRoot.anchorMin = new Vector2(0.5f, 0.5f);
            missileLockTargetBoxRoot.anchorMax = new Vector2(0.5f, 0.5f);
            missileLockTargetBoxRoot.pivot = new Vector2(0.5f, 0.5f);
            missileLockTargetBoxRoot.anchoredPosition = m_missileLockBoxPosition;
            missileLockTargetBoxRoot.sizeDelta = new Vector2(size, size);

            bool locked = m_missileLockBoxAnimT >= 0.995f;
            SetMissileLockTargetBoxColor(locked ? lockBoxLockedColor : lockBoxAcquireColor);
        }

        private void ResetMissileLockTargetBox()
        {
            m_missileLockVisualTarget = null;
            m_missileLockBoxAnimT = 0f;
            m_missileLockBoxHasPosition = false;
            if (missileLockTargetBoxRoot != null)
                missileLockTargetBoxRoot.gameObject.SetActive(false);
        }

        private void CacheMissileLockTargetBox()
        {
            if (missileLockTargetBoxRoot == null && reticleRoot != null)
            {
                Transform existing = reticleRoot.parent != null
                    ? reticleRoot.parent.Find("MissileLockTargetBoxUI")
                    : reticleRoot.Find("MissileLockTargetBoxUI");
                missileLockTargetBoxRoot = existing as RectTransform;
            }

            if (missileLockTargetBoxRoot == null)
                return;

            if (m_missileLockBoxImages == null || m_missileLockBoxImages.Length == 0)
            {
                m_missileLockBoxImages = missileLockTargetBoxRoot
                    .GetComponentsInChildren<Image>(true);
            }
        }

        private void SetMissileLockTargetBoxColor(Color color)
        {
            if (m_missileLockBoxImages == null)
                return;

            for (int i = 0; i < m_missileLockBoxImages.Length; i++)
            {
                if (m_missileLockBoxImages[i] != null)
                    m_missileLockBoxImages[i].color = color;
            }
        }

        private static float SmoothStep01(float value)
        {
            value = Mathf.Clamp01(value);
            return value * value * (3f - 2f * value);
        }

        private Image FindLine(string objectName, Vector2 size, Vector2 position)
        {
            Transform existing = reticleRoot.Find(objectName);
            if (existing == null)
                return null;

            RectTransform rect = existing.GetComponent<RectTransform>();
            if (rect != null)
            {
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = size;
                rect.anchoredPosition = position;
            }

            Image image = existing.GetComponent<Image>();
            if (image != null)
                image.raycastTarget = false;
            return image;
        }

        private bool TryGetAimRay(PlayerEntity player, out Ray aimRay)
        {
            if (player.TryGetAirAimRay(out aimRay))
                return true;

            if (player.MuzzleTransform != null)
            {
                aimRay = new Ray(player.MuzzleTransform.position, player.MuzzleTransform.forward);
                return true;
            }

            if (player.TurretTransform != null)
            {
                aimRay = new Ray(player.TurretTransform.position, player.TurretTransform.forward);
                return true;
            }

            TurretManagerEntity turretManager = player.GetComponent<TurretManagerEntity>();
            if (turretManager != null && turretManager.TryGetAimRay(out aimRay))
                return true;

            aimRay = default;
            return false;
        }

        private bool TryGetHitPoint(Ray aimRay, Transform ownerRoot, out Vector3 hitPoint, out bool hasTarget)
        {
            int hitCount = Physics.RaycastNonAlloc(
                aimRay,
                m_raycastHits,
                maxDistance,
                hitMask,
                QueryTriggerInteraction.Ignore);
            float closestDistance = float.MaxValue;
            bool found = false;
            hitPoint = default;
            hasTarget = false;

            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = m_raycastHits[i];
                Collider hitCollider = hit.collider;
                if (hitCollider == null) continue;
                if (ownerRoot != null && hitCollider.transform.IsChildOf(ownerRoot)) continue;
                if (hit.distance >= closestDistance) continue;

                closestDistance = hit.distance;
                hitPoint = hit.point;
                hasTarget = IsTargetCollider(hitCollider);
                found = true;
            }

            return found;
        }

        private bool IsTargetCollider(Collider hitCollider)
        {
            if (hitCollider == null)
                return false;

            return hitCollider.GetComponentInParent<EnemyEntity>() != null ||
                   hitCollider.GetComponentInParent<EnemyCPUEntity>() != null ||
                   hitCollider.GetComponentInParent<PlayerEntity>() != null;
        }

        private Camera ResolveCamera(PlayerEntity player)
        {
            if (player.CameraTransform != null)
            {
                Camera playerCamera = player.CameraTransform.GetComponent<Camera>();
                if (playerCamera != null && playerCamera.isActiveAndEnabled)
                    return playerCamera;
            }

            Camera childCamera = player.GetComponentInChildren<Camera>(true);
            if (childCamera != null && childCamera.isActiveAndEnabled)
                return childCamera;

            return Camera.main;
        }

        private bool TryResolveCanvas()
        {
            if (m_canvas == null)
                m_canvas = GetComponentInParent<Canvas>();

            if (m_canvas == null)
                m_canvas = FindAnyObjectByType<Canvas>();

            if (m_canvas == null) return false;

            m_canvasRect = m_canvas.transform as RectTransform;
            return m_canvasRect != null;
        }

        private Vector2 ClampToCanvas(Vector2 localPoint)
        {
            Rect rect = m_canvasRect.rect;
            float minX = rect.xMin + screenPadding;
            float maxX = rect.xMax - screenPadding;
            float minY = rect.yMin + screenPadding;
            float maxY = rect.yMax - screenPadding;

            return new Vector2(
                Mathf.Clamp(localPoint.x, minX, maxX),
                Mathf.Clamp(localPoint.y, minY, maxY)
            );
        }

        private void SetColor(Color color)
        {
            if (horizontalLine != null)
                horizontalLine.color = color;

            if (verticalLine != null)
                verticalLine.color = color;

            if (centerDot != null)
                centerDot.color = color;
        }
    }
}
