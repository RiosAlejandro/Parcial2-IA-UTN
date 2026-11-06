using System;
using System.Collections.Generic;
using UnityEngine;

public class AStar
{
    private readonly NavGraph _graph;
    private readonly MinHeap _open = new MinHeap();

    private int[] _cameFrom = Array.Empty<int>();
    private float[] _gScore = Array.Empty<float>();
    private bool[] _closed = Array.Empty<bool>();
    private int[] _stamp = Array.Empty<int>();
    private int _currentStamp;

    private readonly List<int> _nodePath = new List<int>();

    public float LastPathCost { get; private set; }
    public IReadOnlyList<int> LastNodePath => _nodePath;

    public AStar(NavGraph graph)
    {
        _graph = graph;
    }

    public bool TryFindPath(Vector3 from, Vector3 to, List<Vector3> result, Func<NavNode, bool> filter = null)
    {
        result.Clear();
        LastPathCost = 0f;

        if (_graph == null || _graph.NodeCount == 0) return false;

        int start = _graph.NearestNode(from, filter);
        int goal = _graph.NearestNode(to, filter);

        if (start < 0 || goal < 0) return false;

        if (!TryFindNodePath(start, goal, filter)) return false;

        foreach (int index in _nodePath)
            result.Add(_graph.GetPosition(index));

        result.Add(to);

        return true;
    }

    public bool TryGetPathCost(Vector3 from, Vector3 to, out float cost, Func<NavNode, bool> filter = null)
    {
        cost = float.MaxValue;

        if (_graph == null || _graph.NodeCount == 0) return false;

        int start = _graph.NearestNode(from, filter);
        int goal = _graph.NearestNode(to, filter);

        if (start < 0 || goal < 0) return false;
        if (!TryFindNodePath(start, goal, filter)) return false;

        cost = LastPathCost;
        return true;
    }

    // Una sola busqueda con heuristica cero devuelve el nodo mas barato de toda la zona.
    public bool TryFindPathToZone(Vector3 from, ZoneType zone, List<Vector3> result, Func<NavNode, bool> filter = null)
    {
        result.Clear();
        LastPathCost = 0f;

        if (_graph == null || _graph.NodeCount == 0) return false;

        int start = _graph.NearestNode(from, filter);
        if (start < 0) return false;

        if (!Search(start, index => _graph.GetNode(index).zone == zone, _ => 0f, filter)) return false;

        foreach (int index in _nodePath)
            result.Add(_graph.GetPosition(index));

        return true;
    }

    public bool TryFindNodePath(int start, int goal, Func<NavNode, bool> filter = null)
    {
        return Search(start, index => index == goal, index => Heuristic(index, goal), filter);
    }

    private bool Search(int start, Func<int, bool> isGoal, Func<int, float> heuristic, Func<NavNode, bool> filter)
    {
        _nodePath.Clear();
        LastPathCost = 0f;

        EnsureCapacity(_graph.NodeCount);

        _currentStamp++;
        _open.Clear();

        Visit(start);
        _gScore[start] = 0f;
        _open.Push(start, heuristic(start));

        while (_open.TryPop(out int current))
        {
            if (_closed[current]) continue;
            _closed[current] = true;

            if (isGoal(current))
            {
                Rebuild(start, current);
                return true;
            }

            NavNode node = _graph.GetNode(current);

            foreach (NavConnection connection in node.connections)
            {
                int next = connection.to;

                if (filter != null && !filter(_graph.GetNode(next))) continue;

                Visit(next);
                if (_closed[next]) continue;

                float tentative = _gScore[current] + connection.cost;
                if (tentative >= _gScore[next]) continue;

                _gScore[next] = tentative;
                _cameFrom[next] = current;
                _open.Push(next, tentative + heuristic(next));
            }
        }

        return false;
    }

    private void Rebuild(int start, int goal)
    {
        LastPathCost = _gScore[goal];

        int current = goal;

        while (current != start)
        {
            _nodePath.Add(current);
            current = _cameFrom[current];

            if (current < 0) break;
        }

        _nodePath.Add(start);
        _nodePath.Reverse();
    }

    private float Heuristic(int from, int to) => Vector3.Distance(_graph.GetPosition(from), _graph.GetPosition(to));

    private void Visit(int index)
    {
        if (_stamp[index] == _currentStamp) return;

        _stamp[index] = _currentStamp;
        _gScore[index] = float.MaxValue;
        _cameFrom[index] = -1;
        _closed[index] = false;
    }

    private void EnsureCapacity(int count)
    {
        if (_gScore.Length >= count) return;

        _cameFrom = new int[count];
        _gScore = new float[count];
        _closed = new bool[count];
        _stamp = new int[count];
        _currentStamp = 0;
    }

    private class MinHeap
    {
        private int[] _items = new int[64];
        private float[] _keys = new float[64];
        private int _count;

        public void Clear() => _count = 0;

        public void Push(int item, float key)
        {
            if (_count == _items.Length)
            {
                Array.Resize(ref _items, _count * 2);
                Array.Resize(ref _keys, _count * 2);
            }

            int child = _count++;
            _items[child] = item;
            _keys[child] = key;

            while (child > 0)
            {
                int parent = (child - 1) / 2;
                if (_keys[parent] <= _keys[child]) break;

                Swap(parent, child);
                child = parent;
            }
        }

        public bool TryPop(out int item)
        {
            item = 0;
            if (_count == 0) return false;

            item = _items[0];
            _count--;

            if (_count > 0)
            {
                _items[0] = _items[_count];
                _keys[0] = _keys[_count];
                SiftDown();
            }

            return true;
        }

        private void SiftDown()
        {
            int parent = 0;

            while (true)
            {
                int left = parent * 2 + 1;
                if (left >= _count) break;

                int smallest = left;
                int right = left + 1;

                if (right < _count && _keys[right] < _keys[left])
                    smallest = right;

                if (_keys[parent] <= _keys[smallest]) break;

                Swap(parent, smallest);
                parent = smallest;
            }
        }

        private void Swap(int a, int b)
        {
            (_items[a], _items[b]) = (_items[b], _items[a]);
            (_keys[a], _keys[b]) = (_keys[b], _keys[a]);
        }
    }
}
