using UnityEngine;

public class SwarmSpawner : MonoBehaviour, IOnDeathBehavior
{
    [Header("Swarm Configuration")]
    public SwarmStats swarmStats;
    public GameObject swarmPrefab;

    [Header("Spawn Settings")]
    public Vector3 spawnOffset = Vector3.up * 0.5f;

    [Header("Existing Swarm (optionnel - ex: tete du Bloat)")]
    [Tooltip("Si renseigne, utilise ce SwarmController existant au lieu d'instancier un prefab")]
    public SwarmController_AStar existingSwarm;

    public void OnEnemyDeath(Vector3 deathPosition)
    {
        if (swarmStats == null)
        {
            Debug.LogWarning($"SwarmSpawner on {gameObject.name}: Missing swarmStats!");
            return;
        }

        if (existingSwarm != null)
        {
            existingSwarm.transform.SetParent(null);
            existingSwarm.Initialize(swarmStats);
            return;
        }

        if (swarmPrefab == null)
        {
            Debug.LogWarning($"SwarmSpawner on {gameObject.name}: Missing swarmPrefab!");
            return;
        }

        Vector3 spawnPosition = deathPosition + spawnOffset;
        GameObject swarmObject = Instantiate(swarmPrefab, spawnPosition, Quaternion.identity);

        SwarmController_AStar swarmControllerAStar = swarmObject.GetComponent<SwarmController_AStar>();
        if (swarmControllerAStar != null)
        {
            swarmControllerAStar.Initialize(swarmStats);
            return;
        }

        SwarmController swarmController = swarmObject.GetComponent<SwarmController>();
        if (swarmController != null)
            swarmController.Initialize(swarmStats);
        else
            Debug.LogError($"SwarmSpawner: swarmPrefab is missing SwarmController or SwarmController_AStar!");
    }
}