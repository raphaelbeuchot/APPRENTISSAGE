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

        GrabAttack grabAttack = enemyAI.GetComponent<GrabAttack>();
        bool isInBourrade = grabAttack != null && grabAttack.IsInBourrade();

        bool isChasing = enemyAI.enabled
    && (enemyAI.currentState == EnemyAI_AStar.State.Chasing
    || enemyAI.currentState == EnemyAI_AStar.State.Attacking
    || (enemyAI.currentState == EnemyAI_AStar.State.OnIslandPlatform
        && enemyAI.targetHuman != null
        && enemyAI.GetComponent<Rigidbody>().linearVelocity.magnitude > 0.1f))
    && !enemyAI.isDead
    && !isInBourrade;
        animator.SetBool("isChasing", isChasing);
    }

    void OnDisable()
    {
        if (animator != null)
            animator.SetBool("isChasing", false);
    }
}