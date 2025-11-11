using UnityEngine;
using System.Collections.Generic;

public static class PitMeshGenerator
{
    public static Mesh GenerateWallsMesh(PitGridData gridData)
    {
        if (gridData == null) return null;

        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();
        List<Vector2> uvs = new List<Vector2>();

        Dictionary<Vector2Int, float> allCells = gridData.GetAllCells();
        float cellSize = gridData.gridCellSize;

        foreach (var kvp in allCells)
        {
            Vector2Int cellPos = kvp.Key;
            float cellDepth = kvp.Value;

            Vector3 cellWorldPos = gridData.CellToWorld(cellPos);

            // Check les 4 voisins (Nord, Sud, Est, Ouest)
            Vector2Int[] neighbors = new Vector2Int[]
            {
                cellPos + new Vector2Int(0, 1),  // Nord (Z+)
                cellPos + new Vector2Int(0, -1), // Sud (Z-)
                cellPos + new Vector2Int(1, 0),  // Est (X+)
                cellPos + new Vector2Int(-1, 0)  // Ouest (X-)
            };

            Vector3[] wallDirections = new Vector3[]
            {
                Vector3.forward,  // Nord
                Vector3.back,     // Sud
                Vector3.right,    // Est
                Vector3.left      // Ouest
            };

            for (int i = 0; i < 4; i++)
            {
                Vector2Int neighbor = neighbors[i];
                float neighborDepth = gridData.GetDepth(neighbor);

                // Si pas de voisin ou voisin moins profond, on met un mur
                if (neighborDepth == 0f || neighborDepth > cellDepth)
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
                        i
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

    public static Mesh GenerateFloorMesh(PitGridData gridData)
    {
        if (gridData == null) return null;

        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();
        List<Vector2> uvs = new List<Vector2>();

        Dictionary<Vector2Int, float> allCells = gridData.GetAllCells();
        float cellSize = gridData.gridCellSize;

        foreach (var kvp in allCells)
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
        int side)
    {
        int startIndex = vertices.Count;

        Vector3 right = Vector3.Cross(direction, Vector3.up).normalized * cellSize;
        Vector3 up = Vector3.up;

        // Position de base du mur (coin de la cellule)
        Vector3 basePos = cellWorldPos;

        // Ajuste la position selon le cote
        if (side == 0) // Nord (Z+)
        {
            basePos += new Vector3(0, 0, cellSize);
        }
        else if (side == 1) // Sud (Z-)
        {
            basePos += new Vector3(cellSize, 0, 0);
            right = -right;
        }
        else if (side == 2) // Est (X+)
        {
            basePos += new Vector3(cellSize, 0, 0);
        }
        else if (side == 3) // Ouest (X-)
        {
            basePos += new Vector3(0, 0, cellSize);
            right = -right;
        }

        float wallHeight = Mathf.Abs(cellDepth - neighborDepth);
        float wallBottom = cellDepth;

        // 4 vertices du quad
        vertices.Add(basePos + new Vector3(0, wallBottom, 0));           // Bottom left
        vertices.Add(basePos + right + new Vector3(0, wallBottom, 0));   // Bottom right
        vertices.Add(basePos + right + new Vector3(0, wallBottom + wallHeight, 0)); // Top right
        vertices.Add(basePos + new Vector3(0, wallBottom + wallHeight, 0));         // Top left

        // UVs
        uvs.Add(new Vector2(0, 0));
        uvs.Add(new Vector2(1, 0));
        uvs.Add(new Vector2(1, 1));
        uvs.Add(new Vector2(0, 1));

        // Triangles (2 triangles pour le quad)
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