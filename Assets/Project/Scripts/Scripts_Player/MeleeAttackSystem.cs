using UnityEngine;
using System.Collections;

public class MeleeAttackSystem : MonoBehaviour
{
    [Header("Player Stats")]
    public PlayerStats stats;


    [Header("References")]
    private Rigidbody rb;
    private PlayerHealth health;
    private PlayerPhysicsMovement movement;



    // State runtime
    private bool isAttacking = false;
    private float lastAttackTime = 0f;

    // Grab state tracking
    private bool isGrabbed = false;

    private Vector3 originalScale;


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

        // NOUVEAU : Sauvegarder le scale original
        originalScale = transform.localScale;
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

        // Melee attack sur clic gauche OU espace (sauf si grabbed)

        if (Input.GetKeyDown(KeyCode.Space) && CanAttack())
        {
            StartCoroutine(PerformAttack());
        }
    }

    bool CanAttack()
    {
        
        if (isGrabbed)
        {
            Debug.Log("Cannot attack: player is grabbed!");
            return false;
        }

        // Empecher attaque pendant recoil
        PlayerPhysicsMovement movement = GetComponent<PlayerPhysicsMovement>();
        if (movement != null && movement.grabState == PlayerPhysicsMovement.GrabState.Recoil)
        {
            return false;
        }

        if (Time.time - lastAttackTime < stats.attackCooldown)
        {
            return false;
        }

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
            Debug.Log("Cannot attack: movement disabled (probably grabbed)");
            return false;
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

        // Feedback visuel simple
        StartCoroutine(PulseScale());

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

    IEnumerator PulseScale()
    {
        Vector3 targetScale = originalScale * 1.2f;

        // Agrandir
        float elapsed = 0f;
        float duration = stats.attackDuration / 2f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            transform.localScale = Vector3.Lerp(originalScale, targetScale, t);
            yield return null;
        }

        // Rétrécir
        elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            transform.localScale = Vector3.Lerp(targetScale, originalScale, t);
            yield return null;
        }

        // Forcer le retour
        transform.localScale = originalScale;
    }

    void DetectAndHitTargets()
    {
        Debug.Log("MELEE ATTACK!"); // Feedback console

        Collider[] hits = Physics.OverlapSphere(
            transform.position,
            stats.attackRange,
            LayerMask.GetMask("Zombie")
        );

        foreach (Collider hit in hits)
        {
            if (hit.gameObject == gameObject) continue;

            ZombieHealth zombieHealth = hit.GetComponent<ZombieHealth>();
            if (zombieHealth != null && zombieHealth.IsDead()) continue;

            // Knockback
            Rigidbody targetRb = hit.GetComponent<Rigidbody>();
            if (targetRb != null)
            {
                Vector3 knockbackDir = (hit.transform.position - transform.position).normalized;
                knockbackDir.y = 0;

                Vector3 currentVel = targetRb.linearVelocity;
                Vector3 desiredVel = knockbackDir * stats.knockbackForce;
                Vector3 velocityChange = desiredVel - currentVel;

                targetRb.AddForce(velocityChange, ForceMode.VelocityChange);
            }

            // Degats
            if (zombieHealth != null)
            {
                float damage = stats.GetAdjustedDamage();
                zombieHealth.TakeMeleeDamage(damage);
            }

            // Knockdown
            StartCoroutine(KnockdownTarget(hit.gameObject));

            Debug.Log($"{gameObject.name} hit {hit.gameObject.name} for {stats.GetAdjustedDamage()} damage!");
        }
    }

    IEnumerator KnockdownTarget(GameObject target)
    {
        EnemyAI zombieAI = target.GetComponent<EnemyAI>();
        if (zombieAI != null)
        {
            zombieAI.enabled = false;
        }

        yield return new WaitForSeconds(2f);

        if (zombieAI != null && target != null)
        {
            zombieAI.enabled = true;
        }
    }

    // ============================================
    // GESTION DU GRAB
    // ============================================

    public void OnGrabStart()
    {
        isGrabbed = true;

        if (isAttacking)
        {
            StopAllCoroutines();
            isAttacking = false;
            // NOUVEAU : Forcer le retour au scale original
            transform.localScale = originalScale;
        }

        Debug.Log("MeleeAttackSystem: Player grabbed, attacks disabled");
    }

    public void OnGrabEnd()
    {
        isGrabbed = false;
        Debug.Log("MeleeAttackSystem: Player released, attacks enabled");
    }

    public bool IsAttacking() => isAttacking;
    public bool IsGrabbed() => isGrabbed;

    void OnDrawGizmosSelected()
    {
        if (stats == null) return;

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, stats.attackRange);
    }
}