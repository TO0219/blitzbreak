using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class PieceLink
{
    public int otherId;
    public float hp;
    public float maxHp;
    public bool broken;

    public PieceLink(int otherId, float hp)
    {
        this.otherId = otherId;
        this.hp = hp;
        this.maxHp = hp;
        this.broken = false;
    }
}

public class ClusterPiece : MonoBehaviour
{
    [HideInInspector] public int pieceId = -1;
    [HideInInspector] public Rigidbody rb;
    [HideInInspector] public Collider col;

    [Header("Support")]
    public bool isSupport = false;

    [Header("Strength")]
    public float strengthMultiplier = 1f;

    [Header("Cleanup")]
    public bool autoDisable = true;
    public float disableAfterSeconds = 5f;
    public float shrinkDuration = 0.65f;
    public bool enableCollisionWhenDetached = false;

    [HideInInspector] public bool isDetached = false;
    [HideInInspector] public List<PieceLink> links = new List<PieceLink>();

    private Vector3 initialLocalPosition;
    private Quaternion initialLocalRotation;
    private Vector3 initialLocalScale;
    private bool hasInitialTransform = false;
    private Coroutine disableCoroutine;

    private void Awake()
    {
        EnsureComponents();

        rb.isKinematic = true;
        rb.useGravity = false;
    }

    public void EnsureComponents()
    {
        rb = GetComponent<Rigidbody>();
        col = GetComponent<Collider>();

        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody>();
        }

        if (col == null)
        {
            col = gameObject.AddComponent<BoxCollider>();
        }
    }

    public void CaptureInitialTransform()
    {
        initialLocalPosition = transform.localPosition;
        initialLocalRotation = transform.localRotation;
        initialLocalScale = transform.localScale;
        hasInitialTransform = true;
    }

    public void ResetRuntimeState()
    {
        EnsureComponents();

        isDetached = false;
        links.Clear();

        if (!hasInitialTransform)
        {
            CaptureInitialTransform();
        }

        transform.localPosition = initialLocalPosition;
        transform.localRotation = initialLocalRotation;
        transform.localScale = initialLocalScale;
        gameObject.SetActive(true);

        if (disableCoroutine != null)
        {
            StopCoroutine(disableCoroutine);
            disableCoroutine = null;
        }

        if (rb != null)
        {
            rb.isKinematic = false;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.useGravity = false;
            rb.isKinematic = true;
        }
    }

    public PieceLink GetLink(int otherId)
    {
        for (int i = 0; i < links.Count; i++)
        {
            if (links[i].otherId == otherId)
            {
                return links[i];
            }
        }

        return null;
    }

    public bool IsLinkedTo(int otherId)
    {
        PieceLink link = GetLink(otherId);
        return link != null && !link.broken;
    }

    public void AddLink(int otherId, float hp)
    {
        if (GetLink(otherId) != null) return;
        links.Add(new PieceLink(otherId, hp));
    }

    public void ApplyDamageToLink(int otherId, float damage)
    {
        PieceLink link = GetLink(otherId);
        if (link == null) return;
        if (link.broken) return;

        link.hp -= damage;
        if (link.hp <= 0f)
        {
            link.hp = 0f;
            link.broken = true;
        }
    }

    public void ActivatePhysics(IReadOnlyList<Collider> ignoredActorColliders = null)
    {
        EnsureComponents();

        if (rb == null) return;

        if (col != null && !enableCollisionWhenDetached && ignoredActorColliders != null)
        {
            for (int i = 0; i < ignoredActorColliders.Count; i++)
            {
                Collider actorCollider = ignoredActorColliders[i];
                if (actorCollider != null && actorCollider != col)
                {
                    Physics.IgnoreCollision(col, actorCollider, true);
                }
            }
        }

        rb.isKinematic = false;
        rb.useGravity = true;

        if (autoDisable)
        {
            if (disableCoroutine != null)
            {
                StopCoroutine(disableCoroutine);
            }

            disableCoroutine = StartCoroutine(DisableAfterTime());
        }
    }

    private IEnumerator DisableAfterTime()
    {
        yield return new WaitForSeconds(disableAfterSeconds);

        float duration = Mathf.Max(0.01f, shrinkDuration);
        float elapsed = 0f;
        Vector3 startScale = transform.localScale;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            transform.localScale = Vector3.Lerp(startScale, Vector3.zero, t);
            yield return null;
        }

        transform.localScale = Vector3.zero;
        gameObject.SetActive(false);
    }
}
