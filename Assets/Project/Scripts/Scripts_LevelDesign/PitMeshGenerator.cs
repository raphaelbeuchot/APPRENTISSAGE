using UnityEngine;
using System.Collections.Generic;

public static class PitMeshGenerator
{
    // NOUVELLE SIGNATURE - accepte ownedCells optionnel
    public static Mesh GenerateWallsMesh(PitGridData gridData, float floorThickness = 0.2f, Dictionary<Vector2Int, float> ownedCells = null)
    {
        if (gridData == null) return null;

        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();
        List<Vector2> uvs = new List<Vector2>();

        // Si ownedCells n'est pas fourni, utilise toutes les cellules
        Dictionary<Vector2Int, float> cellsToProcess = ownedCells ?? gridData.GetAllCells();
        float cellSize = gridData.gridCellSize;

        foreach (var kvp in cellsToProcess)
        {
            Vector2Int cellPos = kvp.Key;
            float cellDepth = kvp.Value;

            Vector3 cellWorldPos = gridData.CellToWorld(cellPos);

            Vector2Int[] neighbors = new Vector2Int[]
            {
                cellPos + new Vector2Int(0, 1),  // Nord (Z+)
                cellPos + new Vector2Int(0, -1), // Sud (Z-)
                cellPos + new Vector2Int(1, 0),  // Est (X+)
                cellPos + new Vector2Int(-1, 0)  // Ouest (X-)
            };

            Vector3[] wallDirections = new Vector3[]
            {
                Vector3.forward,
                Vector3.back,
                Vector3.right,
                Vector3.left
            };

            for (int i = 0; i < 4; i++)
            {
                Vector2Int neighbor = neighbors[i];

                // MODIFICATION - verifie si le voisin est dans ownedCells aussi
                bool neighborInZone = ownedCells != null ? ownedCells.ContainsKey(neighbor) : gridData.HasCell(neighbor);
                float neighborDepth = neighborInZone ? gridData.GetDepth(neighbor) : 0f;

                if (!neighborInZone || neighborDepth > cellDepth)
                {
                    AddWallQuad(
                        vertices,
                        triangles,
                        uvs,
                        cellWorldPos,
                        cellDepth,
                        neighborDepth,
                        cellSize,
                        wallDirections[i],
                        i,
                        floorThickness
                    );
                }
            }
        }

        Mesh mesh = new Mesh();
        mesh.name = "PitWalls";
        mesh.vertices = vertices.ToArray();
        mesh.triangles = triangles.ToArray();
        mesh.uv = uvs.ToArray();
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        return mesh;
    }

    // NOUVELLE SIGNATURE - accepte ownedCells optionnel
    public static Mesh GenerateFloorMesh(PitGridData gridData, Dictionary<Vector2Int, float> ownedCells = null)
    {
        if (gridData == null) return null;

        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();
        List<Vector2> uvs = new List<Vector2>();

        // Si ownedCells n'est pas fourni, utilise toutes les cellules
        Dictionary<Vector2Int, float> cellsToProcess = ownedCells ?? gridData.GetAllCells();
        float cellSize = gridData.gridCellSize;

        foreach (var kvp in cellsToProcess)
        {
            Vector2Int cellPos = kvp.Key;
            float cellDepth = kvp.Value;

            Vector3 cellWorldPos = gridData.CellToWorld(cellPos);

            AddFloorQuad(vertices, triangles, uvs, cellWorldPos, cellDepth, cellSize);
        }

        Mesh mesh = new Mesh();
        mesh.name = "PitFloors";
        mesh.vertices = vertices.ToArray();
        mesh.triangles = triangles.ToArray();
        mesh.uv = uvs.ToArray();
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        return mesh;
    }

    private static void AddWallQuad(
        List<Vector3> vertices,
        List<int> triangles,
        List<Vector2> uvs,
        Vector3 cellWorldPos,
        float cellDepth,
        float neighborDepth,
        float cellSize,
        Vector3 direction,
        int side,
        float floorThickness = 0.2f)
    {
        int startIndex = vertices.Count;

        Vector3 basePos = cellWorldPos;
        Vector3 right = Vector3.zero;

        if (side == 0) // Nord (Z+)
        {
            basePos += new Vector3(0, 0, cellSize);
            right = Vector3.right * cellSize;
        }
        else if (side == 1) // Sud (Z-)
        {
            basePos += new Vector3(cellSize, 0, 0);
            right = Vector3.left * cellSize;
        }
        else if (side == 2) // Est (X+)
        {
            basePos += new Vector3(cellSize, 0, 0);
            right = Vector3.forward * cellSize;
        }
        else if (side == 3) // Ouest (X-)
        {
            basePos += new Vector3(0, 0, 0);
            right = Vector3.forward * cellSize;
        }

        float wallTop = -floorThickness;
        float wallBottom = cellDepth;

        vertices.Add(basePos + new Vector3(0, wallBottom, 0));
        vertices.Add(basePos + right + new Vector3(0, wallBottom, 0));
        vertices.Add(basePos + right + new Vector3(0, wallTop, 0));
        vertices.Add(basePos + new Vector3(0, wallTop, 0));

        uvs.Add(new Vector2(0, 0));
        uvs.Add(new Vector2(1, 0));
        uvs.Add(new Vector2(1, 1));
        uvs.Add(new Vector2(0, 1));

        triangles.Add(startIndex);
        triangles.Add(startIndex + 2);
        triangles.Add(startIndex + 1);

        triangles.Add(startIndex);
        triangles.Add(startIndex + 3);
        triangles.Add(startIndex + 2);
    }

    private static void AddFloorQuad(
        List<Vector3> vertices,
        List<int> triangles,
        List<Vector2> uvs,
        Vector3 cellWorldPos,
        float cellDepth,
        float cellSize)
    {
        int startIndex = vertices.Count;

        Vector3 bottomLeft = cellWorldPos + new Vector3(0, cellDepth, 0);
        Vector3 bottomRight = cellWorldPos + new Vector3(cellSize, cellDepth, 0);
        Vector3 topRight = cellWorldPos + new Vector3(cellSize, cellDepth, cellSize);
        Vector3 topLeft = cellWorldPos + new Vector3(0, cellDepth, cellSize);

        vertices.Add(bottomLeft);
        vertices.Add(bottomRight);
        vertices.Add(topRight);
        vertices.Add(topLeft);

        uvs.Add(new Vector2(0, 0));
        uvs.Add(new Vector2(1, 0));
        uvs.Add(new Vector2(1, 1));
        uvs.Add(new Vector2(0, 1));

        triangles.Add(startIndex);
        triangles.Add(startIndex + 2);
        triangles.Add(startIndex + 1);

        triangles.Add(startIndex);
        triangles.Add(startIndex + 3);
        triangles.Add(startIndex + 2);
    }
}