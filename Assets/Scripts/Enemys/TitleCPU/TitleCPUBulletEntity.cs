using UnityEngine;

namespace MyGame
{
    /// <summary>
    /// タイトル画面専用の演出弾。対象へダメージは与えない。
    /// </summary>
    public sealed class TitleCPUBulletEntity : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float speed = 15f;
        [SerializeField, Min(0.05f)] private float lifeTime = 3f;
        [SerializeField] private GameObject hitEffectPrefab;

        private TitleCPUEntity m_owner;
        private Rigidbody m_rigidbody;
        private float m_timer;
        private bool m_hasHit;

        public float Speed => speed;

        private void Awake()
        {
            m_rigidbody = GetComponent<Rigidbody>();
        }

        private void OnEnable()
        {
            m_timer = 0f;
            m_hasHit = false;
        }

        public void Setup(TitleCPUEntity owner)
        {
            m_owner = owner;
        }

        public void SetSpeed(float value)
        {
            speed = Mathf.Max(0f, value);
        }

        private void Update()
        {
            if (m_hasHit)
                return;

            if (m_rigidbody == null)
                transform.position += transform.forward * speed * Time.deltaTime;

            m_timer += Time.deltaTime;
            if (m_timer >= lifeTime)
                Destroy(gameObject);
        }

        private void OnTriggerEnter(Collider other)
        {
            TryHit(other, transform.position);
        }

        private void OnCollisionEnter(Collision collision)
        {
            Vector3 hitPoint = collision.contactCount > 0
                ? collision.GetContact(0).point
                : transform.position;
            TryHit(collision.collider, hitPoint);
        }

        private void TryHit(Collider other, Vector3 hitPoint)
        {
            if (m_hasHit || other == null)
                return;

            if (m_owner != null && other.transform.root == m_owner.transform.root)
                return;

            m_hasHit = true;
            if (hitEffectPrefab != null)
                Instantiate(hitEffectPrefab, hitPoint, Quaternion.identity);

            Destroy(gameObject);
        }
    }
}
