using UnityEngine;

public class RendererBoundsExtender : MonoBehaviour
{
    [SerializeField] private Vector3 _boundsCenter = Vector3.zero;
    [SerializeField] private Vector3 _boundsSize = new Vector3(4f, 4f, 4f);

    private void Awake()
    {
        var meshFilter = GetComponentInChildren<MeshFilter>();
        if (meshFilter == null || meshFilter.sharedMesh == null) return;

        Mesh instanceMesh = Instantiate(meshFilter.sharedMesh);
        instanceMesh.bounds = new Bounds(_boundsCenter, _boundsSize);
        meshFilter.sharedMesh = instanceMesh;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireCube(_boundsCenter, _boundsSize);
    }
}
