using UnityEngine;

namespace MyGame
{
    public class TankTrackAnimationController : MonoBehaviour
    {
        [Header("Track Animation")]
        [SerializeField] private RuntimeAnimatorController trackController;
        [SerializeField] private string trackRootName = "TankTrackMotionBase";
        [SerializeField, Min(0)] private int animationRootParentLevels;
        [SerializeField] private string trackStateName = "TankTrack_Move";
        [SerializeField] private bool scaleSpeedByMovement;
        [SerializeField] private float referenceMoveSpeed = 6f;
        [SerializeField] private float animationSpeed = 1f;
        [SerializeField] private float maxAnimationSpeed = 2f;
        [SerializeField] private float stopMoveDistance = 0.005f;

        private Animator[] m_trackAnimators;
        private Transform[] m_trackRoots;
        private Renderer[] m_trackRenderers;
        private PlayerEntity m_player;
        private EnemyCPUEntity m_cpu;
        private Vector3 m_lastPosition;
        private bool m_hasRendererVisibilityOverride;
        private bool m_rendererVisibilityOverride = true;

        private void Awake()
        {
            m_player = GetComponent<PlayerEntity>();
            m_cpu = GetComponent<EnemyCPUEntity>();
            CacheTrackAnimators();
            m_lastPosition = transform.position;
            PlayTrackAnimationFromStart();
            SetTrackPlaybackSpeed(0f);
        }

        private void OnEnable()
        {
            m_lastPosition = transform.position;
            PlayTrackAnimationFromStart();
        }

        private void OnDisable()
        {
            SetTrackPlaybackSpeed(0f);
        }

        private void LateUpdate()
        {
            if (m_trackAnimators == null || m_trackAnimators.Length == 0)
                CacheTrackAnimators();

            if (ShouldHideTrackRenderers())
            {
                SetTrackPlaybackSpeed(0f);
                ApplyTrackRendererVisibility(false);
                return;
            }

            float deltaTime = Time.deltaTime;
            Vector3 currentPosition = transform.position;
            Vector3 movement = currentPosition - m_lastPosition;
            movement.y = 0f;
            m_lastPosition = currentPosition;

            if (deltaTime <= 0f || ShouldStopTrackAnimation())
            {
                SetTrackPlaybackSpeed(0f);
                ApplyStoredTrackRendererVisibility();
                return;
            }

            float moveDistance = movement.magnitude;
            if (moveDistance < stopMoveDistance)
            {
                SetTrackPlaybackSpeed(0f);
                ApplyStoredTrackRendererVisibility();
                return;
            }

            float moveSpeed = moveDistance / deltaTime;
            float speedScale = scaleSpeedByMovement && referenceMoveSpeed > 0f
                ? moveSpeed / referenceMoveSpeed
                : 1f;
            float playbackSpeed = Mathf.Clamp(
                animationSpeed * speedScale,
                0f,
                Mathf.Max(0f, maxAnimationSpeed));

            SetTrackPlaybackSpeed(playbackSpeed);
            ApplyStoredTrackRendererVisibility();
        }

        private bool ShouldStopTrackAnimation()
        {
            if (m_player != null && (m_player.IsAirMode || m_player.IsRespawning))
                return true;

            if (m_cpu != null && m_cpu.IsRespawning)
                return true;

            return false;
        }

        private bool ShouldHideTrackRenderers()
        {
            return m_player != null && m_player.IsRespawning;
        }

        private void CacheTrackAnimators()
        {
            CacheTrackRoots();

            if (m_trackRoots == null || m_trackRoots.Length == 0)
            {
                m_trackAnimators = System.Array.Empty<Animator>();
                return;
            }

            var animators = new System.Collections.Generic.List<Animator>();
            var usedHosts = new System.Collections.Generic.HashSet<Transform>();

            for (int i = 0; i < m_trackRoots.Length; i++)
            {
                Transform trackRoot = m_trackRoots[i];
                if (trackRoot == null)
                    continue;

                Transform animatorHost = GetAnimatorHost(trackRoot);
                if (animatorHost == null || !usedHosts.Add(animatorHost))
                    continue;

                Animator animator = animatorHost.GetComponent<Animator>();
                if (animator == null)
                    animator = animatorHost.gameObject.AddComponent<Animator>();

                ConfigureTrackAnimator(animator);
                PlayTrackAnimation(animator);
                animators.Add(animator);
            }

            m_trackAnimators = animators.ToArray();
        }

        public void SetTrackRenderersActive(bool isActive)
        {
            m_hasRendererVisibilityOverride = true;
            m_rendererVisibilityOverride = isActive;
            ApplyTrackRendererVisibility(isActive);
        }

        private void ApplyStoredTrackRendererVisibility()
        {
            if (!m_hasRendererVisibilityOverride)
                return;

            ApplyTrackRendererVisibility(m_rendererVisibilityOverride);
        }

        private void ApplyTrackRendererVisibility(bool isActive)
        {
            if (m_trackRoots == null || m_trackRoots.Length == 0)
                CacheTrackRoots();

            if (m_trackRenderers == null)
                CacheTrackRenderers();

            for (int i = 0; i < m_trackRenderers.Length; i++)
            {
                if (m_trackRenderers[i] != null)
                    m_trackRenderers[i].enabled = isActive;
            }
        }

        private void CacheTrackRenderers()
        {
            if (m_trackRoots == null || m_trackRoots.Length == 0)
            {
                m_trackRenderers = System.Array.Empty<Renderer>();
                return;
            }

            var renderers = new System.Collections.Generic.List<Renderer>();
            for (int i = 0; i < m_trackRoots.Length; i++)
            {
                Transform trackRoot = m_trackRoots[i];
                if (trackRoot == null)
                    continue;

                renderers.AddRange(trackRoot.GetComponentsInChildren<Renderer>(true));
            }

            Renderer[] allRenderers = GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < allRenderers.Length; i++)
            {
                Renderer candidate = allRenderers[i];
                if (candidate == null || renderers.Contains(candidate))
                    continue;

                if (IsTrackRenderer(candidate))
                    renderers.Add(candidate);
            }

            m_trackRenderers = renderers.ToArray();
        }

        private bool IsTrackRenderer(Renderer renderer)
        {
            if (renderer == null)
                return false;

            if (ContainsIgnoreCase(GetHierarchyName(renderer.transform), trackRootName) ||
                ContainsIgnoreCase(GetHierarchyName(renderer.transform), "Track") ||
                ContainsIgnoreCase(GetHierarchyName(renderer.transform), "Crawler"))
            {
                return true;
            }

            Material[] materials = renderer.sharedMaterials;
            for (int i = 0; i < materials.Length; i++)
            {
                Material material = materials[i];
                if (material == null)
                    continue;

                if (ContainsIgnoreCase(material.name, "Track") ||
                    ContainsIgnoreCase(material.name, "Crawler"))
                {
                    return true;
                }
            }

            return false;
        }

        private string GetHierarchyName(Transform target)
        {
            if (target == null)
                return string.Empty;

            string result = target.name;
            Transform current = target.parent;
            while (current != null && current != transform)
            {
                result = current.name + "/" + result;
                current = current.parent;
            }

            return result;
        }

        private bool ContainsIgnoreCase(string source, string value)
        {
            if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(value))
                return false;

            return source.IndexOf(value, System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void CacheTrackRoots()
        {
            Transform[] transforms = GetComponentsInChildren<Transform>(true);
            int count = 0;

            for (int i = 0; i < transforms.Length; i++)
            {
                if (IsTrackRoot(transforms[i]))
                    count++;
            }

            m_trackRoots = new Transform[count];
            int index = 0;
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform trackRoot = transforms[i];
                if (!IsTrackRoot(trackRoot))
                    continue;

                m_trackRoots[index++] = trackRoot;
            }

            m_trackRenderers = null;
        }

        private bool IsTrackRoot(Transform candidate)
        {
            if (candidate == null)
                return false;

            if (string.IsNullOrEmpty(trackRootName))
                return candidate != transform;

            return candidate.name.Contains(trackRootName);
        }

        private Transform GetAnimatorHost(Transform trackRoot)
        {
            Transform host = trackRoot;
            int parentLevels = Mathf.Max(0, animationRootParentLevels);

            for (int i = 0; i < parentLevels; i++)
            {
                if (host == null || host.parent == null || host.parent == transform)
                    break;

                host = host.parent;
            }

            return host;
        }

        private void ConfigureTrackAnimator(Animator animator)
        {
            if (animator == null)
                return;

            if (trackController != null)
                animator.runtimeAnimatorController = trackController;

            animator.enabled = true;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.updateMode = AnimatorUpdateMode.Normal;
            animator.speed = 0f;
            animator.Rebind();
        }

        private void PlayTrackAnimationFromStart()
        {
            if (m_trackAnimators == null)
                return;

            for (int i = 0; i < m_trackAnimators.Length; i++)
            {
                Animator animator = m_trackAnimators[i];
                PlayTrackAnimation(animator);
            }
        }

        private void PlayTrackAnimation(Animator animator)
        {
            if (animator == null || animator.runtimeAnimatorController == null)
                return;

            animator.Play(trackStateName, 0, 0f);
            animator.Update(0f);
        }

        private void SetTrackPlaybackSpeed(float speed)
        {
            if (m_trackAnimators == null)
                return;

            for (int i = 0; i < m_trackAnimators.Length; i++)
            {
                Animator animator = m_trackAnimators[i];
                if (animator == null || animator.runtimeAnimatorController == null)
                    continue;

                animator.speed = speed;
            }
        }
    }
}
