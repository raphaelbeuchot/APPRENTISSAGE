using UnityEngine;
using System.Collections;

public class MeleeAttackSystem : MonoBehaviour
{
    [Header("Player Stats")]
    public PlayerStats stats; // Reference au ScriptableObject

    [Header("References")]
    private Rigidbody rb;
    private PlayerHealth health;
    private PlayerPhysicsMovement movement;

    // State runtime
    private bool isAttacking = false;
    private float lastAttackTime = 0f;

    // Visual
    private enum AttackArm { Left, Right }
    private AttackArm currentArm = AttackArm.Left;

    void Start()
    {
        if (stats == null)
        {
            Debug.LogError("PlayerStats non assigne sur " + gameObject.name);
            return;
        }

        rb = GetComponent<Rigidbody>();
        health = GetComponent<PlayerHealth>();
        movement = GetComponent<PlayerPhysicsMovement>();
    }

    void OnDisable()
    {
        if (movement != null && !movement.enabled)
        {
            movement.enabled = true;
        }
        isAttacking = false;
    }

    void Update()
    {
        if (stats == null) return;

        // TOUJOURS avec clic gauche
        if (Input.GetMouseButtonDown(0) && CanAttack())
        {
            StartCoroutine(PerformAttack());
        }
    }

    bool CanAttack()
    {
        // Verifier le cooldown (depuis stats)
        if (Time.time - lastAttackTime < stats.attackCooldown)
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

        // Desactiver le mouvement pendant l'attaque
        if (movement != null)
            movement.enabled = false;

        
        // Animation visuelle
        StartCoroutine(RotateArmVisual());
        

        // Delai avant le hit
        yield return new WaitForSeconds(0.1f);
        DetectAndHitTargets();

        // Attendre la fin de l'animation
        yield return new WaitForSeconds(stats.attackDuration - 0.1f);

        // Alterner le bras
        currentArm = (currentArm == AttackArm.Left) ? AttackArm.Right : AttackArm.Left;

        isAttacking = false;

        // Reactiver le mouvement
        if (movement != null)
            movement.enabled = true;
    }

    IEnumerator RotateArmVisual()
    {
        float elapsed = 0f;
        float rotationDirection = (currentArm == AttackArm.Left) ? -1f : 1f;
        Quaternion startRotation = transform.rotation;
        Quaternion targetRotation = startRotation * Quaternion.Euler(0, rotationDirection * 90f, 0);

        // Rotation vers l'avant
        while (elapsed < stats.attackDuration / 2f)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / (stats.attackDuration / 2f);
            transform.rotation = Quaternion.Slerp(startRotation, targetRotation, t);
            yield return null;
        }

        // Rotation retour
        elapsed = 0f;
        while (elapsed < stats.attackDuration / 2f)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / (stats.attackDuration / 2f);
            transform.rotation = Quaternion.Slerp(targetRotation, startRotation, t);
            yield return null;
        }

        transform.rotation = startRotation;
    }

    void DetectAndHitTargets()
    {
        Collider[] hits = Physics.OverlapSphere(
            transform.position,
            stats.attackRange, // Depuis stats !
            LayerMask.GetMask("Zombie")
        );

        foreach (Collider hit in hits)
        {
            if (hit.gameObject == gameObject) continue;

            // Verifier l'angle (90 degres devant)
            Vector3 directionToTarget = (hit.transform.position - transform.position).normalized;
            float angleToTarget = Vector3.Angle(transform.forward, directionToTarget);

            if (angleToTarget <= 90f)
            {
                // Appliquer knockback
                Rigidbody targetRb = hit.GetComponent<Rigidbody>();
                if (targetRb != null)
                {
                    Vector3 knockbackDir = (hit.transform.position - transform.position).normalized;
                    knockbackDir.y = 0;

                    // vitesse actuelle du zombie
                    Vector3 currentVel = targetRb.linearVelocity;
                    // vitesse souhaitée après le knockback (constante)
                    Vector3 desiredVel = knockbackDir * stats.knockbackForce;
                    // delta à appliquer
                    Vector3 velocityChange = desiredVel - currentVel;

                    // appliquer le knockback
                    targetRb.AddForce(velocityChange, ForceMode.VelocityChange);

                    /*
                    // Knockback depuis stats (avec force multiplier)
                    //float knockbackForce = stats.GetAdjustedKnockback();
                    targetRb.AddForce(knockbackDir * stats.knockbackForce, ForceMode.Impulse);
                    */
                }

                // Appliquer damage
                ZombieHealth zombieHealth = hit.GetComponent<ZombieHealth>();
                if (zombieHealth != null)
                {
                    float damage = stats.GetAdjustedDamage();
                    zombieHealth.TakeMeleeDamage(damage);
                }

                // Si le zombie etait en grab, le forcer a relacher
                ZombieGrabSystem grabSystem = hit.GetComponent<ZombieGrabSystem>();
                if (grabSystem != null && grabSystem.IsGrabbing())
                {
                    grabSystem.ForceRelease();
                    Debug.Log($"Force {hit.gameObject.name} a relacher!");
                }

                // Knockdown temporaire
                StartCoroutine(KnockdownTarget(hit.gameObject));

                Debug.Log($"{gameObject.name} hit {hit.gameObject.name} for {stats.GetAdjustedDamage()} damage!");
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

        // Duree du knockdown (tu peux la mettre dans stats si tu veux)
        yield return new WaitForSeconds(2f);

        if (zombieAI != null && target != null)
        {
            zombieAI.enabled = true;
        }
    }

    public bool IsAttacking() => isAttacking;

    void OnDrawGizmosSelected()
    {
        if (stats == null) return;

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, stats.attackRange);
    }
}