using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;

public class ModulePlacementWindow : EditorWindow
{
    private Vector2 scrollPosition;
    private Dictionary<string, List<GameObject>> categorizedPrefabs = new Dictionary<string, List<GameObject>>();
    private Dictionary<string, bool> categoryFoldouts = new Dictionary<string, bool>();
    private Dictionary<string, Color> categoryColors = new Dictionary<string, Color>();

    private ModulePlacementTool.PlacementMode selectedMode = ModulePlacementTool.PlacementMode.Single;
    private GameObject selectedPrefab = null;
    private Dictionary<GameObject, bool> selectedPrefabs = new Dictionary<GameObject, bool>();

    private string basePrefabPath = "Assets/Project/LevelDesign/Modules/Prefabs";

    [MenuItem("Tools/Module Placement")]
    public static void ShowWindow()
    {
        ModulePlacementWindow window = GetWindow<ModulePlacementWindow>("Module Placement");
        window.minSize = new Vector2(300, 450);
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
        GUILayout.Label("Module Placement Tool", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(" case cochée = Rotation aléatoire au placement (vert=ON, gris=OFF)", MessageType.Info);
        GUILayout.Space(5);

        DrawToolControls();
        GUILayout.Space(10);

        DrawModeSelection();
        GUILayout.Space(10);

        if (GUILayout.Button("Refresh Categories", GUILayout.Height(25)))
        {
            LoadAllCategories();
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
        GUILayout.Label("Click a module to activate placement mode", EditorStyles.helpBox);
    }

    private void DrawToolControls()
    {
        GUILayout.BeginVertical(EditorStyles.helpBox);
        GUILayout.Label("Tool Status", EditorStyles.boldLabel);

        if (ModulePlacementTool.IsActive())
        {
            GUI.backgroundColor = Color.green;
            GUILayout.Label("ACTIVE", EditorStyles.boldLabel);
            GUI.backgroundColor = Color.white;

            if (selectedPrefab != null)
            {
                GUILayout.Label("Current: " + selectedPrefab.name, EditorStyles.miniLabel);
            }

            GUI.backgroundColor = Color.red;
            if (GUILayout.Button("DEACTIVATE [ESC]", GUILayout.Height(30)))
            {
                ModulePlacementTool.DeactivateTool();
                selectedPrefab = null;
            }
            GUI.backgroundColor = Color.white;
        }
        else
        {
            GUI.backgroundColor = Color.gray;
            GUILayout.Label("INACTIVE", EditorStyles.boldLabel);
            GUI.backgroundColor = Color.white;
            GUILayout.Label("Select a module below to start", EditorStyles.miniLabel);
        }

        GUILayout.EndVertical();
    }

    private void DrawModeSelection()
    {
        GUILayout.BeginVertical(EditorStyles.helpBox);
        GUILayout.Label("Placement Mode", EditorStyles.boldLabel);

        GUILayout.BeginHorizontal();

        GUI.backgroundColor = selectedMode == ModulePlacementTool.PlacementMode.Single ? Color.green : Color.white;
        if (GUILayout.Button("[1] Single", GUILayout.Height(30)))
        {
            selectedMode = ModulePlacementTool.PlacementMode.Single;
            if (ModulePlacementTool.IsActive())
            {
                ModulePlacementTool.SetMode(selectedMode);
            }
        }

        GUI.backgroundColor = selectedMode == ModulePlacementTool.PlacementMode.Line ? Color.green : Color.white;
        if (GUILayout.Button("[2] Line", GUILayout.Height(30)))
        {
            selectedMode = ModulePlacementTool.PlacementMode.Line;
            if (ModulePlacementTool.IsActive())
            {
                ModulePlacementTool.SetMode(selectedMode);
            }
        }

        GUI.backgroundColor = selectedMode == ModulePlacementTool.PlacementMode.Area ? Color.green : Color.white;
        if (GUILayout.Button("[3] Area", GUILayout.Height(30)))
        {
            selectedMode = ModulePlacementTool.PlacementMode.Area;
            if (ModulePlacementTool.IsActive())
            {
                ModulePlacementTool.SetMode(selectedMode);
            }
        }

        GUI.backgroundColor = Color.white;

        GUILayout.EndHorizontal();

        GUILayout.Label("Single: 1 module | Line: Row | Area: Rectangle", EditorStyles.miniLabel);

        GUILayout.EndVertical();
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
            // Auto-detect mode par categorie
            ModulePlacementTool.PlacementMode suggestedMode = ModulePlacementTool.PlacementMode.Single;
            if (categoryName == "Walls")
            {
                suggestedMode = ModulePlacementTool.PlacementMode.Line;
            }
            else if (categoryName == "Floors")
            {
                suggestedMode = ModulePlacementTool.PlacementMode.Area;
            }

            if (prefabs.Count == 0)
            {
                GUILayout.Label("No prefabs found", EditorStyles.centeredGreyMiniLabel);
            }
            else
            {
                // Bouton pour activer avec les prefabs sélectionnés
                int selectedCount = 0;
                foreach (GameObject prefab in prefabs)
                {
                    if (selectedPrefabs.ContainsKey(prefab) && selectedPrefabs[prefab])
                        selectedCount++;
                }

                if (selectedCount > 0)
                {
                    GUI.backgroundColor = Color.cyan;
                    if (GUILayout.Button("Activate with " + selectedCount + " selected", GUILayout.Height(30)))
                    {
                        List<GameObject> toActivate = new List<GameObject>();
                        foreach (GameObject prefab in prefabs)
                        {
                            if (selectedPrefabs.ContainsKey(prefab) && selectedPrefabs[prefab])
                                toActivate.Add(prefab);
                        }

                        selectedMode = suggestedMode;
                        ModulePlacementTool.ActivateTool(toActivate, selectedMode);
                    }
                    GUI.backgroundColor = Color.white;
                    GUILayout.Space(5);
                }

                // Liste des prefabs avec checkboxes et toggle rotation
                foreach (GameObject prefab in prefabs)
                {
                    ModulePiece modulePiece = prefab.GetComponent<ModulePiece>();

                    GUILayout.BeginHorizontal();

                    // Checkbox sélection
                    if (!selectedPrefabs.ContainsKey(prefab))
                        selectedPrefabs[prefab] = false;

                    bool wasChecked = selectedPrefabs[prefab];
                    bool isChecked = EditorGUILayout.Toggle(wasChecked, GUILayout.Width(20));
                    selectedPrefabs[prefab] = isChecked;

                    // Nom du prefab (cliquable aussi pour toggle)
                    if (GUILayout.Button(prefab.name, EditorStyles.label, GUILayout.Height(20)))
                    {
                        selectedPrefabs[prefab] = !selectedPrefabs[prefab];
                    }

                    // NOUVEAU : Toggle rotation aléatoire
                    GUILayout.FlexibleSpace();

                    GUIContent rotationIcon = new GUIContent("rot", "Rotation aléatoire au placement");

                    if (modulePiece != null)
                    {
                        GUI.backgroundColor = modulePiece.allowRandomRotation ? Color.green : Color.gray;
                        bool newRandomRotation = GUILayout.Toggle(modulePiece.allowRandomRotation, rotationIcon, GUI.skin.button, GUILayout.Width(30));
                        GUI.backgroundColor = Color.white;

                        if (newRandomRotation != modulePiece.allowRandomRotation)
                        {
                            Undo.RecordObject(prefab, "Toggle Random Rotation");
                            modulePiece.allowRandomRotation = newRandomRotation;
                            EditorUtility.SetDirty(prefab);
                            PrefabUtility.RecordPrefabInstancePropertyModifications(prefab);
                        }
                    }

                    GUILayout.EndHorizontal();
                }
            }
        }

        GUILayout.EndVertical();
        GUILayout.Space(5);
    }
    private void OnDestroy()
    {
        ModulePlacementTool.DeactivateTool();
    }

    private void OnInspectorUpdate()
    {
        Repaint();
    }
}