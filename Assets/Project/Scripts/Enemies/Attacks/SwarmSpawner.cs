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

        PulseNoise pulse = existingSwarm.GetComponent<PulseNoise>();
        if (pulse != null) pulse.enabled = false;

        existingSwarm.transform.SetParent(null);
        existingSwarm.transform.localScale = Vector3.one;

        if (pulse != null) pulse.ResetBaseScale();

        Rigidbody swarmRb = existingSwarm.GetComponent<Rigidbody>();
        if (swarmRb != null)
        {
            swarmRb.linearVelocity = Vector3.zero;
            swarmRb.useGravity = true;
        }

        existingSwarm.Initialize(swarmStats);
    }
}