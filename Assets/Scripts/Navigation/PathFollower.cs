using System.Collections.Generic;
using UnityEngine;

public class PathFollower
{
    private readonly List<Vector3> _path = new List<Vector3>();
    private int _index;

    public IReadOnlyList<Vector3> Path => _path;
    public int Index => _index;
    public bool IsFinished => _index >= _path.Count;
    public bool HasPath => _path.Count > 0 && !IsFinished;
    public Vector3 CurrentWaypoint => _path[Mathf.Clamp(_index, 0, _path.Count - 1)];
    public Vector3 Destination => _path.Count > 0 ? _path[_path.Count - 1] : Vector3.zero;

    public void SetPath(List<Vector3> path)
    {
        _path.Clear();
        _path.AddRange(path);
        _index = 0;
    }

    public void Clear()
    {
        _path.Clear();
        _index = 0;
    }

    public bool Advance(Vector3 position, float waypointRadius)
    {
        float sqrRadius = waypointRadius * waypointRadius;

        while (_index < _path.Count)
        {
            Vector3 offset = _path[_index] - position;
            offset.y = 0f;

            if (offset.sqrMagnitude > sqrRadius) break;

            _index++;
        }

        return IsFinished;
    }

    // Saltea waypoints con linea de vista directa para no zigzaguear entre nodos.
    public void Smooth(NavGraph graph, Vector3 position)
    {
        if (graph == null) return;

        while (_index + 1 < _path.Count && graph.HasLineOfSight(position, _path[_index + 1]))
            _index++;
    }

    public void DrawGizmo(Vector3 position, Color color)
    {
        if (!HasPath) return;

        Gizmos.color = color;
        Gizmos.DrawLine(position, _path[_index]);

        for (int i = _index; i < _path.Count - 1; i++)
            Gizmos.DrawLine(_path[i], _path[i + 1]);

        for (int i = _index; i < _path.Count; i++)
            Gizmos.DrawSphere(_path[i], 0.18f);
    }
}
