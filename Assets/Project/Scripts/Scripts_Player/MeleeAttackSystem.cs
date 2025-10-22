using UnityEngine;
using System.Collections;

public class MeleeAttackSystem : MonoBehaviour
{
    [Header("Attack Settings")]
    [SerializeField] private float attackRange = 2f;
    [SerializeField] private float attackKnockbackForce = 35f;
    [SerializeField] private float attackDuration = 0.3f;
    [SerializeField] private float attackCooldown = 0.5f; // Petit délai entre les attaques

    [Header("Target Knockdown")]
    [SerializeField] private float knockdownDuration = 3f;

    [Header("Visual Feedback")]
    [SerializeField] private float armRotationAngle = 90f;

    private Rigidbody rb;
    private HumanHealth health;
    private PlayerPhysicsMovement movement;

    private bool isAttacking = false;
    private float lastAttackTime = 0f;

    private enum AttackArm { Left, Right }
    private AttackArm currentArm = AttackArm.Left;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        health = GetComponent<HumanHealth>();
        movement = GetComponent<PlayerPhysicsMovement>();
    }

    void OnDisable()
    {
        // Réactiver le movement si le script est désactivé
        if (movement != null && !movement.enabled)
        {
            movement.enabled = true;
        }
        isAttacking = false;
    }

    void Update()
    {
        // TOUJOURS avec clic gauche
        if (Input.GetMouseButtonDown(0) && CanAttack())
        {
            StartCoroutine(PerformAttack());
        }
    }

    bool CanAttack()
    {
        // Vérifier le cooldown
        if (Time.time - lastAttackTime < attackCooldown)
        {
            return false;
        }

        if (isAttacking)
        {
            Debug.Log("Cannot attack: already attacking");
            return false;
        }

        if (health != null && health.IsDead())
        {
            Debug.Log("Cannot attack: player is dead");
            return false;
        }

        if (movement != null && !movement.enabled)
        {
            Debug.Log("Cannot attack: movement disabled, trying to re-enable...");
            movement.enabled = true;
        }

        return true;
    }

    IEnumerator PerformAttack()
    {
        isAttacking = true;
        lastAttackTime = Time.time;

        if (movement != null) movement.enabled = false;

        StartCoroutine(RotateArmVisual());

        yield return new WaitForSeconds(0.1f);
        DetectAndHitTargets();

        yield return new WaitForSeconds(attackDuration - 0.1f);

        // Alterner le bras
        currentArm = (currentArm == AttackArm.Left) ? AttackArm.Right : AttackArm.Left;

        isAttacking = false;

        // Réactiver le mouvement
        if (movement != null) movement.enabled = true;
    }

    IEnumerator RotateArmVisual()
    {
        float elapsed = 0f;
        float rotationDirection = (currentArm == AttackArm.Left) ? -1f : 1f;
        Quaternion startRotation = transform.rotation;
        Quaternion targetRotation = startRotation * Quaternion.Euler(0, rotationDirection * armRotationAngle, 0);

        while (elapsed < attackDuration / 2f)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / (attackDuration / 2f);
            transform.rotation = Quaternion.Slerp(startRotation, targetRotation, t);
            yield return null;
        }

        elapsed = 0f;
        while (elapsed < attackDuration / 2f)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / (attackDuration / 2f);
            transform.rotation = Quaternion.Slerp(targetRotation, startRotation, t);
            yield return null;
        }

        transform.rotation = startRotation;
    }

    void DetectAndHitTargets()
    {
        Collider[] hits = Physics.OverlapSphere(
            transform.position,
            attackRange,
            LayerMask.GetMask("Zombie")
        );

        foreach (Collider hit in hits)
        {
            if (hit.gameObject == gameObject) continue;

            Vector3 directionToTarget = (hit.transform.position - transform.position).normalized;
            float angleToTarget = Vector3.Angle(transform.forward, directionToTarget);

            if (angleToTarget <= 90f)
            {
                Rigidbody targetRb = hit.GetComponent<Rigidbody>();

                if (targetRb != null)
                {
                    Vector3 knockbackDir = (hit.transform.position - transform.position).normalized;
                    knockbackDir.y = 0;

                    targetRb.AddForce(knockbackDir * attackKnockbackForce, ForceMode.Impulse);

                    StartCoroutine(KnockdownTarget(hit.gameObject));

                    // Si le zombie était en grab, le forcer à relâcher
                    ZombieGrabSystem grabSystem = hit.GetComponent<ZombieGrabSystem>();
                    if (grabSystem != null && grabSystem.IsGrabbing())
                    {
                        grabSystem.ForceRelease();
                        Debug.Log($"Forcé {hit.gameObject.name} à relâcher!");
                    }

                    Debug.Log($"{gameObject.name} hit {hit.gameObject.name}!");
                }
            }
        }
    }

    IEnumerator KnockdownTarget(GameObject target)
    {
        ZombieAI zombieAI = target.GetComponent<ZombieAI>();
        if (zombieAI != null)
        {
            zombieAI.enabled = false;
        }

        yield return new WaitForSeconds(knockdownDuration);

        if (zombieAI != null)
        {
            zombieAI.enabled = true;
        }
    }

    public bool IsAttacking() => isAttacking;

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}