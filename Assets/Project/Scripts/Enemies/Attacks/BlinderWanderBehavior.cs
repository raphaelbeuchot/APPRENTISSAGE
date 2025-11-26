using UnityEngine;
using Pathfinding; // AJOUTER pour AIPath
using System.Collections;

public class BlinderWanderBehavior : MonoBehaviour
{
    [Header("References")]
    public BlinderStats stats;
    private AIPath aiPath; // CHANGEMENT: NavMeshAgent -> AIPath
    private bool isWandering = false;
    private Coroutine wanderCoroutine;

    void Start()
    {
        aiPath = GetComponent<AIPath>(); // CHANGEMENT: GetComponent<AIPath>()
        if (aiPath == null)
        {
            Debug.LogError("BlinderWanderBehavior needs AIPath!"); // CHANGEMENT: message erreur
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

        // CHANGEMENT: null check + AIPath n'a pas isStopped
        if (aiPath != null)
        {
            aiPath.canMove = false; // CHANGEMENT: utiliser canMove
        }
    }

    IEnumerator WanderPattern()
    {
        while (isWandering)
        {
            // CHANGEMENT: A* utilise des positions directes, pas NavMesh.SamplePosition
            Vector3 randomDirection = Random.insideUnitSphere * stats.wanderRadius;
            randomDirection += transform.position;
            randomDirection.y = transform.position.y; // Garder Y constant

            // CHANGEMENT: Vérifier si point est walkable via A* graph
            var gg = AstarPath.active.data.gridGraph;
            if (gg != null)
            {
                var node = gg.GetNearest(randomDirection).node;
                if (node != null && node.Walkable)
                {
                    aiPath.canMove = true; // CHANGEMENT
                    aiPath.maxSpeed = stats.wanderSpeed; // CHANGEMENT: speed -> maxSpeed
                    aiPath.destination = (Vector3)node.position; // CHANGEMENT: SetDestination -> destination

                    // 2. Marcher pendant walkDuration
                    yield return new WaitForSeconds(stats.walkDuration);

                    // 3. S'arrêter (animation "chasse mouches")
                    aiPath.canMove = false; // CHANGEMENT

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
            else
            {
                yield return new WaitForSeconds(0.5f);
            }
        }
    }

    public bool IsWandering() => isWandering;
}