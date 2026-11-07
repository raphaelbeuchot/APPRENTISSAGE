using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(SeatingStand))]
public class SeatingStandEditor : Editor
{
    public override void OnInspectorGUI()
    {
        SeatingStand ss = (SeatingStand)target;
        if (DrawDefaultInspector()) SceneView.RepaintAll(); // gizmos a jour

        GUILayout.Space(10);
        GUILayout.BeginVertical(EditorStyles.helpBox);

        if (ss.prefab == null)
            EditorGUILayout.HelpBox("Choisir le prefab du fauteuil.", MessageType.Warning);
        else
            GUILayout.Label(ss.rows * ss.seatsPerRow + " fauteuils", EditorStyles.miniLabel);

        GUI.enabled = ss.prefab != null;
        GUI.backgroundColor = Color.green;
        if (GUILayout.Button("Generer (remplace les existants)", GUILayout.Height(30)))
            Generate(ss);
        GUI.enabled = true;

        GUI.backgroundColor = Color.red;
        if (GUILayout.Button("Effacer", GUILayout.Height(22)))
            Clear(ss);
        GUI.backgroundColor = Color.white;

        GUILayout.EndVertical();
    }

    static void Generate(SeatingStand ss)
    {
        Undo.SetCurrentGroupName("Generer SeatingStand");
        int group = Undo.GetCurrentGroup();

        Clear(ss);
        GameObject container = new GameObject(SeatingStand.ContainerName);
        Undo.RegisterCreatedObjectUndo(container, "Generer SeatingStand");
        container.transform.SetParent(ss.transform, false);
        Undo.RecordObject(ss, "Generer SeatingStand");
        ss.generated = container.transform;

        bool isAsset = PrefabUtility.IsPartOfPrefabAsset(ss.prefab);
        Quaternion rot = Quaternion.Euler(0f, ss.yawOffset, 0f);
        Vector3 scale = ss.prefab.transform.localScale;
        int perRow = Mathf.Max(1, ss.seatsPerRow);
        int i = 0;
        foreach (Vector3 p in ss.SeatPositions())
        {
            GameObject inst = isAsset
                ? (GameObject)PrefabUtility.InstantiatePrefab(ss.prefab, container.transform)
                : Instantiate(ss.prefab, container.transform);
            inst.name = ss.prefab.name + "_R" + (i / perRow + 1) + "_" + (i % perRow + 1);
            if (!isAsset) inst.SetActive(true);
            inst.transform.localPosition = p;
            inst.transform.localRotation = rot;
            inst.transform.localScale = scale;
            Undo.RegisterCreatedObjectUndo(inst, "Generer SeatingStand");
            i++;
        }

        Undo.CollapseUndoOperations(group);
    }

    static void Clear(SeatingStand ss)
    {
        Transform existing = ss.generated != null && ss.generated.parent == ss.transform
            ? ss.generated
            : ss.transform.Find(SeatingStand.ContainerName);
        if (existing != null) Undo.DestroyObjectImmediate(existing.gameObject);
    }
}
