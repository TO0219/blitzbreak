using UnityEngine;

namespace MyGame
{
    public class AirShooterEnemyEntity : ShooterEnemyEntity
    {
        [SerializeField] private float hoverHeight = 3f;
        [SerializeField] private float hoverAdjustSpeed = 1f;
        [SerializeField] private float groundCheckDistance = 20f;
        [SerializeField] private LayerMask groundCheckMask = Physics.DefaultRaycastLayers;

        private readonly RaycastHit[] m_groundHits = new RaycastHit[16];

        protected override void Start()
        {
            InitializeShooter();
            SnapAboveGround();
        }

        protected override void Chase(Vector3 direction)
        {
            if (direction.sqrMagnitude < 0.001f)
                return;

            Vector3 lookDir = direction;
            lookDir.y = 0f;

            if (lookDir.sqrMagnitude > 0.001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(lookDir);

                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    targetRotation,
                    rotateSpeed * Time.deltaTime
                );
            }

            // 横移動のみ
            Vector3 moveDir = direction;
            moveDir.y = 0f;

            if (moveDir.sqrMagnitude > 0.001f)
            {
                MoveWithinSpawnArea(moveDir.normalized * moveSpeed * Time.deltaTime);
            }

            AdjustHeight(hoverHeight);
        }

        protected override void Wander()
        {
            base.Wander();

            AdjustHeight(hoverHeight);
        }

        protected override Vector3 GetDirection(Vector3 targetPos, Vector3 transformPos)
        {
            Vector3 direction = targetPos - transformPos;
            //direction.y = 2f;
            return direction;
        }

        private void AdjustHeight(float targetHeight)
        {
            if (TryGetGroundHit(out RaycastHit hit))
            {
                Vector3 pos = transform.position;

                float targetY = hit.point.y + targetHeight;

                pos.y = Mathf.Lerp(
                    pos.y,
                    targetY,
                    hoverAdjustSpeed * Time.deltaTime
                );

                transform.position = pos;
            }
        }

        private void SnapAboveGround()
        {
            if (!TryGetGroundHit(out RaycastHit hit))
                return;

            Vector3 pos = transform.position;
            pos.y = Mathf.Max(pos.y, hit.point.y + hoverHeight);
            transform.position = pos;
        }

        private bool TryGetGroundHit(out RaycastHit groundHit)
        {
            groundHit = default;
            Vector3 origin =
                transform.position + Vector3.up * Mathf.Max(0.1f, hoverHeight);
            float distance =
                Mathf.Max(0.1f, groundCheckDistance + hoverHeight);
            int hitCount = Physics.RaycastNonAlloc(
                origin,
                Vector3.down,
                m_groundHits,
                distance,
                groundCheckMask,
                QueryTriggerInteraction.Ignore);

            float nearestDistance = float.PositiveInfinity;
            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = m_groundHits[i];
                Collider hitCollider = hit.collider;
                if (hitCollider == null || hitCollider.transform.root == transform.root)
                    continue;

                if (hitCollider.GetComponentInParent<EnemyEntity>() != null)
                    continue;

                if (hitCollider.GetComponentInParent<EnemyCPUEntity>() != null)
                    continue;

                if (hitCollider.GetComponentInParent<PlayerEntity>() != null)
                    continue;

                if (hit.distance >= nearestDistance)
                    continue;

                nearestDistance = hit.distance;
                groundHit = hit;
            }

            return groundHit.collider != null;
        }
    }
}
