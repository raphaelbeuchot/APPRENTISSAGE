using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

[InitializeOnLoad]
public class PitBrushSystem
{
    private static PitZone activePitZone; // RESTE : le pit zone actuellement actif
    private static PitGridData activeGridData;
    private static int brushSize = 1;
    private static float paintDepth = 1f;
    private static bool paintMode = true;
    private static bool brushEnabled = false;

    private static Vector2Int currentHoverCell;
    private static bool isPainting = false;
    private static HashSet<Vector2Int> paintedCellsThisStroke = new HashSet<Vector2Int>();

    private static int nextZoneID = 0; // NOUVEAU : compteur pour les IDs

    private const string BRUSH_ENABLED_KEY = "PitBrush_Enabled";
    private const string NEXT_ZONE_ID_KEY = "PitBrush_NextZoneID"; // NOUVEAU

    static PitBrushSystem()
    {
        brushEnabled = EditorPrefs.GetBool(BRUSH_ENABLED_KEY, false);
        nextZoneID = EditorPrefs.GetInt(NEXT_ZONE_ID_KEY, 0); // NOUVEAU
        SceneView.duringSceneGui += OnSceneGUI;
    }

    public static void SetActiveGridData(PitGridData data)
    {
        activeGridData = data;
    }

    // NOUVELLE METHODE : Definir le PitZone actif
    public static void SetActivePitZone(PitZone zone)
    {
        activePitZone = zone;
        Debug.Log("Active PitZone set to: " + (zone != null ? zone.name : "NULL"));
    }

    // NOUVELLE METHODE : Recuperer le PitZone actif
    public static PitZone GetActivePitZone()
    {
        return activePitZone;
    }

    // NOUVELLE METHODE : Creer un nouveau PitZone
    public static PitZone CreateNewPitZone()
    {
        if (activeGridData == null)
        {
            Debug.LogError("Cannot create PitZone: No active grid data!");
            return null;
        }

        GameObject pitZoneObj = new GameObject("PitZone_" + nextZoneID);
        PitZone newZone = pitZoneObj.AddComponent<PitZone>();
        newZone.Initialize(nextZoneID, activeGridData);

        Undo.RegisterCreatedObjectUndo(pitZoneObj, "Create Pit Zone");

        activePitZone = newZone;
        nextZoneID++;
        EditorPrefs.SetInt(NEXT_ZONE_ID_KEY, nextZoneID);

        Debug.Log("Created new PitZone with ID: " + newZone.zoneID);

        return newZone;
    }

    public static void SetBrushSize(int size)
    {
        brushSize = size;
    }

    public static void SetPaintDepth(float depth)
    {
        paintDepth = depth;
    }

    public static void SetPaintMode(bool isPaint)
    {
        paintMode = isPaint;
    }

    private static void OnSceneGUI(SceneView sceneView)
    {
        Event e = Event.current;

        HandleKeyboardInput(e, sceneView);

        if (brushEnabled && activeGridData != null)
        {
            DrawPaintedCells();
            UpdateHoverCell(e);
            HandleMouseInput(e);
            DrawBrushPreview();
            DrawBrushOverlay(sceneView);

            if (brushEnabled)
            {
                HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
            }
        }
    }

    private static void DrawPaintedCells()
    {
        if (activeGridData == null) return;

        Dictionary<Vector2Int, float> allCells = activeGridData.GetAllCells();
        float cellSize = activeGridData.gridCellSize;

        foreach (var kvp in allCells)
        {
            Vector2Int cellPos = kvp.Key;
            float depth = kvp.Value;

            Vector3 worldPos = activeGridData.CellToWorld(cellPos);
            Vector3 cubeCenter = worldPos + new Vector3(cellSize * 0.5f, 0f, cellSize * 0.5f);

            // MODIFICATION : colorie differemment selon la zone proprietaire
            int ownerID = activeGridData.GetCellOwner(cellPos);
            Color cellColor;

            if (ownerID == -1)
            {
                // Cellule sans proprietaire (orpheline) - jaune
                cellColor = new Color(1f, 1f, 0f, 0.5f);
            }
            else if (activePitZone != null && ownerID == activePitZone.zoneID)
            {
                // Cellule de la zone active - vert
                float normalizedDepth = Mathf.Clamp01(-depth / 10f);
                cellColor = Color.Lerp(new Color(0, 1f, 0f, 0.4f), new Color(0, 0.5f, 0f, 0.6f), normalizedDepth);
            }
            else
            {
                // Cellule d'une autre zone - bleu
                float normalizedDepth = Mathf.Clamp01(-depth / 10f);
                cellColor = Color.Lerp(new Color(0, 0.5f, 1f, 0.4f), new Color(0, 0, 0.5f, 0.6f), normalizedDepth);
            }

            Handles.color = cellColor;
            DrawSolidCube(cubeCenter, new Vector3(cellSize, 0.1f, cellSize));

            Handles.color = new Color(1, 1, 1, 0.5f);
            Handles.DrawWireCube(cubeCenter, new Vector3(cellSize, 0.1f, cellSize));
        }
    }

    private static void HandleKeyboardInput(Event e, SceneView sceneView)
    {
        if (e.type == EventType.KeyDown)
        {
            if (e.keyCode == KeyCode.P)
            {
                brushEnabled = !brushEnabled;
                EditorPrefs.SetBool(BRUSH_ENABLED_KEY, brushEnabled);
                Debug.Log("Pit Brush: " + (brushEnabled ? "ON" : "OFF"));
                e.Use();
                sceneView.Repaint();
            }
            else if (e.keyCode == KeyCode.B && brushEnabled)
            {
                brushSize++;
                if (brushSize > 8) brushSize = 1;
                Debug.Log("Brush Size: " + brushSize + "x" + brushSize);
                e.Use();
                sceneView.Repaint();
            }
        }
    }

    private static void UpdateHoverCell(Event e)
    {
        Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
        Plane groundPlane = new Plane(Vector3.up, Vector3.zero);
        float distance;

        if (groundPlane.Raycast(ray, out distance))
        {
            Vector3 hitPoint = ray.GetPoint(distance);
            currentHoverCell = activeGridData.WorldToCell(hitPoint);
        }
    }

    private static void HandleMouseInput(Event e)
    {
        if (isPainting)
        {
            SceneView.RepaintAll();
        }

        if (e.type == EventType.MouseDown && e.button == 0 && !e.alt)
        {
            isPainting = true;
            paintedCellsThisStroke.Clear();
            PaintAtCurrentCell();
            e.Use();
        }
        else if (e.type == EventType.MouseDrag && e.button == 0 && isPainting)
        {
            PaintAtCurrentCell();
            e.Use();
        }
        else if (e.type == EventType.MouseUp && e.button == 0)
        {
            if (isPainting)
            {
                isPainting = false;
                paintedCellsThisStroke.Clear();
                EditorUtility.SetDirty(activeGridData);

                // MODIFICATION : Genere les meshes du PitZone actif uniquement
                if (activePitZone != null)
                {
                    activePitZone.GenerateMeshes();
                    EditorUtility.SetDirty(activePitZone);
                    Debug.Log("Meshes regenerated for PitZone " + activePitZone.zoneID);
                }
                else
                {
                    Debug.LogWarning("No active PitZone! Create one first.");
                }

                SceneView.RepaintAll();
            }
            e.Use();
        }

        if (isPainting && (e.type == EventType.Layout || e.type == EventType.MouseDrag))
        {
            HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
        }
    }

    private static void PaintAtCurrentCell()
    {
        // VERIFICATION : Il faut un PitZone actif pour peindre
        if (activePitZone == null)
        {
            Debug.LogWarning("No active PitZone selected! Create or select one first.");
            return;
        }

        int halfSize = brushSize / 2;

        for (int x = 0; x < brushSize; x++)
        {
            for (int z = 0; z < brushSize; z++)
            {
                Vector2Int cellOffset = new Vector2Int(x - halfSize, z - halfSize);
                Vector2Int targetCell = currentHoverCell + cellOffset;

                if (!paintedCellsThisStroke.Contains(targetCell))
                {
                    if (paintMode)
                    {
                        // VERIFICATION : La cellule est-elle deja possedee par une autre zone?
                        int currentOwner = activeGridData.GetCellOwner(targetCell);

                        if (currentOwner != -1 && currentOwner != activePitZone.zoneID)
                        {
                            // Cellule deja possedee par une autre zone - BLOQUE
                            Debug.LogWarning("Cell " + targetCell + " is already owned by PitZone " + currentOwner);
                            continue;
                        }

                        // Peint la cellule
                        activeGridData.SetDepth(targetCell, -paintDepth);
                        activeGridData.AssignCellToZone(targetCell, activePitZone.zoneID);
                    }
                    else
                    {
                        // Mode erase - enleve la cellule
                        activeGridData.SetDepth(targetCell, 0f);
                        activeGridData.RemoveCellOwnership(targetCell);
                    }

                    paintedCellsThisStroke.Add(targetCell);
                }
            }
        }

        SceneView.RepaintAll();
    }

    private static void DrawBrushPreview()
    {
        if (activeGridData == null) return;

        int halfSize = brushSize / 2;
        float cellSize = activeGridData.gridCellSize;

        Color previewColor = paintMode ? new Color(0, 1, 0, 0.3f) : new Color(1, 0, 0, 0.3f);
        Color wireColor = paintMode ? Color.green : Color.red;

        for (int x = 0; x < brushSize; x++)
        {
            for (int z = 0; z < brushSize; z++)
            {
                Vector2Int cellOffset = new Vector2Int(x - halfSize, z - halfSize);
                Vector2Int targetCell = currentHoverCell + cellOffset;
                Vector3 worldPos = activeGridData.CellToWorld(targetCell);

                Vector3 cubeCenter = worldPos + new Vector3(cellSize * 0.5f, 0f, cellSize * 0.5f);
                Vector3 cubeSize = new Vector3(cellSize, 0.1f, cellSize);

                // MODIFICATION : Preview rouge si cellule bloquee
                int cellOwner = activeGridData.GetCellOwner(targetCell);
                bool isBlocked = paintMode && cellOwner != -1 && activePitZone != null && cellOwner != activePitZone.zoneID;

                if (isBlocked)
                {
                    previewColor = new Color(1, 0, 0, 0.5f);
                    wireColor = Color.red;
                }
                else
                {
                    previewColor = paintMode ? new Color(0, 1, 0, 0.3f) : new Color(1, 0, 0, 0.3f);
                    wireColor = paintMode ? Color.green : Color.red;
                }

                Handles.color = previewColor;
                DrawSolidCube(cubeCenter, cubeSize);

                Handles.color = wireColor;
                Handles.DrawWireCube(cubeCenter, cubeSize);
            }
        }
    }

    private static void DrawSolidCube(Vector3 center, Vector3 size)
    {
        Vector3 halfSize = size * 0.5f;

        Vector3[] vertices = new Vector3[8];
        vertices[0] = center + new Vector3(-halfSize.x, -halfSize.y, -halfSize.z);
        vertices[1] = center + new Vector3(halfSize.x, -halfSize.y, -halfSize.z);
        vertices[2] = center + new Vector3(halfSize.x, -halfSize.y, halfSize.z);
        vertices[3] = center + new Vector3(-halfSize.x, -halfSize.y, halfSize.z);
        vertices[4] = center + new Vector3(-halfSize.x, halfSize.y, -halfSize.z);
        vertices[5] = center + new Vector3(halfSize.x, halfSize.y, -halfSize.z);
        vertices[6] = center + new Vector3(halfSize.x, halfSize.y, halfSize.z);
        vertices[7] = center + new Vector3(-halfSize.x, halfSize.y, halfSize.z);

        Handles.DrawAAConvexPolygon(vertices[4], vertices[5], vertices[6], vertices[7]);
    }

    private static void DrawBrushOverlay(SceneView sceneView)
    {
        Handles.BeginGUI();

        GUILayout.BeginArea(new Rect(10, 150, 300, 220)); // AUGMENTE la hauteur

        GUIStyle boxStyle = new GUIStyle(GUI.skin.box);
        boxStyle.normal.background = MakeTex(2, 2, new Color(0, 0, 0, 0.8f));

        GUILayout.BeginVertical(boxStyle);

        GUIStyle labelStyle = new GUIStyle(GUI.skin.label);
        labelStyle.normal.textColor = Color.white;
        labelStyle.fontStyle = FontStyle.Bold;

        GUILayout.Label("PIT BRUSH SYSTEM", labelStyle);

        if (activeGridData == null)
        {
            labelStyle.normal.textColor = Color.yellow;
            GUILayout.Label("No Grid Data loaded", labelStyle);
        }
        else
        {
            labelStyle.normal.textColor = Color.cyan;
            GUILayout.Label("Grid: " + activeGridData.name, labelStyle);

            int cellCount = activeGridData.GetAllCells().Count;
            GUILayout.Label("Active cells: " + cellCount, labelStyle);
        }

        // NOUVEAU : Affiche le PitZone actif
        if (activePitZone != null)
        {
            labelStyle.normal.textColor = Color.green;
            GUILayout.Label("Active Zone: " + activePitZone.name + " (ID: " + activePitZone.zoneID + ")", labelStyle);
        }
        else
        {
            labelStyle.normal.textColor = Color.red;
            GUILayout.Label("No active PitZone!", labelStyle);
        }

        Color modeColor = paintMode ? Color.green : Color.red;
        string modeText = paintMode ? "PAINT (Dig)" : "ERASE (Fill)";
        labelStyle.normal.textColor = modeColor;
        GUILayout.Label("Mode: " + modeText, labelStyle);

        labelStyle.normal.textColor = Color.white;
        GUILayout.Label("Brush: " + brushSize + "x" + brushSize + " (" + (brushSize * 0.5f) + "m)", labelStyle);
        GUILayout.Label("Depth: " + paintDepth.ToString("F1") + "m", labelStyle);

        labelStyle.normal.textColor = Color.yellow;
        GUILayout.Label("[P] Toggle Brush | [B] Change Size", labelStyle);

        GUILayout.EndVertical();
        GUILayout.EndArea();

        Handles.EndGUI();
    }

    private static Texture2D MakeTex(int width, int height, Color col)
    {
        Color[] pix = new Color[width * height];
        for (int i = 0; i < pix.Length; i++)
            pix[i] = col;

        Texture2D result = new Texture2D(width, height);
        result.SetPixels(pix);
        result.Apply();
        return result;
    }
}