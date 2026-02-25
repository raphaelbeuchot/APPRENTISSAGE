using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlatformBridge : MonoBehaviour
{
    [Header("Waypoints")]
    public List<Transform> waypoints = new List<Transform>();

    [Header("Tile Settings")]
    public GameObject tilePrefab;
    public float tileSpacing = 1.0f;

    [Header("Fall Settings")]
    public float tileDropDepth = 5f;
    public float tileDropDuration = 0.5f;
    public float cascadeDelay = 0.08f;
    public float tileRotationOnFall = 45f;

    [Header("Rise Settings")]
    public float tileRiseDuration = 0.3f;
    public float cascadeRiseDelay = 0.05f;

    [HideInInspector]
    public List<GameObject> tiles = new List<GameObject>();

    private List<Vector3> originalPositions = new List<Vector3>();
    private List<Quaternion> originalRotations = new List<Quaternion>();
    private bool isFalling = false;

    private void Start()
    {
        CacheOriginalTransforms();
    }

    private void CacheOriginalTransforms()
    {
        originalPositions.Clear();
        originalRotations.Clear();
        foreach (GameObject tile in tiles)
        {
            if (tile != null)
            {
                originalPositions.Add(tile.transform.position);
                originalRotations.Add(tile.transform.rotation);
            }
            else
            {
                originalPositions.Add(Vector3.zero);
                originalRotations.Add(Quaternion.identity);
            }
        }
    }

    public void TriggerFall()
    {
        if (isFalling) return;
        StartCoroutine(CascadeFall());
    }

    public void TriggerRise()
    {
        StartCoroutine(CascadeRise());
    }

    private IEnumerator CascadeFall()
    {
        isFalling = true;
        for (int i = 0; i < tiles.Count; i++)
        {
            if (tiles[i] != null)
                StartCoroutine(FallTile(tiles[i]));
            yield return new WaitForSeconds(cascadeDelay);
        }
    }

    private IEnumerator FallTile(GameObject tile)
    {
        Vector3 startPos = tile.transform.position;
        Vector3 endPos = startPos + Vector3.down * tileDropDepth;
        Quaternion startRot = tile.transform.rotation;
        Quaternion endRot = startRot * Quaternion.Euler(tileRotationOnFall, 0f, tileRotationOnFall);

        float elapsed = 0f;
        while (elapsed < tileDropDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / tileDropDuration;
            // ease in
            t = t * t;
            tile.transform.position = Vector3.Lerp(startPos, endPos, t);
            tile.transform.rotation = Quaternion.Lerp(startRot, endRot, t);
            yield return null;
        }

        tile.transform.position = endPos;
        tile.transform.rotation = endRot;
        tile.SetActive(false);
    }

    private IEnumerator CascadeRise()
    {
        isFalling = false;
        // remontee dans l'ordre inverse : N -> 0
        for (int i = tiles.Count - 1; i >= 0; i--)
        {
            if (tiles[i] != null)
            {
                tiles[i].SetActive(true);
                StartCoroutine(RiseTile(tiles[i], originalPositions[i], originalRotations[i]));
            }
            yield return new WaitForSeconds(cascadeRiseDelay);
        }
    }

    private IEnumerator RiseTile(GameObject tile, Vector3 targetPos, Quaternion targetRot)
    {
        Vector3 startPos = tile.transform.position;
        Quaternion startRot = tile.transform.rotation;

        float elapsed = 0f;
        while (elapsed < tileRiseDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / tileRiseDuration;
            // ease out
            t = 1f - (1f - t) * (1f - t);
            tile.transform.position = Vector3.Lerp(startPos, targetPos, t);
            tile.transform.rotation = Quaternion.Lerp(startRot, targetRot, t);
            yield return null;
        }

        tile.transform.position = targetPos;
        tile.transform.rotation = targetRot;
    }

    [ContextMenu("Generate Tiles")]
    public void GenerateTiles()
    {
        ClearTiles();

        if (waypoints.Count < 2)
        {
            Debug.LogWarning("[PlatformBridge] Au moins 2 waypoints requis.");
            return;
        }

        if (tilePrefab == null)
        {
            Debug.LogWarning("[PlatformBridge] tilePrefab non assigne.");
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

        int tileIndex = 0;

        for (int i = 0; i < waypoints.Count - 1; i++)
        {
            if (waypoints[i] == null || waypoints[i + 1] == null)
            {
                Debug.LogWarning("[PlatformBridge] Waypoint null au segment " + i);
                continue;
            }

            Vector3 from = waypoints[i].position;
            Vector3 to = waypoints[i + 1].position;
            Vector3 direction = (to - from).normalized;
            float segmentLength = Vector3.Distance(from, to);
            int count = Mathf.FloorToInt(segmentLength / tileSpacing);

            for (int j = 0; j < count; j++)
            {
                // centree dans l'intervalle
                Vector3 pos = from + direction * (j * tileSpacing + tileSpacing * 0.5f);
                Quaternion rot = Quaternion.LookRotation(direction);

                GameObject tile = Instantiate(tilePrefab, pos, rot, tilesContainer);
                tile.name = "Tile_" + tileIndex.ToString("D2");
                tiles.Add(tile);
                tileIndex++;
            }
        }

        Debug.Log("[PlatformBridge] " + tiles.Count + " dalles generees.");
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