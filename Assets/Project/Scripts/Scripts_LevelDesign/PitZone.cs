using System.Collections.Generic;
using UnityEngine;
using Unity.AI.Navigation;

public class PitZone : MonoBehaviour
{
    [Header("Zone Identity")]
    public int zoneID = -1;

    [Header("Grid Data")]
    public PitGridData gridData;

    [Header("NavMesh Settings")]
    public float navMeshMarginWidth = 0.35f;

    [Header("Mesh Settings")]
    public float floorThickness = 0.2f;

    [Header("Mesh References")]
    public MeshFilter wallsMeshFilter;
    public MeshRenderer wallsRenderer;
    public MeshFilter floorMeshFilter;
    public MeshRenderer floorRenderer;

    [Header("Materials")]
    public Material wallMaterial;
    public Material floorMaterial;

   

    public void Initialize(int id, PitGridData data)
    {
        zoneID = id;
        gridData = data;
        gameObject.name = "PitZone_" + id;
    }

    private Dictionary<Vector2Int, float> GetOwnedCells()
    {
        if (gridData == null || zoneID == -1)
        {
            return new Dictionary<Vector2Int, float>();
        }

        Dictionary<Vector2Int, float> cells = gridData.GetCellsForZone(zoneID);

        if (cells.Count == 0 && gridData.GetAllCells().Count > 0)
        {
            gridData.OnEnable();
            cells = gridData.GetCellsForZone(zoneID);
        }

        return cells;
    }

    public void GenerateMeshes()
    {
        if (gridData == null)
        {
            Debug.LogWarning("PitZone: No grid data assigned!");
            return;
        }
        if (zoneID == -1)
        {
            Debug.LogWarning("PitZone: No zone ID assigned!");
            return;
        }
        SetupMeshObjects();

        if (wallsMeshFilter != null)
        {
            int pitWallLayer = LayerMask.NameToLayer("PitWall");
            Debug.Log("[PitZone] Attempting to set PitWall layer. Layer ID: " + pitWallLayer);
            wallsMeshFilter.gameObject.layer = pitWallLayer;
            Debug.Log("[PitZone] Walls GameObject layer is now: " + wallsMeshFilter.gameObject.layer + " (name: " + LayerMask.LayerToName(wallsMeshFilter.gameObject.layer) + ")");
        }

        Dictionary<Vector2Int, float> ownedCells = GetOwnedCells();
        if (ownedCells.Count == 0)
        {
            Debug.LogWarning("PitZone " + zoneID + ": No cells owned, cannot generate meshes!");
            return;
        }
        Mesh wallsMesh = PitMeshGenerator.GenerateWallsMesh(gridData, floorThickness, ownedCells);
        Mesh floorMesh = PitMeshGenerator.GenerateFloorMesh(gridData, ownedCells);
        if (wallsMesh != null && wallsMeshFilter != null)
        {
            wallsMeshFilter.sharedMesh = wallsMesh;
        }
        if (floorMesh != null && floorMeshFilter != null)
        {
            floorMeshFilter.sharedMesh = floorMesh;
        }
        AddMeshColliders();
        Debug.Log("PitZone " + zoneID + ": Meshes generated successfully with " + ownedCells.Count + " cells!");
        SetupFloorCollisionDetector();
    }
    private void SetupFloorCollisionDetector()
    {
        if (floorMeshFilter == null) return;

        Transform floorTransform = floorMeshFilter.transform;
        PitFloorCollisionDetector detector = floorTransform.GetComponent<PitFloorCollisionDetector>();

        if (detector == null)
        {
            detector = floorTransform.gameObject.AddComponent<PitFloorCollisionDetector>();
        }

        // Trouve le PitFillDamageController s'il existe
        PitFill pitFill = GetComponent<PitFill>();
        PitFillDamageController damageController = pitFill != null ? pitFill.GetComponent<PitFillDamageController>() : null;

        detector.Initialize(this, damageController);

        Debug.Log("PitZone: Floor collision detector setup");
    }
    private void AddMeshColliders()
    {
        if (wallsMeshFilter != null && wallsMeshFilter.sharedMesh != null)
        {
            Transform wallsTransform = wallsMeshFilter.transform;
            MeshCollider wallsCollider = wallsTransform.GetComponent<MeshCollider>();

            if (wallsCollider == null)
            {
                wallsCollider = wallsTransform.gameObject.AddComponent<MeshCollider>();
            }

            wallsCollider.sharedMesh = wallsMeshFilter.sharedMesh;
            wallsCollider.convex = false;
        }

        if (floorMeshFilter != null && floorMeshFilter.sharedMesh != null)
        {
            Transform floorTransform = floorMeshFilter.transform;
            MeshCollider floorCollider = floorTransform.GetComponent<MeshCollider>();

            if (floorCollider == null)
            {
                floorCollider = floorTransform.gameObject.AddComponent<MeshCollider>();
            }

            floorCollider.sharedMesh = floorMeshFilter.sharedMesh;
            floorCollider.convex = false;
        }

        Debug.Log("PitZone: MeshColliders added to Walls and Floor");
    }

    private void SetupMeshObjects()
    {
        Transform wallsTransform = transform.Find("Walls");
        if (wallsTransform == null)
        {
            GameObject wallsObj = new GameObject("Walls");
            wallsObj.transform.SetParent(transform);
            wallsObj.transform.localPosition = Vector3.zero;
            wallsObj.transform.localRotation = Quaternion.identity;

            // NOUVEAU : Assigner automatiquement le layer PitWall
            wallsObj.layer = LayerMask.NameToLayer("PitWall");

            wallsTransform = wallsObj.transform;

            wallsMeshFilter = wallsObj.AddComponent<MeshFilter>();
            wallsRenderer = wallsObj.AddComponent<MeshRenderer>();
        }
        else
        {
            // NOUVEAU : S'assurer que le layer est correct même si l'objet existe déjà
            wallsTransform.gameObject.layer = LayerMask.NameToLayer("PitWall");

            if (wallsMeshFilter == null)
                wallsMeshFilter = wallsTransform.GetComponent<MeshFilter>();
            if (wallsRenderer == null)
                wallsRenderer = wallsTransform.GetComponent<MeshRenderer>();
        }

        Transform floorTransform = transform.Find("Floor");
        if (floorTransform == null)
        {
            GameObject floorObj = new GameObject("Floor");
            floorObj.transform.SetParent(transform);
            floorObj.transform.localPosition = Vector3.zero;
            floorObj.transform.localRotation = Quaternion.identity;
            floorObj.layer = LayerMask.NameToLayer("Ground");
            floorTransform = floorObj.transform;

            floorMeshFilter = floorObj.AddComponent<MeshFilter>();
            floorRenderer = floorObj.AddComponent<MeshRenderer>();
        }
        else
        {
            if (floorMeshFilter == null)
                floorMeshFilter = floorTransform.GetComponent<MeshFilter>();
            if (floorRenderer == null)
                floorRenderer = floorTransform.GetComponent<MeshRenderer>();
        }

        if (wallMaterial != null && wallsRenderer != null)
        {
            wallsRenderer.sharedMaterial = wallMaterial;
        }

        if (floorMaterial != null && floorRenderer != null)
        {
            floorRenderer.sharedMaterial = floorMaterial;
        }
    }

    public void ClearMeshes()
    {
        if (wallsMeshFilter != null)
        {
            wallsMeshFilter.sharedMesh = null;
        }

        if (floorMeshFilter != null)
        {
            floorMeshFilter.sharedMesh = null;
        }

        Debug.Log("PitZone: Meshes cleared");
    }

    

       

    public float GetMaxDepth()
    {
        if (gridData == null) return 0f;

        float maxDepth = 0f;
        Dictionary<Vector2Int, float> ownedCells = GetOwnedCells();

        foreach (var kvp in ownedCells)
        {
            if (kvp.Value < maxDepth)
            {
                maxDepth = kvp.Value;
            }
        }

        return maxDepth;
    }

    

    

    

    public void CreateNavMeshMargin()
    {
        CreateNavMeshMargin(navMeshMarginWidth);
    }

    public void CreateNavMeshMargin(float marginWidth)
    {
        if (gridData == null)
        {
            Debug.LogError("PitZone: No grid data assigned!");
            return;
        }

        Transform existingMargin = transform.Find("NavMeshMargin");
        if (existingMargin != null)
        {
            DestroyImmediate(existingMargin.gameObject);
        }

        GameObject marginParent = new GameObject("NavMeshMargin");
        marginParent.transform.SetParent(transform);
        marginParent.transform.localPosition = Vector3.zero;

        Dictionary<Vector2Int, float> ownedCells = GetOwnedCells();

        if (ownedCells.Count == 0)
        {
            Debug.LogWarning("PitZone: No owned cells, cannot create NavMesh margin");
            return;
        }

        float cellSize = gridData.gridCellSize;
        float thickness = 0.1f;

        foreach (var kvp in ownedCells)
        {
            Vector2Int cellPos = kvp.Key;
            Vector3 cellWorldPos = gridData.CellToWorld(cellPos);

            if (!ownedCells.ContainsKey(cellPos + new Vector2Int(0, 1)))
            {
                CreateMarginSegment(marginParent, cellWorldPos, cellSize, thickness, marginWidth, "North");
            }

            if (!ownedCells.ContainsKey(cellPos + new Vector2Int(0, -1)))
            {
                CreateMarginSegment(marginParent, cellWorldPos, cellSize, thickness, marginWidth, "South");
            }

            if (!ownedCells.ContainsKey(cellPos + new Vector2Int(1, 0)))
            {
                CreateMarginSegment(marginParent, cellWorldPos, cellSize, thickness, marginWidth, "East");
            }

            if (!ownedCells.ContainsKey(cellPos + new Vector2Int(-1, 0)))
            {
                CreateMarginSegment(marginParent, cellWorldPos, cellSize, thickness, marginWidth, "West");
            }
        }

        Debug.Log("NavMeshMargin created: " + marginParent.transform.childCount + " segments following pit shape");
    }

    private void CreateMarginSegment(GameObject parent, Vector3 cellWorldPos, float cellSize,
                                  float thickness, float marginWidth, string direction)
    {
        GameObject segment = GameObject.CreatePrimitive(PrimitiveType.Cube);
        segment.name = "Margin_" + direction;
        segment.transform.SetParent(parent.transform);
        segment.layer = LayerMask.NameToLayer("Ground");

        Vector3 position = Vector3.zero;
        Vector3 scale = Vector3.zero;

        switch (direction)
        {
            case "North":
                position = cellWorldPos + new Vector3(cellSize * 0.5f, 0f, cellSize - marginWidth * 0.5f);
                scale = new Vector3(cellSize, thickness, marginWidth);
                break;

            case "South":
                position = cellWorldPos + new Vector3(cellSize * 0.5f, 0f, marginWidth * 0.5f);
                scale = new Vector3(cellSize, thickness, marginWidth);
                break;

            case "East":
                position = cellWorldPos + new Vector3(cellSize - marginWidth * 0.5f, 0f, cellSize * 0.5f);
                scale = new Vector3(marginWidth, thickness, cellSize);
                break;

            case "West":
                position = cellWorldPos + new Vector3(marginWidth * 0.5f, 0f, cellSize * 0.5f);
                scale = new Vector3(marginWidth, thickness, cellSize);
                break;
        }

        segment.transform.position = position;
        segment.transform.localScale = scale;

        Collider col = segment.GetComponent<Collider>();
        if (col != null)
        {
            DestroyImmediate(col);
        }
    }

}