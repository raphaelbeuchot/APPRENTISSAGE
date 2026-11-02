using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(Barriere))]
public class BarriereEditor : Editor
{
    // Champs sans objet selon la source : masques.
    static readonly string[] libreSeulement = { "longueur" };
    static readonly string[] aretesSeulement = { "angleAretes", "arc" };

    readonly EdgeChainsGUI.Cache chainCache = new EdgeChainsGUI.Cache();

    public override void OnInspectorGUI()
    {
        Barriere b = (Barriere)target;
        bool aretes = b.source == Barriere.Source.Aretes;

        serializedObject.Update();
        SerializedProperty it = serializedObject.GetIterator();
        for (bool enter = true; it.NextVisible(enter); enter = false)
        {
            if (it.propertyPath == "m_Script")
            {
                using (new EditorGUI.DisabledScope(true)) EditorGUILayout.PropertyField(it);
                continue;
            }
            if (System.Array.IndexOf(aretes ? libreSeulement : aretesSeulement, it.propertyPath) >= 0) continue;
            EditorGUILayout.PropertyField(it, true);
        }
        serializedObject.ApplyModifiedProperties();

        if (aretes)
        {
            GUILayout.Space(10);
            GUILayout.BeginVertical(EditorStyles.helpBox);
            if (b.GetComponent<MeshFilter>() == null)
                EditorGUILayout.HelpBox("Pas de mesh sur ce GO : la source Aretes suit les aretes du mesh de ce GO.", MessageType.Warning);
            else
            {
                List<MeshEdgeChains.Chain> chains = chainCache.Get(b.transform, b.angleAretes);
                if (EdgeChainsGUI.DrawChainList(b, b.chainesCochees, chains, b.transform.lossyScale))
                    b.Rebuild();
                else if (b.chainesCochees.Count == 0 && chains.Count > 0)
                    EditorGUILayout.HelpBox("Cocher au moins une chaine.", MessageType.Info);
            }
            GUILayout.EndVertical();
        }

        GUILayout.Space(5);
        if (GUILayout.Button("Reconstruire")) b.Rebuild();

        // Changement d'echelle du mesh, de mesh... : pas d'OnValidate, on verifie ici.
        b.RebuildSiChange();
    }

    void OnSceneGUI()
    {
        Barriere b = (Barriere)target;
        if (b.source != Barriere.Source.Aretes || b.GetComponent<MeshFilter>() == null) return;
        EdgeChainsGUI.DrawSceneChains(b.transform, chainCache.Get(b.transform, b.angleAretes), b.chainesCochees, b.arc);
    }
}
