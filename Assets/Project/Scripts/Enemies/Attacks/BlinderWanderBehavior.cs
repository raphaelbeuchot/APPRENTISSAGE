using UnityEngine;
using UnityEngine.AI;
using System.Collections;

public class BlinderWanderBehavior : MonoBehaviour
{
    [Header("References")]
    public BlinderStats stats;
    private NavMeshAgent agent;

    private bool isWandering = false;
    private Coroutine wanderCoroutine;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        if (agent == null)
        {
            Debug.LogError("BlinderWanderBehavior needs NavMeshAgent!");
            return;
        }
    }

    public void StartWandering()
    {
        if (isWandering) return;

        isWandering = true;
        wanderCoroutine = StartCoroutine(WanderPattern());
    }

    public void StopWandering()
    {
        isWandering = false;

        if (wanderCoroutine != null)
        {
            StopCoroutine(wanderCoroutine);
            wanderCoroutine = null;
        }

        agent.isStopped = true;
    }

    IEnumerator WanderPattern()
    {
        while (isWandering)
        {
            // 1. Choisir destination random
            Vector3 randomDirection = Random.insideUnitSphere * stats.wanderRadius;
            randomDirection += transform.position;

            NavMeshHit hit;
            if (NavMesh.SamplePosition(randomDirection, out hit, stats.wanderRadius, NavMesh.AllAreas))
            {
                agent.isStopped = false;
                agent.speed = stats.wanderSpeed;
                agent.SetDestination(hit.position);

                // 2. Marcher pendant walkDuration
                yield return new WaitForSeconds(stats.walkDuration);

                // 3. S'arrêter (animation "chasse mouches")
                agent.isStopped = true;

                // TODO: Trigger animation "swat flies" ici
                Debug.Log("Blinder swatting flies...");

                yield return new WaitForSeconds(stats.stopDuration);
            }
            else
            {
                // Si pas de point valide trouvé, attendre un peu
                yield return new WaitForSeconds(0.5f);
            }
        }
    }

    public bool IsWandering() => isWandering;
}