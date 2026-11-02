using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

// Interface commune aux composants qui suivent les aretes vives d'un mesh (EdgeLights, Barriere) :
// liste des chaines a cocher dans l'Inspector, affichage numerote dans la Scene view, cache de la detection.
public static class EdgeChainsGUI
{
    // Recalcul seulement si le mesh, l'echelle ou le seuil changent (OnSceneGUI tourne a chaque repaint).
    public class Cache
    {
        List<MeshEdgeChains.Chain> chains;
        Mesh mesh;
        Vector3 scale;
        float angle;

        public List<MeshEdgeChains.Chain> Get(Transform tr, float edgeAngle)
        {
            MeshFilter mf = tr.GetComponent<MeshFilter>();
            Mesh m = mf != null ? mf.sharedMesh : null;
            Vector3 s = tr.lossyScale;
            if (chains == null || m != mesh || s != scale || edgeAngle != angle)
            {
                chains = MeshEdgeChains.Find(m, s, edgeAngle);
                mesh = m;
                scale = s;
                angle = edgeAngle;
            }
            return chains;
        }
    }

    static readonly Color[] chainColors =
    {
        new Color(1f, 0.3f, 0.3f), new Color(0.3f, 1f, 0.4f), new Color(0.35f, 0.6f, 1f), new Color(1f, 0.85f, 0.2f),
        new Color(1f, 0.4f, 1f), new Color(0.3f, 1f, 1f), new Color(1f, 0.6f, 0.2f), new Color(0.7f, 0.5f, 1f)
    };
    static readonly Color unselectedColor = new Color(0.6f, 0.6f, 0.6f, 0.35f);

    // Cases a cocher (Tout / Rien + une par chaine, avec sa longueur). Retourne true si la selection a change.
    public static bool DrawChainList(Object owner, List<int> selected, List<MeshEdgeChains.Chain> chains, Vector3 scale)
    {
        GUILayout.Label("Aretes vives : " + chains.Count + " chaines (numeros dans la Scene view)", EditorStyles.boldLabel);
        if (chains.Count == 0)
        {
            EditorGUILayout.HelpBox("Aucune arete vive : baisser l'angle ?", MessageType.Info);
            return false;
        }

        bool changed = false;
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Tout")) { SetSelection(owner, selected, chains.Count, true); changed = true; }
        if (GUILayout.Button("Rien")) { SetSelection(owner, selected, chains.Count, false); changed = true; }
        GUILayout.EndHorizontal();

        for (int i = 0; i < chains.Count; i++)
        {
            bool on = selected.Contains(i);
            float len = chains[i].points.Count >= 2 ? new EdgeChainPath(chains[i], scale, 360f).TotalLength : 0f;
            string label = i + " - " + (chains[i].closed ? "boucle" : "ouverte") + " - " + len.ToString("0.00") + " m";
            bool now = EditorGUILayout.ToggleLeft(label, on);
            if (now == on) continue;
            Undo.RecordObject(owner, "Chaines");
            if (now) selected.Add(i); else selected.Remove(i);
            selected.Sort();
            EditorUtility.SetDirty(owner);
            SceneView.RepaintAll();
            changed = true;
        }
        return changed;
    }

    static void SetSelection(Object owner, List<int> selected, int count, bool all)
    {
        Undo.RecordObject(owner, "Chaines");
        selected.Clear();
        if (all) for (int i = 0; i < count; i++) selected.Add(i);
        EditorUtility.SetDirty(owner);
        SceneView.RepaintAll();
    }

    // Chaines cochees en couleur vive, les autres en gris pale. Boucle cochee avec arc < 360 : la portion garnie
    // en trait epais, le reste de la boucle en trait fin de la meme couleur.
    public static void DrawSceneChains(Transform tr, List<MeshEdgeChains.Chain> chains, List<int> selected, float arc)
    {
        Matrix4x4 m = tr.localToWorldMatrix;
        for (int i = 0; i < chains.Count; i++)
        {
            MeshEdgeChains.Chain c = chains[i];
            if (c.points.Count < 2) continue;
            bool on = selected.Contains(i);
            var cp = new EdgeChainPath(c, tr.lossyScale, arc);
            float active = on ? cp.Length : 0f;
            Color color = chainColors[i % chainColors.Length];

            if (active > 0f)
            {
                Handles.color = color;
                Handles.DrawAAPolyLine(5f, ToWorld(cp.Polyline(0f, active), m));
            }
            if (active < cp.TotalLength)
            {
                Handles.color = on ? new Color(color.r, color.g, color.b, 0.5f) : unselectedColor;
                Handles.DrawAAPolyLine(2f, ToWorld(cp.Polyline(active, cp.TotalLength), m));
            }
            List<Vector3> labelLine = cp.Polyline(0f, cp.TotalLength);
            Handles.Label(m.MultiplyPoint3x4(labelLine[labelLine.Count / 2]), " " + i + (c.closed ? " (boucle)" : ""),
                          EditorStyles.boldLabel);
        }
    }

    static Vector3[] ToWorld(List<Vector3> local, Matrix4x4 m)
    {
        var world = new Vector3[local.Count];
        for (int k = 0; k < local.Count; k++) world[k] = m.MultiplyPoint3x4(local[k]);
        return world;
    }
}
