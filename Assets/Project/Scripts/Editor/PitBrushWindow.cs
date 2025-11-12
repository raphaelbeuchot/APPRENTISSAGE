using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

public class PitBrushWindow : EditorWindow
{
    private PitGridData activeGridData;
    private int brushSize = 1;
    private float paintDepth = 1f;
    private bool paintMode = true;

    private Vector2 scrollPosition;

    // NOUVEAU : Pour le dropdown de selection de PitZone
    private PitZone[] availablePitZones;
    private string[] pitZoneNames;
    private int selectedPitZoneIndex = -1;

    [MenuItem("Tools/Pit Brush")]
    public static void ShowWindow()
    {
        PitBrushWindow window = GetWindow<PitBrushWindow>("Pit Brush");
        window.minSize = new Vector2(300, 500); // AUGMENTE la hauteur
        window.Show();
    }

    private void OnGUI()
    {
        scrollPosition = GUILayout.BeginScrollView(scrollPosition);

        GUILayout.Label("PIT BRUSH TOOL", EditorStyles.boldLabel);
        GUILayout.Space(10);

        DrawGridDataSection();
        GUILayout.Space(10);

        DrawPitZoneSection(); // NOUVEAU
        GUILayout.Space(10);

        DrawBrushSettings();
        GUILayout.Space(10);

        DrawModeSelection();
        GUILayout.Space(10);

        DrawInfoSection();

        GUILayout.EndScrollView();
    }

    // NOUVELLE SECTION : Gestion des PitZones
    private void DrawPitZoneSection()
    {
        GUILayout.BeginVertical(EditorStyles.helpBox);
        GUILayout.Label("Pit Zones", EditorStyles.boldLabel);

        // Rafraichit la liste des PitZones
        RefreshPitZoneList();

        // Bouton pour creer un nouveau PitZone
        GUI.enabled = activeGridData != null;
        GUI.backgroundColor = Color.green;
        if (GUILayout.Button("Create New Pit Zone", GUILayout.Height(35)))
        {
            PitZone newZone = PitBrushSystem.CreateNewPitZone();
            if (newZone != null)
            {
                RefreshPitZoneList();
                SelectPitZone(newZone);
            }
        }
        GUI.backgroundColor = Color.white;
        GUI.enabled = true;

        GUILayout.Space(5);

        // Dropdown pour selectionner un PitZone existant
        if (availablePitZones != null && availablePitZones.Length > 0)
        {
            GUILayout.Label("Select Active Zone:", EditorStyles.miniLabel);

            int newIndex = EditorGUILayout.Popup(selectedPitZoneIndex, pitZoneNames);

            if (newIndex != selectedPitZoneIndex && newIndex >= 0 && newIndex < availablePitZones.Length)
            {
                selectedPitZoneIndex = newIndex;
                SelectPitZone(availablePitZones[selectedPitZoneIndex]);
            }

            // Affiche les infos du PitZone actif
            PitZone activeZone = PitBrushSystem.GetActivePitZone();
            if (activeZone != null)
            {
                GUILayout.Space(5);
                GUI.backgroundColor = new Color(0.2f, 0.8f, 0.2f);
                GUILayout.BeginVertical(EditorStyles.helpBox);
                GUILayout.Label("Active: " + activeZone.name, EditorStyles.boldLabel);

                if (activeGridData != null)
                {
                    int ownedCells = activeGridData.GetCellsForZone(activeZone.zoneID).Count;
                    GUILayout.Label("Cells: " + ownedCells, EditorStyles.miniLabel);
                }

                GUILayout.EndVertical();
                GUI.backgroundColor = Color.white;
            }
        }
        else
        {
            GUILayout.Label("No PitZones in scene", EditorStyles.miniLabel);
        }

        GUILayout.EndVertical();
    }

    // NOUVELLE METHODE : Rafraichit la liste des PitZones disponibles
    private void RefreshPitZoneList()
    {
        availablePitZones = FindObjectsOfType<PitZone>();

        if (activeGridData != null)
        {
            // Filtre pour ne garder que les PitZones du gridData actif
            availablePitZones = availablePitZones.Where(z => z.gridData == activeGridData).ToArray();
        }

        // Trie par ID
        availablePitZones = availablePitZones.OrderBy(z => z.zoneID).ToArray();

        // Cree les noms pour le dropdown
        pitZoneNames = new string[availablePitZones.Length];
        for (int i = 0; i < availablePitZones.Length; i++)
        {
            pitZoneNames[i] = availablePitZones[i].name + " (ID: " + availablePitZones[i].zoneID + ")";
        }

        // Met a jour l'index selectionne
        PitZone currentActive = PitBrushSystem.GetActivePitZone();
        if (currentActive != null)
        {
            for (int i = 0; i < availablePitZones.Length; i++)
            {
                if (availablePitZones[i] == currentActive)
                {
                    selectedPitZoneIndex = i;
                    break;
                }
            }
        }
    }

    // NOUVELLE METHODE : Selectionne un PitZone comme actif
    private void SelectPitZone(PitZone zone)
    {
        PitBrushSystem.SetActivePitZone(zone);
        Selection.activeGameObject = zone.gameObject;
        SceneView.RepaintAll();
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
            RefreshPitZoneList(); // NOUVEAU : rafraichit la liste quand on change de grid
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

            // NOUVEAU : Legende des couleurs
            GUILayout.Space(5);
            GUILayout.Label("Color Legend:", EditorStyles.boldLabel);
            GUILayout.Label("- Green cells: Active zone", EditorStyles.miniLabel);
            GUILayout.Label("- Blue cells: Other zones", EditorStyles.miniLabel);
            GUILayout.Label("- Yellow cells: Orphan (no owner)", EditorStyles.miniLabel);
            GUILayout.Label("- Red preview: Blocked cells", EditorStyles.miniLabel);
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

    // NOUVEAU : Rafraichit la fenetre automatiquement
    private void OnInspectorUpdate()
    {
        Repaint();
    }
}