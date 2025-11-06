using UnityEngine;
using UnityEditor;

[InitializeOnLoad]
public class ModuleSnapSystem
{
    private static bool positionSnapEnabled = true;
    private static bool rotationSnapEnabled = true;

    private const string POSITION_SNAP_KEY = "ModuleSnap_PositionEnabled";
    private const string ROTATION_SNAP_KEY = "ModuleSnap_RotationEnabled";

    static ModuleSnapSystem()
    {
        positionSnapEnabled = EditorPrefs.GetBool(POSITION_SNAP_KEY, true);
        rotationSnapEnabled = EditorPrefs.GetBool(ROTATION_SNAP_KEY, true);

        SceneView.duringSceneGui += OnSceneGUI;
    }

    private static void OnSceneGUI(SceneView sceneView)
    {
        Event e = Event.current;

        if (e.type == EventType.KeyDown)
        {
            if (e.keyCode == KeyCode.T)
            {
                positionSnapEnabled = !positionSnapEnabled;
                EditorPrefs.SetBool(POSITION_SNAP_KEY, positionSnapEnabled);
                Debug.Log("Position Snap: " + (positionSnapEnabled ? "ON" : "OFF"));
                e.Use();
                sceneView.Repaint();
            }
            else if (e.keyCode == KeyCode.R)
            {
                rotationSnapEnabled = !rotationSnapEnabled;
                EditorPrefs.SetBool(ROTATION_SNAP_KEY, rotationSnapEnabled);
                Debug.Log("Rotation Snap: " + (rotationSnapEnabled ? "ON" : "OFF"));
                e.Use();
                sceneView.Repaint();
            }
        }

        if (Selection.activeGameObject != null)
        {
            ModulePiece module = Selection.activeGameObject.GetComponent<ModulePiece>();
            if (module != null)
            {
                Transform t = module.transform;
                bool changed = false;

                Vector3 newPosition = t.position;
                Quaternion newRotation = t.rotation;

                if (positionSnapEnabled)
                {
                    float snapSize = module.snapSize;
                    newPosition = SnapPosition(t.position, snapSize);
                    if (newPosition != t.position)
                    {
                        changed = true;
                    }
                }

                if (rotationSnapEnabled)
                {
                    newRotation = SnapRotation(t.rotation);
                    if (newRotation.eulerAngles != t.rotation.eulerAngles)
                    {
                        changed = true;
                    }
                }

                if (changed)
                {
                    Undo.RecordObject(t, "Snap Module");
                    t.position = newPosition;
                    t.rotation = newRotation;
                }

                bool hasCollision = module.CheckForCollision();
                DrawSnapOverlay(sceneView, module, hasCollision);
            }
        }
    }

    private static Vector3 SnapPosition(Vector3 position, float snapSize)
    {
        return new Vector3(
            Mathf.Round(position.x / snapSize) * snapSize,
            Mathf.Round(position.y / snapSize) * snapSize,
            Mathf.Round(position.z / snapSize) * snapSize
        );
    }

    private static Quaternion SnapRotation(Quaternion rotation)
    {
        Vector3 euler = rotation.eulerAngles;
        euler.x = Mathf.Round(euler.x / 90f) * 90f;
        euler.y = Mathf.Round(euler.y / 90f) * 90f;
        euler.z = Mathf.Round(euler.z / 90f) * 90f;
        return Quaternion.Euler(euler);
    }

    private static void DrawSnapOverlay(SceneView sceneView, ModulePiece module, bool hasCollision)
    {
        Handles.BeginGUI();

        GUILayout.BeginArea(new Rect(10, 10, 300, 140));

        GUIStyle boxStyle = new GUIStyle(GUI.skin.box);
        boxStyle.normal.background = MakeTex(2, 2, new Color(0, 0, 0, 0.8f));

        GUILayout.BeginVertical(boxStyle);

        GUIStyle labelStyle = new GUIStyle(GUI.skin.label);
        labelStyle.normal.textColor = Color.white;
        labelStyle.fontStyle = FontStyle.Bold;

        GUILayout.Label("MODULE SNAP SYSTEM", labelStyle);

        labelStyle.normal.textColor = Color.cyan;
        GUILayout.Label("Category: " + module.category, labelStyle);

        Color posColor = positionSnapEnabled ? Color.green : Color.red;
        string posText = positionSnapEnabled ? "ON (" + module.snapSize + "m)" : "OFF (Free)";
        labelStyle.normal.textColor = posColor;
        GUILayout.Label("[T] Position: " + posText, labelStyle);

        Color rotColor = rotationSnapEnabled ? Color.green : Color.red;
        string rotText = rotationSnapEnabled ? "ON (90deg)" : "OFF (Free)";
        labelStyle.normal.textColor = rotColor;
        GUILayout.Label("[R] Rotation: " + rotText, labelStyle);

        if (hasCollision)
        {
            labelStyle.normal.textColor = Color.yellow;
            GUILayout.Label("WARNING: Module overlap detected!", labelStyle);
        }

        GUILayout.EndVertical();
        GUILayout.EndArea();

        Handles.EndGUI();
    }

    private static Texture2D MakeTex(int width, int height, Color col)
    {
        Color[] pix = new Color[width * height];
        for (int i = 0; i < pix.Length; i++)
            pix[i] = col;

        Texture2D result = new Texture2D(width, height);
        result.SetPixels(pix);
        result.Apply();
        return result;
    }
}