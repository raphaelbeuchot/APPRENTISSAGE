using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(MarqueeLightSpawner))]
public class MarqueeLightSpawnerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        // Afficher l'Inspector normal
        DrawDefaultInspector();

        MarqueeLightSpawner spawner = (MarqueeLightSpawner)target;

        GUILayout.Space(15);

        // Section Preview
        EditorGUILayout.LabelField("Preview Editor", EditorStyles.boldLabel);

        EditorGUILayout.HelpBox(
            "Génère les bulbs dans l'éditeur pour visualisation.\n" +
            "Les bulbs preview ne seront PAS sauvegardées dans la scène.",
            MessageType.Info
        );

        GUILayout.Space(5);

        // Boutons
        GUILayout.BeginHorizontal();

        if (GUILayout.Button("Generate Preview", GUILayout.Height(35)))
        {
            spawner.GeneratePreview();
            SceneView.RepaintAll(); // Force refresh de la Scene view
        }

        if (GUILayout.Button("Clear Preview", GUILayout.Height(35)))
        {
            spawner.ClearPreview();
            SceneView.RepaintAll();
        }

        GUILayout.EndHorizontal();
    }
}