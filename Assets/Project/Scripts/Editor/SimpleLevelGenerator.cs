using System.IO;
using Pathfinding;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SimpleLevelGenerator : EditorWindow
{
    private int levelNumber = 1;
    private float levelWidth = 20f;
    private float levelLength = 30f;
    private LevelGeneratorConfig config;

    [MenuItem("Tools/Simple Level Generator")]
    public static void ShowWindow()
    {
        GetWindow<SimpleLevelGenerator>("Simple Level Generator");
    }

    private void OnGUI()
    {
        GUILayout.Label("SIMPLE Level Generator", EditorStyles.boldLabel);
        GUILayout.Space(10);

        config = (LevelGeneratorConfig)EditorGUILayout.ObjectField("Config Asset", config, typeof(LevelGeneratorConfig), false);

        if (config == null)
        {
            EditorGUILayout.HelpBox("Assignez une config", MessageType.Warning);
            return;
        }

        GUILayout.Space(10);

        levelNumber = EditorGUILayout.IntField("Level Number", levelNumber);
        levelWidth = EditorGUILayout.FloatField("Width (m)", levelWidth);
        levelLength = EditorGUILayout.FloatField("Length (m)", levelLength);

        GUILayout.Space(20);

        if (GUILayout.Button("GENERATE", GUILayout.Height(40)))
        {
            GenerateLevel();
        }
    }

    private void GenerateLevel()
    {
        // Validation
        if (levelNumber <= 0 || levelWidth <= 0 || levelLength <= 0)
        {
            EditorUtility.DisplayDialog("Erreur", "Les valeurs doivent être > 0", "OK");
            return;
        }

        // Nom scène
        string levelName = $"Level_{levelNumber:D3}";
        string scenePath = $"Assets/Project/Scenes/Levels/{levelName}.unity";

        // Créer dossier si besoin
        string folderPath = "Assets/Project/Scenes/Levels";
        if (!Directory.Exists(folderPath))
        {
            Directory.CreateDirectory(folderPath);
            AssetDatabase.Refresh();
        }

        // Créer scène
        Scene newScene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        // Root
        GameObject levelRoot = new GameObject(levelName);

        // Créer JUSTE 2 dossiers
        GameObject managersFolder = new GameObject("_Managers");
        managersFolder.transform.SetParent(levelRoot.transform);

        GameObject gameplayFolder = new GameObject("_Gameplay");
        gameplayFolder.transform.SetParent(levelRoot.transform);

        // === A* PATHFINDING ===
        GameObject astarObject = new GameObject("A* Pathfinding");
        astarObject.transform.SetParent(managersFolder.transform);
        AstarPath astarPath = astarObject.AddComponent<AstarPath>();

        // === PIT GRID DATA ===
        string pitGridPath = $"Assets/Project/ScriptableObjects/Levels/Lvl{levelNumber:D3}_Grid.asset";
        string pitGridFolder = "Assets/Project/ScriptableObjects/Levels";

        if (!Directory.Exists(pitGridFolder))
        {
            Directory.CreateDirectory(pitGridFolder);
            AssetDatabase.Refresh();
        }

        if (File.Exists(pitGridPath))
        {
            AssetDatabase.DeleteAsset(pitGridPath);
        }

        PitGridData pitGridData = ScriptableObject.CreateInstance<PitGridData>();
        pitGridData.gridCellSize = 0.5f;
        AssetDatabase.CreateAsset(pitGridData, pitGridPath);
        AssetDatabase.SaveAssets();

        GameObject pitGridManager = new GameObject("PitGridManager");
        pitGridManager.transform.SetParent(gameplayFolder.transform);

        // === GRID GRAPH A* ===
        GridGraph gridGraph = astarPath.data.AddGraph(typeof(GridGraph)) as GridGraph;

        if (gridGraph != null)
        {
            gridGraph.center = new Vector3(0, 0, levelLength / 2f);
            gridGraph.SetDimensions((int)(levelWidth / 0.25f), (int)(levelLength / 0.25f), 0.25f);
            gridGraph.erodeIterations = 0;

            gridGraph.collision.collisionCheck = true;
            gridGraph.collision.diameter = 0.5f;
            gridGraph.collision.height = 2f;
            gridGraph.collision.mask = LayerMask.GetMask("Obstacle");

            gridGraph.collision.use2D = false;
            gridGraph.collision.heightCheck = true;
            gridGraph.collision.fromHeight = 100f;
            gridGraph.collision.unwalkableWhenNoGround = false;
            gridGraph.collision.heightMask = LayerMask.GetMask("Ground");

            gridGraph.neighbours = NumNeighbours.Four;

            Debug.Log("<color=cyan>Grid Graph configuré</color>");
        }

        // === PREFABS DU SO ===
        foreach (var entry in config.prefabsToSpawn)
        {
            if (!entry.enabled || entry.prefab == null)
                continue;

            GameObject parentFolder = gameplayFolder;

            // Récupérer le path et recharger
            string assetPath = AssetDatabase.GetAssetPath(entry.prefab);
            GameObject prefabToInstantiate = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);

            if (prefabToInstantiate == null)
            {
                Debug.LogError($"Impossible de charger : {assetPath}");
                continue;
            }

            // Instancier
            GameObject obj = PrefabUtility.InstantiatePrefab(prefabToInstantiate) as GameObject;
            obj.transform.SetParent(parentFolder.transform);
            obj.transform.localPosition = entry.position;

            Debug.Log($"<color=green>Prefab instancié</color>");
        }

        // Sauvegarder
        EditorSceneManager.SaveScene(newScene, scenePath);

        Debug.Log($"<color=green>Level créé !</color>");
    }
}