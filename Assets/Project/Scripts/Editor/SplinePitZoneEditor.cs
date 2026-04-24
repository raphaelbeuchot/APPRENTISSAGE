using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(SplinePitZone))]
public class SplinePitZoneEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        SplinePitZone zone = (SplinePitZone)target;

        GUILayout.Space(10);
        GUILayout.BeginVertical(EditorStyles.helpBox);
        GUILayout.Label("Mesh Generation", EditorStyles.boldLabel);

        GUI.backgroundColor = Color.green;
        if (GUILayout.Button("Generate Meshes", GUILayout.Height(35)))
        {
            zone.GenerateMeshes();
            EditorUtility.SetDirty(zone);
            SceneView.RepaintAll();
        }

        GUI.backgroundColor = Color.red;
        if (GUILayout.Button("Clear Meshes", GUILayout.Height(25)))
        {
            if (EditorUtility.DisplayDialog(
                "Clear Meshes",
                "Supprimer les meshes generes ?",
                "Oui",
                "Annuler"))
            {
                zone.ClearMeshes();
                EditorUtility.SetDirty(zone);
                SceneView.RepaintAll();
            }
        }

        GUILayout.Space(5);
        GUILayout.BeginVertical(EditorStyles.helpBox);
        GUILayout.Label("A* Pathfinding", EditorStyles.boldLabel);
        GUI.backgroundColor = Color.yellow;
        if (GUILayout.Button("Apply A* Walkability", GUILayout.Height(30)))
        {
            zone.ApplyAStarWalkability();
            EditorUtility.SetDirty(zone);
        }
        GUI.backgroundColor = Color.white;
        GUILayout.EndVertical();

        GUI.backgroundColor = Color.white;
        GUILayout.EndVertical();

        GUILayout.Space(5);
        GUILayout.BeginVertical(EditorStyles.helpBox);
        GUILayout.Label("Info", EditorStyles.boldLabel);

        UnityEngine.Splines.SplineContainer sc = zone.GetSplineContainer();
        if (sc == null)
        {
            EditorGUILayout.HelpBox("Aucun SplineContainer trouve. Ajoute un SplineContainer sur ce GO puis dessine une spline fermee.", MessageType.Warning);
        }
        else
        {
            int knots = sc.Spline.Count;
            EditorGUILayout.HelpBox("SplineContainer OK - " + knots + " knots", MessageType.Info);
        }

        GUILayout.EndVertical();
    }
}