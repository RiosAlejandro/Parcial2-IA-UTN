using UnityEngine;

public class ZoneVolume : MonoBehaviour
{
    [SerializeField] private ZoneType _type = ZoneType.Corridor;
    [SerializeField] private Vector2 _size = new Vector2(10f, 10f);
    [SerializeField] private bool _drawGizmo = true;

    public ZoneType Type => _type;
    public Vector2 Size => _size;
    public Vector3 Center => transform.position;

    public void Configure(ZoneType type, Vector2 size)
    {
        _type = type;
        _size = size;
    }

    public bool Contains(Vector3 position)
    {
        Vector3 center = transform.position;

        return Mathf.Abs(position.x - center.x) <= _size.x * 0.5f
            && Mathf.Abs(position.z - center.z) <= _size.y * 0.5f;
    }

    public Vector3 RandomPoint()
    {
        Vector3 center = transform.position;

        return new Vector3(
            center.x + Random.Range(-_size.x * 0.5f, _size.x * 0.5f),
            center.y,
            center.z + Random.Range(-_size.y * 0.5f, _size.y * 0.5f));
    }

    public static Color ColorFor(ZoneType type)
    {
        switch (type)
        {
            case ZoneType.Start: return new Color(0.2f, 0.85f, 0.45f);
            case ZoneType.Hunt: return new Color(1f, 0.7f, 0.15f);
            case ZoneType.Safe: return new Color(0.7f, 0.45f, 1f);
            case ZoneType.Exit: return new Color(0.3f, 0.65f, 1f);
            default: return new Color(0.6f, 0.6f, 0.6f);
        }
    }

    private void OnDrawGizmos()
    {
        if (!_drawGizmo) return;

        Color color = ColorFor(_type);
        Vector3 size = new Vector3(_size.x, 0.1f, _size.y);

        Gizmos.color = new Color(color.r, color.g, color.b, 0.15f);
        Gizmos.DrawCube(transform.position, size);

        Gizmos.color = color;
        Gizmos.DrawWireCube(transform.position, size);
    }
}
