using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(PitZone))]
public class PitZoneEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        PitZone pitZone = (PitZone)target;

        GUILayout.Space(10);

        DrawMeshGenerationSection(pitZone);

        GUILayout.Space(10);

        DrawPitStructureSection(pitZone);

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

    private void DrawPitStructureSection(PitZone pitZone)
    {
        GUILayout.BeginVertical(EditorStyles.helpBox);
        GUILayout.Label("Pit Structure", EditorStyles.boldLabel);

        GUI.backgroundColor = Color.cyan;
        if (GUILayout.Button("Create NavMesh Margin", GUILayout.Height(30)))
        {
            Undo.RecordObject(pitZone, "Create NavMesh Margin");
            pitZone.CreateNavMeshMargin();
            EditorUtility.SetDirty(pitZone);
            SceneView.RepaintAll();
        }
        GUI.backgroundColor = Color.white;

        GUILayout.Space(5);
        EditorGUILayout.HelpBox("Use 'Tools > Pit Fill Editor' to add fill content.", MessageType.Info);

        GUILayout.EndVertical();
    }

    private void DrawInfoSection(PitZone pitZone)
    {
        if (pitZone.gridData != null)
        {
            int cellCount = 0;
            if (pitZone.zoneID != -1)
            {
                cellCount = pitZone.gridData.GetCellsForZone(pitZone.zoneID).Count;
            }

            float maxDepth = pitZone.GetMaxDepth();
            GUILayout.Label("Active cells: " + cellCount + " | Max depth: " + maxDepth.ToString("F1") + "m", EditorStyles.miniLabel);
        }
        else
        {
            GUILayout.Label("No grid data assigned", EditorStyles.miniLabel);
        }
    }
}