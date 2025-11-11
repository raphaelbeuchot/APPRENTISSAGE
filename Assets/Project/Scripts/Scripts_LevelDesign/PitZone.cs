using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode]
public class PitZone : MonoBehaviour
{
    [Header("Grid Data")]
    public PitGridData gridData;

    [Header("Mesh References")]
    public MeshFilter wallsMeshFilter;
    public MeshRenderer wallsRenderer;
    public MeshFilter floorMeshFilter;
    public MeshRenderer floorRenderer;

    [Header("Materials")]
    public Material wallMaterial;
    public Material floorMaterial;

    [Header("Fill Content")]
    public PitContentType fillType;
    [Range(0f, 1f)]
    public float fillHeightPercent = 0.5f;
    public GameObject contentInstance;

    private void OnValidate()
    {
        if (wallsMeshFilter == null)
        {
            wallsMeshFilter = GetComponentInChildren<MeshFilter>();
        }
    }

    public void GenerateMeshes()
    {
        if (gridData == null)
        {
            Debug.LogWarning("PitZone: No grid data assigned!");
            return;
        }

        SetupMeshObjects();

        Mesh wallsMesh = PitMeshGenerator.GenerateWallsMesh(gridData);
        Mesh floorMesh = PitMeshGenerator.GenerateFloorMesh(gridData);

        if (wallsMesh != null && wallsMeshFilter != null)
        {
            wallsMeshFilter.sharedMesh = wallsMesh;
        }

        if (floorMesh != null && floorMeshFilter != null)
        {
            floorMeshFilter.sharedMesh = floorMesh;
        }

        Debug.Log("PitZone: Meshes generated successfully!");
    }

    private void SetupMeshObjects()
    {
        // Cree ou trouve le child object pour les walls
        Transform wallsTransform = transform.Find("Walls");
        if (wallsTransform == null)
        {
            GameObject wallsObj = new GameObject("Walls");
            wallsObj.transform.SetParent(transform);
            wallsObj.transform.localPosition = Vector3.zero;
            wallsObj.transform.localRotation = Quaternion.identity;
            wallsTransform = wallsObj.transform;

            wallsMeshFilter = wallsObj.AddComponent<MeshFilter>();
            wallsRenderer = wallsObj.AddComponent<MeshRenderer>();
        }
        else
        {
            if (wallsMeshFilter == null)
                wallsMeshFilter = wallsTransform.GetComponent<MeshFilter>();
            if (wallsRenderer == null)
                wallsRenderer = wallsTransform.GetComponent<MeshRenderer>();
        }

        // Cree ou trouve le child object pour les floors
        Transform floorTransform = transform.Find("Floor");
        if (floorTransform == null)
        {
            GameObject floorObj = new GameObject("Floor");
            floorObj.transform.SetParent(transform);
            floorObj.transform.localPosition = Vector3.zero;
            floorObj.transform.localRotation = Quaternion.identity;
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

        // Assigne les materiaux par defaut si pas deja assignes
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

    public void SpawnFillContent()
    {
        if (fillType == null)
        {
            Debug.LogWarning("PitZone: No fill type assigned!");
            return;
        }

        if (fillType.contentPrefab == null)
        {
            Debug.LogWarning("PitZone: Fill type has no prefab assigned!");
            return;
        }

        ClearFillContent();

        float maxDepth = GetMaxDepth();
        if (maxDepth >= 0)
        {
            Debug.LogWarning("PitZone: No depth found in grid data!");
            return;
        }

        float fillHeight = maxDepth * fillHeightPercent;
        Vector3 fillPosition = GetFillPosition(fillHeight);

        contentInstance = Instantiate(fillType.contentPrefab, transform); // Parent au PitZone
        contentInstance.name = "Content_" + fillType.contentName;
        contentInstance.transform.position = fillPosition; // PUIS on positionne

        ScaleFillContent(fillHeight);

        Debug.Log("PitZone: Spawned fill content: " + fillType.contentName + " at height " + fillHeight);
    }

    public void ClearFillContent()
    {
        if (contentInstance != null)
        {
            if (Application.isPlaying)
            {
                Destroy(contentInstance);
            }
            else
            {
                DestroyImmediate(contentInstance);
            }
            contentInstance = null;
        }
    }

    public float GetMaxDepth()
    {
        if (gridData == null) return 0f;

        float maxDepth = 0f;
        Dictionary<Vector2Int, float> allCells = gridData.GetAllCells();

        foreach (var kvp in allCells)
        {
            if (kvp.Value < maxDepth)
            {
                maxDepth = kvp.Value;
            }
        }

        return maxDepth;
    }

    public Vector3 GetFillPosition(float fillHeight)
    {
        if (gridData == null) return Vector3.zero;

        Dictionary<Vector2Int, float> allCells = gridData.GetAllCells();

        if (allCells.Count == 0) return Vector3.zero;

        Vector2Int min = new Vector2Int(int.MaxValue, int.MaxValue);
        Vector2Int max = new Vector2Int(int.MinValue, int.MinValue);

        // Trouve les bounds en grid coordinates
        foreach (var kvp in allCells)
        {
            if (kvp.Key.x < min.x) min.x = kvp.Key.x;
            if (kvp.Key.y < min.y) min.y = kvp.Key.y;
            if (kvp.Key.x > max.x) max.x = kvp.Key.x;
            if (kvp.Key.y > max.y) max.y = kvp.Key.y;
        }

        // Centre en grid coordinates
        Vector2Int centerGrid = new Vector2Int(
            (min.x + max.x) / 2,
            (min.y + max.y) / 2
        );

        // Converti en world position
        Vector3 centerWorld = gridData.CellToWorld(centerGrid);

        // Position Y : depuis le fond
        float maxDepth = GetMaxDepth();
        float fillHeightAbsolute = Mathf.Abs(maxDepth * fillHeightPercent);
        centerWorld.y = maxDepth + (fillHeightAbsolute * 0.5f);

        return centerWorld;
    }

    private void ScaleFillContent(float fillHeight)
    {
        if (contentInstance == null || gridData == null) return;

        Dictionary<Vector2Int, float> allCells = gridData.GetAllCells();

        Vector2Int min = new Vector2Int(int.MaxValue, int.MaxValue);
        Vector2Int max = new Vector2Int(int.MinValue, int.MinValue);

        foreach (var kvp in allCells)
        {
            if (kvp.Key.x < min.x) min.x = kvp.Key.x;
            if (kvp.Key.y < min.y) min.y = kvp.Key.y;
            if (kvp.Key.x > max.x) max.x = kvp.Key.x;
            if (kvp.Key.y > max.y) max.y = kvp.Key.y;
        }

        float cellSize = gridData.gridCellSize;
        float sizeX = (max.x - min.x + 1) * cellSize;
        float sizeZ = (max.y - min.y + 1) * cellSize;
        float maxDepth = GetMaxDepth();
        float sizeY = Mathf.Abs(maxDepth * fillHeightPercent);

        contentInstance.transform.localScale = new Vector3(sizeX, sizeY, sizeZ);
    }
}