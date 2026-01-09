using System.IO;
using Pathfinding;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI; 

public class LevelGeneratorWindow : EditorWindow
{
    // Input fields
    private int levelNumber = 1;
    private float levelWidth = 20f;
    private float levelLength = 30f;
    private LevelGeneratorConfig config;


    // Menu item pour ouvrir la fenetre
    [MenuItem("Tools/Level Generator")]
    public static void ShowWindow()
    {
        GetWindow<LevelGeneratorWindow>("Level Generator");
    }

    private void OnGUI()
    {
        GUILayout.Label("Level Generator - Etape 1", EditorStyles.boldLabel);
        GUILayout.Space(10);

        // Section Config
        GUILayout.Label("Configuration", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        config = (LevelGeneratorConfig)EditorGUILayout.ObjectField(
            "Config Asset",
            config,
            typeof(LevelGeneratorConfig),
            false
        );

        // Bouton editer config
        GUI.enabled = config != null;
        if (GUILayout.Button("Editer", GUILayout.Width(60)))
        {
            Selection.activeObject = config;
        }
        GUI.enabled = true;

        EditorGUILayout.EndHorizontal();

        // Bouton creer nouvelle config
        if (config == null)
        {
            EditorGUILayout.HelpBox("Assignez une config ou creez-en une nouvelle.", MessageType.Warning);

            if (GUILayout.Button("Creer Nouvelle Config"))
            {
                CreateNewConfig();
            }
            return;
        }

        GUILayout.Space(10);

        // Validation et preview
        int activePrefabs = 0;
        int nullPrefabs = 0;

        foreach (var entry in config.prefabsToSpawn)
        {
            if (entry.enabled)
            {
                if (entry.prefab != null)
                    activePrefabs++;
                else
                    nullPrefabs++;
            }
        }

        // Affichage preview
        if (activePrefabs > 0)
        {
            EditorGUILayout.HelpBox($"{activePrefabs} prefab(s) recurrent(s) seront instancies", MessageType.Info);
        }
        else
        {
            EditorGUILayout.HelpBox("Aucun prefab recurrent actif dans cette config", MessageType.None);
        }

        // Warning si prefabs null
        if (nullPrefabs > 0)
        {
            EditorGUILayout.HelpBox($"ATTENTION: {nullPrefabs} prefab(s) actif(s) sont null !", MessageType.Warning);
        }

        GUILayout.Space(20);

        // Section Settings
        GUILayout.Label("Level Settings", EditorStyles.boldLabel);
        levelNumber = EditorGUILayout.IntField("Level Number", levelNumber);
        levelWidth = EditorGUILayout.FloatField("Width (m)", levelWidth);
        levelLength = EditorGUILayout.FloatField("Length (m)", levelLength);

        GUILayout.Space(20);

        // Bouton generation (desactive si problemes)
        GUI.enabled = nullPrefabs == 0;
        if (GUILayout.Button("GENERATE LEVEL", GUILayout.Height(40)))
        {
            GenerateLevel();
        }
        GUI.enabled = true;

        if (nullPrefabs > 0)
        {
            EditorGUILayout.HelpBox("Corrigez les prefabs null avant de generer", MessageType.Error);
        }
    }

    private void GenerateLevel()
    {
        // Validation
        if (levelNumber <= 0)
        {
            EditorUtility.DisplayDialog("Erreur", "Le numero de niveau doit etre superieur a 0", "OK");
            return;
        }

        if (levelWidth <= 0 || levelLength <= 0)
        {
            EditorUtility.DisplayDialog("Erreur", "Les dimensions doivent etre superieures a 0", "OK");
            return;
        }

        // Nom du niveau
        string levelName = $"Level_{levelNumber:D3}";

        // Path de sauvegarde
        string scenePath = $"Assets/Project/Scenes/Levels/{levelName}.unity";

        // Verifier si le dossier existe, sinon le creer
        string folderPath = "Assets/Project/Scenes/Levels";
        if (!Directory.Exists(folderPath))
        {
            Directory.CreateDirectory(folderPath);
            AssetDatabase.Refresh();
        }

        // Verifier si la scene existe deja
        if (File.Exists(scenePath))
        {
            bool overwrite = EditorUtility.DisplayDialog(
                "Scene existe deja",
                $"La scene {levelName} existe deja. Voulez-vous la remplacer ?",
                "Oui",
                "Non"
            );

            if (!overwrite)
            {
                Debug.Log("Generation annulee par l'utilisateur.");
                return;
            }
        }

        // Creer nouvelle scene vide
        Scene newScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // Creer GameObject root
        GameObject levelRoot = new GameObject(levelName);
        levelRoot.transform.position = Vector3.zero;

        /*// Creer Quad de visualisation sol
        GameObject groundPlane = GameObject.CreatePrimitive(PrimitiveType.Quad);
        groundPlane.name = "GroundPlane_TEMP";
        groundPlane.transform.SetParent(levelRoot.transform);
        
        // Position : centre du terrain en Z
        groundPlane.transform.position = new Vector3(0, 0, levelLength / 2f);

        // Rotation : couche au sol
        groundPlane.transform.rotation = Quaternion.Euler(90, 0, 0);

        // Scale : dimensions du terrain
        groundPlane.transform.localScale = new Vector3(levelWidth, levelLength, 1);

        // Material gris de base
        MeshRenderer renderer = groundPlane.GetComponent<MeshRenderer>();

        // Essayer plusieurs shaders selon le pipeline
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Standard");
        if (shader == null)
            shader = Shader.Find("Unlit/Color");

        Material greyMat = new Material(shader);
        greyMat.color = new Color(0.5f, 0.5f, 0.5f);

        // Si URP Lit, set aussi la base color
        if (greyMat.HasProperty("_BaseColor"))
            greyMat.SetColor("_BaseColor", new Color(0.5f, 0.5f, 0.5f));

        renderer.material = greyMat;

        */

        // Creer la hierarchie de dossiers vides
        GameObject managersFolder = new GameObject("_Managers");
        managersFolder.transform.SetParent(levelRoot.transform);
        managersFolder.transform.position = Vector3.zero;

        GameObject gameplayFolder = new GameObject("_Gameplay");
        gameplayFolder.transform.SetParent(levelRoot.transform);
        gameplayFolder.transform.position = Vector3.zero;

        GameObject cameraFolder = new GameObject("_Camera");
        cameraFolder.transform.SetParent(levelRoot.transform);
        cameraFolder.transform.position = Vector3.zero;

        GameObject lightingFolder = new GameObject("_Lighting");
        lightingFolder.transform.SetParent(levelRoot.transform);
        lightingFolder.transform.position = Vector3.zero;

        GameObject uiFolder = new GameObject("_UI");
        uiFolder.transform.SetParent(levelRoot.transform);
        uiFolder.transform.position = Vector3.zero;

        /// === MANAGERS ===

        // InputManager
        GameObject inputManager = new GameObject("InputManager");
        inputManager.transform.SetParent(managersFolder.transform);
        inputManager.transform.position = Vector3.zero;
        inputManager.AddComponent<PlayerInputManager>();

        // LevelManager
        GameObject levelManager = new GameObject("LevelManager");
        levelManager.transform.SetParent(managersFolder.transform);
        levelManager.transform.position = Vector3.zero;
        LevelManager levelManagerScript = levelManager.AddComponent<LevelManager>();

        // CountdownManager
        GameObject countdownManager = new GameObject("CountdownManager");
        countdownManager.transform.SetParent(managersFolder.transform);
        countdownManager.transform.position = Vector3.zero;
        countdownManager.AddComponent<CountdownManager>();
        // References (shutter, gameManager) a assigner manuellement

        // SentinelCycleManager
        GameObject sentinelCycleManager = new GameObject("SentinelCycleManager");
        sentinelCycleManager.transform.SetParent(managersFolder.transform);
        sentinelCycleManager.transform.position = Vector3.zero;
        sentinelCycleManager.AddComponent<SentinelCycleManager>();

        // GameManager
        GameObject gameManager = new GameObject("GameManager");
        gameManager.transform.SetParent(managersFolder.transform);
        gameManager.transform.position = Vector3.zero;
        GameManager gameManagerScript = gameManager.AddComponent<GameManager>();
        gameManager.AddComponent<AudioSource>(); // Le script a besoin d'un AudioSource

        // Assigner GameManager au CountdownManager
        CountdownManager cdScript = countdownManager.GetComponent<CountdownManager>();
        if (cdScript != null && gameManager != null)
        {
            cdScript.gameManager = gameManagerScript;
            Debug.Log("<color=cyan>CountdownManager.gameManager assigné</color>");
        }

        // A* Pathfinding
        GameObject astarObject = new GameObject("A* Pathfinding");
        astarObject.transform.SetParent(managersFolder.transform);
        astarObject.transform.position = Vector3.zero;

        AstarPath astarPath = astarObject.AddComponent<AstarPath>();

        // === PIT GRID DATA ===

        // Path de sauvegarde du SO
        string pitGridPath = $"Assets/Project/ScriptableObjects/Levels/Lvl{levelNumber:D3}_Grid.asset";

        // Verifier si le dossier existe
        string pitGridFolder = "Assets/Project/ScriptableObjects/Levels";
        if (!Directory.Exists(pitGridFolder))
        {
            Directory.CreateDirectory(pitGridFolder);
            AssetDatabase.Refresh();
        }

        // Supprimer l'ancien SO s'il existe
        if (File.Exists(pitGridPath))
        {
            AssetDatabase.DeleteAsset(pitGridPath);
            Debug.Log($"<color=yellow>Ancien PitGridData supprime</color>");
        }

        // Creer le nouveau ScriptableObject PitGridData
        PitGridData pitGridData = ScriptableObject.CreateInstance<PitGridData>();
        pitGridData.gridCellSize = 0.5f;

        // Sauvegarder le ScriptableObject
        AssetDatabase.CreateAsset(pitGridData, pitGridPath);
        AssetDatabase.SaveAssets();

        Debug.Log($"<color=green>PitGridData cree : {pitGridPath}</color>");

        // GameObject qui reference le PitGridData (optionnel)
        GameObject pitGridManager = new GameObject("PitGridManager");
        pitGridManager.transform.SetParent(gameplayFolder.transform);
        pitGridManager.transform.position = Vector3.zero;

        // Creer un Grid Graph
        GridGraph gridGraph = astarPath.data.AddGraph(typeof(GridGraph)) as GridGraph;

        if (gridGraph != null)
        {
            // Configuration du graph
            gridGraph.center = new Vector3(0, 0, levelLength / 2f);
            gridGraph.SetDimensions((int)(levelWidth / 0.25f), (int)(levelLength / 0.25f), 0.25f);

            // Erosion
            gridGraph.erodeIterations = 0;

            // Collision Testing  
            gridGraph.collision.collisionCheck = true;
            gridGraph.collision.diameter = 0.5f;
            gridGraph.collision.height = 2f;
            gridGraph.collision.mask = LayerMask.GetMask("Obstacle"); // Obstacle Layer Mask

            // Height Testing
            gridGraph.collision.use2D = false;
            gridGraph.collision.heightCheck = true;
            gridGraph.collision.fromHeight = 100f;
            gridGraph.collision.unwalkableWhenNoGround = false;
            gridGraph.collision.heightMask = LayerMask.GetMask("Ground");



            // Cut corners
            gridGraph.neighbours = NumNeighbours.Four; // Pas de diagonales

            Debug.Log("<color=cyan>Grid Graph configure automatiquement</color>");
        }


        // === GAMEPLAY ===

        // FinalZoneTrigger
        GameObject finalZoneTrigger = new GameObject("FinalZoneTrigger");
        finalZoneTrigger.transform.SetParent(gameplayFolder.transform);
        finalZoneTrigger.transform.position = new Vector3(0, 0.5f, levelLength - 5f);

        BoxCollider triggerCollider = finalZoneTrigger.AddComponent<BoxCollider>();
        triggerCollider.isTrigger = true;
        triggerCollider.size = new Vector3(levelWidth, 1f, 10f);

        CameraZoneSwitcher zoneSwitcher = finalZoneTrigger.AddComponent<CameraZoneSwitcher>();
        // Les references cameras seront assignees a la fin quand on cree les cameras

        // === LEVEL ESSENTIELS ===

        // Creer dossier LevelEssentiels
        GameObject levelEssentielsFolder = new GameObject("LevelEssentiels");
        levelEssentielsFolder.transform.SetParent(gameplayFolder.transform);
        levelEssentielsFolder.transform.position = Vector3.zero;

        // Charger les prefabs
        GameObject startZonePlatformPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Project/Prefabs/LevelEssentiels/StartZonePlatform.prefab");
        GameObject metalShutterPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Project/Prefabs/LevelEssentiels/MetalShutter.prefab");
        GameObject mainSentinelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Project/Prefabs/LevelEssentiels/MainSentinel.prefab");
        GameObject goalDoorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Project/Prefabs/LevelEssentiels/GoalDoor.prefab");

        // Instancier StartZonePlatform
        GameObject startZonePlatform = null;
        if (startZonePlatformPrefab != null)
        {
            startZonePlatform = PrefabUtility.InstantiatePrefab(startZonePlatformPrefab) as GameObject;
            startZonePlatform.transform.SetParent(levelEssentielsFolder.transform);
            startZonePlatform.transform.position = new Vector3(0, 0.02f, 1.2f); // Centré sur sa profondeur 2.4m

            // Ajuster dimensions : largeur = levelWidth, épaisseur = 0.2m, profondeur = 2.4m
            startZonePlatform.transform.localScale = new Vector3(levelWidth, 0.2f, 2.4f);
        }
        else
        {
            Debug.LogWarning("Prefab StartZonePlatform non trouvé !");
        }

        // Instancier MetalShutter
        GameObject metalShutter = null;
        if (metalShutterPrefab != null)
        {
            metalShutter = PrefabUtility.InstantiatePrefab(metalShutterPrefab) as GameObject;
            metalShutter.transform.SetParent(levelEssentielsFolder.transform);
            metalShutter.transform.localScale = new Vector3(levelWidth, 2.5f, 0.5f);

            // Positionner : Y = moitié de la hauteur pour que le bas touche le sol
            metalShutter.transform.position = new Vector3(0, 1.25f, 2.4f); // Y = 2.5 / 2
        }
        else
        {
            Debug.LogWarning("Prefab MetalShutter non trouvé !");
        }

        // Instancier GoalDoor
        GameObject goalDoor = null;
        if (goalDoorPrefab != null)
        {
            goalDoor = PrefabUtility.InstantiatePrefab(goalDoorPrefab) as GameObject;
            goalDoor.transform.SetParent(levelEssentielsFolder.transform);
            goalDoor.transform.position = new Vector3(0, 0, levelLength); // Position temporaire

            // Calculer la hauteur réelle du prefab et ajuster Y
            MeshRenderer renderer = goalDoor.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                float halfHeight = renderer.bounds.extents.y;
                goalDoor.transform.position = new Vector3(0, halfHeight, levelLength);
            }
        }
        else
        {
            Debug.LogWarning("Prefab GoalDoor non trouvé !");
        }

        // Instancier MainSentinel
        GameObject mainSentinel = null;
        if (mainSentinelPrefab != null)
        {
            mainSentinel = PrefabUtility.InstantiatePrefab(mainSentinelPrefab) as GameObject;
            mainSentinel.transform.SetParent(levelEssentielsFolder.transform);
            mainSentinel.transform.position = new Vector3(0, 5, levelLength); // Position temporaire

            // Calculer la hauteur réelle du prefab et ajuster Y (5m + demi-hauteur)
            MeshRenderer renderer = mainSentinel.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                float halfHeight = renderer.bounds.extents.y;
                mainSentinel.transform.position = new Vector3(0, 5 + halfHeight, levelLength);
            }
        }
        else
        {
            Debug.LogWarning("Prefab MainSentinel non trouvé !");
        }

        // === PREFABS RECURRENTS (depuis config SO) ===

        Debug.Log($"<color=cyan>Instanciation de {config.prefabsToSpawn.Length} prefabs recurrents...</color>");

        foreach (var entry in config.prefabsToSpawn)
        {
            if (!entry.enabled || entry.prefab == null)
            {
                if (entry.enabled && entry.prefab == null)
                    Debug.LogWarning($"<color=yellow>Prefab '{entry.name}' active mais null !</color>");
                continue;
            }

            // Trouver le parent
            GameObject parentFolder = null;
            switch (entry.parentFolder)
            {
                case "_Managers": parentFolder = managersFolder; break;
                case "_Gameplay": parentFolder = gameplayFolder; break;
                case "_Camera": parentFolder = cameraFolder; break;
                case "_Lighting": parentFolder = lightingFolder; break;
                case "_UI": parentFolder = uiFolder; break;
                default: parentFolder = levelRoot; break;
            }

            // Instancier
            GameObject instance = PrefabUtility.InstantiatePrefab(entry.prefab) as GameObject;
            instance.transform.SetParent(parentFolder.transform);
            instance.transform.localPosition = entry.position;
            instance.transform.localRotation = Quaternion.Euler(entry.rotation);
            instance.transform.localScale = entry.scale;

            Debug.Log($"<color=green>Prefab '{entry.name}' instancie dans {entry.parentFolder}</color>");
        }

        // === ASSIGNATION AUTOMATIQUE DES REFERENCES ===

        // CountdownManager references
        CountdownManager countdownScript = countdownManager.GetComponent<CountdownManager>();
        if (countdownScript != null && metalShutter != null)
        {
            MetalShutter shutterScript = metalShutter.GetComponent<MetalShutter>();
            if (shutterScript != null)
            {
                countdownScript.shutter = shutterScript;
                Debug.Log("<color=cyan>CountdownManager.shutter assigné</color>");
            }
        }

        // Assigner gameManager au CountdownManager (on le fera après avoir créé GameManager)
        // Pour l'instant on note qu'il faudra le faire

        // === CAMERA SYSTEM ===

        // Charger le prefab Camera
        GameObject cameraPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Project/Prefabs/Camera/Camera_Prefab.prefab");

        if (cameraPrefab != null)
        {
            GameObject cameraSystem = PrefabUtility.InstantiatePrefab(cameraPrefab) as GameObject;
            cameraSystem.transform.SetParent(cameraFolder.transform);
            cameraSystem.transform.position = Vector3.zero;

            Debug.Log("<color=cyan>Camera System instancié</color>");
        }
        else
        {
            Debug.LogWarning("Prefab Camera_Prefab non trouvé dans Prefabs/Camera/ !");
        }

        // === UI ===

        // Canvas
        GameObject canvasObject = new GameObject("Canvas");
        canvasObject.transform.SetParent(uiFolder.transform);

        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler canvasScaler = canvasObject.AddComponent<CanvasScaler>();
        canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasScaler.referenceResolution = new Vector2(1920, 1080);
        canvasScaler.matchWidthOrHeight = 0.5f;

        canvasObject.AddComponent<GraphicRaycaster>();

        // EnemyHealthBarManager
        GameObject healthBarManager = new GameObject("EnemyHealthBarManager");
        healthBarManager.transform.SetParent(canvasObject.transform);
        healthBarManager.AddComponent<EnemyHealthBarManager>();

        // EventSystem
        GameObject eventSystem = new GameObject("EventSystem");
        eventSystem.transform.SetParent(uiFolder.transform);
        eventSystem.AddComponent<UnityEngine.EventSystems.EventSystem>();
        eventSystem.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();

        // === LIGHTING ===

        // Directional Light
        GameObject directionalLight = new GameObject("Directional Light");
        directionalLight.transform.SetParent(lightingFolder.transform);
        directionalLight.transform.position = Vector3.zero;
        directionalLight.transform.rotation = Quaternion.Euler(50, -30, 0);

        Light light = directionalLight.AddComponent<Light>();
        light.type = LightType.Directional;
        light.color = Color.white;
        light.intensity = 1f;

        // Global Volume (Post-Processing)
        GameObject globalVolume = new GameObject("Global Volume");
        globalVolume.transform.SetParent(lightingFolder.transform);
        globalVolume.transform.position = Vector3.zero;

        // Note: Le Volume component necessite le package Post Processing
        // Si pas installe, ce GameObject sera vide mais pret a recevoir le component

        // Sauvegarder la scene
        bool saved = EditorSceneManager.SaveScene(newScene, scenePath);

        if (saved)
        {
            Debug.Log($"<color=green>Scene {levelName} creee avec succes !</color>");
            Debug.Log($"Path : {scenePath}");
            Debug.Log($"Dimensions : {levelWidth}m x {levelLength}m");
        }
        else
        {
            Debug.LogError("Erreur lors de la sauvegarde de la scene.");
        }
    }
    private void CreateNewConfig()
    {
        string folder = "Assets/Project/ScriptableObjects";

        if (!System.IO.Directory.Exists(folder))
        {
            System.IO.Directory.CreateDirectory(folder);
            AssetDatabase.Refresh();
        }

        // Trouver un nom unique
        int counter = 1;
        string path = $"{folder}/LevelGenConfig_{counter:D3}.asset";

        while (System.IO.File.Exists(path))
        {
            counter++;
            path = $"{folder}/LevelGenConfig_{counter:D3}.asset";
        }

        // Creer le SO
        LevelGeneratorConfig newConfig = ScriptableObject.CreateInstance<LevelGeneratorConfig>();
        AssetDatabase.CreateAsset(newConfig, path);
        AssetDatabase.SaveAssets();

        config = newConfig;
        Selection.activeObject = newConfig;

        Debug.Log($"<color=green>Config creee : {path}</color>");
    }
}