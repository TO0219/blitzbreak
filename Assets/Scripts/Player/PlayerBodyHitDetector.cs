using UnityEngine;

namespace MyGame
{
    public class PlayerBodyHitDetector : MonoBehaviour
    {
        [SerializeField] private PlayerEntity player;
        [SerializeField] private int damageAmount = 1;

        private void Awake()
        {
            if (player == null)
                player = GetComponentInParent<PlayerEntity>();
        }

        private void OnTriggerEnter(Collider other)
        {
            // 自分自身は無視
            if (other.transform.root == transform.root)
                return;

            if (other.CompareTag("EnemyBullet"))
            {
                if (other.GetComponentInParent<EnemyBulletEntity>() != null ||
                    other.GetComponentInParent<EnemyCPUBulletEntity>() != null)
                    return;

                Collider bodyCollider = GetComponent<Collider>();
                if (player == null ||
                    (bodyCollider != null && !player.IsActiveDamageCollider(bodyCollider)))
                {
                    return;
                }

                player.Damage(damageAmount, other.bounds.center);
            }

            if (other.CompareTag("Wall"))
            {
                //壁との当たり判定処理は後に（PlayerEntityで競合する関係上）
            }
        }
    }
}
