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

        bool isChasing = (enemyAI.currentState == EnemyAI_AStar.State.Chasing
               || enemyAI.currentState == EnemyAI_AStar.State.Attacking)
               && !enemyAI.isDead;

        animator.SetBool("isChasing", isChasing);
    }

    void OnDisable()
    {
        if (animator != null)
            animator.SetBool("isChasing", false);
    }
}