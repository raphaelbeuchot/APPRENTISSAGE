using UnityEngine;
using Pathfinding; // AJOUTER pour AIPath
using System.Collections;

public class BlinderWanderBehavior : MonoBehaviour
{
    [Header("References")]
    public EnemyStats stats; private AIPath aiPath; 
    private bool isWandering = false;
    private Coroutine wanderCoroutine;

    void Start()
    {
        aiPath = GetComponent<AIPath>();
        if (aiPath == null)
        {
            Debug.LogError("BlinderWanderBehavior needs AIPath!");
            return;
        }

        if (stats == null)
        {
            Debug.LogError($"BlinderWanderBehavior on {gameObject.name}: EnemyStats not assigned!");
            return;
        }

        if (stats.attackType != EnemyStats.AttackType.Blinder)
        {
            Debug.LogWarning($"BlinderWanderBehavior on {gameObject.name}: EnemyStats attackType should be Blinder!");
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
            Vector3 randomDirection = Random.insideUnitSphere * stats.blinderWanderRadius;
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
                    aiPath.maxSpeed = stats.blinderWanderSpeed; // CHANGEMENT: speed -> maxSpeed
                    aiPath.destination = (Vector3)node.position; // CHANGEMENT: SetDestination -> destination

                    // 2. Marcher pendant walkDuration
                    yield return new WaitForSeconds(stats.blinderWalkDuration);

                    // 3. S'arrêter (animation "chasse mouches")
                    aiPath.canMove = false; // CHANGEMENT

                    // TODO: Trigger animation "swat flies" ici
                    Debug.Log("Blinder swatting flies...");
                    yield return new WaitForSeconds(stats.blinderStopDuration);
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