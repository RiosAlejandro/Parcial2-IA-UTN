using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class WolfSetup
{
    private const string ScenePath = "Assets/Scenes/Parcial2IA.unity";

    private const int WolfLayer = 9;
    private const int HunterLayerMask = 1 << 3;
    private const int WallLayerMask = 1 << 8;

    private const float WolfScale = 1.4f;
    private const float ViewRadius = 8f;
    private const float ViewAngle = 100f;

    private class Route
    {
        public string Name;
        public bool PingPong;
        public Vector2[] Points;
    }

    private static readonly Route[] Routes =
    {
        new Route
        {
            Name = "L1 anillo oeste",
            PingPong = true,
            Points = new[] { new Vector2(-23.5f, 14f), new Vector2(-23.5f, -14f) }
        },
        new Route
        {
            Name = "L2 anillo norte y sala norte",
            PingPong = false,
            Points = new[]
            {
                new Vector2(-20f, 15.5f), new Vector2(20f, 15.5f),
                new Vector2(8f, 11f), new Vector2(-8f, 11f)
            }
        },
        new Route
        {
            Name = "L3 anillo este",
            PingPong = true,
            Points = new[] { new Vector2(23.5f, 14f), new Vector2(23.5f, -14f) }
        },
        new Route
        {
            Name = "L4 anillo sur y sala sur",
            PingPong = false,
            Points = new[]
            {
                new Vector2(20f, -15.5f), new Vector2(-20f, -15.5f),
                new Vector2(-8f, -11f), new Vector2(8f, -11f)
            }
        }
    };

    [MenuItem("Parcial2/Colocar lobos")]
    public static void PlaceWolves()
    {
        if (Object.FindFirstObjectByType<NavGraph>() == null)
            EditorSceneManager.OpenScene(ScenePath);

        NavGraph graph = Object.FindFirstObjectByType<NavGraph>();

        if (graph == null)
        {
            Debug.LogError("WOLVES ERROR: no hay NavGraph en la escena.");
            return;
        }

        Transform routesParent = ResetObject("WolfRoutes");
        Transform wolvesParent = ResetObject("Wolves");

        for (int i = 0; i < Routes.Length; i++)
            CreateWolf(Routes[i], i, graph, routesParent, wolvesParent);

        EditorSceneManager.MarkSceneDirty(wolvesParent.gameObject.scene);
        EditorSceneManager.SaveScene(wolvesParent.gameObject.scene);

        Debug.Log($"WOLVES colocados={Routes.Length}");
    }

    private static void CreateWolf(Route route, int index, NavGraph graph, Transform routesParent, Transform wolvesParent)
    {
        Transform[] waypoints = CreateWaypoints(route, routesParent);

        GameObject wolf = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        wolf.name = $"Wolf {index + 1}";
        wolf.layer = WolfLayer;
        wolf.transform.SetParent(wolvesParent, false);
        wolf.transform.position = new Vector3(route.Points[0].x, WolfScale, route.Points[0].y);
        wolf.transform.localScale = Vector3.one * WolfScale;

        Rigidbody body = wolf.AddComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;

        FieldOfViewOverlap sensor = wolf.AddComponent<FieldOfViewOverlap>();
        ConfigureSensor(sensor);

        wolf.AddComponent<FieldOfViewGizmos>();

        WolfAgent agent = wolf.AddComponent<WolfAgent>();
        ConfigureAgent(agent, graph, sensor, waypoints, route.PingPong);

        wolf.AddComponent<WolfFeedback>();
    }

    private static Transform[] CreateWaypoints(Route route, Transform routesParent)
    {
        GameObject group = new GameObject(route.Name);
        group.transform.SetParent(routesParent, false);

        Transform[] waypoints = new Transform[route.Points.Length];

        for (int i = 0; i < route.Points.Length; i++)
        {
            GameObject point = new GameObject($"WP {i + 1}");
            point.transform.SetParent(group.transform, false);
            point.transform.position = new Vector3(route.Points[i].x, 0.5f, route.Points[i].y);
            point.AddComponent<PointGizmo>();

            waypoints[i] = point.transform;
        }

        return waypoints;
    }

    private static void ConfigureSensor(FieldOfViewOverlap sensor)
    {
        SerializedObject serialized = new SerializedObject(sensor);

        SetFloat(serialized, "_viewRadius", ViewRadius);
        SetFloat(serialized, "_viewAngle", ViewAngle);
        SetFloat(serialized, "_eyeHeight", 1f);
        SetFloat(serialized, "_checkInterval", 0.15f);
        SetInt(serialized, "_targetMask", HunterLayerMask);
        SetInt(serialized, "_obstacleMask", WallLayerMask);

        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ConfigureAgent(WolfAgent agent, NavGraph graph, FieldOfViewSensor sensor, Transform[] waypoints, bool pingPong)
    {
        SerializedObject serialized = new SerializedObject(agent);

        SetObject(serialized, "_graph", graph);
        SetObject(serialized, "_sensor", sensor);
        SetBool(serialized, "_pingPong", pingPong);

        SerializedProperty array = serialized.FindProperty("_waypoints");

        if (array != null)
        {
            array.arraySize = waypoints.Length;

            for (int i = 0; i < waypoints.Length; i++)
                array.GetArrayElementAtIndex(i).objectReferenceValue = waypoints[i];
        }

        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetFloat(SerializedObject target, string name, float value)
    {
        SerializedProperty property = target.FindProperty(name);
        if (property != null) property.floatValue = value;
    }

    private static void SetInt(SerializedObject target, string name, int value)
    {
        SerializedProperty property = target.FindProperty(name);
        if (property != null) property.intValue = value;
    }

    private static void SetBool(SerializedObject target, string name, bool value)
    {
        SerializedProperty property = target.FindProperty(name);
        if (property != null) property.boolValue = value;
    }

    private static void SetObject(SerializedObject target, string name, Object value)
    {
        SerializedProperty property = target.FindProperty(name);
        if (property != null) property.objectReferenceValue = value;
    }

    private static Transform ResetObject(string name)
    {
        GameObject existing = GameObject.Find(name);

        if (existing != null)
            Object.DestroyImmediate(existing);

        return new GameObject(name).transform;
    }
}
