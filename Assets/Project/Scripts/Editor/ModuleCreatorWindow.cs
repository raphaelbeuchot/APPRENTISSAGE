using UnityEngine;
using UnityEditor;
using System.IO;

public class ModuleCreatorWindow : EditorWindow
{
    private GameObject selectedMesh;
    private string moduleName = "NewModule";
    private string category = "Props";
    private float snapSize = 1f;
    private Color gizmoColor = Color.cyan;
    private bool createNewCategory = false;
    private string newCategoryName = "";

    private string basePrefabPath = "Assets/Project/LevelDesign/Modules/Prefabs";

    [MenuItem("Tools/Module Creator")]
    public static void ShowWindow()
    {
        ModuleCreatorWindow window = GetWindow<ModuleCreatorWindow>("Module Creator");
        window.minSize = new Vector2(350, 300);
        window.Show();
    }

    private void OnGUI()
    {
        GUILayout.Label("Module Creator", EditorStyles.boldLabel);
        GUILayout.Space(10);

        if (Selection.activeGameObject != null)
        {
            selectedMesh = Selection.activeGameObject;
            GUILayout.Label("Selected: " + selectedMesh.name, EditorStyles.helpBox);
        }
        else
        {
            GUILayout.Label("No object selected in scene", EditorStyles.helpBox);
        }

        GUILayout.Space(10);

        GUILayout.Label("Module Settings", EditorStyles.boldLabel);

        moduleName = EditorGUILayout.TextField("Module Name:", moduleName);

        GUILayout.Space(5);

        createNewCategory = EditorGUILayout.Toggle("Create New Category:", createNewCategory);

        if (createNewCategory)
        {
            newCategoryName = EditorGUILayout.TextField("New Category Name:", newCategoryName);
        }
        else
        {
            string[] existingCategories = GetExistingCategories();
            if (existingCategories.Length > 0)
            {
                int selectedIndex = System.Array.IndexOf(existingCategories, category);
                if (selectedIndex < 0) selectedIndex = 0;

                selectedIndex = EditorGUILayout.Popup("Category:", selectedIndex, existingCategories);
                category = existingCategories[selectedIndex];
            }
            else
            {
                category = EditorGUILayout.TextField("Category:", category);
            }
        }

        GUILayout.Space(5);

        snapSize = EditorGUILayout.FloatField("Snap Size (m):", snapSize);
        gizmoColor = EditorGUILayout.ColorField("Gizmo Color:", gizmoColor);

        GUILayout.Space(20);

        GUI.backgroundColor = Color.green;
        if (GUILayout.Button("Create Module Prefab", GUILayout.Height(40)))
        {
            CreateModulePrefab();
        }
        GUI.backgroundColor = Color.white;

        GUILayout.Space(10);
        GUILayout.Label("This will:", EditorStyles.helpBox);
        GUILayout.Label("1. Create parent with pivot at (0,0,0)", EditorStyles.miniLabel);
        GUILayout.Label("2. Add ModulePiece component", EditorStyles.miniLabel);
        GUILayout.Label("3. Move selected mesh as child", EditorStyles.miniLabel);
        GUILayout.Label("4. Save as prefab in correct folder", EditorStyles.miniLabel);
    }

    private string[] GetExistingCategories()
    {
        if (!Directory.Exists(basePrefabPath))
            return new string[0];

        string[] categoryFolders = Directory.GetDirectories(basePrefabPath);
        string[] categoryNames = new string[categoryFolders.Length];

        for (int i = 0; i < categoryFolders.Length; i++)
        {
            categoryNames[i] = Path.GetFileName(categoryFolders[i]);
        }

        return categoryNames;
    }

    private void CreateModulePrefab()
    {
        if (selectedMesh == null)
        {
            EditorUtility.DisplayDialog("Error", "Please select a mesh object in the scene first!", "OK");
            return;
        }

        if (string.IsNullOrWhiteSpace(moduleName))
        {
            EditorUtility.DisplayDialog("Error", "Module name cannot be empty!", "OK");
            return;
        }

        string targetCategory = createNewCategory ? newCategoryName : category;

        if (string.IsNullOrWhiteSpace(targetCategory))
        {
            EditorUtility.DisplayDialog("Error", "Category name cannot be empty!", "OK");
            return;
        }

        string categoryPath = Path.Combine(basePrefabPath, targetCategory);

        if (!Directory.Exists(categoryPath))
        {
            Directory.CreateDirectory(categoryPath);
            AssetDatabase.Refresh();
            Debug.Log("Created new category folder: " + categoryPath);
        }

        GameObject parent = new GameObject(moduleName);
        parent.transform.position = Vector3.zero;
        parent.transform.rotation = Quaternion.identity;

        ModulePiece modulePiece = parent.AddComponent<ModulePiece>();
        modulePiece.category = targetCategory;
        modulePiece.snapSize = snapSize;
        modulePiece.gizmoColor = gizmoColor;

        selectedMesh.transform.SetParent(parent.transform);

        string prefabPath = Path.Combine(categoryPath, moduleName + ".prefab");
        prefabPath = AssetDatabase.GenerateUniqueAssetPath(prefabPath);

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(parent, prefabPath);

        if (prefab != null)
        {
            Debug.Log("Module prefab created: " + prefabPath);
            EditorUtility.DisplayDialog("Success", "Module prefab created successfully!\n\n" + prefabPath, "OK");

            DestroyImmediate(parent);

            Selection.activeObject = prefab;
            EditorGUIUtility.PingObject(prefab);
        }
        else
        {
            EditorUtility.DisplayDialog("Error", "Failed to create prefab!", "OK");
            DestroyImmediate(parent);
        }
    }
}