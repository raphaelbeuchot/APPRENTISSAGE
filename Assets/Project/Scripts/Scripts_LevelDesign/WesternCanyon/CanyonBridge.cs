using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;

public class CanyonBridge : MonoBehaviour
{
    [Header("Spline")]
    public SplineContainer splineContainer;

    [Header("Tile Settings")]
    public GameObject tilePrefab;
    public float tileSpacing = 0.5f;

    [HideInInspector]
    public List<GameObject> tiles = new List<GameObject>();

    [ContextMenu("Generate Tiles")]
    public void GenerateTiles()
    {
        ClearTiles();

        if (splineContainer == null)
        {
            Debug.LogWarning("[CanyonBridge] SplineContainer non assigne.");
            return;
        }

        if (tilePrefab == null)
        {
            Debug.LogWarning("[CanyonBridge] tilePrefab non assigne.");
            return;
        }

        Transform tilesContainer = transform.Find("Tiles");
        if (tilesContainer == null)
        {
            GameObject containerGO = new GameObject("Tiles");
            containerGO.transform.SetParent(transform);
            containerGO.transform.localPosition = Vector3.zero;
            tilesContainer = containerGO.transform;
        }

        Spline spline = splineContainer.Spline;
        float splineLength = spline.GetLength();
        int count = Mathf.FloorToInt(splineLength / tileSpacing);

        for (int i = 0; i < count; i++)
        {
            float t = (i * tileSpacing + tileSpacing * 0.5f) / splineLength;
            t = Mathf.Clamp01(t);

            // position et tangente le long de la spline
            Vector3 localPos = spline.EvaluatePosition(t);
            Vector3 localTangent = spline.EvaluateTangent(t);

            // passage en world space
            Vector3 worldPos = splineContainer.transform.TransformPoint(localPos);
            Vector3 worldTangent = splineContainer.transform.TransformDirection(localTangent).normalized;

            // orientation : la planche est perpendiculaire a la tangente
            // forward de la planche = tangente de la spline
            Quaternion rot = Quaternion.LookRotation(worldTangent, Vector3.up);

            GameObject tile = Instantiate(tilePrefab, worldPos, rot, tilesContainer);
            tile.name = "Tile_" + i.ToString("D3");
            tiles.Add(tile);
        }

        Debug.Log("[CanyonBridge] " + tiles.Count + " planches generees sur " + splineLength.ToString("F1") + "m.");
    }

    [ContextMenu("Clear Tiles")]
    public void ClearTiles()
    {
        Transform tilesContainer = transform.Find("Tiles");
        if (tilesContainer != null)
        {
            while (tilesContainer.childCount > 0)
                DestroyImmediate(tilesContainer.GetChild(0).gameObject);
        }
        tiles.Clear();
    }
}