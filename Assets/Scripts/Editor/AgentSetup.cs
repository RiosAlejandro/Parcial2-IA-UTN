using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class AgentSetup
{
    private const string ScenePath = "Assets/Scenes/Parcial2IA.unity";
    private const string BoidPrefabPath = "Assets/Prefaps/CapsuleBoid.prefab";
    private const string PointOfInterestPrefabPath = "Assets/Prefaps/Sphere.prefab";

    private const int HunterLayer = 3;
    private const int BoidCount = 8;
    private const float BoidHeight = 1f;
    private const float HunterScale = 2f;
    private const float CotoMargin = 1f;

    [MenuItem("Parcial2/Colocar agentes del Parcial 1")]
    public static void PlaceAgents()
    {
        if (Object.FindFirstObjectByType<NavGraph>() == null)
            EditorSceneManager.OpenScene(ScenePath);

        ZoneVolume hunt = FindZone(ZoneType.Hunt);
        ZoneVolume start = FindZone(ZoneType.Start);

        if (hunt == null || start == null)
        {
            Debug.LogError("AGENTS ERROR: falta la zona del Coto o la Zona inicial. Construi el nivel primero.");
            return;
        }

        ArenaBounds bounds = SetupBounds(hunt);
        int placed = SetupBoids(bounds);
        GameObject hunter = SetupHunter(start);

        EditorSceneManager.MarkSceneDirty(hunter.scene);
        EditorSceneManager.SaveScene(hunter.scene);

        Debug.Log($"AGENTS boids={placed} coto={bounds.Width}x{bounds.Height} cazador={hunter.transform.position}");
    }

    private static ArenaBounds SetupBounds(ZoneVolume hunt)
    {
        ArenaBounds bounds = Object.FindFirstObjectByType<ArenaBounds>();

        if (bounds == null)
            bounds = new GameObject("CotoBounds").AddComponent<ArenaBounds>();

        bounds.Configure(
            hunt.Center,
            hunt.Size.x - CotoMargin * 2f,
            hunt.Size.y - CotoMargin * 2f);

        return bounds;
    }

    private static int SetupBoids(ArenaBounds bounds)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BoidPrefabPath);

        if (prefab == null)
        {
            Debug.LogError($"AGENTS ERROR: no encontre el prefab {BoidPrefabPath}");
            return 0;
        }

        Transform parent = ResetParent("Boids");

        for (int i = 0; i < BoidCount; i++)
        {
            GameObject boid = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);

            Vector3 point = bounds.GetRandomPoint();
            point.y = BoidHeight;

            boid.transform.position = point;
            boid.name = i == 0 ? "CapsuleBoid" : $"CapsuleBoid ({i})";
        }

        return BoidCount;
    }

    private static GameObject SetupHunter(ZoneVolume start)
    {
        HunterAgent existing = Object.FindFirstObjectByType<HunterAgent>();

        if (existing != null)
            Object.DestroyImmediate(existing.gameObject);

        GameObject hunter = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        hunter.name = "CapsuleHunter";
        hunter.layer = HunterLayer;
        hunter.transform.position = new Vector3(start.Center.x, HunterScale, start.Center.z);
        hunter.transform.localScale = Vector3.one * HunterScale;

        Rigidbody body = hunter.AddComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;

        HunterAgent agent = hunter.AddComponent<HunterAgent>();
        hunter.AddComponent<HunterFeedback>();

        AssignPointOfInterestPrefab(agent);
        AssignGraph(agent);

        return hunter;
    }

    private static void AssignGraph(HunterAgent agent)
    {
        NavGraph graph = Object.FindFirstObjectByType<NavGraph>();

        if (graph == null)
        {
            Debug.LogError("AGENTS ERROR: no hay NavGraph en la escena");
            return;
        }

        SerializedObject serialized = new SerializedObject(agent);
        SerializedProperty property = serialized.FindProperty("_graph");

        if (property == null)
        {
            Debug.LogError("AGENTS ERROR: HunterAgent no tiene el campo _graph");
            return;
        }

        property.objectReferenceValue = graph;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void AssignPointOfInterestPrefab(HunterAgent agent)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PointOfInterestPrefabPath);

        if (prefab == null)
        {
            Debug.LogError($"AGENTS ERROR: no encontre el prefab {PointOfInterestPrefabPath}");
            return;
        }

        SerializedObject serialized = new SerializedObject(agent);
        SerializedProperty property = serialized.FindProperty("_pointOfInterestPrefab");

        if (property == null)
        {
            Debug.LogError("AGENTS ERROR: HunterAgent no tiene el campo _pointOfInterestPrefab");
            return;
        }

        property.objectReferenceValue = prefab;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static Transform ResetParent(string name)
    {
        GameObject existing = GameObject.Find(name);

        if (existing != null)
            Object.DestroyImmediate(existing);

        return new GameObject(name).transform;
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
