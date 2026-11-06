using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public struct NavConnection
{
    public int to;
    public float cost;

    public NavConnection(int to, float cost)
    {
        this.to = to;
        this.cost = cost;
    }
}

[Serializable]
public class NavNode
{
    public Vector3 position;
    public ZoneType zone;
    public List<NavConnection> connections = new List<NavConnection>();
}

public class NavGraph : MonoBehaviour
{
    [Header("Area de muestreo")]
    [SerializeField] private Vector2 _areaCenter = Vector2.zero;
    [SerializeField] private Vector2 _areaSize = new Vector2(80f, 40f);
    [SerializeField] private float _nodeSpacing = 2.5f;
    [SerializeField] private float _sampleHeight = 0.5f;

    [Header("Validacion")]
    [SerializeField] private LayerMask _obstacleMask = 1 << 8;
    [SerializeField] private LayerMask _floorMask = 1;
    [SerializeField] private LayerMask _zoneMask = 1 << 10;
    [SerializeField] private float _clearance = 1.2f;
    [SerializeField] private float _maxConnectionDistance = 4.5f;

    [Header("Gizmos")]
    [SerializeField] private bool _drawNodes = true;
    [SerializeField] private bool _drawConnections = true;
    [SerializeField] private float _nodeGizmoRadius = 0.25f;

    [SerializeField, HideInInspector] private List<NavNode> _nodes = new List<NavNode>();

    public IReadOnlyList<NavNode> Nodes => _nodes;
    public int NodeCount => _nodes.Count;

    public NavNode GetNode(int index) => _nodes[index];

    public void ConfigureArea(Vector2 center, Vector2 size)
    {
        _areaCenter = center;
        _areaSize = size;
    }

    public Vector3 GetPosition(int index) => _nodes[index].position;

    public int NearestNode(Vector3 position, Func<NavNode, bool> filter = null, bool requireLineOfSight = false)
    {
        int nearest = -1;
        float nearestSqrDist = float.MaxValue;

        for (int i = 0; i < _nodes.Count; i++)
        {
            NavNode node = _nodes[i];

            if (filter != null && !filter(node)) continue;

            float sqrDist = (node.position - position).sqrMagnitude;
            if (sqrDist >= nearestSqrDist) continue;

            if (requireLineOfSight && !HasLineOfSight(position, node.position)) continue;

            nearest = i;
            nearestSqrDist = sqrDist;
        }

        return nearest;
    }

    public void CollectNodesInZone(ZoneType zone, List<int> result)
    {
        result.Clear();

        for (int i = 0; i < _nodes.Count; i++)
        {
            if (_nodes[i].zone == zone)
                result.Add(i);
        }
    }

    public bool HasLineOfSight(Vector3 from, Vector3 to)
    {
        Vector3 origin = from + Vector3.up * _sampleHeight;
        Vector3 target = to + Vector3.up * _sampleHeight;
        Vector3 direction = target - origin;
        float distance = direction.magnitude;

        if (distance <= Mathf.Epsilon) return true;

        return !Physics.Raycast(origin, direction / distance, distance, _obstacleMask);
    }

    [ContextMenu("Generar grafo")]
    public void Generate()
    {
        _nodes.Clear();

        SampleNodes();
        ConnectNodes();
        AssignZones();

        Debug.Log($"Grafo generado: {_nodes.Count} nodos, {CountConnections()} conexiones.", this);
    }

    [ContextMenu("Limpiar grafo")]
    public void Clear()
    {
        _nodes.Clear();
    }

    private void SampleNodes()
    {
        float minX = _areaCenter.x - _areaSize.x * 0.5f;
        float minZ = _areaCenter.y - _areaSize.y * 0.5f;
        int columns = Mathf.FloorToInt(_areaSize.x / _nodeSpacing) + 1;
        int rows = Mathf.FloorToInt(_areaSize.y / _nodeSpacing) + 1;

        for (int column = 0; column < columns; column++)
        {
            for (int row = 0; row < rows; row++)
            {
                Vector3 point = new Vector3(minX + column * _nodeSpacing, _sampleHeight, minZ + row * _nodeSpacing);

                if (!IsWalkable(point)) continue;

                _nodes.Add(new NavNode { position = point, zone = ZoneType.Corridor });
            }
        }
    }

    private bool IsWalkable(Vector3 point)
    {
        if (Physics.CheckSphere(point, _clearance, _obstacleMask)) return false;

        return Physics.Raycast(point + Vector3.up * 2f, Vector3.down, 4f, _floorMask);
    }

    private void ConnectNodes()
    {
        float maxSqrDist = _maxConnectionDistance * _maxConnectionDistance;

        for (int i = 0; i < _nodes.Count; i++)
        {
            for (int j = i + 1; j < _nodes.Count; j++)
            {
                Vector3 from = _nodes[i].position;
                Vector3 to = _nodes[j].position;

                float sqrDist = (to - from).sqrMagnitude;
                if (sqrDist > maxSqrDist) continue;

                if (!HasLineOfSight(from, to)) continue;

                float cost = Mathf.Sqrt(sqrDist);
                _nodes[i].connections.Add(new NavConnection(j, cost));
                _nodes[j].connections.Add(new NavConnection(i, cost));
            }
        }
    }

    private void AssignZones()
    {
        Collider[] buffer = new Collider[8];

        foreach (NavNode node in _nodes)
        {
            int found = Physics.OverlapSphereNonAlloc(node.position, 0.1f, buffer, _zoneMask, QueryTriggerInteraction.Collide);

            for (int i = 0; i < found; i++)
            {
                if (!buffer[i].TryGetComponent(out ZoneVolume zone)) continue;

                node.zone = zone.Type;
                break;
            }
        }
    }

    private int CountConnections()
    {
        int total = 0;

        foreach (NavNode node in _nodes)
            total += node.connections.Count;

        return total / 2;
    }

    private void OnDrawGizmos()
    {
        if (_nodes == null || _nodes.Count == 0) return;

        if (_drawConnections)
        {
            Gizmos.color = new Color(0.55f, 0.6f, 0.7f, 0.5f);

            for (int i = 0; i < _nodes.Count; i++)
            {
                foreach (NavConnection connection in _nodes[i].connections)
                {
                    if (connection.to <= i) continue;

                    Gizmos.DrawLine(_nodes[i].position, _nodes[connection.to].position);
                }
            }
        }

        if (!_drawNodes) return;

        foreach (NavNode node in _nodes)
        {
            Gizmos.color = ZoneVolume.ColorFor(node.zone);
            Gizmos.DrawSphere(node.position, _nodeGizmoRadius);
        }
    }
}
