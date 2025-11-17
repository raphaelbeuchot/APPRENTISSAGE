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

        // Pour Empty : pas de mesh visuel
        if (fillType.category == PitContentType.ContentCategory.Empty)
        {
            Debug.Log("PitFill: Empty pit - no visual mesh generated");
            return null;
        }

        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();
        List<Vector2> uvs = new List<Vector2>();

        float cellSize = pitZone.gridData.gridCellSize;
        float fillHeightAbsolute = Mathf.Abs(fillHeight);
        float fillSurfaceY = pitZone.GetMaxDepth() + fillHeightAbsolute;

        // Crée un quad (surface plane) par cellule
        foreach (var kvp in ownedCells)
        {
            Vector2Int cellPos = kvp.Key;
            Vector3 cellWorldPos = pitZone.gridData.CellToWorld(cellPos);

            AddSurfaceQuad(vertices, triangles, uvs, cellWorldPos, cellSize, fillSurfaceY);
        }

        Mesh mesh = new Mesh();
        mesh.name = "FillSurface_" + fillType.contentName;
        mesh.vertices = vertices.ToArray();
        mesh.triangles = triangles.ToArray();
        mesh.uv = uvs.ToArray();
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        Debug.Log($"PitFill: Generated {ownedCells.Count} surface quads at Y={fillSurfaceY:F2}m");

        return mesh;
    }

    private void AddSurfaceQuad(List<Vector3> vertices, List<int> triangles, List<Vector2> uvs,
                            Vector3 cellWorldPos, float cellSize, float surfaceY)
    {
        int startIndex = vertices.Count;

        // 4 coins du quad (surface du liquide uniquement)
        Vector3 v0 = cellWorldPos + new Vector3(0, surfaceY, 0);
        Vector3 v1 = cellWorldPos + new Vector3(cellSize, surfaceY, 0);
        Vector3 v2 = cellWorldPos + new Vector3(cellSize, surfaceY, cellSize);
        Vector3 v3 = cellWorldPos + new Vector3(0, surfaceY, cellSize);

        // Ajouter les vertices
        vertices.Add(v0);
        vertices.Add(v1);
        vertices.Add(v2);
        vertices.Add(v3);

        // UVs
        uvs.Add(new Vector2(0, 0));
        uvs.Add(new Vector2(1, 0));
        uvs.Add(new Vector2(1, 1));
        uvs.Add(new Vector2(0, 1));

        // Triangles INVERSÉS (normales vers le haut)
        triangles.Add(startIndex);
        triangles.Add(startIndex + 2);
        triangles.Add(startIndex + 1);

        triangles.Add(startIndex);
        triangles.Add(startIndex + 3);
        triangles.Add(startIndex + 2);
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

        float triggerY;
        float triggerHeight = 0.1f; // Trigger très fin (10cm)

        // Déterminer la position Y du trigger selon le type
        if (fillType.category == PitContentType.ContentCategory.Empty)
        {
            // Empty : trigger à -2m (fixe) pour détecter chutes >= 3m
            triggerY = -2f;
            Debug.Log("PitFill: Empty pit - trigger at Y=-2m");
        }
        else
        {
            // Liquides : trigger aligné avec la surface visuelle
            float fillHeightAbsolute = Mathf.Abs(maxDepth * fillHeightPercent);
            triggerY = maxDepth + fillHeightAbsolute;
            Debug.Log($"PitFill: {fillType.contentName} - trigger at Y={triggerY:F2}m (surface level)");
        }

        // Crée un BoxCollider trigger fin par cellule
        foreach (var kvp in ownedCells)
        {
            Vector2Int cellPos = kvp.Key;
            Vector3 cellWorldPos = pitZone.gridData.CellToWorld(cellPos);

            BoxCollider cellCollider = gameObject.AddComponent<BoxCollider>();
            cellCollider.isTrigger = true;

            // Position du trigger (centre du collider)
            Vector3 localPos = cellWorldPos + new Vector3(cellSize * 0.5f, triggerY, cellSize * 0.5f);
            cellCollider.center = localPos - transform.position;
            cellCollider.size = new Vector3(cellSize, triggerHeight, cellSize);
        }

        Debug.Log($"PitFill: Created {ownedCells.Count} trigger colliders (height={triggerHeight}m)");
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