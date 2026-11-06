using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class LevelArea
{
    public string name = "Area";
    public Vector2 center;
    public Vector2 size = new Vector2(10f, 10f);
    public ZoneType zone = ZoneType.Corridor;

    public LevelArea() { }

    public LevelArea(string name, Vector2 center, Vector2 size, ZoneType zone = ZoneType.Corridor)
    {
        this.name = name;
        this.center = center;
        this.size = size;
        this.zone = zone;
    }

    public bool Contains(float x, float z)
    {
        return Mathf.Abs(x - center.x) <= size.x * 0.5f
            && Mathf.Abs(z - center.y) <= size.y * 0.5f;
    }
}

public class LevelBuilder : MonoBehaviour
{
    [Header("Layout")]
    [SerializeField] private List<LevelArea> _areas = new List<LevelArea>();

    [Header("Construccion")]
    [SerializeField] private float _cellSize = 1f;
    [SerializeField] private float _wallHeight = 3f;
    [SerializeField] private float _wallThickness = 1f;
    [SerializeField] private float _floorThickness = 0.2f;

    [Header("Capas")]
    [SerializeField] private int _floorLayer = 0;
    [SerializeField] private int _wallLayer = 8;
    [SerializeField] private int _zoneLayer = 10;

    [Header("Materiales (opcionales)")]
    [SerializeField] private Material _floorMaterial;
    [SerializeField] private Material _wallMaterial;

    private const string ContainerName = "LevelGeometry";

    public IReadOnlyList<LevelArea> Areas => _areas;

    [ContextMenu("Cargar layout de ejemplo (Variante A)")]
    public void LoadDefaultLayout()
    {
        _areas = new List<LevelArea>
        {
            new LevelArea("Anillo norte", new Vector2(0f, 15.5f), new Vector2(52f, 5f)),
            new LevelArea("Anillo sur", new Vector2(0f, -15.5f), new Vector2(52f, 5f)),
            new LevelArea("Anillo oeste", new Vector2(-23.5f, 0f), new Vector2(5f, 36f)),
            new LevelArea("Anillo este", new Vector2(23.5f, 0f), new Vector2(5f, 36f)),

            new LevelArea("Coto de caza", new Vector2(0f, 0f), new Vector2(24f, 14f), ZoneType.Hunt),
            new LevelArea("Pasillo centro oeste", new Vector2(-16.5f, 0f), new Vector2(9f, 5f)),
            new LevelArea("Pasillo centro este", new Vector2(16.5f, 0f), new Vector2(9f, 5f)),

            new LevelArea("Sala norte", new Vector2(0f, 11f), new Vector2(20f, 4f)),
            new LevelArea("Sala sur", new Vector2(0f, -11f), new Vector2(20f, 4f)),

            new LevelArea("Zona segura noreste", new Vector2(16.5f, 10.5f), new Vector2(6f, 5f), ZoneType.Safe),
            new LevelArea("Zona segura suroeste", new Vector2(-16.5f, -10.5f), new Vector2(6f, 5f), ZoneType.Safe),

            new LevelArea("Zona inicial", new Vector2(-33f, 0f), new Vector2(10f, 10f), ZoneType.Start),
            new LevelArea("Pasillo inicio", new Vector2(-27f, 0f), new Vector2(4f, 5f)),

            new LevelArea("Salida", new Vector2(33f, 0f), new Vector2(10f, 10f), ZoneType.Exit),
            new LevelArea("Pasillo salida", new Vector2(27f, 0f), new Vector2(4f, 5f))
        };
    }

    [ContextMenu("Construir nivel")]
    public void Build()
    {
        if (_areas == null || _areas.Count == 0)
        {
            Debug.LogWarning("No hay areas definidas. Carga primero un layout.", this);
            return;
        }

        Transform container = ResetContainer();

        bool[,] walkable = Rasterize(out Vector2 origin, out int columns, out int rows);

        BuildFloors(container, walkable, origin, columns, rows);
        BuildWalls(container, walkable, origin, columns, rows);
        BuildZones(container);

        Debug.Log($"Nivel construido: {_areas.Count} areas, grilla {columns}x{rows}.", this);
    }

    [ContextMenu("Limpiar nivel")]
    public void ClearLevel()
    {
        Transform container = transform.Find(ContainerName);
        if (container == null) return;

        DestroyTarget(container.gameObject);
    }

    public Bounds GetWorldBounds()
    {
        Bounds bounds = new Bounds(new Vector3(_areas[0].center.x, 0f, _areas[0].center.y), Vector3.zero);

        foreach (LevelArea area in _areas)
        {
            bounds.Encapsulate(new Vector3(area.center.x - area.size.x * 0.5f, 0f, area.center.y - area.size.y * 0.5f));
            bounds.Encapsulate(new Vector3(area.center.x + area.size.x * 0.5f, 0f, area.center.y + area.size.y * 0.5f));
        }

        return bounds;
    }

    private Transform ResetContainer()
    {
        ClearLevel();

        GameObject container = new GameObject(ContainerName);
        container.transform.SetParent(transform, false);

        return container.transform;
    }

    private bool[,] Rasterize(out Vector2 origin, out int columns, out int rows)
    {
        Bounds bounds = GetWorldBounds();

        float margin = _wallThickness + _cellSize;
        float minX = bounds.min.x - margin;
        float minZ = bounds.min.z - margin;
        float maxX = bounds.max.x + margin;
        float maxZ = bounds.max.z + margin;

        columns = Mathf.CeilToInt((maxX - minX) / _cellSize);
        rows = Mathf.CeilToInt((maxZ - minZ) / _cellSize);
        origin = new Vector2(minX, minZ);

        bool[,] walkable = new bool[columns, rows];

        for (int column = 0; column < columns; column++)
        {
            for (int row = 0; row < rows; row++)
            {
                Vector2 point = CellCenter(origin, column, row);

                foreach (LevelArea area in _areas)
                {
                    if (!area.Contains(point.x, point.y)) continue;

                    walkable[column, row] = true;
                    break;
                }
            }
        }

        return walkable;
    }

    private Vector2 CellCenter(Vector2 origin, int column, int row)
    {
        return new Vector2(
            origin.x + (column + 0.5f) * _cellSize,
            origin.y + (row + 0.5f) * _cellSize);
    }

    private void BuildFloors(Transform container, bool[,] walkable, Vector2 origin, int columns, int rows)
    {
        Transform floors = CreateChild(container, "Floors");

        ForEachRun(walkable, columns, rows, true, (startColumn, endColumn, row) =>
        {
            float width = (endColumn - startColumn + 1) * _cellSize;
            Vector2 start = CellCenter(origin, startColumn, row);

            Vector3 center = new Vector3(start.x + (width - _cellSize) * 0.5f, -_floorThickness * 0.5f, start.y);
            Vector3 size = new Vector3(width, _floorThickness, _cellSize);

            CreateBox(floors, "Floor", center, size, _floorLayer, _floorMaterial);
        });

        WarnIfTooManyPieces(floors);
    }

    private void BuildWalls(Transform container, bool[,] walkable, Vector2 origin, int columns, int rows)
    {
        Transform walls = CreateChild(container, "Walls");

        bool[,] isWall = new bool[columns, rows];

        for (int column = 0; column < columns; column++)
        {
            for (int row = 0; row < rows; row++)
            {
                if (walkable[column, row]) continue;
                if (!HasWalkableNeighbour(walkable, columns, rows, column, row)) continue;

                isWall[column, row] = true;
            }
        }

        ForEachRun(isWall, columns, rows, true, (startColumn, endColumn, row) =>
        {
            float width = (endColumn - startColumn + 1) * _cellSize;
            Vector2 start = CellCenter(origin, startColumn, row);

            Vector3 center = new Vector3(start.x + (width - _cellSize) * 0.5f, _wallHeight * 0.5f, start.y);
            Vector3 size = new Vector3(width, _wallHeight, _cellSize);

            CreateBox(walls, "Wall", center, size, _wallLayer, _wallMaterial);
        });
    }

    private bool HasWalkableNeighbour(bool[,] walkable, int columns, int rows, int column, int row)
    {
        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dz = -1; dz <= 1; dz++)
            {
                if (dx == 0 && dz == 0) continue;

                int nx = column + dx;
                int nz = row + dz;

                if (nx < 0 || nx >= columns || nz < 0 || nz >= rows) continue;
                if (walkable[nx, nz]) return true;
            }
        }

        return false;
    }

    private void BuildZones(Transform container)
    {
        Transform zones = CreateChild(container, "Zones");

        foreach (LevelArea area in _areas)
        {
            if (area.zone == ZoneType.Corridor) continue;

            GameObject zoneObject = new GameObject(area.name);
            zoneObject.transform.SetParent(zones, false);
            zoneObject.transform.localPosition = new Vector3(area.center.x, 0f, area.center.y);
            zoneObject.layer = _zoneLayer;

            ZoneVolume volume = zoneObject.AddComponent<ZoneVolume>();
            volume.Configure(area.zone, area.size);

            BoxCollider trigger = zoneObject.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.size = new Vector3(area.size.x, _wallHeight, area.size.y);
            trigger.center = new Vector3(0f, _wallHeight * 0.5f, 0f);

            CreateZoneFloor(zoneObject.transform, area);
        }
    }

    private void CreateZoneFloor(Transform parent, LevelArea area)
    {
        Vector3 center = new Vector3(0f, 0.02f, 0f);
        Vector3 size = new Vector3(area.size.x, 0.04f, area.size.y);

        GameObject floor = CreateBox(parent, "ZoneFloor", center, size, _floorLayer, _floorMaterial);

        if (floor.TryGetComponent(out Collider collider))
            DestroyTarget(collider);

        floor.AddComponent<ZoneFloorTint>().SetColor(ZoneVolume.ColorFor(area.zone));
    }

    // Agrupa celdas consecutivas para generar una caja por tramo en vez de una por celda.
    private void ForEachRun(bool[,] cells, int columns, int rows, bool value, Action<int, int, int> onRun)
    {
        for (int row = 0; row < rows; row++)
        {
            int startColumn = -1;

            for (int column = 0; column <= columns; column++)
            {
                bool matches = column < columns && cells[column, row] == value;

                if (matches)
                {
                    if (startColumn < 0) startColumn = column;
                    continue;
                }

                if (startColumn < 0) continue;

                onRun(startColumn, column - 1, row);
                startColumn = -1;
            }
        }
    }

    private Transform CreateChild(Transform parent, string name)
    {
        GameObject child = new GameObject(name);
        child.transform.SetParent(parent, false);

        return child.transform;
    }

    private GameObject CreateBox(Transform parent, string name, Vector3 localPosition, Vector3 size, int layer, Material material)
    {
        GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        box.name = name;
        box.layer = layer;
        box.transform.SetParent(parent, false);
        box.transform.localPosition = localPosition;
        box.transform.localScale = size;

        if (material != null && box.TryGetComponent(out Renderer renderer))
            renderer.sharedMaterial = material;

        return box;
    }

    private void WarnIfTooManyPieces(Transform floors)
    {
        if (floors.childCount <= 400) return;

        Debug.LogWarning($"El piso genero {floors.childCount} objetos. Si molesta, subi el tamano de celda.", this);
    }

    private void DestroyTarget(UnityEngine.Object target)
    {
        if (Application.isPlaying) Destroy(target);
        else DestroyImmediate(target);
    }

    private void OnDrawGizmosSelected()
    {
        if (_areas == null) return;

        foreach (LevelArea area in _areas)
        {
            Gizmos.color = ZoneVolume.ColorFor(area.zone);
            Gizmos.DrawWireCube(
                new Vector3(area.center.x, 0f, area.center.y),
                new Vector3(area.size.x, 0.1f, area.size.y));
        }
    }
}
