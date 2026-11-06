using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class LevelSetup
{
    private const string ScenePath = "Assets/Scenes/Parcial2IA.unity";

    [MenuItem("Parcial2/Crear escena base")]
    public static void CreateBaseScene()
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        GameObject level = new GameObject("Level");
        LevelBuilder builder = level.AddComponent<LevelBuilder>();
        NavGraph graph = level.AddComponent<NavGraph>();

        builder.LoadDefaultLayout();
        builder.Build();

        Bounds bounds = builder.GetWorldBounds();
        graph.ConfigureArea(
            new Vector2(bounds.center.x, bounds.center.z),
            new Vector2(bounds.size.x + 4f, bounds.size.z + 4f));

        Physics.SyncTransforms();
        graph.Generate();

        PlaceCamera();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);

        Debug.Log($"Escena guardada en {ScenePath}. Nodos: {graph.NodeCount}.");
    }

    [MenuItem("Parcial2/Regenerar grafo de la escena actual")]
    public static void RegenerateGraph()
    {
        NavGraph graph = Object.FindFirstObjectByType<NavGraph>();

        if (graph == null)
        {
            Debug.LogError("No hay NavGraph en la escena.");
            return;
        }

        Physics.SyncTransforms();
        graph.Generate();

        EditorSceneManager.MarkSceneDirty(graph.gameObject.scene);
    }

    private static void PlaceCamera()
    {
        Camera camera = Object.FindFirstObjectByType<Camera>();
        if (camera == null) return;

        camera.transform.position = new Vector3(0f, 58f, -26f);
        camera.transform.rotation = Quaternion.Euler(62f, 0f, 0f);
        camera.farClipPlane = 200f;
    }
}
