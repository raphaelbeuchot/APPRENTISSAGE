using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "PitGridData", menuName = "Level Design/Pit Grid Data")]
public class PitGridData : ScriptableObject
{
    [System.Serializable]
    public class CellData
    {
        public Vector2Int position;
        public float depth;
        public int ownerZoneID = -1; // NOUVEAU : -1 = pas de proprietaire

        public CellData(Vector2Int pos, float d)
        {
            position = pos;
            depth = d;
            ownerZoneID = -1;
        }
    }

    [SerializeField]
    private List<CellData> serializedCells = new List<CellData>();

    private Dictionary<Vector2Int, float> cells = new Dictionary<Vector2Int, float>();
    private Dictionary<Vector2Int, int> cellOwnership = new Dictionary<Vector2Int, int>(); // NOUVEAU

    public float gridCellSize = 0.5f;

    public void OnEnable()  // Change de "private void" à "public void"
    {
        LoadFromSerialized();
    }

    private void LoadFromSerialized()
    {
        cells.Clear();
        cellOwnership.Clear();

        

        foreach (CellData data in serializedCells)
        {
            cells[data.position] = data.depth;
            cellOwnership[data.position] = data.ownerZoneID;
        }

    }

    private void SaveToSerialized()
    {
        serializedCells.Clear();
        foreach (var kvp in cells)
        {
            CellData data = new CellData(kvp.Key, kvp.Value);

            // NOUVEAU : sauvegarde l'owner
            if (cellOwnership.ContainsKey(kvp.Key))
            {
                data.ownerZoneID = cellOwnership[kvp.Key];
            }

            serializedCells.Add(data);
        }
    }

    // NOUVELLES METHODES
    public void AssignCellToZone(Vector2Int cellPos, int zoneID)
    {
        if (cells.ContainsKey(cellPos))
        {
            cellOwnership[cellPos] = zoneID;
            Debug.Log("ASSIGNED cell " + cellPos + " to zone " + zoneID); // DEBUG
            SaveToSerialized();
        }
        else
        {
            Debug.LogWarning("Cannot assign cell " + cellPos + " - cell doesn't exist in grid!"); // DEBUG
        }
    }

    public int GetCellOwner(Vector2Int cellPos)
    {
        if (cellOwnership.ContainsKey(cellPos))
        {
            return cellOwnership[cellPos];
        }
        return -1; // Pas de proprietaire
    }

    public Dictionary<Vector2Int, float> GetCellsForZone(int zoneID)
    {
        Dictionary<Vector2Int, float> zoneCells = new Dictionary<Vector2Int, float>();

        foreach (var kvp in cells)
        {
            if (cellOwnership.ContainsKey(kvp.Key) && cellOwnership[kvp.Key] == zoneID)
            {
                zoneCells[kvp.Key] = kvp.Value;
            }
        }

        return zoneCells;
    }

    public void RemoveCellOwnership(Vector2Int cellPos)
    {
        if (cellOwnership.ContainsKey(cellPos))
        {
            cellOwnership[cellPos] = -1;
            SaveToSerialized();
        }
    }
    // FIN NOUVELLES METHODES

    public float GetDepth(Vector2Int cellPos)
    {
        if (cells.ContainsKey(cellPos))
        {
            return cells[cellPos];
        }
        return 0f;
    }

    public void SetDepth(Vector2Int cellPos, float depth)
    {
        if (depth == 0f)
        {
            cells.Remove(cellPos);
            cellOwnership.Remove(cellPos); // NOUVEAU : enleve aussi l'ownership
        }
        else
        {
            cells[cellPos] = depth;
            // L'ownership sera assigne par AssignCellToZone separement
        }
        SaveToSerialized();
    }

    public bool HasCell(Vector2Int cellPos)
    {
        return cells.ContainsKey(cellPos);
    }

    public Dictionary<Vector2Int, float> GetAllCells()
    {
        return new Dictionary<Vector2Int, float>(cells);
    }

    public void ClearAll()
    {
        cells.Clear();
        cellOwnership.Clear(); // NOUVEAU
        serializedCells.Clear();
    }

    public Vector3 CellToWorld(Vector2Int cellPos)
    {
        float x = cellPos.x * gridCellSize;
        float z = cellPos.y * gridCellSize;
        return new Vector3(x, 0f, z);
    }

    public Vector2Int WorldToCell(Vector3 worldPos)
    {
        int x = Mathf.RoundToInt(worldPos.x / gridCellSize);
        int z = Mathf.RoundToInt(worldPos.z / gridCellSize);
        return new Vector2Int(x, z);
    }

    public Vector3 GetCellWorldPosition(Vector2Int cellPos, float depthOffset = 0f)
    {
        Vector3 basePos = CellToWorld(cellPos);
        float depth = GetDepth(cellPos);
        return new Vector3(basePos.x, depth + depthOffset, basePos.z);
    }
}