using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(ConcentricCirclesSpawner))]
public class ConcentricCirclesSpawnerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        // Afficher l'Inspector normal
        DrawDefaultInspector();

        ConcentricCirclesSpawner spawner = (ConcentricCirclesSpawner)target;

        GUILayout.Space(15);

        // Section Preview
        EditorGUILayout.LabelField("Preview Editor", EditorStyles.boldLabel);

        EditorGUILayout.HelpBox(
            "Genere les cercles concentriques dans l'editeur pour visualisation.\n" +
            "- Nombre de bulbs calcule automatiquement selon espacement\n" +
            "- Alignement radial garanti selon startAngle\n" +
            "- Preview non sauvegardee dans la scene",
            MessageType.Info
        );

        GUILayout.Space(5);

        // Afficher info calcul bulbs
        if (spawner != null)
        {
            EditorGUILayout.LabelField("Calcul Bulbs par Cercle:", EditorStyles.boldLabel);

            SerializedProperty radiiProp = serializedObject.FindProperty("radii");
            SerializedProperty spacingProp = serializedObject.FindProperty("bulbSpacing");

            if (radiiProp != null && spacingProp != null)
            {
                float spacing = spacingProp.floatValue;

                for (int i = 0; i < radiiProp.arraySize; i++)
                {
                    float radius = radiiProp.GetArrayElementAtIndex(i).floatValue;
                    float circumference = 2f * Mathf.PI * radius;
                    int bulbCount = Mathf.Max(1, Mathf.RoundToInt(circumference / spacing));

                    EditorGUILayout.LabelField($"  Cercle {i + 1} (R={radius:F1}m) = {bulbCount} bulbs");
                }
            }
        }

        GUILayout.Space(10);

        // Boutons
        GUILayout.BeginHorizontal();

        if (GUILayout.Button("Generate Preview", GUILayout.Height(35)))
        {
            spawner.GeneratePreview();
            SceneView.RepaintAll();
        }

        if (GUILayout.Button("Clear Preview", GUILayout.Height(35)))
        {
            spawner.ClearPreview();
            SceneView.RepaintAll();
        }

        GUILayout.EndHorizontal();
    }
}