using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

public class PitFillEditor : EditorWindow
{
    private PitZone selectedPitZone;
    private List<PitZone> allPitZones = new List<PitZone>();
    private int selectedPitZoneIndex = 0;
    private string[] pitZoneNames;

    private PitContentType selectedFillType;
    private float fillHeightPercent = 0.5f;

    private Vector2 scrollPosition;

    [MenuItem("Tools/Pit Fill Editor")]
    public static void ShowWindow()
    {
        PitFillEditor window = GetWindow<PitFillEditor>("Pit Fill Editor");
        window.minSize = new Vector2(400, 300);
        window.Show();
    }

    private void OnEnable()
    {
        RefreshPitZonesList();
    }

    private void OnFocus()
    {
        RefreshPitZonesList();
    }

    private void RefreshPitZonesList()
    {
        allPitZones = FindObjectsOfType<PitZone>().ToList();

        if (allPitZones.Count > 0)
        {
            pitZoneNames = allPitZones.Select(pz => pz.name + " (ID: " + pz.zoneID + ")").ToArray();

            if (selectedPitZone != null && allPitZones.Contains(selectedPitZone))
            {
                selectedPitZoneIndex = allPitZones.IndexOf(selectedPitZone);
            }
            else
            {
                selectedPitZoneIndex = 0;
                selectedPitZone = allPitZones[0];
            }
        }
        else
        {
            pitZoneNames = new string[] { "No PitZones found" };
            selectedPitZone = null;
        }
    }

    private void OnGUI()
    {
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        GUILayout.Space(10);
        EditorGUILayout.LabelField("PIT FILL EDITOR", EditorStyles.boldLabel);
        GUILayout.Space(10);

        DrawPitZoneSelection();

        if (selectedPitZone != null)
        {
            GUILayout.Space(10);
            EditorGUILayout.LabelField("Fill Settings", EditorStyles.boldLabel);
            DrawFillSettings();

            GUILayout.Space(10);
            DrawButtons();

            GUILayout.Space(10);
            DrawInfo();
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawPitZoneSelection()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        EditorGUILayout.LabelField("Select PitZone", EditorStyles.boldLabel);

        if (allPitZones.Count == 0)
        {
            EditorGUILayout.HelpBox("No PitZones found in scene. Create one using Pit Brush first.", MessageType.Warning);

            if (GUILayout.Button("Refresh List"))
            {
                RefreshPitZonesList();
            }
        }
        else
        {
            EditorGUI.BeginChangeCheck();
            selectedPitZoneIndex = EditorGUILayout.Popup("PitZone", selectedPitZoneIndex, pitZoneNames);

            if (EditorGUI.EndChangeCheck())
            {
                selectedPitZone = allPitZones[selectedPitZoneIndex];
                LoadCurrentFillSettings();
                Repaint();
            }

            if (GUILayout.Button("Refresh List", GUILayout.Width(100)))
            {
                RefreshPitZonesList();
            }
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawFillSettings()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        selectedFillType = (PitContentType)EditorGUILayout.ObjectField("Fill Type", selectedFillType, typeof(PitContentType), false);

        GUILayout.Space(5);

        EditorGUI.BeginChangeCheck();
        fillHeightPercent = EditorGUILayout.Slider("Fill Height (%)", fillHeightPercent, 0f, 1f);

        if (selectedPitZone != null)
        {
            float maxDepth = selectedPitZone.GetMaxDepth();
            float fillMeters = Mathf.Abs(maxDepth * fillHeightPercent);
            EditorGUILayout.LabelField("Fill Height (meters)", fillMeters.ToString("F2") + "m", EditorStyles.helpBox);
        }

        if (EditorGUI.EndChangeCheck())
        {
            Repaint();
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawButtons()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        GUI.enabled = selectedFillType != null;

        if (GUILayout.Button("Generate Fill Content", GUILayout.Height(30)))
        {
            GenerateFillContent();
        }

        GUI.enabled = true;

        if (GUILayout.Button("Clear Fill Content", GUILayout.Height(30)))
        {
            ClearFillContent();
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawInfo()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Pit Info", EditorStyles.boldLabel);

        if (selectedPitZone != null)
        {
            float maxDepth = selectedPitZone.GetMaxDepth();
            EditorGUILayout.LabelField("Pit Depth", Mathf.Abs(maxDepth).ToString("F2") + "m");

            int cellCount = selectedPitZone.gridData != null ?
                selectedPitZone.gridData.GetCellsForZone(selectedPitZone.zoneID).Count : 0;
            EditorGUILayout.LabelField("Cell Count", cellCount.ToString());

            // CHANGEMENT: Cherche le PitFill dans les enfants
            Transform fillTransform = selectedPitZone.transform.Find("FillContent");
            PitFill pitFill = fillTransform != null ? fillTransform.GetComponent<PitFill>() : null;

            if (pitFill != null)
            {
                EditorGUILayout.LabelField("Fill Status", "Active", EditorStyles.boldLabel);
                if (pitFill.fillType != null)
                {
                    EditorGUILayout.LabelField("Current Fill Type", pitFill.fillType.contentName);
                }
            }
            else
            {
                EditorGUILayout.LabelField("Fill Status", "None", EditorStyles.boldLabel);
            }
        }

        EditorGUILayout.EndVertical();
    }

    private void LoadCurrentFillSettings()
    {
        if (selectedPitZone == null) return;

        // CHANGEMENT: Cherche le PitFill dans les enfants
        Transform fillTransform = selectedPitZone.transform.Find("FillContent");
        PitFill pitFill = fillTransform != null ? fillTransform.GetComponent<PitFill>() : null;

        if (pitFill != null)
        {
            selectedFillType = pitFill.fillType;
            fillHeightPercent = pitFill.fillHeightPercent;
        }
    }

    private void GenerateFillContent()
    {
        if (selectedPitZone == null)
        {
            Debug.LogError("No PitZone selected!");
            return;
        }

        if (selectedFillType == null)
        {
            Debug.LogError("No Fill Type selected!");
            return;
        }

        // CHANGEMENT: Crée ou trouve le GameObject FillContent
        Transform fillTransform = selectedPitZone.transform.Find("FillContent");
        GameObject fillObject;

        if (fillTransform == null)
        {
            fillObject = new GameObject("FillContent");
            fillObject.transform.SetParent(selectedPitZone.transform);
            fillObject.transform.localPosition = Vector3.zero;
            fillObject.transform.localRotation = Quaternion.identity;
            Undo.RegisterCreatedObjectUndo(fillObject, "Create Fill Content");
        }
        else
        {
            fillObject = fillTransform.gameObject;
        }

        // Ajoute ou récupère le component PitFill
        PitFill pitFill = fillObject.GetComponent<PitFill>();
        if (pitFill == null)
        {
            pitFill = fillObject.AddComponent<PitFill>();
            Undo.RegisterCreatedObjectUndo(pitFill, "Add PitFill Component");
        }

        Undo.RecordObject(pitFill, "Generate Fill Content");

        pitFill.pitZone = selectedPitZone;
        pitFill.fillType = selectedFillType;
        pitFill.fillHeightPercent = fillHeightPercent;

        pitFill.GenerateFillContent();

        EditorUtility.SetDirty(pitFill);
        EditorUtility.SetDirty(selectedPitZone);

        Debug.Log("Fill content generated for: " + selectedPitZone.name);
    }

    private void ClearFillContent()
    {
        if (selectedPitZone == null) return;

        // CHANGEMENT: Cherche le FillContent dans les enfants
        Transform fillTransform = selectedPitZone.transform.Find("FillContent");

        if (fillTransform != null)
        {
            Undo.DestroyObjectImmediate(fillTransform.gameObject);
            Debug.Log("Fill content cleared for: " + selectedPitZone.name);
        }
    }
}