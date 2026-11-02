using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(EdgeLights))]
public class EdgeLightsEditor : Editor
{
    // ---------------------------------------------------------------- Sources du chemin

    // Ce qui porte les lumieres : longueur du chemin (metres) et point de bord en repere local du GO.
    abstract class EdgePath
    {
        public abstract string Label { get; }
        public abstract float Length { get; }
        // s, outward, up en metres. side = -1 gauche, +1 droite. forward en repere local, unitaire.
        public abstract void Point(float s, int side, float outward, float up, out Vector3 localPos, out Vector3 forward);
    }

    class RampPath : EdgePath
    {
        readonly Ramp ramp;
        public RampPath(Ramp ramp) { this.ramp = ramp; }
        public override string Label => "Ramp";
        public override float Length => ramp.WalkLength;
        public override void Point(float s, int side, float outward, float up, out Vector3 localPos, out Vector3 forward)
        {
            ramp.GetEdgePoint(s, side, outward, up, out localPos, out forward);
        }
    }

    // Boite du mesh (repere local) : chemin le long de X ou Z, dessus = face haute. Distances converties en metres
    // reels avec l'echelle du GO.
    class BoxPath : EdgePath
    {
        readonly Bounds b;
        readonly Vector3 scale;
        readonly bool alongX;

        public BoxPath(Bounds bounds, Vector3 lossyScale, EdgeLights.Axis axis)
        {
            b = bounds;
            scale = new Vector3(Mathf.Max(1e-4f, Mathf.Abs(lossyScale.x)), Mathf.Max(1e-4f, Mathf.Abs(lossyScale.y)),
                                Mathf.Max(1e-4f, Mathf.Abs(lossyScale.z)));
            alongX = axis == EdgeLights.Axis.X
                     || (axis == EdgeLights.Axis.Auto && b.size.x * scale.x > b.size.z * scale.z);
        }

        public override string Label => "Boite du mesh, chemin le long de " + (alongX ? "X" : "Z");
        public override float Length => alongX ? b.size.x * scale.x : b.size.z * scale.z;

        public override void Point(float s, int side, float outward, float up, out Vector3 localPos, out Vector3 forward)
        {
            float y = b.max.y + up / scale.y;
            if (alongX)
            {
                // En avancant vers +X, la droite est -Z.
                float x = b.min.x + s / scale.x;
                float z = b.center.z - side * (b.extents.z + outward / scale.z);
                localPos = new Vector3(x, y, z);
                forward = Vector3.right;
            }
            else
            {
                float z = b.min.z + s / scale.z;
                float x = b.center.x + side * (b.extents.x + outward / scale.x);
                localPos = new Vector3(x, y, z);
                forward = Vector3.forward;
            }
        }
    }

    // Chaine d'aretes vives (repere local du mesh). Abscisse en metres reels (echelle du GO appliquee).
    // side et outward ignores ; up = vers le haut du monde. forward dans le repere tourne du GO, sans l'echelle.
    class ChainPath : EdgePath
    {
        readonly List<Vector3> pts;   // boucle : le premier point est repete a la fin
        readonly float[] cumul;       // longueur cumulee en metres jusqu'a chaque point
        readonly Vector3 scale;
        readonly Transform tr;

        public ChainPath(MeshEdgeChains.Chain chain, Transform tr)
        {
            this.tr = tr;
            scale = tr.lossyScale;
            pts = new List<Vector3>(chain.points);
            if (chain.closed) pts.Add(chain.points[0]);
            cumul = new float[pts.Count];
            for (int i = 1; i < pts.Count; i++)
                cumul[i] = cumul[i - 1] + Vector3.Scale(pts[i] - pts[i - 1], scale).magnitude;
        }

        public override string Label => "Arete";
        public override float Length => cumul[cumul.Length - 1];

        public override void Point(float s, int side, float outward, float up, out Vector3 localPos, out Vector3 forward)
        {
            int i = 1;
            while (i < pts.Count - 1 && cumul[i] < s) i++;
            float segLen = cumul[i] - cumul[i - 1];
            float t = segLen > 1e-6f ? Mathf.Clamp01((s - cumul[i - 1]) / segLen) : 0f;
            localPos = Vector3.Lerp(pts[i - 1], pts[i], t) + tr.InverseTransformVector(Vector3.up * up);
            forward = Vector3.Scale(pts[i] - pts[i - 1], scale).normalized;
        }
    }

    static EdgePath GetPath(EdgeLights el)
    {
        Ramp ramp = el.GetComponent<Ramp>();
        if (ramp != null) return new RampPath(ramp);
        MeshFilter mf = el.GetComponent<MeshFilter>();
        if (mf != null && mf.sharedMesh != null) return new BoxPath(mf.sharedMesh.bounds, el.transform.lossyScale, el.axis);
        return null;
    }

    // Un passage de generation : un bord (gauche / droite) d'une Ramp ou d'une boite, ou une chaine cochee.
    class Run
    {
        public EdgePath path;
        public int side;
        public string name;
        public bool closed;
    }

    static List<Run> GetRuns(EdgeLights el, List<MeshEdgeChains.Chain> chains)
    {
        var runs = new List<Run>();
        if (el.source == EdgeLights.Source.Aretes)
        {
            foreach (int i in el.selectedChains)
                if (i >= 0 && i < chains.Count && chains[i].points.Count >= 2)
                    runs.Add(new Run { path = new ChainPath(chains[i], el.transform), side = 0, name = "C" + i, closed = chains[i].closed });
            return runs;
        }

        EdgePath path = GetPath(el);
        if (path == null) return runs;
        if (el.sides != EdgeLights.Sides.Droite) runs.Add(new Run { path = path, side = -1, name = "L" });
        if (el.sides != EdgeLights.Sides.Gauche) runs.Add(new Run { path = path, side = 1, name = "R" });
        return runs;
    }

    static List<MeshEdgeChains.Chain> FindChains(EdgeLights el)
    {
        MeshFilter mf = el.GetComponent<MeshFilter>();
        return MeshEdgeChains.Find(mf != null ? mf.sharedMesh : null, el.transform.lossyScale, el.edgeAngle);
    }

    // ---------------------------------------------------------------- Inspector

    // Champs sans objet selon la source : masques.
    static readonly string[] autoOnly = { "sides", "axis", "outward" };
    static readonly string[] aretesOnly = { "edgeAngle" };

    public override void OnInspectorGUI()
    {
        EdgeLights el = (EdgeLights)target;
        bool aretes = el.source == EdgeLights.Source.Aretes;

        serializedObject.Update();
        SerializedProperty it = serializedObject.GetIterator();
        for (bool enter = true; it.NextVisible(enter); enter = false)
        {
            if (it.propertyPath == "m_Script")
            {
                using (new EditorGUI.DisabledScope(true)) EditorGUILayout.PropertyField(it);
                continue;
            }
            if (System.Array.IndexOf(aretes ? autoOnly : aretesOnly, it.propertyPath) >= 0) continue;
            EditorGUILayout.PropertyField(it, true);
        }
        serializedObject.ApplyModifiedProperties();

        List<MeshEdgeChains.Chain> chains = aretes ? GetChains(el) : null;
        List<Run> runs = GetRuns(el, chains);

        GUILayout.Space(10);
        GUILayout.BeginVertical(EditorStyles.helpBox);

        if (aretes) DrawChainList(el, chains);

        if (!aretes && runs.Count == 0)
            EditorGUILayout.HelpBox("Pas de mesh sur ce GO.", MessageType.Warning);
        else
        {
            if (!aretes) GUILayout.Label("Source : " + runs[0].path.Label, EditorStyles.miniLabel);
            if (el.prefab == null)
                EditorGUILayout.HelpBox("Choisir un prefab ou un GO.", MessageType.Warning);
            else if (IsInsideGenerated(el))
                EditorGUILayout.HelpBox("Le modele est dans " + EdgeLights.ContainerName + " : il serait efface a la " +
                                        "generation. Le sortir de ce dossier.", MessageType.Error);
            else if (aretes && runs.Count == 0)
                EditorGUILayout.HelpBox("Cocher au moins une chaine.", MessageType.Warning);
            else
            {
                int total = 0;
                foreach (Run r in runs) total += Positions(el, r.path.Length, r.closed).Length;
                GUILayout.Label(total + " instances", EditorStyles.miniLabel);
            }
        }

        GUI.enabled = el.prefab != null && runs.Count > 0 && !IsInsideGenerated(el);
        GUI.backgroundColor = Color.green;
        if (GUILayout.Button("Generer (remplace les existantes)", GUILayout.Height(30)))
            Generate(el);
        GUI.enabled = true;

        GUI.backgroundColor = Color.red;
        if (GUILayout.Button("Effacer", GUILayout.Height(22)))
            Clear(el);
        GUI.backgroundColor = Color.white;

        GUILayout.EndVertical();
    }

    void DrawChainList(EdgeLights el, List<MeshEdgeChains.Chain> chains)
    {
        GUILayout.Label("Aretes vives : " + chains.Count + " chaines (numeros dans la Scene view)", EditorStyles.boldLabel);
        if (chains.Count == 0)
        {
            EditorGUILayout.HelpBox("Aucune arete vive : baisser l'angle ?", MessageType.Info);
            return;
        }

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Tout")) SetSelection(el, chains.Count, true);
        if (GUILayout.Button("Rien")) SetSelection(el, chains.Count, false);
        GUILayout.EndHorizontal();

        for (int i = 0; i < chains.Count; i++)
        {
            bool on = el.selectedChains.Contains(i);
            float len = new ChainPath(chains[i], el.transform).Length;
            string label = i + " - " + (chains[i].closed ? "boucle" : "ouverte") + " - " + len.ToString("0.00") + " m";
            bool now = EditorGUILayout.ToggleLeft(label, on);
            if (now == on) continue;
            Undo.RecordObject(el, "Chaines EdgeLights");
            if (now) el.selectedChains.Add(i); else el.selectedChains.Remove(i);
            el.selectedChains.Sort();
            EditorUtility.SetDirty(el);
            SceneView.RepaintAll();
        }
    }

    static void SetSelection(EdgeLights el, int count, bool all)
    {
        Undo.RecordObject(el, "Chaines EdgeLights");
        el.selectedChains.Clear();
        if (all) for (int i = 0; i < count; i++) el.selectedChains.Add(i);
        EditorUtility.SetDirty(el);
        SceneView.RepaintAll();
    }

    // ---------------------------------------------------------------- Aretes vives : affichage

    // Recalcul seulement si le mesh, l'echelle ou le seuil changent (OnSceneGUI tourne a chaque repaint).
    List<MeshEdgeChains.Chain> cachedChains;
    Mesh cachedMesh;
    Vector3 cachedScale;
    float cachedAngle;

    List<MeshEdgeChains.Chain> GetChains(EdgeLights el)
    {
        MeshFilter mf = el.GetComponent<MeshFilter>();
        Mesh mesh = mf != null ? mf.sharedMesh : null;
        Vector3 scale = el.transform.lossyScale;
        if (cachedChains == null || mesh != cachedMesh || scale != cachedScale || el.edgeAngle != cachedAngle)
        {
            cachedChains = FindChains(el);
            cachedMesh = mesh;
            cachedScale = scale;
            cachedAngle = el.edgeAngle;
        }
        return cachedChains;
    }

    static readonly Color[] chainColors =
    {
        new Color(1f, 0.3f, 0.3f), new Color(0.3f, 1f, 0.4f), new Color(0.35f, 0.6f, 1f), new Color(1f, 0.85f, 0.2f),
        new Color(1f, 0.4f, 1f), new Color(0.3f, 1f, 1f), new Color(1f, 0.6f, 0.2f), new Color(0.7f, 0.5f, 1f)
    };
    static readonly Color unselectedColor = new Color(0.6f, 0.6f, 0.6f, 0.35f);

    // Chaines cochees en couleur vive, les autres en gris pale.
    void OnSceneGUI()
    {
        EdgeLights el = (EdgeLights)target;
        if (el.source != EdgeLights.Source.Aretes) return;

        List<MeshEdgeChains.Chain> chains = GetChains(el);
        Matrix4x4 m = el.transform.localToWorldMatrix;
        for (int i = 0; i < chains.Count; i++)
        {
            MeshEdgeChains.Chain c = chains[i];
            bool on = el.selectedChains.Contains(i);
            int n = c.points.Count + (c.closed ? 1 : 0);
            var world = new Vector3[n];
            for (int p = 0; p < n; p++) world[p] = m.MultiplyPoint3x4(c.points[p % c.points.Count]);

            Handles.color = on ? chainColors[i % chainColors.Length] : unselectedColor;
            Handles.DrawAAPolyLine(on ? 5f : 2f, world);
            Handles.Label(world[c.points.Count / 2], " " + i + (c.closed ? " (boucle)" : ""), EditorStyles.boldLabel);
        }
    }

    // ---------------------------------------------------------------- Generation

    // Abscisses le long du chemin. Ouvert : de la marge de debut a la marge de fin, les deux bouts compris.
    // Boucle : sans marges, n intervalles egaux, le dernier point retombe sur le premier donc n'est pas repete.
    static float[] Positions(EdgeLights el, float length, bool closed)
    {
        float spacing = Mathf.Max(0.05f, el.spacing);
        if (closed)
        {
            if (length < 1e-4f) return new float[0];
            int count = Mathf.Max(1, Mathf.RoundToInt(length / spacing));
            float loopStep = length / count;
            float[] l = new float[count];
            for (int i = 0; i < count; i++) l[i] = i * loopStep;
            return l;
        }

        float start = el.startMargin;
        float usable = length - el.startMargin - el.endMargin;
        if (usable < 0f) return new float[0];
        if (usable < 1e-4f) return new[] { start };

        int intervals = Mathf.Max(1, Mathf.RoundToInt(usable / spacing));
        float step = usable / intervals;
        float[] s = new float[intervals + 1];
        for (int i = 0; i <= intervals; i++) s[i] = start + i * step;
        return s;
    }

    static void Generate(EdgeLights el)
    {
        Undo.SetCurrentGroupName("Generer EdgeLights");
        int group = Undo.GetCurrentGroup();

        Clear(el);
        Ramp ramp = el.GetComponent<Ramp>();
        if (ramp != null) ramp.Rebuild(); // pivot et dimensions a jour
        bool aretes = el.source == EdgeLights.Source.Aretes;
        List<Run> runs = GetRuns(el, aretes ? FindChains(el) : null);
        if (runs.Count == 0) return;

        GameObject container = new GameObject(EdgeLights.ContainerName);
        Undo.RegisterCreatedObjectUndo(container, "Generer EdgeLights");
        container.transform.SetParent(el.transform, false);

        bool isAsset = PrefabUtility.IsPartOfPrefabAsset(el.prefab);
        Vector3 prefabScale = el.prefab.transform.localScale;
        Vector3 parentScale = el.transform.lossyScale;
        // Compense l'echelle du parent : les instances gardent la taille du prefab.
        Vector3 scale = new Vector3(
            prefabScale.x / Mathf.Max(1e-4f, Mathf.Abs(parentScale.x)),
            prefabScale.y / Mathf.Max(1e-4f, Mathf.Abs(parentScale.y)),
            prefabScale.z / Mathf.Max(1e-4f, Mathf.Abs(parentScale.z)));

        foreach (Run run in runs)
        {
            float[] positions = Positions(el, run.path.Length, run.closed);
            for (int i = 0; i < positions.Length; i++)
            {
                run.path.Point(positions[i], run.side, el.outward, el.height, out Vector3 pos, out Vector3 forward);

                GameObject inst = isAsset
                    ? (GameObject)PrefabUtility.InstantiatePrefab(el.prefab, container.transform)
                    : Instantiate(el.prefab, container.transform);
                inst.name = el.prefab.name + "_" + run.name + (aretes ? "_" : "") + i.ToString("D2");
                if (!isAsset) inst.SetActive(true); // modele de la scene eventuellement desactive
                inst.transform.localPosition = pos;
                if (!el.alignToPath)
                    inst.transform.rotation = el.prefab.transform.rotation;
                else if (aretes)
                {
                    // Haut du monde ; arete verticale (coin de cube) : on prend +Z monde pour ne pas partir en vrille.
                    Vector3 worldForward = el.transform.rotation * forward;
                    Vector3 up = Mathf.Abs(Vector3.Dot(worldForward, Vector3.up)) > 0.999f ? Vector3.forward : Vector3.up;
                    inst.transform.rotation = Quaternion.LookRotation(worldForward, up);
                }
                else
                    inst.transform.rotation = el.transform.rotation * Quaternion.LookRotation(forward, Vector3.up);
                inst.transform.localScale = scale;
                Undo.RegisterCreatedObjectUndo(inst, "Generer EdgeLights");
            }
        }

        Undo.CollapseUndoOperations(group);
    }

    // Un modele range dans le dossier genere disparaitrait au premier Generer / Effacer.
    static bool IsInsideGenerated(EdgeLights el)
    {
        if (el.prefab == null || PrefabUtility.IsPartOfPrefabAsset(el.prefab)) return false;
        Transform container = el.transform.Find(EdgeLights.ContainerName);
        return container != null && el.prefab.transform.IsChildOf(container);
    }

    static void Clear(EdgeLights el)
    {
        Transform existing = el.transform.Find(EdgeLights.ContainerName);
        if (existing != null) Undo.DestroyObjectImmediate(existing.gameObject);
    }
}
