using UnityEngine;

public class EnemyAnimationController : MonoBehaviour
{
    private Animator animator;
    private EnemyAI_AStar enemyAI;

    void Start()
    {
        animator = GetComponent<Animator>();
        enemyAI = GetComponent<EnemyAI_AStar>();
    }

    void Update()
    {
        if (animator == null || enemyAI == null) return;

        bool isChasing = enemyAI.currentState == EnemyAI_AStar.State.Chasing
                      || enemyAI.currentState == EnemyAI_AStar.State.Attacking;

        animator.SetBool("isChasing", isChasing);
    }
}