using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

[InitializeOnLoad]
public class ModulePlacementTool
{
    // Mode de placement
    public enum PlacementMode
    {
        Single,
        Line,
        Area
    }

    // État du tool
    private static bool isActive = false;
    private static GameObject selectedPrefab = null;
    private static GameObject previewInstance = null;
    private static PlacementMode currentMode = PlacementMode.Single;

    // Placement en cours
    private static bool isPlacing = false;
    private static Vector3 startPosition;
    private static List<GameObject> dragPreviews = new List<GameObject>();

    // Rotation
    private static int currentRotation = 0; // 0, 90, 180, 270

    // Validation
    private static bool isValidPlacement = true;

    private const string TOOL_ACTIVE_KEY = "ModulePlacement_Active";

    static ModulePlacementTool()
    {
        SceneView.duringSceneGui += OnSceneGUI;
    }

    public static void ActivateTool(GameObject prefab, PlacementMode mode = PlacementMode.Single)
    {
        if (prefab == null) return;

        selectedPrefab = prefab;
        currentMode = mode;
        isActive = true;
        currentRotation = 0;

        CreatePreview();

        Debug.Log("Placement Tool activated: " + prefab.name + " (Mode: " + mode + ")");
    }

    public static void DeactivateTool()
    {
        isActive = false;
        DestroyPreview();
        ClearDragPreviews();
        selectedPrefab = null;

        Debug.Log("Placement Tool deactivated");
    }

    public static bool IsActive()
    {
        return isActive;
    }

    public static void SetMode(PlacementMode mode)
    {
        currentMode = mode;
        Debug.Log("Placement Mode: " + mode);
    }

    private static void OnSceneGUI(SceneView sceneView)
    {
        if (!isActive || selectedPrefab == null)
        {
            return;
        }

        Event e = Event.current;

        HandleKeyboardInput(e);
        HandleMouseInput(e, sceneView);

        UpdatePreviewPosition(sceneView);
        DrawPlacementOverlay(sceneView);

        // Bloque la selection pendant le placement
        if (isActive)
        {
            HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
        }

        sceneView.Repaint();
    }

    private static void HandleKeyboardInput(Event e)
    {
        if (e.type == EventType.KeyDown)
        {
            // Echap = Annuler
            if (e.keyCode == KeyCode.Escape)
            {
                DeactivateTool();
                e.Use();
            }
            // 1/2/3 = Changer de mode
            else if (e.keyCode == KeyCode.Alpha1)
            {
                SetMode(PlacementMode.Single);
                e.Use();
            }
            else if (e.keyCode == KeyCode.Alpha2)
            {
                SetMode(PlacementMode.Line);
                e.Use();
            }
            else if (e.keyCode == KeyCode.Alpha3)
            {
                SetMode(PlacementMode.Area);
                e.Use();
            }
        }

        // Molette = Rotation
        if (e.type == EventType.ScrollWheel)
        {
            if (e.delta.y > 0)
            {
                currentRotation = (currentRotation + 90) % 360;
            }
            else if (e.delta.y < 0)
            {
                currentRotation = (currentRotation - 90 + 360) % 360;
            }

            UpdatePreviewRotation();
            e.Use();
        }
    }

    private static void HandleMouseInput(Event e, SceneView sceneView)
    {
        // Debut du placement
        if (e.type == EventType.MouseDown && e.button == 0 && !e.alt)
        {
            isPlacing = true;
            startPosition = GetSnappedMousePosition(sceneView);

            if (currentMode == PlacementMode.Single)
            {
                // Mode single = place directement
                PlaceModule(startPosition);
                e.Use();
            }
            else
            {
                // Mode line/area = commence le drag
                e.Use();
            }
        }
        // Drag en cours
        else if (e.type == EventType.MouseDrag && isPlacing)
        {
            if (currentMode != PlacementMode.Single)
            {
                UpdateDragPreview(sceneView);
            }
            e.Use();
        }
        // Fin du placement
        else if (e.type == EventType.MouseUp && e.button == 0 && isPlacing)
        {
            if (currentMode != PlacementMode.Single)
            {
                ValidateDragPlacement();
            }

            isPlacing = false;
            e.Use();
        }
    }

    private static void UpdatePreviewPosition(SceneView sceneView)
    {
        if (previewInstance == null) return;

        Vector3 mousePos = GetSnappedMousePosition(sceneView);
        previewInstance.transform.position = mousePos;

        // Check collision
        ModulePiece modulePiece = previewInstance.GetComponent<ModulePiece>();
        if (modulePiece != null)
        {
            isValidPlacement = !modulePiece.CheckForCollision();
            UpdatePreviewMaterial(isValidPlacement);
        }
    }

    private static Vector3 GetSnappedMousePosition(SceneView sceneView)
    {
        Ray ray = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);
        Plane groundPlane = new Plane(Vector3.up, Vector3.zero);
        float distance;

        if (groundPlane.Raycast(ray, out distance))
        {
            Vector3 hitPoint = ray.GetPoint(distance);
            return SnapToGrid(hitPoint);
        }

        return Vector3.zero;
    }

    private static Vector3 SnapToGrid(Vector3 position)
    {
        if (selectedPrefab == null) return position;

        ModulePiece modulePiece = selectedPrefab.GetComponent<ModulePiece>();
        float snapSize = modulePiece != null ? modulePiece.snapSize : 1f;

        // Snap au COIN de la grille
        return new Vector3(
            Mathf.Round(position.x / snapSize) * snapSize,
            Mathf.Round(position.y / snapSize) * snapSize,
            Mathf.Round(position.z / snapSize) * snapSize
        );
    }

    private static void CreatePreview()
    {
        if (selectedPrefab == null) return;

        DestroyPreview();

        previewInstance = Object.Instantiate(selectedPrefab);
        previewInstance.name = "[PREVIEW] " + selectedPrefab.name;
        previewInstance.hideFlags = HideFlags.HideAndDontSave;

        UpdatePreviewRotation();
        MakePreviewTransparent(previewInstance);
    }

    private static void DestroyPreview()
    {
        if (previewInstance != null)
        {
            Object.DestroyImmediate(previewInstance);
            previewInstance = null;
        }
    }

    private static void UpdatePreviewRotation()
    {
        if (previewInstance != null)
        {
            previewInstance.transform.rotation = Quaternion.Euler(0, currentRotation, 0);
        }
    }

    private static void MakePreviewTransparent(GameObject obj)
    {
        MeshRenderer[] renderers = obj.GetComponentsInChildren<MeshRenderer>();
        foreach (MeshRenderer renderer in renderers)
        {
            Material[] mats = renderer.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
            {
                if (mats[i] != null)
                {
                    Material previewMat = new Material(mats[i]);
                    previewMat.SetFloat("_Mode", 3); // Transparent
                    previewMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    previewMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    previewMat.SetInt("_ZWrite", 0);
                    previewMat.DisableKeyword("_ALPHATEST_ON");
                    previewMat.EnableKeyword("_ALPHABLEND_ON");
                    previewMat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                    previewMat.renderQueue = 3000;

                    Color color = previewMat.color;
                    color.a = 0.5f;
                    previewMat.color = color;

                    mats[i] = previewMat;
                }
            }
            renderer.sharedMaterials = mats;
        }

        // Desactive les colliders
        Collider[] colliders = obj.GetComponentsInChildren<Collider>();
        foreach (Collider col in colliders)
        {
            col.enabled = false;
        }
    }

    private static void UpdatePreviewMaterial(bool isValid)
    {
        if (previewInstance == null) return;

        Color tintColor = isValid ? Color.green : Color.red;

        MeshRenderer[] renderers = previewInstance.GetComponentsInChildren<MeshRenderer>();
        foreach (MeshRenderer renderer in renderers)
        {
            Material[] mats = renderer.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
            {
                if (mats[i] != null)
                {
                    mats[i].color = new Color(tintColor.r, tintColor.g, tintColor.b, 0.5f);
                }
            }
        }
    }

    private static void UpdateDragPreview(SceneView sceneView)
    {
        ClearDragPreviews();

        Vector3 currentPos = GetSnappedMousePosition(sceneView);
        List<Vector3> positions = CalculateDragPositions(startPosition, currentPos);

        foreach (Vector3 pos in positions)
        {
            GameObject preview = Object.Instantiate(selectedPrefab);
            preview.name = "[DRAG_PREVIEW]";
            preview.hideFlags = HideFlags.HideAndDontSave;
            preview.transform.position = pos;
            preview.transform.rotation = Quaternion.Euler(0, currentRotation, 0);

            MakePreviewTransparent(preview);
            dragPreviews.Add(preview);
        }
    }

    private static List<Vector3> CalculateDragPositions(Vector3 start, Vector3 end)
    {
        List<Vector3> positions = new List<Vector3>();

        if (selectedPrefab == null) return positions;

        ModulePiece modulePiece = selectedPrefab.GetComponent<ModulePiece>();
        float snapSize = modulePiece != null ? modulePiece.snapSize : 1f;

        if (currentMode == PlacementMode.Line)
        {
            // Ligne droite (axe dominant)
            float deltaX = Mathf.Abs(end.x - start.x);
            float deltaZ = Mathf.Abs(end.z - start.z);

            if (deltaX > deltaZ)
            {
                // Ligne horizontale
                int steps = Mathf.RoundToInt(deltaX / snapSize);
                float direction = Mathf.Sign(end.x - start.x);

                for (int i = 0; i <= steps; i++)
                {
                    positions.Add(new Vector3(start.x + i * snapSize * direction, start.y, start.z));
                }
            }
            else
            {
                // Ligne verticale
                int steps = Mathf.RoundToInt(deltaZ / snapSize);
                float direction = Mathf.Sign(end.z - start.z);

                for (int i = 0; i <= steps; i++)
                {
                    positions.Add(new Vector3(start.x, start.y, start.z + i * snapSize * direction));
                }
            }
        }
        else if (currentMode == PlacementMode.Area)
        {
            // Rectangle
            int stepsX = Mathf.RoundToInt(Mathf.Abs(end.x - start.x) / snapSize);
            int stepsZ = Mathf.RoundToInt(Mathf.Abs(end.z - start.z) / snapSize);

            float dirX = Mathf.Sign(end.x - start.x);
            float dirZ = Mathf.Sign(end.z - start.z);

            for (int x = 0; x <= stepsX; x++)
            {
                for (int z = 0; z <= stepsZ; z++)
                {
                    positions.Add(new Vector3(
                        start.x + x * snapSize * dirX,
                        start.y,
                        start.z + z * snapSize * dirZ
                    ));
                }
            }
        }

        return positions;
    }

    private static void ClearDragPreviews()
    {
        foreach (GameObject preview in dragPreviews)
        {
            if (preview != null)
            {
                Object.DestroyImmediate(preview);
            }
        }
        dragPreviews.Clear();
    }

    private static void PlaceModule(Vector3 position)
    {
        if (selectedPrefab == null) return;

        GameObject parent = GetOrCreateParent();
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(selectedPrefab, parent.transform);
        instance.transform.position = position;
        instance.transform.rotation = Quaternion.Euler(0, currentRotation, 0);

        AssignLayerIfFloor(instance);

        Undo.RegisterCreatedObjectUndo(instance, "Place Module");
    }

    private static void ValidateDragPlacement()
    {
        Vector3 currentPos = GetSnappedMousePosition(SceneView.lastActiveSceneView);
        List<Vector3> positions = CalculateDragPositions(startPosition, currentPos);

        foreach (Vector3 pos in positions)
        {
            PlaceModule(pos);
        }

        ClearDragPreviews();
    }

    private static GameObject GetOrCreateParent()
    {
        GameObject parent = GameObject.Find("LevelModules");

        if (parent == null)
        {
            parent = new GameObject("LevelModules");
            Undo.RegisterCreatedObjectUndo(parent, "Create LevelModules");
        }

        if (selectedPrefab != null)
        {
            ModulePiece modulePiece = selectedPrefab.GetComponent<ModulePiece>();
            if (modulePiece != null && !string.IsNullOrEmpty(modulePiece.category))
            {
                Transform categoryParent = parent.transform.Find(modulePiece.category);
                if (categoryParent == null)
                {
                    GameObject categoryObj = new GameObject(modulePiece.category);
                    categoryObj.transform.SetParent(parent.transform);
                    Undo.RegisterCreatedObjectUndo(categoryObj, "Create Category Folder");
                    return categoryObj;
                }
                return categoryParent.gameObject;
            }
        }

        return parent;
    }

    private static void AssignLayerIfFloor(GameObject obj)
    {
        if (selectedPrefab == null) return;

        ModulePiece modulePiece = selectedPrefab.GetComponent<ModulePiece>();
        if (modulePiece != null && modulePiece.category == "Floors")
        {
            int groundLayer = LayerMask.NameToLayer("Ground");
            if (groundLayer != -1)
            {
                obj.layer = groundLayer;

                // Assigne aussi aux enfants
                foreach (Transform child in obj.GetComponentsInChildren<Transform>(true))
                {
                    child.gameObject.layer = groundLayer;
                }
            }
        }
    }

    private static void DrawPlacementOverlay(SceneView sceneView)
    {
        Handles.BeginGUI();

        GUILayout.BeginArea(new Rect(10, 10, 350, 180));

        GUIStyle boxStyle = new GUIStyle(GUI.skin.box);
        boxStyle.normal.background = MakeTex(2, 2, new Color(0, 0, 0, 0.8f));

        GUILayout.BeginVertical(boxStyle);

        GUIStyle labelStyle = new GUIStyle(GUI.skin.label);
        labelStyle.normal.textColor = Color.white;
        labelStyle.fontStyle = FontStyle.Bold;

        GUILayout.Label("MODULE PLACEMENT TOOL", labelStyle);

        if (selectedPrefab != null)
        {
            labelStyle.normal.textColor = Color.cyan;
            GUILayout.Label("Module: " + selectedPrefab.name, labelStyle);
        }

        labelStyle.normal.textColor = Color.yellow;
        GUILayout.Label("Mode: " + currentMode.ToString(), labelStyle);

        labelStyle.normal.textColor = Color.white;
        GUILayout.Label("Rotation: " + currentRotation + " deg", labelStyle);

        labelStyle.normal.textColor = isValidPlacement ? Color.green : Color.red;
        GUILayout.Label("Status: " + (isValidPlacement ? "Valid" : "COLLISION!"), labelStyle);

        GUILayout.Space(5);
        labelStyle.normal.textColor = Color.gray;
        GUILayout.Label("[1] Single [2] Line [3] Area", labelStyle);
        GUILayout.Label("[Mouse Wheel] Rotate", labelStyle);
        GUILayout.Label("[ESC] Cancel", labelStyle);

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