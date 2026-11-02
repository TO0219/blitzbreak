using UnityEngine;

public class DestructibleObject : MonoBehaviour
{
    [SerializeField] private GameObject intactRoot;
    [SerializeField] private GameObject fracturedRoot;
    [SerializeField] private ConnectedClusterDestruction cluster;
    [SerializeField] private bool broken = false;

    private void Awake()
    {
        ResolveCluster();

        if (cluster != null)
        {
            cluster.BuildCluster();
        }

        SetBrokenState(false);
    }

    public void Initialize(
        GameObject intactRoot,
        GameObject fracturedRoot,
        ConnectedClusterDestruction cluster)
    {
        this.intactRoot = intactRoot;
        this.fracturedRoot = fracturedRoot;
        this.cluster = cluster;

        if (this.cluster != null)
        {
            this.cluster.BuildCluster();
        }

        SetBrokenState(false);
    }

    public void BreakAt(Vector3 hitPoint)
    {
        if (!broken)
        {
            SetBrokenState(true);
        }

        ResolveCluster();

        if (cluster != null)
        {
            if (cluster.PieceCount == 0)
            {
                cluster.BuildCluster();
            }

            cluster.DamageAt(hitPoint);
        }
    }

    public void SetBrokenState(bool isBroken)
    {
        broken = isBroken;

        if (intactRoot != null)
        {
            intactRoot.SetActive(!isBroken);
        }

        if (fracturedRoot != null)
        {
            fracturedRoot.SetActive(isBroken);
        }
    }

    private void ResolveCluster()
    {
        if (fracturedRoot == null) return;

        bool needsCluster = cluster == null ||
            (cluster.transform != fracturedRoot.transform &&
             !cluster.transform.IsChildOf(fracturedRoot.transform));

        if (!needsCluster) return;

        cluster = fracturedRoot.GetComponent<ConnectedClusterDestruction>();
        if (cluster == null)
        {
            cluster = fracturedRoot.GetComponentInChildren<ConnectedClusterDestruction>(true);
        }
    }
}
