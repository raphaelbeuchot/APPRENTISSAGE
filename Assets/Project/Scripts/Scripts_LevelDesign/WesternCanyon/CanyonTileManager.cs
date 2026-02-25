using UnityEngine;

public class CanyonTileManager : MonoBehaviour
{
    [Header("Rise Settings")]
    public float riseDelayMin = 0f;
    public float riseDelayMax = 1.5f;

    [Header("References")]
    public SentinelCycleManager sentinelCycleManager;

    private CanyonTile[] tiles;

    private void Start()
    {
        if (sentinelCycleManager == null)
            sentinelCycleManager = FindObjectOfType<SentinelCycleManager>();

        if (sentinelCycleManager == null)
            Debug.LogWarning("[CanyonTileManager] SentinelCycleManager non trouve.");

        tiles = FindObjectsOfType<CanyonTile>();
        Debug.Log("[CanyonTileManager] " + tiles.Length + " dalles trouvees.");
    }

    public void OnRedLight()
    {
        foreach (CanyonTile tile in tiles)
        {
            if (tile != null)
                tile.riseDelay = Random.Range(riseDelayMin, riseDelayMax);
            tile.TriggerRise();
        }
    }
}