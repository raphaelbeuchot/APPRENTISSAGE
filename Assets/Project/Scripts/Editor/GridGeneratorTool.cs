using Pathfinding;
using UnityEditor;
using UnityEngine;

public class GridGeneratorTool : EditorWindow
{
    private float levelWidth = 20f;
    private float levelLength = 30f;

    [MenuItem("Tools/Generate Grid")]
    public static void ShowWindow()
    {
        GetWindow<GridGeneratorTool>("Generate Grid");
    }

    private void OnGUI()
    {
        GUILayout.Label("Grid Generator", EditorStyles.boldLabel);
        GUILayout.Space(10);

        levelWidth = EditorGUILayout.FloatField("Width (m)", levelWidth);
        levelLength = EditorGUILayout.FloatField("Length (m)", levelLength);

        GUILayout.Space(20);

        if (GUILayout.Button("GENERATE GRID", GUILayout.Height(40)))
        {
            GenerateGrid();
        }
    }

    private void GenerateGrid()
    {
        if (levelWidth <= 0 || levelLength <= 0)
        {
            EditorUtility.DisplayDialog("Erreur", "Les dimensions doivent être > 0", "OK");
            return;
        }

        // Trouver le A* Pathfinding dans la scène
        AstarPath astarPath = FindObjectOfType<AstarPath>();

        if (astarPath == null)
        {
            EditorUtility.DisplayDialog("Erreur", "Aucun A* Pathfinding trouvé dans la scène !", "OK");
            return;
        }

        // Supprimer tous les graphs existants
        if (astarPath.data.graphs != null && astarPath.data.graphs.Length > 0)
        {
            foreach (var graph in astarPath.data.graphs)
            {
                astarPath.data.RemoveGraph(graph);
            }
            Debug.Log("<color=yellow>Anciens graphs supprimés</color>");
        }

        // Créer nouveau Grid Graph
        GridGraph gridGraph = astarPath.data.AddGraph(typeof(GridGraph)) as GridGraph;

        if (gridGraph != null)
        {
            gridGraph.center = new Vector3(0, 0, levelLength / 2f);
            gridGraph.SetDimensions((int)(levelWidth / 0.25f), (int)(levelLength / 0.25f), 0.25f);
            gridGraph.erodeIterations = 0;

            gridGraph.collision.collisionCheck = true;
            gridGraph.collision.diameter = 0.5f;
            gridGraph.collision.height = 2f;
            gridGraph.collision.mask = LayerMask.GetMask("Obstacle");

            gridGraph.collision.use2D = false;
            gridGraph.collision.heightCheck = true;
            gridGraph.collision.fromHeight = 100f;
            gridGraph.collision.unwalkableWhenNoGround = false;
            gridGraph.collision.heightMask = LayerMask.GetMask("Ground");

            gridGraph.neighbours = NumNeighbours.Four;

            // Scan automatique
            astarPath.Scan();

            Debug.Log($"<color=green>Grid généré avec succès !</color>");
            Debug.Log($"Dimensions : {levelWidth}m x {levelLength}m");
        }
    }
}