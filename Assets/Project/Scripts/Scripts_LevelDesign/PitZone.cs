using System.Collections.Generic;
using UnityEngine;
using Unity.AI.Navigation;

[ExecuteInEditMode]
public class PitZone : MonoBehaviour
{
    [Header("Zone Identity")]
    public int zoneID = -1;

    [Header("Grid Data")]
    public PitGridData gridData;

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

    [Header("Fill Content")]
    public PitContentType fillType;
    [Range(0f, 1f)]
    public float fillHeightPercent = 0.5f;
    public GameObject contentInstance;

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

        if (zoneID == -1)
        {
            Debug.LogWarning("PitZone: No zone ID assigned!");
            return;
        }

        SetupMeshObjects();

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

        Debug.Log("PitZone " + zoneID + ": Meshes generated successfully with " + ownedCells.Count + " cells!");
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

        ClearFillContent();

        float maxDepth = GetMaxDepth();

        if (maxDepth >= 0)
        {
            Debug.LogWarning("PitZone: No depth found in grid data!");
            return;
        }

        // Cree le parent pour le fill content
        contentInstance = new GameObject("Content_" + fillType.contentName);
        contentInstance.transform.SetParent(transform);
        contentInstance.transform.localPosition = Vector3.zero;

        // Genere le mesh procedurale qui suit les cellules
        Mesh fillMesh = GenerateFillMesh(maxDepth * fillHeightPercent);

        if (fillMesh != null)
        {
            MeshFilter meshFilter = contentInstance.AddComponent<MeshFilter>();
            MeshRenderer meshRenderer = contentInstance.AddComponent<MeshRenderer>();

            meshFilter.sharedMesh = fillMesh;

            // Assigne le materiau du prefab si disponible
            if (fillType.contentPrefab != null)
            {
                MeshRenderer prefabRenderer = fillType.contentPrefab.GetComponent<MeshRenderer>();
                if (prefabRenderer != null)
                {
                    meshRenderer.sharedMaterial = prefabRenderer.sharedMaterial;
                }
            }
        }

        Debug.Log("PitZone: Spawned fill content: " + fillType.contentName);
    }

    private Mesh GenerateFillMesh(float fillHeight)
    {
        if (gridData == null) return null;

        Dictionary<Vector2Int, float> ownedCells = GetOwnedCells();
        if (ownedCells.Count == 0) return null;

        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();
        List<Vector2> uvs = new List<Vector2>();

        float cellSize = gridData.gridCellSize;
        float fillHeightAbsolute = Mathf.Abs(fillHeight);
        float fillBottom = GetMaxDepth();
        float fillTop = fillBottom + fillHeightAbsolute;

        foreach (var kvp in ownedCells)
        {
            Vector2Int cellPos = kvp.Key;
            Vector3 cellWorldPos = gridData.CellToWorld(cellPos);

            // Genere un cube pour cette cellule
            AddFillCube(vertices, triangles, uvs, cellWorldPos, cellSize, fillBottom, fillTop);
        }

        Mesh mesh = new Mesh();
        mesh.name = "FillContent";
        mesh.vertices = vertices.ToArray();
        mesh.triangles = triangles.ToArray();
        mesh.uv = uvs.ToArray();
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        return mesh;
    }

    private void AddFillCube(List<Vector3> vertices, List<int> triangles, List<Vector2> uvs,
                             Vector3 cellWorldPos, float cellSize, float bottom, float top)
    {
        int startIndex = vertices.Count;

        // 8 vertices du cube
        Vector3 v0 = cellWorldPos + new Vector3(0, bottom, 0);
        Vector3 v1 = cellWorldPos + new Vector3(cellSize, bottom, 0);
        Vector3 v2 = cellWorldPos + new Vector3(cellSize, bottom, cellSize);
        Vector3 v3 = cellWorldPos + new Vector3(0, bottom, cellSize);
        Vector3 v4 = cellWorldPos + new Vector3(0, top, 0);
        Vector3 v5 = cellWorldPos + new Vector3(cellSize, top, 0);
        Vector3 v6 = cellWorldPos + new Vector3(cellSize, top, cellSize);
        Vector3 v7 = cellWorldPos + new Vector3(0, top, cellSize);

        // Bottom face
        vertices.Add(v0); vertices.Add(v1); vertices.Add(v2); vertices.Add(v3);
        uvs.Add(new Vector2(0, 0)); uvs.Add(new Vector2(1, 0)); uvs.Add(new Vector2(1, 1)); uvs.Add(new Vector2(0, 1));
        AddQuad(triangles, startIndex);

        // Top face
        vertices.Add(v7); vertices.Add(v6); vertices.Add(v5); vertices.Add(v4);
        uvs.Add(new Vector2(0, 0)); uvs.Add(new Vector2(1, 0)); uvs.Add(new Vector2(1, 1)); uvs.Add(new Vector2(0, 1));
        AddQuad(triangles, startIndex + 4);

        // Front face
        vertices.Add(v0); vertices.Add(v4); vertices.Add(v5); vertices.Add(v1);
        uvs.Add(new Vector2(0, 0)); uvs.Add(new Vector2(0, 1)); uvs.Add(new Vector2(1, 1)); uvs.Add(new Vector2(1, 0));
        AddQuad(triangles, startIndex + 8);

        // Back face
        vertices.Add(v3); vertices.Add(v7); vertices.Add(v6); vertices.Add(v2);
        uvs.Add(new Vector2(0, 0)); uvs.Add(new Vector2(0, 1)); uvs.Add(new Vector2(1, 1)); uvs.Add(new Vector2(1, 0));
        AddQuad(triangles, startIndex + 12);

        // Left face
        vertices.Add(v0); vertices.Add(v3); vertices.Add(v7); vertices.Add(v4);
        uvs.Add(new Vector2(0, 0)); uvs.Add(new Vector2(1, 0)); uvs.Add(new Vector2(1, 1)); uvs.Add(new Vector2(0, 1));
        AddQuad(triangles, startIndex + 16);

        // Right face
        vertices.Add(v1); vertices.Add(v5); vertices.Add(v6); vertices.Add(v2);
        uvs.Add(new Vector2(0, 0)); uvs.Add(new Vector2(0, 1)); uvs.Add(new Vector2(1, 1)); uvs.Add(new Vector2(1, 0));
        AddQuad(triangles, startIndex + 20);
    }

    private void AddQuad(List<int> triangles, int startIndex)
    {
        triangles.Add(startIndex);
        triangles.Add(startIndex + 1);
        triangles.Add(startIndex + 2);

        triangles.Add(startIndex);
        triangles.Add(startIndex + 2);
        triangles.Add(startIndex + 3);
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

    public Vector3 GetFillPosition(float fillHeight)
    {
        if (gridData == null) return Vector3.zero;

        Dictionary<Vector2Int, float> ownedCells = GetOwnedCells();

        if (ownedCells.Count == 0) return Vector3.zero;

        Vector2Int min = new Vector2Int(int.MaxValue, int.MaxValue);
        Vector2Int max = new Vector2Int(int.MinValue, int.MinValue);

        foreach (var kvp in ownedCells)
        {
            if (kvp.Key.x < min.x) min.x = kvp.Key.x;
            if (kvp.Key.y < min.y) min.y = kvp.Key.y;
            if (kvp.Key.x > max.x) max.x = kvp.Key.x;
            if (kvp.Key.y > max.y) max.y = kvp.Key.y;
        }

        float cellSize = gridData.gridCellSize;

        float centerWorldX = ((min.x + max.x) * 0.5f * cellSize) + (cellSize * 0.5f);
        float centerWorldZ = ((min.y + max.y) * 0.5f * cellSize) + (cellSize * 0.5f);

        float maxDepth = GetMaxDepth();
        float fillHeightAbsolute = Mathf.Abs(maxDepth * fillHeightPercent);
        float centerWorldY = maxDepth + (fillHeightAbsolute * 0.5f);

        return new Vector3(centerWorldX, centerWorldY, centerWorldZ);
    }

    private void ScaleFillContent(float fillHeight)
    {
        if (contentInstance == null || gridData == null) return;

        Dictionary<Vector2Int, float> ownedCells = GetOwnedCells();

        Vector2Int min = new Vector2Int(int.MaxValue, int.MaxValue);
        Vector2Int max = new Vector2Int(int.MinValue, int.MinValue);

        foreach (var kvp in ownedCells)
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

    public void SetupDamageSystem()
    {
        PitDamageController damageController = GetComponentInChildren<PitDamageController>();

        if (damageController == null)
        {
            GameObject damageObj = new GameObject("DamageController");
            damageObj.transform.SetParent(transform);
            damageObj.transform.localPosition = Vector3.zero;
            damageObj.transform.localRotation = Quaternion.identity;

            damageController = damageObj.AddComponent<PitDamageController>();
            damageController.pitZone = this;

            Debug.Log("PitZone: Created PitDamageController");
        }

        damageController.UpdateTriggerBounds();

        Debug.Log("PitZone: Damage system setup complete");
    }

    public void CreateNavMeshMargin(float marginWidth = 0.5f)
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
        float thickness = 0.01f;

        // Pour chaque cellule, verifie les 4 directions
        foreach (var kvp in ownedCells)
        {
            Vector2Int cellPos = kvp.Key;
            Vector3 cellWorldPos = gridData.CellToWorld(cellPos);

            // Nord (Z+)
            if (!ownedCells.ContainsKey(cellPos + new Vector2Int(0, 1)))
            {
                CreateMarginSegment(marginParent, cellWorldPos, cellSize, thickness, marginWidth, "North");
            }

            // Sud (Z-)
            if (!ownedCells.ContainsKey(cellPos + new Vector2Int(0, -1)))
            {
                CreateMarginSegment(marginParent, cellWorldPos, cellSize, thickness, marginWidth, "South");
            }

            // Est (X+)
            if (!ownedCells.ContainsKey(cellPos + new Vector2Int(1, 0)))
            {
                CreateMarginSegment(marginParent, cellWorldPos, cellSize, thickness, marginWidth, "East");
            }

            // Ouest (X-)
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
                // INVERSE : au lieu de +cellSize, on met -marginWidth/2 (interieur)
                position = cellWorldPos + new Vector3(cellSize * 0.5f, 0f, cellSize - marginWidth * 0.5f);
                scale = new Vector3(cellSize, thickness, marginWidth);
                break;

            case "South":
                // INVERSE : au lieu de -marginWidth/2, on met +marginWidth/2 (interieur)
                position = cellWorldPos + new Vector3(cellSize * 0.5f, 0f, marginWidth * 0.5f);
                scale = new Vector3(cellSize, thickness, marginWidth);
                break;

            case "East":
                // INVERSE : au lieu de +cellSize, on met -marginWidth/2 (interieur)
                position = cellWorldPos + new Vector3(cellSize - marginWidth * 0.5f, 0f, cellSize * 0.5f);
                scale = new Vector3(marginWidth, thickness, cellSize);
                break;

            case "West":
                // INVERSE : au lieu de -marginWidth/2, on met +marginWidth/2 (interieur)
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