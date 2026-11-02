using UnityEngine;

namespace MyGame
{
    public sealed class EnemyMoveAnimationController : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        [SerializeField, Min(0f)] private float moveSpeedThreshold = 0.02f;
        [SerializeField, Min(0f)] private float animationSpeed = 1f;
        [SerializeField] private string moveStateName = "Move";
        [SerializeField, Min(0f)] private float moveStopGraceTime = 0.18f;
        [SerializeField] private string attackTriggerName = "Attack";
        [SerializeField, Min(0.01f)] private float attackAnimationHoldTime = 0.55f;

        private Transform m_motionRoot;
        private Vector3 m_lastPosition;
        private bool m_hasLastPosition;
        private float m_moveGraceTimer;
        private float m_attackAnimationTimer;
        private int m_moveStateHash;
        private int m_attackTriggerHash;

        private void Awake()
        {
            CacheReferences();
            CacheAnimatorHashes();
        }

        private void OnEnable()
        {
            CacheReferences();
            CacheAnimatorHashes();
            ResetMotionSample();
            SetAnimatorSpeed(0f);
        }

        private void OnDisable()
        {
            SetAnimatorSpeed(0f);
        }

        private void LateUpdate()
        {
            CacheReferences();

            if (animator == null || m_motionRoot == null)
                return;

            if (m_attackAnimationTimer > 0f)
            {
                m_attackAnimationTimer -= Time.deltaTime;
                SetAnimatorSpeed(animationSpeed);
                m_lastPosition = m_motionRoot.position;
                m_hasLastPosition = true;
                return;
            }

            if (!m_hasLastPosition || Time.deltaTime <= 0f)
            {
                ResetMotionSample();
                return;
            }

            Vector3 position = m_motionRoot.position;
            Vector3 delta = position - m_lastPosition;
            delta.y = 0f;

            float currentSpeed = delta.magnitude / Time.deltaTime;
            if (currentSpeed > moveSpeedThreshold)
            {
                m_moveGraceTimer = Mathf.Max(0f, moveStopGraceTime);
            }
            else if (m_moveGraceTimer > 0f)
            {
                m_moveGraceTimer -= Time.deltaTime;
            }

            bool shouldPlayMove = currentSpeed > moveSpeedThreshold || m_moveGraceTimer > 0f;
            if (shouldPlayMove)
            {
                RestartMoveStateIfEnded();
                SetAnimatorSpeed(animationSpeed);
            }
            else
            {
                SetAnimatorSpeed(0f);
            }

            m_lastPosition = position;
        }

        public void PlayAttack()
        {
            CacheReferences();
            CacheAnimatorHashes();

            if (animator == null || m_attackTriggerHash == 0)
                return;

            m_attackAnimationTimer = Mathf.Max(0.01f, attackAnimationHoldTime);
            SetAnimatorSpeed(animationSpeed);
            animator.ResetTrigger(m_attackTriggerHash);
            animator.SetTrigger(m_attackTriggerHash);
        }

        private void CacheReferences()
        {
            if (animator == null)
                animator = GetComponentInChildren<Animator>(true);

            if (m_motionRoot != null)
                return;

            EnemyEntity enemy = GetComponentInParent<EnemyEntity>();
            m_motionRoot = enemy != null ? enemy.transform : transform;
        }

        private void ResetMotionSample()
        {
            if (m_motionRoot == null)
            {
                m_hasLastPosition = false;
                return;
            }

            m_lastPosition = m_motionRoot.position;
            m_hasLastPosition = true;
        }

        private void CacheAnimatorHashes()
        {
            m_moveStateHash = string.IsNullOrWhiteSpace(moveStateName)
                ? 0
                : Animator.StringToHash(moveStateName);
            m_attackTriggerHash = string.IsNullOrWhiteSpace(attackTriggerName)
                ? 0
                : Animator.StringToHash(attackTriggerName);
        }

        private void RestartMoveStateIfEnded()
        {
            if (animator == null ||
                m_moveStateHash == 0 ||
                animator.IsInTransition(0))
            {
                return;
            }

            AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
            if (stateInfo.shortNameHash == m_moveStateHash &&
                stateInfo.normalizedTime >= 0.98f)
            {
                animator.Play(m_moveStateHash, 0, 0f);
            }
        }

        private void SetAnimatorSpeed(float speed)
        {
            if (animator == null || Mathf.Approximately(animator.speed, speed))
                return;

            animator.speed = speed;
        }
    }
}
