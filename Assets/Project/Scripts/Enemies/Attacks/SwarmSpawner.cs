using UnityEngine;

/// <summary>
/// Component to spawn a swarm when the enemy dies (for Bloated zombies)
/// </summary>
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
        
        // Calculate spawn position
        Vector3 spawnPosition = deathPosition + spawnOffset;
        
        // Spawn explosion effect if available
        if (explosionEffectPrefab != null)
        {
            Instantiate(explosionEffectPrefab, deathPosition, Quaternion.identity);
        }
        
        // Play explosion sound if available
        if (explosionSound != null)
        {
            AudioSource.PlayClipAtPoint(explosionSound, deathPosition);
        }
        
        // Spawn the swarm
        GameObject swarmObject = Instantiate(swarmPrefab, spawnPosition, Quaternion.identity);
        SwarmController swarmController = swarmObject.GetComponent<SwarmController>();
        
        if (swarmController != null)
        {
            swarmController.Initialize(swarmStats);
            Debug.Log($"Swarm spawned at {spawnPosition} from Bloated death!");
        }
        else
        {
            Debug.LogError($"SwarmSpawner: swarmPrefab is missing SwarmController component!");
        }
    }
}
