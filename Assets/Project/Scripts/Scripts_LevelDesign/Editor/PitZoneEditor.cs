using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(PitZone))]
public class PitZoneEditor : Editor
{
    private bool fillEditMode = false;
    private float previewFillHeight = 0.5f;

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        PitZone pitZone = (PitZone)target;

        GUILayout.Space(10);

        DrawMeshGenerationSection(pitZone);

        GUILayout.Space(10);

        DrawFillContentSection(pitZone);

        GUILayout.Space(10);

        DrawDamageSystemSection(pitZone); // NOUVEAU

        GUILayout.Space(5);

        DrawInfoSection(pitZone);
    }

    private void DrawMeshGenerationSection(PitZone pitZone)
    {
        GUILayout.BeginVertical(EditorStyles.helpBox);
        GUILayout.Label("Mesh Generation", EditorStyles.boldLabel);

        GUI.backgroundColor = Color.green;
        if (GUILayout.Button("Generate Meshes", GUILayout.Height(35)))
        {
            pitZone.GenerateMeshes();
            EditorUtility.SetDirty(pitZone);
            SceneView.RepaintAll();
        }
        GUI.backgroundColor = Color.white;

        GUI.backgroundColor = Color.red;
        if (GUILayout.Button("Clear Meshes", GUILayout.Height(25)))
        {
            if (EditorUtility.DisplayDialog(
                "Clear Meshes",
                "Are you sure you want to clear all meshes?",
                "Yes",
                "No"))
            {
                pitZone.ClearMeshes();
                EditorUtility.SetDirty(pitZone);
                SceneView.RepaintAll();
            }
        }
        GUI.backgroundColor = Color.white;

        GUILayout.EndVertical();
    }

    private void DrawFillContentSection(PitZone pitZone)
    {
        GUILayout.BeginVertical(EditorStyles.helpBox);
        GUILayout.Label("Fill Content", EditorStyles.boldLabel);

        if (!fillEditMode)
        {
            GUI.backgroundColor = Color.cyan;
            if (GUILayout.Button("Fill Pit", GUILayout.Height(30)))
            {
                fillEditMode = true;
                previewFillHeight = pitZone.fillHeightPercent;
                SceneView.RepaintAll();
            }
            GUI.backgroundColor = Color.white;

            if (pitZone.contentInstance != null)
            {
                GUILayout.Label("Current: " + (pitZone.fillType != null ? pitZone.fillType.contentName : "Unknown"), EditorStyles.miniLabel);

                GUI.backgroundColor = Color.red;
                if (GUILayout.Button("Clear Fill", GUILayout.Height(25)))
                {
                    pitZone.ClearFillContent();
                    EditorUtility.SetDirty(pitZone);
                    SceneView.RepaintAll();
                }
                GUI.backgroundColor = Color.white;
            }
        }
        else
        {
            GUILayout.Label("FILL EDIT MODE", EditorStyles.boldLabel);

            pitZone.fillType = (PitContentType)EditorGUILayout.ObjectField(
                "Content Type:",
                pitZone.fillType,
                typeof(PitContentType),
                false
            );

            previewFillHeight = EditorGUILayout.Slider("Fill Height:", previewFillHeight, 0f, 1f);
            GUILayout.Label((previewFillHeight * 100f).ToString("F0") + "%", EditorStyles.boldLabel);

            GUILayout.BeginHorizontal();

            GUI.backgroundColor = Color.green;
            if (GUILayout.Button("VALIDATE", GUILayout.Height(30)))
            {
                pitZone.fillHeightPercent = previewFillHeight;
                pitZone.SpawnFillContent();
                fillEditMode = false;
                EditorUtility.SetDirty(pitZone);
                SceneView.RepaintAll();
            }

            GUI.backgroundColor = Color.red;
            if (GUILayout.Button("CANCEL", GUILayout.Height(30)))
            {
                fillEditMode = false;
                SceneView.RepaintAll();
            }
            GUI.backgroundColor = Color.white;

            GUILayout.EndHorizontal();
        }

        GUILayout.EndVertical();
    }

    // NOUVELLE SECTION - DAMAGE SYSTEM
    private void DrawDamageSystemSection(PitZone pitZone)
    {
        GUILayout.BeginVertical(EditorStyles.helpBox);
        GUILayout.Label("Damage System", EditorStyles.boldLabel);

        PitDamageController damageController = pitZone.GetComponentInChildren<PitDamageController>();

        if (damageController == null)
        {
            GUI.backgroundColor = Color.red;
            if (GUILayout.Button("Setup Damage System", GUILayout.Height(40)))
            {
                Undo.RecordObject(pitZone, "Setup Damage System");
                pitZone.SetupDamageSystem();
                EditorUtility.SetDirty(pitZone);
                SceneView.RepaintAll();
            }
            GUI.backgroundColor = Color.white;
        }
        else
        {
            GUILayout.Label("Damage system active", EditorStyles.miniLabel);

            GUI.backgroundColor = Color.yellow;
            if (GUILayout.Button("Update Trigger Bounds", GUILayout.Height(25)))
            {
                damageController.UpdateTriggerBounds();
                EditorUtility.SetDirty(damageController);
                SceneView.RepaintAll();
            }
            GUI.backgroundColor = Color.white;
        }

        // AJOUTE ICI (nouveau bouton)
        GUILayout.Space(5);
        GUI.backgroundColor = Color.cyan;
        if (GUILayout.Button("Create NavMesh Margin", GUILayout.Height(25)))
        {
            Undo.RecordObject(pitZone, "Create NavMesh Margin");
            pitZone.CreateNavMeshMargin(0.5f);
            EditorUtility.SetDirty(pitZone);
            SceneView.RepaintAll();
        }
        GUI.backgroundColor = Color.white;

        GUILayout.EndVertical();
    }

    private void DrawInfoSection(PitZone pitZone)
    {
        if (pitZone.gridData != null)
        {
            int cellCount = pitZone.gridData.GetAllCells().Count;
            float maxDepth = pitZone.GetMaxDepth();
            GUILayout.Label("Active cells: " + cellCount + " | Max depth: " + maxDepth.ToString("F1") + "m", EditorStyles.miniLabel);
        }
        else
        {
            GUILayout.Label("No grid data assigned", EditorStyles.miniLabel);
        }
    }

    private void OnSceneGUI()
    {
        PitZone pitZone = (PitZone)target;

        if (fillEditMode && pitZone.fillType != null && pitZone.gridData != null)
        {
            DrawFillPreview(pitZone);
        }
    }

    private void DrawFillPreview(PitZone pitZone)
    {
        float maxDepth = pitZone.GetMaxDepth();
        if (maxDepth >= 0) return;

        float fillHeight = maxDepth * previewFillHeight;
        Vector3 fillPosition = pitZone.GetFillPosition(fillHeight);

        var allCells = pitZone.gridData.GetAllCells();
        Vector2Int min = new Vector2Int(int.MaxValue, int.MaxValue);
        Vector2Int max = new Vector2Int(int.MinValue, int.MinValue);

        foreach (var kvp in allCells)
        {
            if (kvp.Key.x < min.x) min.x = kvp.Key.x;
            if (kvp.Key.y < min.y) min.y = kvp.Key.y;
            if (kvp.Key.x > max.x) max.x = kvp.Key.x;
            if (kvp.Key.y > max.y) max.y = kvp.Key.y;
        }

        float cellSize = pitZone.gridData.gridCellSize;
        float sizeX = (max.x - min.x + 1) * cellSize;
        float sizeZ = (max.y - min.y + 1) * cellSize;
        float sizeY = Mathf.Abs(fillHeight);

        Vector3 cubeSize = new Vector3(sizeX, sizeY, sizeZ);
        Vector3 cubeCenter = fillPosition;

        // Preview semi-transparent
        Handles.color = pitZone.fillType.previewColor;
        DrawSolidBox(cubeCenter, cubeSize);

        // Wireframe
        Handles.color = new Color(
            pitZone.fillType.previewColor.r,
            pitZone.fillType.previewColor.g,
            pitZone.fillType.previewColor.b,
            1f
        );
        Handles.DrawWireCube(cubeCenter, cubeSize);

        // Label
        Handles.Label(
            cubeCenter + Vector3.up * (sizeY * 0.5f + 0.2f),
            pitZone.fillType.contentName + " (" + (previewFillHeight * 100f).ToString("F0") + "%)",
            EditorStyles.boldLabel
        );
    }

    private void DrawSolidBox(Vector3 center, Vector3 size)
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

        // 6 faces du cube
        Handles.DrawAAConvexPolygon(vertices[0], vertices[1], vertices[2], vertices[3]); // Bottom
        Handles.DrawAAConvexPolygon(vertices[4], vertices[5], vertices[6], vertices[7]); // Top
        Handles.DrawAAConvexPolygon(vertices[0], vertices[1], vertices[5], vertices[4]); // Front
        Handles.DrawAAConvexPolygon(vertices[2], vertices[3], vertices[7], vertices[6]); // Back
        Handles.DrawAAConvexPolygon(vertices[0], vertices[3], vertices[7], vertices[4]); // Left
        Handles.DrawAAConvexPolygon(vertices[1], vertices[2], vertices[6], vertices[5]); // Right
    }
}