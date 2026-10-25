using UnityEngine;
using UnityEditor;

// Inspecteur de BallStack : bouton Relancer (nouvelle graine) au-dessus des reglages
[CustomEditor(typeof(BallStack))]
[CanEditMultipleObjects]
public class BallStackEditor : Editor
{
    public override void OnInspectorGUI()
    {
        if (GUILayout.Button("Relancer (nouvelle graine)", GUILayout.Height(26)))
        {
            foreach (Object t in targets)
                ((BallStack)t).Reroll();
        }
        EditorGUILayout.Space();
        DrawDefaultInspector();
    }
}
