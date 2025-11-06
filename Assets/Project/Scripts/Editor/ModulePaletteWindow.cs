using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;

public class ModulePaletteWindow : EditorWindow
{
    private Vector2 scrollPosition;
    private Dictionary<string, List<GameObject>> categorizedPrefabs = new Dictionary<string, List<GameObject>>();
    private Dictionary<string, bool> categoryFoldouts = new Dictionary<string, bool>();
    private Dictionary<string, Color> categoryColors = new Dictionary<string, Color>();

    private string basePrefabPath = "Assets/Project/LevelDesign/Modules/Prefabs";

    private string newCategoryName = "";
    private float newCategorySnapSize = 1f;
    private bool showAddCategoryPanel = false;

    [MenuItem("Tools/Module Palette")]
    public static void ShowWindow()
    {
        ModulePaletteWindow window = GetWindow<ModulePaletteWindow>("Module Palette");
        window.minSize = new Vector2(300, 400);
        window.Show();
    }

    private void OnEnable()
    {
        LoadAllCategories();
        InitializeDefaultColors();
    }

    private void InitializeDefaultColors()
    {
        if (!categoryColors.ContainsKey("Floors")) categoryColors["Floors"] = Color.cyan;
        if (!categoryColors.ContainsKey("Walls")) categoryColors["Walls"] = Color.yellow;
        if (!categoryColors.ContainsKey("Props")) categoryColors["Props"] = Color.green;
        if (!categoryColors.ContainsKey("Hazards")) categoryColors["Hazards"] = Color.magenta;
        if (!categoryColors.ContainsKey("Special")) categoryColors["Special"] = Color.blue;
    }

    private void LoadAllCategories()
    {
        categorizedPrefabs.Clear();

        if (!Directory.Exists(basePrefabPath))
        {
            Debug.LogWarning("Base prefab path does not exist: " + basePrefabPath);
            return;
        }

        string[] categoryFolders = Directory.GetDirectories(basePrefabPath);

        foreach (string folderPath in categoryFolders)
        {
            string categoryName = Path.GetFileName(folderPath);
            List<GameObject> prefabs = LoadPrefabsFromFolder(folderPath);

            categorizedPrefabs[categoryName] = prefabs;

            if (!categoryFoldouts.ContainsKey(categoryName))
            {
                categoryFoldouts[categoryName] = true;
            }

            if (!categoryColors.ContainsKey(categoryName))
            {
                categoryColors[categoryName] = new Color(Random.Range(0.4f, 1f), Random.Range(0.4f, 1f), Random.Range(0.4f, 1f));
            }
        }
    }

    private List<GameObject> LoadPrefabsFromFolder(string folderPath)
    {
        List<GameObject> prefabs = new List<GameObject>();

        if (!Directory.Exists(folderPath))
            return prefabs;

        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { folderPath });

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null)
            {
                prefabs.Add(prefab);
            }
        }

        return prefabs;
    }

    private void OnGUI()
    {
        GUILayout.Label("Module Palette", EditorStyles.boldLabel);
        GUILayout.Space(5);

        GUILayout.BeginHorizontal();

        if (GUILayout.Button("Refresh Categories", GUILayout.Height(25)))
        {
            LoadAllCategories();
        }

        GUI.backgroundColor = Color.green;
        if (GUILayout.Button("Add Category", GUILayout.Height(25)))
        {
            showAddCategoryPanel = !showAddCategoryPanel;
        }
        GUI.backgroundColor = Color.white;

        GUILayout.EndHorizontal();

        if (showAddCategoryPanel)
        {
            DrawAddCategoryPanel();
        }

        GUILayout.Space(10);

        scrollPosition = GUILayout.BeginScrollView(scrollPosition);

        foreach (var kvp in categorizedPrefabs)
        {
            string categoryName = kvp.Key;
            List<GameObject> prefabs = kvp.Value;
            Color categoryColor = categoryColors.ContainsKey(categoryName) ? categoryColors[categoryName] : Color.white;

            DrawCategory(categoryName, prefabs, categoryColor);
        }

        GUILayout.EndScrollView();

        GUILayout.Space(10);
        GUILayout.Label("Click a module to spawn at (0,0,0)", EditorStyles.helpBox);
        GUILayout.Label("[T] Toggle Position Snap | [R] Toggle Rotation Snap | [G] Toggle Grid", EditorStyles.helpBox);
    }

    private void DrawAddCategoryPanel()
    {
        GUILayout.BeginVertical(EditorStyles.helpBox);
        GUILayout.Label("Create New Category", EditorStyles.boldLabel);

        GUILayout.BeginHorizontal();
        GUILayout.Label("Name:", GUILayout.Width(80));
        newCategoryName = GUILayout.TextField(newCategoryName);
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.Label("Snap Size:", GUILayout.Width(80));
        newCategorySnapSize = EditorGUILayout.FloatField(newCategorySnapSize);
        GUILayout.Label("m");
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();

        GUI.backgroundColor = Color.green;
        if (GUILayout.Button("Create", GUILayout.Height(25)))
        {
            CreateNewCategory(newCategoryName, newCategorySnapSize);
        }
        GUI.backgroundColor = Color.white;

        if (GUILayout.Button("Cancel", GUILayout.Height(25)))
        {
            showAddCategoryPanel = false;
            newCategoryName = "";
            newCategorySnapSize = 1f;
        }

        GUILayout.EndHorizontal();

        GUILayout.EndVertical();
        GUILayout.Space(5);
    }

    private void CreateNewCategory(string categoryName, float snapSize)
    {
        if (string.IsNullOrWhiteSpace(categoryName))
        {
            EditorUtility.DisplayDialog("Error", "Category name cannot be empty!", "OK");
            return;
        }

        string newFolderPath = Path.Combine(basePrefabPath, categoryName);

        if (Directory.Exists(newFolderPath))
        {
            EditorUtility.DisplayDialog("Error", "Category already exists!", "OK");
            return;
        }

        Directory.CreateDirectory(newFolderPath);
        AssetDatabase.Refresh();

        Debug.Log("Created new category: " + categoryName + " with snap size: " + snapSize + "m at " + newFolderPath);

        LoadAllCategories();

        showAddCategoryPanel = false;
        newCategoryName = "";
        newCategorySnapSize = 1f;
    }

    private void DrawCategory(string categoryName, List<GameObject> prefabs, Color color)
    {
        GUILayout.BeginVertical(EditorStyles.helpBox);

        bool foldout = categoryFoldouts.ContainsKey(categoryName) ? categoryFoldouts[categoryName] : true;

        GUI.backgroundColor = color;
        foldout = EditorGUILayout.Foldout(foldout, categoryName + " (" + prefabs.Count + ")", true, EditorStyles.foldoutHeader);
        GUI.backgroundColor = Color.white;

        categoryFoldouts[categoryName] = foldout;

        if (foldout)
        {
            if (prefabs.Count == 0)
            {
                GUILayout.Label("No prefabs found", EditorStyles.centeredGreyMiniLabel);
            }
            else
            {
                foreach (GameObject prefab in prefabs)
                {
                    if (GUILayout.Button(prefab.name, GUILayout.Height(25)))
                    {
                        SpawnModule(prefab);
                    }
                }
            }
        }

        GUILayout.EndVertical();
        GUILayout.Space(5);
    }

    private void SpawnModule(GameObject prefab)
    {
        GameObject instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
        instance.transform.position = Vector3.zero;

        Undo.RegisterCreatedObjectUndo(instance, "Spawn Module");
        Selection.activeGameObject = instance;

        SceneView.lastActiveSceneView.FrameSelected();
    }
}