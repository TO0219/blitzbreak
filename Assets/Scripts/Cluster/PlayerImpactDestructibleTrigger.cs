using Unity.Netcode;
using UnityEngine;

namespace MyGame
{
    public class PlayerImpactDestructibleTrigger : MonoBehaviour
    {
        [SerializeField] private DestructibleBuilding destructible;
        [SerializeField] private float contactDamage = 999f;
        [SerializeField] private float minImpactSpeed = 0.4f;

        private bool triggered;

        public void Initialize(
            DestructibleBuilding targetDestructible,
            float generatedContactDamage,
            float generatedMinImpactSpeed)
        {
            destructible = targetDestructible;
            contactDamage = Mathf.Max(1f, generatedContactDamage);
            minImpactSpeed = Mathf.Max(0f, generatedMinImpactSpeed);
            triggered = false;
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (triggered ||
                collision == null ||
                !IsPlayerCollider(collision.collider))
            {
                return;
            }

            if (minImpactSpeed > 0f &&
                collision.relativeVelocity.sqrMagnitude < minImpactSpeed * minImpactSpeed)
            {
                return;
            }

            Vector3 hitPoint = collision.contactCount > 0
                ? collision.GetContact(0).point
                : transform.position;

            Break(hitPoint);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (triggered || !IsPlayerCollider(other))
                return;

            Vector3 hitPoint = other != null
                ? other.bounds.ClosestPoint(transform.position)
                : transform.position;

            Break(hitPoint);
        }

        private bool IsPlayerCollider(Collider targetCollider)
        {
            return targetCollider != null &&
                   targetCollider.GetComponentInParent<PlayerEntity>() != null;
        }

        private void Break(Vector3 hitPoint)
        {
            if (destructible == null || destructible.IsDestroyed)
                return;

            triggered = true;
            if (NetworkManager.Singleton != null &&
                NetworkManager.Singleton.IsListening &&
                NetworkBattleManager.Instance != null)
            {
                NetworkBattleManager.Instance.RequestBuildingImpactDamage(
                    destructible.NetworkId,
                    contactDamage,
                    hitPoint);
                return;
            }

            destructible.DamageByPlayerImpact(contactDamage, hitPoint);
        }
    }
}
