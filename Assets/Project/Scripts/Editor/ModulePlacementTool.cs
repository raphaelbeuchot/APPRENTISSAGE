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

    // Etat du tool
    private static bool isActive = false;

    private static List<GameObject> selectedPrefabs = new List<GameObject>();
    private static GameObject currentPrefab = null; // Prefab utilise pour le preview
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

    public static void ActivateTool(List<GameObject> prefabs, PlacementMode mode = PlacementMode.Single)
    {
        if (prefabs == null || prefabs.Count == 0) return;

        selectedPrefabs = new List<GameObject>(prefabs);
        currentPrefab = prefabs[0]; // Utilise le premier pour le preview
        currentMode = mode;
        isActive = true;
        currentRotation = 0;

        CreatePreview();

        Debug.Log("Placement Tool activated with " + prefabs.Count + " prefabs (Mode: " + mode + ")");
    }

    // GARDE AUSSI l'ancienne signature pour compatibilite
    public static void ActivateTool(GameObject prefab, PlacementMode mode = PlacementMode.Single)
    {
        List<GameObject> list = new List<GameObject>();
        list.Add(prefab);
        ActivateTool(list, mode);
    }

    private static GameObject GetRandomPrefab()
    {
        if (selectedPrefabs == null || selectedPrefabs.Count == 0)
            return null;

        int randomIndex = Random.Range(0, selectedPrefabs.Count);
        return selectedPrefabs[randomIndex];
    }

    private static int GetRandomRotation()
    {
        int[] rotations = { 0, 90, 180, 270 };
        int randomIndex = Random.Range(0, rotations.Length);
        return rotations[randomIndex];
    }

    public static void DeactivateTool()
    {
        isActive = false;
        DestroyPreview();
        ClearDragPreviews();
        selectedPrefabs.Clear();
        currentPrefab = null;

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
        if (!isActive || selectedPrefabs == null || selectedPrefabs.Count == 0)
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

        // Check collision avec Physics.OverlapBox
        isValidPlacement = CheckValidPlacement(mousePos);
        UpdatePreviewMaterial(isValidPlacement);
    }

    private static bool CheckValidPlacement(Vector3 position)
    {
        if (currentPrefab == null) return true;

        ModulePiece modulePiece = currentPrefab.GetComponent<ModulePiece>();
        if (modulePiece == null) return true;

        // Recupere les bounds du prefab
        Bounds prefabBounds = GetCombinedBounds(currentPrefab);

        if (prefabBounds.size == Vector3.zero)
        {
            // Pas de renderer trouve, fallback sur box simple
            Vector3 halfExtents = new Vector3(modulePiece.snapSize * 0.45f, 0.25f, modulePiece.snapSize * 0.45f);
            Collider[] colliders = Physics.OverlapBox(position, halfExtents, Quaternion.Euler(0, currentRotation, 0));

            foreach (Collider col in colliders)
            {
                ModulePiece other = col.GetComponentInParent<ModulePiece>();
                if (other != null && other.category == modulePiece.category)
                {
                    return false;
                }
            }
            return true;
        }

        // Applique la rotation aux bounds
        Bounds rotatedBounds = RotateBounds(prefabBounds, currentRotation);

        // Centre les bounds a la position du placement
        rotatedBounds.center = position + prefabBounds.center;

        // Trouve tous les ModulePiece dans la scene
        ModulePiece[] allModules = Object.FindObjectsOfType<ModulePiece>();

        foreach (ModulePiece other in allModules)
        {
            if (other.category != modulePiece.category) continue;

            // Recupere les bounds de l'autre module
            Bounds otherBounds = GetCombinedBounds(other.gameObject);
            otherBounds.center = other.transform.position + otherBounds.center;

            if (rotatedBounds.Intersects(otherBounds))
            {
                // NOUVEAU : Verifie si c'est vraiment un overlap ou juste un contact
                float overlapVolume = CalculateOverlapVolume(rotatedBounds, otherBounds);

                // Si l'overlap est significatif (plus de 10% du volume), c'est une collision
                float volumeThreshold = rotatedBounds.size.x * rotatedBounds.size.y * rotatedBounds.size.z * 0.1f;

                if (overlapVolume > volumeThreshold)
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static float CalculateOverlapVolume(Bounds a, Bounds b)
    {
        Vector3 min = Vector3.Max(a.min, b.min);
        Vector3 max = Vector3.Min(a.max, b.max);

        Vector3 size = max - min;

        if (size.x <= 0 || size.y <= 0 || size.z <= 0)
        {
            return 0f;
        }

        return size.x * size.y * size.z;
    }

    private static Bounds GetCombinedBounds(GameObject obj)
    {
        Renderer[] renderers = obj.GetComponentsInChildren<Renderer>();

        if (renderers.Length == 0)
        {
            return new Bounds(Vector3.zero, Vector3.zero);
        }

        Bounds combined = renderers[0].bounds;

        for (int i = 1; i < renderers.Length; i++)
        {
            combined.Encapsulate(renderers[i].bounds);
        }

        // Converti en local bounds
        combined.center -= obj.transform.position;

        return combined;
    }

    private static Bounds RotateBounds(Bounds bounds, float rotationY)
    {
        // Pour simplifier, on prend la bounding box apres rotation
        Vector3[] corners = new Vector3[8];
        Vector3 center = bounds.center;
        Vector3 extents = bounds.extents;

        corners[0] = center + new Vector3(-extents.x, -extents.y, -extents.z);
        corners[1] = center + new Vector3(extents.x, -extents.y, -extents.z);
        corners[2] = center + new Vector3(-extents.x, extents.y, -extents.z);
        corners[3] = center + new Vector3(extents.x, extents.y, -extents.z);
        corners[4] = center + new Vector3(-extents.x, -extents.y, extents.z);
        corners[5] = center + new Vector3(extents.x, -extents.y, extents.z);
        corners[6] = center + new Vector3(-extents.x, extents.y, extents.z);
        corners[7] = center + new Vector3(extents.x, extents.y, extents.z);

        Quaternion rotation = Quaternion.Euler(0, rotationY, 0);

        for (int i = 0; i < 8; i++)
        {
            corners[i] = rotation * (corners[i] - center) + center;
        }

        Bounds rotated = new Bounds(corners[0], Vector3.zero);
        for (int i = 1; i < 8; i++)
        {
            rotated.Encapsulate(corners[i]);
        }

        return rotated;
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
    if (currentPrefab == null) return position;

    ModulePiece modulePiece = currentPrefab.GetComponent<ModulePiece>();
    float snapSize = modulePiece != null ? modulePiece.snapSize : 1f;

    // Snap UNIQUEMENT X et Z, garde Y = 0
    return new Vector3(
        Mathf.Round(position.x / snapSize) * snapSize,
        0f,  // <-- FORCE Y = 0
        Mathf.Round(position.z / snapSize) * snapSize
    );
}

    private static void CreatePreview()
    {
        if (currentPrefab == null) return;

        DestroyPreview();

        previewInstance = Object.Instantiate(currentPrefab);
        previewInstance.name = "[PREVIEW] " + currentPrefab.name;
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
            GameObject preview = Object.Instantiate(currentPrefab);
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

        if (currentPrefab == null) return positions;

        ModulePiece modulePiece = currentPrefab.GetComponent<ModulePiece>();
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
        GameObject prefabToPlace = GetRandomPrefab();
        if (prefabToPlace == null) return;

        GameObject parent = GetOrCreateParent(prefabToPlace);
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefabToPlace, parent.transform);
        instance.transform.position = position;
        instance.transform.rotation = Quaternion.Euler(0, GetRandomRotation(), 0);

        AssignLayerIfFloor(instance, prefabToPlace);

        Undo.RegisterCreatedObjectUndo(instance, "Place Module");
    }

    private static void ValidateDragPlacement()
    {
        Vector3 currentPos = GetSnappedMousePosition(SceneView.lastActiveSceneView);
        List<Vector3> positions = CalculateDragPositions(startPosition, currentPos);

        foreach (Vector3 pos in positions)
        {
            // NOUVEAU : Vérifie qu'il n'y a pas déjà une dalle à cette position
            if (CheckValidPlacement(pos))
            {
                PlaceModule(pos);
            }
        }

        ClearDragPreviews();
    }

    private static GameObject GetOrCreateParent(GameObject prefab)
    {
        GameObject parent = GameObject.Find("LevelModules");

        if (parent == null)
        {
            parent = new GameObject("LevelModules");
            Undo.RegisterCreatedObjectUndo(parent, "Create LevelModules");
        }

        if (prefab != null)
        {
            ModulePiece modulePiece = prefab.GetComponent<ModulePiece>();
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

    private static void AssignLayerIfFloor(GameObject obj, GameObject prefab)
    {
        if (prefab == null) return;

        ModulePiece modulePiece = prefab.GetComponent<ModulePiece>();
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

        GUILayout.BeginArea(new Rect(10, 10, 350, 200));

        GUIStyle boxStyle = new GUIStyle(GUI.skin.box);
        boxStyle.normal.background = MakeTex(2, 2, new Color(0, 0, 0, 0.8f));

        GUILayout.BeginVertical(boxStyle);

        GUIStyle labelStyle = new GUIStyle(GUI.skin.label);
        labelStyle.normal.textColor = Color.white;
        labelStyle.fontStyle = FontStyle.Bold;

        GUILayout.Label("MODULE PLACEMENT TOOL", labelStyle);

        if (selectedPrefabs != null && selectedPrefabs.Count > 0)
        {
            labelStyle.normal.textColor = Color.cyan;
            GUILayout.Label("Modules: " + selectedPrefabs.Count + " selected", labelStyle);
            if (currentPrefab != null)
            {
                labelStyle.normal.textColor = Color.white;
                GUILayout.Label("Preview: " + currentPrefab.name, labelStyle);
            }
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