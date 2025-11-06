using UnityEngine;
using UnityEditor;

[InitializeOnLoad]
public class ModuleGridRenderer
{
    private static bool gridEnabled = true;
    private static int gridSize = 20;
    private static float cellSize = 4f;
    private static Color gridColor = new Color(0.5f, 0.5f, 0.5f, 0.3f);
    private static Color subGridColor = new Color(0.3f, 0.8f, 0.3f, 0.2f);

    private const string GRID_ENABLED_KEY = "ModuleGrid_Enabled";

    static ModuleGridRenderer()
    {
        gridEnabled = EditorPrefs.GetBool(GRID_ENABLED_KEY, true);
        SceneView.duringSceneGui += OnSceneGUI;
    }

    private static void OnSceneGUI(SceneView sceneView)
    {
        Event e = Event.current;

        if (e.type == EventType.KeyDown && e.keyCode == KeyCode.G)
        {
            gridEnabled = !gridEnabled;
            EditorPrefs.SetBool(GRID_ENABLED_KEY, gridEnabled);
            Debug.Log("Grid: " + (gridEnabled ? "ON" : "OFF"));
            e.Use();
            sceneView.Repaint();
        }

        if (gridEnabled)
        {
            DrawGrid();
        }
    }

    private static void DrawGrid()
    {
        Handles.zTest = UnityEngine.Rendering.CompareFunction.LessEqual;

        float halfSize = gridSize * cellSize / 2f;

        for (int i = 0; i <= gridSize; i++)
        {
            float offset = i * cellSize - halfSize;

            bool isMainLine = (i % 1 == 0);
            Handles.color = isMainLine ? gridColor : subGridColor;

            Vector3 startX = new Vector3(offset, 0, -halfSize);
            Vector3 endX = new Vector3(offset, 0, halfSize);
            Handles.DrawLine(startX, endX);

            Vector3 startZ = new Vector3(-halfSize, 0, offset);
            Vector3 endZ = new Vector3(halfSize, 0, offset);
            Handles.DrawLine(startZ, endZ);
        }

        for (int i = 0; i < gridSize; i++)
        {
            for (int j = 0; j < gridSize; j++)
            {
                float x = (i * cellSize) - halfSize + (cellSize / 2f);
                float z = (j * cellSize) - halfSize + (cellSize / 2f);

                Handles.color = subGridColor;

                Vector3 center = new Vector3(x, 0, z);
                Vector3 p1 = center + new Vector3(-cellSize / 2f + 1, 0, 0);
                Vector3 p2 = center + new Vector3(cellSize / 2f - 1, 0, 0);
                Handles.DrawLine(p1, p2);

                Vector3 p3 = center + new Vector3(0, 0, -cellSize / 2f + 1);
                Vector3 p4 = center + new Vector3(0, 0, cellSize / 2f - 1);
                Handles.DrawLine(p3, p4);
            }
        }
    }
}