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

    static EdgePath GetPath(EdgeLights el)
    {
        Ramp ramp = el.GetComponent<Ramp>();
        if (ramp != null) return new RampPath(ramp);
        MeshFilter mf = el.GetComponent<MeshFilter>();
        if (mf != null && mf.sharedMesh != null) return new BoxPath(mf.sharedMesh.bounds, el.transform.lossyScale, el.axis);
        return null;
    }

    // ---------------------------------------------------------------- Inspector

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EdgeLights el = (EdgeLights)target;
        EdgePath path = GetPath(el);

        GUILayout.Space(10);
        GUILayout.BeginVertical(EditorStyles.helpBox);

        if (path == null)
            EditorGUILayout.HelpBox("Pas de mesh sur ce GO.", MessageType.Warning);
        else
        {
            GUILayout.Label("Source : " + path.Label, EditorStyles.miniLabel);
            if (el.prefab == null)
                EditorGUILayout.HelpBox("Choisir un prefab ou un GO.", MessageType.Warning);
            else if (IsInsideGenerated(el))
                EditorGUILayout.HelpBox("Le modele est dans " + EdgeLights.ContainerName + " : il serait efface a la " +
                                        "generation. Le sortir de ce dossier.", MessageType.Error);
            else
            {
                int perSide = Positions(el, path.Length).Length;
                int sidesCount = el.sides == EdgeLights.Sides.LesDeux ? 2 : 1;
                GUILayout.Label(perSide * sidesCount + " instances (" + perSide + " par bord)", EditorStyles.miniLabel);
            }
        }

        GUI.enabled = el.prefab != null && path != null && !IsInsideGenerated(el);
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

    // Abscisses le long de la ligne mediane, de la marge de debut a la marge de fin, espacement ajuste.
    static float[] Positions(EdgeLights el, float length)
    {
        float start = el.startMargin;
        float usable = length - el.startMargin - el.endMargin;
        if (usable < 0f) return new float[0];
        if (usable < 1e-4f) return new[] { start };

        int intervals = Mathf.Max(1, Mathf.RoundToInt(usable / Mathf.Max(0.05f, el.spacing)));
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
        EdgePath path = GetPath(el);
        if (path == null) return;

        GameObject container = new GameObject(EdgeLights.ContainerName);
        Undo.RegisterCreatedObjectUndo(container, "Generer EdgeLights");
        container.transform.SetParent(el.transform, false);

        float[] positions = Positions(el, path.Length);
        bool isAsset = PrefabUtility.IsPartOfPrefabAsset(el.prefab);
        Vector3 prefabScale = el.prefab.transform.localScale;
        Vector3 parentScale = el.transform.lossyScale;
        // Compense l'echelle du parent : les instances gardent la taille du prefab.
        Vector3 scale = new Vector3(
            prefabScale.x / Mathf.Max(1e-4f, Mathf.Abs(parentScale.x)),
            prefabScale.y / Mathf.Max(1e-4f, Mathf.Abs(parentScale.y)),
            prefabScale.z / Mathf.Max(1e-4f, Mathf.Abs(parentScale.z)));

        for (int side = -1; side <= 1; side += 2)
        {
            if (side < 0 && el.sides == EdgeLights.Sides.Droite) continue;
            if (side > 0 && el.sides == EdgeLights.Sides.Gauche) continue;
            string sideName = side < 0 ? "L" : "R";

            for (int i = 0; i < positions.Length; i++)
            {
                path.Point(positions[i], side, el.outward, el.height, out Vector3 pos, out Vector3 forward);

                GameObject inst = isAsset
                    ? (GameObject)PrefabUtility.InstantiatePrefab(el.prefab, container.transform)
                    : Instantiate(el.prefab, container.transform);
                inst.name = el.prefab.name + "_" + sideName + i.ToString("D2");
                if (!isAsset) inst.SetActive(true); // modele de la scene eventuellement desactive
                inst.transform.localPosition = pos;
                if (el.alignToPath)
                    inst.transform.rotation = el.transform.rotation * Quaternion.LookRotation(forward, Vector3.up);
                else
                    inst.transform.rotation = el.prefab.transform.rotation;
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
