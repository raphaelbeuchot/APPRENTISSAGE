using UnityEngine;

public class EnemyIdleVariant : MonoBehaviour
{
    [SerializeField] private RuntimeAnimatorController[] idleVariants;

    private void Start()
    {
        if (idleVariants == null || idleVariants.Length == 0)
            return;

        Animator animator = GetComponentInChildren<Animator>();
        if (animator == null)
            return;

        int randomIndex = Random.Range(0, idleVariants.Length);
        animator.runtimeAnimatorController = idleVariants[randomIndex];

        animator.Play("Idle", 0, Random.value);
    }
}