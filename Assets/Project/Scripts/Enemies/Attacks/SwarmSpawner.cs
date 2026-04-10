using UnityEngine;
public class SwarmSpawner : MonoBehaviour, IOnDeathBehavior
{
    [Header("Swarm Configuration")]
    public SwarmStats swarmStats;
    [HideInInspector]
    public SwarmController_AStar existingSwarm;

    void Awake()
    {
        existingSwarm = GetComponentInChildren<SwarmController_AStar>();
    }

    public void OnEnemyDeath(Vector3 deathPosition)
    {
        if (existingSwarm == null)
        {
            Debug.LogWarning($"SwarmSpawner on {gameObject.name}: no SwarmController_AStar found!");
            return;
        }

        Rigidbody swarmRb = existingSwarm.GetComponent<Rigidbody>();

        existingSwarm.transform.SetParent(null);

        if (swarmRb != null)
        {
            swarmRb.linearVelocity = Vector3.zero;
            swarmRb.useGravity = true;
        }

        existingSwarm.Initialize(swarmStats);
    }
}