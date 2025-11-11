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

        public CellData(Vector2Int pos, float d)
        {
            position = pos;
            depth = d;
        }
    }

    [SerializeField]
    private List<CellData> serializedCells = new List<CellData>();

    private Dictionary<Vector2Int, float> cells = new Dictionary<Vector2Int, float>();

    public float gridCellSize = 0.5f;

    public void OnEnable()
    {
        LoadFromSerialized();
    }

    private void LoadFromSerialized()
    {
        cells.Clear();
        foreach (CellData data in serializedCells)
        {
            cells[data.position] = data.depth;
        }
    }

    private void SaveToSerialized()
    {
        serializedCells.Clear();
        foreach (var kvp in cells)
        {
            serializedCells.Add(new CellData(kvp.Key, kvp.Value));
        }
    }

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
        }
        else
        {
            cells[cellPos] = depth;
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