using UnityEngine;
using System.Collections.Generic;

public class PitFill : MonoBehaviour
{
    [Header("References")]
    public PitZone pitZone;

    [Header("Fill Settings")]
    public PitContentType fillType;

    [Range(0f, 1f)]
    [Tooltip("Fill height as percentage of pit depth (0 = empty, 1 = full)")]
    public float fillHeightPercent = 0.5f;

    [Header("Mesh References")]
    public MeshFilter fillMeshFilter;
    public MeshRenderer fillRenderer;

    private void Awake()
    {
        if (pitZone == null)
        {
            pitZone = GetComponentInParent<PitZone>();
        }

        if (pitZone == null)
        {
            Debug.LogError("PitFill: No PitZone found in parent!");
        }
    }

    public void GenerateFillContent()
    {
        if (fillType == null)
        {
            Debug.LogWarning("PitFill: No fill type assigned!");
            return;
        }

        if (pitZone == null)
        {
            Debug.LogError("PitFill: No PitZone reference!");
            return;
        }

        ClearFillContent();
        SetupFillMeshObjects();

        float maxDepth = pitZone.GetMaxDepth();

        if (maxDepth >= 0)
        {
            Debug.LogWarning("PitFill: No depth found in pit!");
            return;
        }

        Mesh fillMesh = GenerateFillMesh(maxDepth * fillHeightPercent);

        if (fillMesh != null && fillMeshFilter != null)
        {
            fillMeshFilter.sharedMesh = fillMesh;
        }

        CreateFillTriggers();

        Debug.Log("PitFill: Generated fill content - " + fillType.contentName + " at " + GetFillHeightMeters().ToString("F2") + "m");

        // Ajoute le damage controller s'il n'existe pas
        PitFillDamageController damageController = GetComponent<PitFillDamageController>();
        if (damageController == null)
        {
            damageController = gameObject.AddComponent<PitFillDamageController>();
            damageController.pitFill = this;
            damageController.pitZone = pitZone;
        }

        // Met a jour le floor detector avec la reference au damage controller
        if (pitZone != null && pitZone.floorMeshFilter != null)
        {
            PitFloorCollisionDetector detector = pitZone.floorMeshFilter.GetComponent<PitFloorCollisionDetector>();
            if (detector != null)
            {
                detector.Initialize(pitZone, damageController);
            }
        }
    }

    private void SetupFillMeshObjects()
    {
        if (fillMeshFilter == null)
        {
            fillMeshFilter = GetComponent<MeshFilter>();
            if (fillMeshFilter == null)
            {
                fillMeshFilter = gameObject.AddComponent<MeshFilter>();
            }
        }

        if (fillRenderer == null)
        {
            fillRenderer = GetComponent<MeshRenderer>();
            if (fillRenderer == null)
            {
                fillRenderer = gameObject.AddComponent<MeshRenderer>();
            }
        }

        if (fillType != null && fillType.contentPrefab != null)
        {
            MeshRenderer prefabRenderer = fillType.contentPrefab.GetComponent<MeshRenderer>();
            if (prefabRenderer != null)
            {
                fillRenderer.sharedMaterial = prefabRenderer.sharedMaterial;
            }
        }
    }

    private Mesh GenerateFillMesh(float fillHeight)
    {
        if (pitZone == null || pitZone.gridData == null) return null;

        Dictionary<Vector2Int, float> ownedCells = pitZone.gridData.GetCellsForZone(pitZone.zoneID);
        if (ownedCells.Count == 0) return null;

        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();
        List<Vector2> uvs = new List<Vector2>();

        float cellSize = pitZone.gridData.gridCellSize;
        float fillHeightAbsolute = Mathf.Abs(fillHeight);
        float fillBottom = pitZone.GetMaxDepth();
        float fillTop = fillBottom + fillHeightAbsolute;

        foreach (var kvp in ownedCells)
        {
            Vector2Int cellPos = kvp.Key;
            Vector3 cellWorldPos = pitZone.gridData.CellToWorld(cellPos);

            AddFillCube(vertices, triangles, uvs, cellWorldPos, cellSize, fillBottom, fillTop);
        }

        Mesh mesh = new Mesh();
        mesh.name = "FillContent_" + fillType.contentName;
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

    private void CreateFillTriggers()
    {
        if (pitZone == null || pitZone.gridData == null) return;

        // Supprime les anciens colliders
        BoxCollider[] oldColliders = GetComponents<BoxCollider>();
        foreach (BoxCollider col in oldColliders)
        {
            DestroyImmediate(col);
        }

        Dictionary<Vector2Int, float> ownedCells = pitZone.gridData.GetCellsForZone(pitZone.zoneID);
        float cellSize = pitZone.gridData.gridCellSize;
        float maxDepth = pitZone.GetMaxDepth();
        float fillHeightAbsolute = Mathf.Abs(maxDepth * fillHeightPercent);
        float sizeY = fillHeightAbsolute;

        // Cree un BoxCollider trigger par cellule
        foreach (var kvp in ownedCells)
        {
            Vector2Int cellPos = kvp.Key;
            Vector3 cellWorldPos = pitZone.gridData.CellToWorld(cellPos);

            BoxCollider cellCollider = gameObject.AddComponent<BoxCollider>();
            cellCollider.isTrigger = true;

            // Position relative au PitFill
            Vector3 localPos = cellWorldPos + new Vector3(cellSize * 0.5f, maxDepth + fillHeightAbsolute * 0.5f, cellSize * 0.5f);
            cellCollider.center = localPos - transform.position;
            cellCollider.size = new Vector3(cellSize, sizeY, cellSize);
        }

        Debug.Log("PitFill: Created " + ownedCells.Count + " trigger colliders");
    }

    public void ClearFillContent()
    {
        if (fillMeshFilter != null)
        {
            fillMeshFilter.sharedMesh = null;
        }

        // Supprime tous les BoxColliders
        BoxCollider[] colliders = GetComponents<BoxCollider>();
        foreach (BoxCollider col in colliders)
        {
            DestroyImmediate(col);
        }

        Debug.Log("PitFill: Content cleared");
    }

    public float GetFillHeightMeters()
    {
        if (pitZone == null) return 0f;
        float maxDepth = pitZone.GetMaxDepth();
        return Mathf.Abs(maxDepth * fillHeightPercent);
    }

    public float GetFillSurfaceHeight()
    {
        if (pitZone == null) return 0f;
        float maxDepth = pitZone.GetMaxDepth();
        return maxDepth + GetFillHeightMeters();
    }
}