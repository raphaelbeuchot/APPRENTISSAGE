using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(EdgeLights))]
public class EdgeLightsEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EdgeLights el = (EdgeLights)target;
        Ramp ramp = el.GetComponent<Ramp>();

        GUILayout.Space(10);
        GUILayout.BeginVertical(EditorStyles.helpBox);

        if (el.prefab == null)
            EditorGUILayout.HelpBox("Choisir un prefab.", MessageType.Warning);
        else if (ramp != null)
        {
            int perSide = Positions(el, ramp.WalkLength, out _).Length;
            int sidesCount = el.sides == EdgeLights.Sides.LesDeux ? 2 : 1;
            GUILayout.Label(perSide * sidesCount + " instances (" + perSide + " par bord)", EditorStyles.miniLabel);
        }

        GUI.enabled = el.prefab != null && ramp != null;
        GUI.backgroundColor = Color.green;
        if (GUILayout.Button("Generer (remplace les existantes)", GUILayout.Height(30)))
            Generate(el, ramp);
        GUI.enabled = true;

        GUI.backgroundColor = Color.red;
        if (GUILayout.Button("Effacer", GUILayout.Height(22)))
            Clear(el);
        GUI.backgroundColor = Color.white;

        GUILayout.EndVertical();
    }

    // Abscisses le long de la ligne mediane, de la marge de debut a la marge de fin, espacement ajuste.
    static float[] Positions(EdgeLights el, float length, out float actualSpacing)
    {
        float start = el.startMargin;
        float usable = length - el.startMargin - el.endMargin;
        actualSpacing = 0f;
        if (usable < 0f) return new float[0];
        if (usable < 1e-4f) return new[] { start };

        int intervals = Mathf.Max(1, Mathf.RoundToInt(usable / Mathf.Max(0.05f, el.spacing)));
        actualSpacing = usable / intervals;
        float[] s = new float[intervals + 1];
        for (int i = 0; i <= intervals; i++) s[i] = start + i * actualSpacing;
        return s;
    }

    static void Generate(EdgeLights el, Ramp ramp)
    {
        Undo.SetCurrentGroupName("Generer EdgeLights");
        int group = Undo.GetCurrentGroup();

        Clear(el);
        ramp.Rebuild(); // pivot et dimensions a jour

        GameObject container = new GameObject(EdgeLights.ContainerName);
        Undo.RegisterCreatedObjectUndo(container, "Generer EdgeLights");
        container.transform.SetParent(el.transform, false);

        float[] positions = Positions(el, ramp.WalkLength, out _);
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
                ramp.GetEdgePoint(positions[i], side, el.outward, el.height, out Vector3 pos, out Vector3 forward);

                GameObject inst = isAsset
                    ? (GameObject)PrefabUtility.InstantiatePrefab(el.prefab, container.transform)
                    : Instantiate(el.prefab, container.transform);
                inst.name = el.prefab.name + "_" + sideName + i.ToString("D2");
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

    static void Clear(EdgeLights el)
    {
        Transform existing = el.transform.Find(EdgeLights.ContainerName);
        if (existing != null) Undo.DestroyObjectImmediate(existing.gameObject);
    }
}
