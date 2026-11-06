using UnityEngine;

// Delimita el Coto de caza: los boids no pueden salir de esta area.
public class ArenaBounds : MonoBehaviour
{
    public static ArenaBounds Instance { get; private set; }

    [SerializeField] private float height = 14f;
    [SerializeField] private float width = 24f;
    [SerializeField] private bool drawGizmos = true;

    public float Width => width;
    public float Height => height;
    public Vector3 Center => transform.position;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void Configure(Vector3 center, float newWidth, float newHeight)
    {
        transform.position = center;
        width = newWidth;
        height = newHeight;
    }

    public bool Contains(Vector3 position)
    {
        Vector3 center = transform.position;

        return Mathf.Abs(position.x - center.x) <= width / 2
            && Mathf.Abs(position.z - center.z) <= height / 2;
    }

    public Vector3 Clamp(Vector3 position)
    {
        Vector3 center = transform.position;
        Vector3 newPosition = position;

        newPosition.x = Mathf.Clamp(position.x, center.x - width / 2, center.x + width / 2);
        newPosition.z = Mathf.Clamp(position.z, center.z - height / 2, center.z + height / 2);
        return newPosition;
    }

    public Vector3 InwardDirection(Vector3 position, float margin)
    {
        Vector3 center = transform.position;
        Vector3 inward = Vector3.zero;

        float halfWidth = width / 2;
        float halfHeight = height / 2;

        if (position.x > center.x + halfWidth - margin) inward.x = -1f;
        else if (position.x < center.x - halfWidth + margin) inward.x = 1f;

        if (position.z > center.z + halfHeight - margin) inward.z = -1f;
        else if (position.z < center.z - halfHeight + margin) inward.z = 1f;

        return inward == Vector3.zero ? Vector3.zero : inward.normalized;
    }

    public Vector3 GetRandomPoint()
    {
        Vector3 center = transform.position;

        float x = center.x + Random.Range(-width / 2f, width / 2f);
        float z = center.z + Random.Range(-height / 2f, height / 2f);
        return new Vector3(x, center.y, z);
    }

    private void OnDrawGizmos()
    {
        if (!drawGizmos) return;

        Gizmos.color = ZoneVolume.ColorFor(ZoneType.Hunt);
        Gizmos.DrawWireCube(transform.position, new Vector3(width, 0.1f, height));
    }
}
