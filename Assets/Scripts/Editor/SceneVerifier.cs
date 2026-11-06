using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class SceneVerifier
{
    private const string ScenePath = "Assets/Scenes/Parcial2IA.unity";

    private const int BoidLayer = 6;
    private const int HunterLayer = 3;
    private const int WallLayer = 8;
    private const int WolfLayer = 9;

    [MenuItem("Parcial2/Verificar integracion de agentes")]
    public static void VerifyAgents()
    {
        if (Object.FindFirstObjectByType<NavGraph>() == null)
            EditorSceneManager.OpenScene(ScenePath);

        StringBuilder report = new StringBuilder();
        int errors = 0;

        ArenaBounds bounds = Object.FindFirstObjectByType<ArenaBounds>();
        ZoneVolume hunt = FindZone(ZoneType.Hunt);
        ZoneVolume start = FindZone(ZoneType.Start);

        if (bounds == null || hunt == null || start == null)
        {
            Debug.LogError("SCENE ERROR: falta ArenaBounds, el Coto o la Zona inicial.");
            return;
        }

        report.AppendLine($"SCENE coto bounds {bounds.Width}x{bounds.Height} en {bounds.Center}");
        errors += Check(report, "bounds dentro del Coto",
            bounds.Width <= hunt.Size.x && bounds.Height <= hunt.Size.y);

        errors += VerifyBoids(report, bounds);
        errors += VerifyHunter(report, start);
        errors += VerifyWolves(report);

        report.AppendLine(errors == 0 ? "SCENE OK" : $"SCENE {errors} PROBLEMAS");
        Debug.Log(report.ToString());
    }

    private static int VerifyBoids(StringBuilder report, ArenaBounds bounds)
    {
        BoidAgent[] boids = Object.FindObjectsByType<BoidAgent>(FindObjectsSortMode.None);
        int errors = 0;

        report.AppendLine($"SCENE boids: {boids.Length}");
        errors += Check(report, "al menos 6 boids", boids.Length >= 6);

        int outside = 0;
        int wrongLayer = 0;
        int withoutCollider = 0;
        int badMask = 0;

        foreach (BoidAgent boid in boids)
        {
            if (!bounds.Contains(boid.transform.position)) outside++;
            if (boid.gameObject.layer != BoidLayer) wrongLayer++;
            if (boid.GetComponent<Collider>() == null) withoutCollider++;

            FieldOfViewSensor sensor = boid.GetComponent<FieldOfViewSensor>();
            if (sensor == null || !ObstacleMaskCoversWalls(sensor)) badMask++;
        }

        errors += Check(report, "todos dentro del coto", outside == 0, $"{outside} fuera");
        errors += Check(report, "todos en la capa Boid", wrongLayer == 0, $"{wrongLayer} mal");
        errors += Check(report, "todos con collider", withoutCollider == 0, $"{withoutCollider} sin collider");
        errors += Check(report, "sensor bloqueado por paredes", badMask == 0, $"{badMask} con mascara incorrecta");

        return errors;
    }

    private static bool ObstacleMaskCoversWalls(FieldOfViewSensor sensor)
    {
        SerializedObject serialized = new SerializedObject(sensor);
        SerializedProperty property = serialized.FindProperty("_obstacleMask");

        return property != null && (property.intValue & (1 << WallLayer)) != 0;
    }

    private static int VerifyHunter(StringBuilder report, ZoneVolume start)
    {
        HunterAgent hunter = Object.FindFirstObjectByType<HunterAgent>();
        int errors = 0;

        errors += Check(report, "hay cazador", hunter != null);
        if (hunter == null) return errors;

        report.AppendLine($"SCENE cazador en {hunter.transform.position}");

        errors += Check(report, "cazador en la Zona inicial", start.Contains(hunter.transform.position));
        errors += Check(report, "cazador en la capa Hunter", hunter.gameObject.layer == HunterLayer);
        errors += Check(report, "cazador con collider", hunter.GetComponent<Collider>() != null);
        errors += Check(report, "cazador con feedback", hunter.GetComponent<HunterFeedback>() != null);

        SerializedObject serialized = new SerializedObject(hunter);
        SerializedProperty prefab = serialized.FindProperty("_pointOfInterestPrefab");

        errors += Check(report, "prefab de objeto de interes asignado",
            prefab != null && prefab.objectReferenceValue != null);

        return errors;
    }

    private static int VerifyWolves(StringBuilder report)
    {
        WolfAgent[] wolves = Object.FindObjectsByType<WolfAgent>(FindObjectsSortMode.None);
        HunterAgent hunter = Object.FindFirstObjectByType<HunterAgent>();
        int errors = 0;

        report.AppendLine($"SCENE lobos: {wolves.Length}");
        errors += Check(report, "al menos 4 lobos", wolves.Length >= 4);

        if (wolves.Length == 0) return errors;

        float hunterSpeed = ReadFloat(hunter, "_moveSpeed");
        int wrongLayer = 0;
        int withoutCollider = 0;
        int withoutWaypoints = 0;
        int withoutSensor = 0;
        int tooFast = 0;

        foreach (WolfAgent wolf in wolves)
        {
            if (wolf.gameObject.layer != WolfLayer) wrongLayer++;
            if (wolf.GetComponent<Collider>() == null) withoutCollider++;
            if (!wolf.HasWaypoints) withoutWaypoints++;

            FieldOfViewSensor sensor = wolf.GetComponent<FieldOfViewSensor>();
            if (sensor == null || !TargetsHunter(sensor)) withoutSensor++;

            float patrol = ReadFloat(wolf, "_patrolSpeed");
            float chase = ReadFloat(wolf, "_chaseSpeed");

            if (patrol >= hunterSpeed || chase >= hunterSpeed) tooFast++;
        }

        errors += Check(report, "todos en la capa Wolf", wrongLayer == 0, $"{wrongLayer} mal");
        errors += Check(report, "todos con collider", withoutCollider == 0, $"{withoutCollider} sin collider");
        errors += Check(report, "todos con ruta de patrulla", withoutWaypoints == 0, $"{withoutWaypoints} sin waypoints");
        errors += Check(report, "todos con sensor apuntando al Cazador", withoutSensor == 0, $"{withoutSensor} mal");
        errors += Check(report, $"todos mas lentos que el Cazador ({hunterSpeed})", tooFast == 0, $"{tooFast} muy rapidos");

        return errors;
    }

    private static bool TargetsHunter(FieldOfViewSensor sensor)
    {
        SerializedObject serialized = new SerializedObject(sensor);
        SerializedProperty target = serialized.FindProperty("_targetMask");
        SerializedProperty obstacle = serialized.FindProperty("_obstacleMask");

        return target != null && (target.intValue & (1 << HunterLayer)) != 0
            && obstacle != null && (obstacle.intValue & (1 << WallLayer)) != 0;
    }

    private static float ReadFloat(Object target, string name)
    {
        if (target == null) return 0f;

        SerializedProperty property = new SerializedObject(target).FindProperty(name);

        return property != null ? property.floatValue : 0f;
    }

    private static int Check(StringBuilder report, string label, bool passed, string detail = "")
    {
        report.AppendLine(passed
            ? $"SCENE   ok   {label}"
            : $"SCENE   FALLA {label} {detail}");

        return passed ? 0 : 1;
    }

    private static ZoneVolume FindZone(ZoneType type)
    {
        foreach (ZoneVolume zone in Object.FindObjectsByType<ZoneVolume>(FindObjectsSortMode.None))
        {
            if (zone.Type == type) return zone;
        }

        return null;
    }
}
