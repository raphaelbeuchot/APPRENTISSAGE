using UnityEngine;
using UnityEditor;

public class PitBrushWindow : EditorWindow
{
    private PitGridData activeGridData;
    private int brushSize = 1;
    private float paintDepth = 1f;
    private bool paintMode = true;

    private Vector2 scrollPosition;

    [MenuItem("Tools/Pit Brush")]
    public static void ShowWindow()
    {
        PitBrushWindow window = GetWindow<PitBrushWindow>("Pit Brush");
        window.minSize = new Vector2(300, 400);
        window.Show();
    }

    private void OnGUI()
    {
        scrollPosition = GUILayout.BeginScrollView(scrollPosition);

        GUILayout.Label("PIT BRUSH TOOL", EditorStyles.boldLabel);
        GUILayout.Space(10);

        DrawGridDataSection();
        GUILayout.Space(10);

        DrawBrushSettings();
        GUILayout.Space(10);

        DrawModeSelection();
        GUILayout.Space(10);

        DrawInfoSection();

        GUILayout.EndScrollView();
    }

    private void DrawGridDataSection()
    {
        GUILayout.BeginVertical(EditorStyles.helpBox);
        GUILayout.Label("Grid Data", EditorStyles.boldLabel);

        PitGridData newGridData = (PitGridData)EditorGUILayout.ObjectField(
            "Active Grid:",
            activeGridData,
            typeof(PitGridData),
            false
        );

        if (newGridData != activeGridData)
        {
            activeGridData = newGridData;
            PitBrushSystem.SetActiveGridData(activeGridData);
        }

        GUILayout.BeginHorizontal();

        if (GUILayout.Button("Create New Grid", GUILayout.Height(25)))
        {
            CreateNewGridData();
        }

        GUI.enabled = activeGridData != null;
        if (GUILayout.Button("Clear All", GUILayout.Height(25)))
        {
            if (EditorUtility.DisplayDialog(
                "Clear Grid",
                "Are you sure you want to clear all pit data?",
                "Yes",
                "No"))
            {
                activeGridData.ClearAll();
                EditorUtility.SetDirty(activeGridData);
                SceneView.RepaintAll();
            }
        }
        GUI.enabled = true;

        GUILayout.EndHorizontal();

        GUILayout.EndVertical();
    }

    private void DrawBrushSettings()
    {
        GUILayout.BeginVertical(EditorStyles.helpBox);
        GUILayout.Label("Brush Settings", EditorStyles.boldLabel);

        GUILayout.BeginHorizontal();
        GUILayout.Label("Brush Size:", GUILayout.Width(100));
        brushSize = EditorGUILayout.IntSlider(brushSize, 1, 8);
        GUILayout.Label(brushSize + "x" + brushSize + " cells");
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.Label("Paint Depth:", GUILayout.Width(100));
        paintDepth = EditorGUILayout.Slider(paintDepth, 0.5f, 10f);
        GUILayout.Label(paintDepth.ToString("F1") + "m");
        GUILayout.EndHorizontal();

        float snappedDepth = Mathf.Round(paintDepth / 0.5f) * 0.5f;
        if (snappedDepth != paintDepth)
        {
            paintDepth = snappedDepth;
        }

        PitBrushSystem.SetBrushSize(brushSize);
        PitBrushSystem.SetPaintDepth(paintDepth);

        GUILayout.EndVertical();
    }

    private void DrawModeSelection()
    {
        GUILayout.BeginVertical(EditorStyles.helpBox);
        GUILayout.Label("Mode", EditorStyles.boldLabel);

        GUILayout.BeginHorizontal();

        GUI.backgroundColor = paintMode ? Color.green : Color.white;
        if (GUILayout.Button("PAINT (Dig)", GUILayout.Height(30)))
        {
            paintMode = true;
            PitBrushSystem.SetPaintMode(true);
        }

        GUI.backgroundColor = !paintMode ? Color.red : Color.white;
        if (GUILayout.Button("ERASE (Fill)", GUILayout.Height(30)))
        {
            paintMode = false;
            PitBrushSystem.SetPaintMode(false);
        }

        GUI.backgroundColor = Color.white;

        GUILayout.EndHorizontal();

        GUILayout.EndVertical();
    }

    private void DrawInfoSection()
    {
        GUILayout.BeginVertical(EditorStyles.helpBox);
        GUILayout.Label("Instructions", EditorStyles.boldLabel);

        if (activeGridData == null)
        {
            GUILayout.Label("Create or load a Grid Data to start painting", EditorStyles.wordWrappedLabel);
        }
        else
        {
            int cellCount = activeGridData.GetAllCells().Count;
            GUILayout.Label("Active cells: " + cellCount, EditorStyles.label);

            GUILayout.Space(5);
            GUILayout.Label("Press [P] to toggle Pit Brush mode", EditorStyles.miniLabel);
            GUILayout.Label("Press [B] to cycle brush size", EditorStyles.miniLabel);
            GUILayout.Label("Click and drag in Scene view to paint", EditorStyles.miniLabel);
        }

        GUILayout.EndVertical();
    }

    private void CreateNewGridData()
    {
        string path = EditorUtility.SaveFilePanelInProject(
            "Create Pit Grid Data",
            "NewPitGrid",
            "asset",
            "Choose location for new Pit Grid Data"
        );

        if (!string.IsNullOrEmpty(path))
        {
            PitGridData newGrid = CreateInstance<PitGridData>();
            AssetDatabase.CreateAsset(newGrid, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            activeGridData = newGrid;
            PitBrushSystem.SetActiveGridData(activeGridData);

            EditorUtility.FocusProjectWindow();
            Selection.activeObject = newGrid;

            Debug.Log("Created new Pit Grid Data at: " + path);
        }
    }

    private void OnDestroy()
    {
        PitBrushSystem.SetActiveGridData(null);
    }
}