using UnityEngine;

public class SwarmSpawner : MonoBehaviour, IOnDeathBehavior
{
    [Header("Swarm Configuration")]
    [Tooltip("Stats for the swarm to spawn")]
    public SwarmStats swarmStats;

    [Tooltip("Swarm prefab to instantiate")]
    public GameObject swarmPrefab;

    [Header("Spawn Settings")]
    [Tooltip("Offset from death position to spawn swarm")]
    public Vector3 spawnOffset = Vector3.up * 0.5f;

    [Header("Visual/Audio (Optional)")]
    [Tooltip("Explosion effect on death (optional)")]
    public GameObject explosionEffectPrefab;

    [Tooltip("Explosion sound (optional)")]
    public AudioClip explosionSound;

    public void OnEnemyDeath(Vector3 deathPosition)
    {
        if (swarmPrefab == null || swarmStats == null)
        {
            Debug.LogWarning($"SwarmSpawner on {gameObject.name}: Missing swarmPrefab or swarmStats!");
            return;
        }

        Vector3 spawnPosition = deathPosition + spawnOffset;

        if (explosionEffectPrefab != null)
        {
            Instantiate(explosionEffectPrefab, deathPosition, Quaternion.identity);
        }

        if (explosionSound != null)
        {
            AudioSource.PlayClipAtPoint(explosionSound, deathPosition);
        }

        GameObject swarmObject = Instantiate(swarmPrefab, spawnPosition, Quaternion.identity);

        // Try A* version first
        SwarmController_AStar swarmControllerAStar = swarmObject.GetComponent<SwarmController_AStar>();
        if (swarmControllerAStar != null)
        {
            swarmControllerAStar.Initialize(swarmStats);
            Debug.Log($"Swarm (A*) spawned at {spawnPosition} from Bloated death!");
            return;
        }

        // Fallback to NavMesh version
        SwarmController swarmController = swarmObject.GetComponent<SwarmController>();
        if (swarmController != null)
        {
            swarmController.Initialize(swarmStats);
            Debug.Log($"Swarm (NavMesh) spawned at {spawnPosition} from Bloated death!");
        }
        else
        {
            Debug.LogError($"SwarmSpawner: swarmPrefab is missing SwarmController or SwarmController_AStar component!");
        }
    }
}