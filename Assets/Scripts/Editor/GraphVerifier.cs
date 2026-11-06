using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class GraphVerifier
{
    private const string ScenePath = "Assets/Scenes/Parcial2IA.unity";

    [MenuItem("Parcial2/Verificar grafo y A*")]
    public static void Verify()
    {
        if (Object.FindFirstObjectByType<NavGraph>() == null)
            EditorSceneManager.OpenScene(ScenePath);

        NavGraph graph = Object.FindFirstObjectByType<NavGraph>();

        if (graph == null)
        {
            Debug.LogError("VERIFY: no hay NavGraph en la escena.");
            return;
        }

        StringBuilder report = new StringBuilder();
        report.AppendLine($"VERIFY nodos={graph.NodeCount}");

        ReportZones(graph, report);
        ReportReachability(graph, report);
        ReportWolfFilter(graph, report);
        ReportZoneSearch(graph, report);

        Debug.Log(report.ToString());
    }

    private static void ReportZoneSearch(NavGraph graph, StringBuilder report)
    {
        ZoneVolume[] zones = Object.FindObjectsByType<ZoneVolume>(FindObjectsSortMode.None);
        ZoneVolume start = Find(zones, ZoneType.Start);
        ZoneVolume exit = Find(zones, ZoneType.Exit);

        if (start == null || exit == null) return;

        AStar astar = new AStar(graph);
        List<Vector3> path = new List<Vector3>();

        foreach (ZoneType zone in new[] { ZoneType.Hunt, ZoneType.Safe, ZoneType.Exit })
        {
            bool found = astar.TryFindPathToZone(start.Center, zone, path);

            report.AppendLine(found
                ? $"VERIFY zona-search Start -> {zone}: costo {astar.LastPathCost:0.0}, llega a {path[path.Count - 1]}"
                : $"VERIFY ERROR: zona-search no encontro {zone}");
        }

        if (astar.TryFindPathToZone(exit.Center, ZoneType.Safe, path))
        {
            Vector3 refuge = path[path.Count - 1];
            bool northEast = refuge.x > 0f && refuge.z > 0f;

            report.AppendLine(northEast
                ? $"VERIFY zona-search Salida -> Safe: elige el noreste {refuge} (costo {astar.LastPathCost:0.0})"
                : $"VERIFY ERROR: desde la Salida eligio el refugio lejano {refuge}");
        }
    }

    private static void ReportZones(NavGraph graph, StringBuilder report)
    {
        List<int> indices = new List<int>();

        foreach (ZoneType zone in System.Enum.GetValues(typeof(ZoneType)))
        {
            graph.CollectNodesInZone(zone, indices);
            report.AppendLine($"VERIFY zona {zone}: {indices.Count} nodos");
        }
    }

    private static void ReportReachability(NavGraph graph, StringBuilder report)
    {
        ZoneVolume[] zones = Object.FindObjectsByType<ZoneVolume>(FindObjectsSortMode.None);
        ZoneVolume start = Find(zones, ZoneType.Start);

        if (start == null)
        {
            report.AppendLine("VERIFY ERROR: no hay zona inicial");
            return;
        }

        AStar astar = new AStar(graph);
        List<Vector3> path = new List<Vector3>();

        foreach (ZoneVolume zone in zones)
        {
            if (zone == start) continue;

            bool found = astar.TryFindPath(start.Center, zone.Center, path);

            report.AppendLine(found
                ? $"VERIFY camino Start -> {zone.Type} ({zone.name}): {path.Count} puntos, costo {astar.LastPathCost:0.0}"
                : $"VERIFY ERROR: sin camino Start -> {zone.Type} ({zone.name})");
        }
    }

    private static void ReportWolfFilter(NavGraph graph, StringBuilder report)
    {
        ZoneVolume[] zones = Object.FindObjectsByType<ZoneVolume>(FindObjectsSortMode.None);
        ZoneVolume start = Find(zones, ZoneType.Start);
        ZoneVolume hunt = Find(zones, ZoneType.Hunt);
        ZoneVolume safe = Find(zones, ZoneType.Safe);

        if (start == null || hunt == null || safe == null)
        {
            report.AppendLine("VERIFY ERROR: faltan zonas para probar el filtro de lobos");
            return;
        }

        AStar astar = new AStar(graph);
        List<Vector3> path = new List<Vector3>();

        bool toHunt = astar.TryFindPath(start.Center, hunt.Center, path, WolfFilter);
        report.AppendLine(toHunt
            ? $"VERIFY lobo Start -> Hunt: OK ({path.Count} puntos)"
            : "VERIFY ERROR: el lobo no puede llegar al Coto");

        int safeNode = graph.NearestNode(safe.Center, WolfFilter);
        bool safeExcluded = safeNode < 0 || graph.GetNode(safeNode).zone != ZoneType.Safe;

        report.AppendLine(safeExcluded
            ? "VERIFY lobo -> zona segura: correctamente excluida"
            : "VERIFY ERROR: el lobo puede navegar dentro de una zona segura");
    }

    private static bool WolfFilter(NavNode node) => node.zone != ZoneType.Safe;

    private static ZoneVolume Find(ZoneVolume[] zones, ZoneType type)
    {
        foreach (ZoneVolume zone in zones)
        {
            if (zone.Type == type) return zone;
        }

        return null;
    }
}
