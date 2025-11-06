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
    private AudioSource audioSource;

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
        
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

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
        try
        {
            lastAttackTime = Time.time;
            StartCoroutine(PulseScale());
            yield return new WaitForSeconds(0.1f);
            DetectAndHitTargets();
            yield return new WaitForSeconds(stats.attackDuration - 0.1f);
            currentArm = (currentArm == AttackArm.Left) ? AttackArm.Right : AttackArm.Left;
        }
        finally
        {
            isAttacking = false;
        }
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

        // Retrecir
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
        Debug.Log("MELEE ATTACK!");

        Collider[] hits = Physics.OverlapSphere(
            transform.position + Vector3.up * 1f,
            stats.attackRange,
            LayerMask.GetMask("Zombie", "Swarm")
        );

        bool hitSomething = false;

        foreach (Collider hit in hits)
        {
            if (hit.gameObject == gameObject) continue;

            // === NOUVEAU : VERIFIER L'ANGLE ===
            Vector3 directionToTarget = (hit.transform.position - transform.position).normalized;
            float angleToTarget = Vector3.Angle(transform.forward, directionToTarget);

            // Si l'angle est trop grand, skip cette cible
            if (angleToTarget > stats.attackAngle / 2f)
            {
                continue;
            }
            // ===================================

            // === GESTION ZOMBIES ===
            EnemyHealth enemyHealth = hit.GetComponent<EnemyHealth>();
            if (enemyHealth != null)
            {
                if (enemyHealth.IsDead()) continue;

                hitSomething = true;

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
                float damage = stats.GetAdjustedDamage();
                enemyHealth.TakeMeleeDamage(damage);

                // Notifier les Blinders (audio global)
                MeleeAudioManager.TriggerMeleeHit(hit.transform.position);

                // NOUVEAU : Check si c'est un Blinder qui se fait taper directement
                ChargeAttack chargeAttack = hit.GetComponent<ChargeAttack>();
                if (chargeAttack != null)
                {
                    // Blinder frappe : il charge vers le player
                    chargeAttack.OnDirectHit(transform.position);
                }
                else
                {
                    // Zombie normal : knockdown
                    StartCoroutine(KnockdownTarget(hit.gameObject));
                }

                Debug.Log($"{gameObject.name} hit {hit.gameObject.name} for {damage} damage!");
            }

            // === GESTION NUEES ===
            SwarmController swarm = hit.GetComponent<SwarmController>();
            if (swarm != null)
            {
                hitSomething = true;
                float damage = stats.GetAdjustedDamage();
                swarm.TakeDamage(damage);
                Debug.Log($"{gameObject.name} hit swarm for {damage} damage!");
            }
        }

        // Jouer le bon son
        if (audioSource != null)
        {
            if (hitSomething && stats.attackHitSound != null)
            {
                audioSource.PlayOneShot(stats.attackHitSound);
            }
            else if (stats.attackSound != null)
            {
                audioSource.PlayOneShot(stats.attackSound);
            }
        }
    }

    IEnumerator KnockdownTarget(GameObject target)
    {
        // NOUVEAU : Skip knockdown si Blinder
        ChargeAttack chargeAttack = target.GetComponent<ChargeAttack>();
        if (chargeAttack != null)
        {
            yield break; // Pas de knockdown pour Blinder
        }

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

        Vector3 position = transform.position + Vector3.up * 1f;

        // Sphere de portée
        Gizmos.color = new Color(1f, 1f, 0f, 0.3f);
        Gizmos.DrawWireSphere(position, stats.attackRange);

        // Cône d'attaque
        Gizmos.color = Color.red;
        Vector3 forward = transform.forward;

        // Ligne centrale
        Gizmos.DrawRay(position, forward * stats.attackRange);

        // Bords du cône
        float halfAngle = stats.attackAngle / 2f;
        Vector3 leftBoundary = Quaternion.Euler(0, -halfAngle, 0) * forward;
        Vector3 rightBoundary = Quaternion.Euler(0, halfAngle, 0) * forward;

        Gizmos.DrawRay(position, leftBoundary * stats.attackRange);
        Gizmos.DrawRay(position, rightBoundary * stats.attackRange);

        // Arc pour visualiser le cône
        int segments = 20;
        Vector3 previousPoint = position + leftBoundary * stats.attackRange;
        for (int i = 1; i <= segments; i++)
        {
            float angle = Mathf.Lerp(-halfAngle, halfAngle, i / (float)segments);
            Vector3 direction = Quaternion.Euler(0, angle, 0) * forward;
            Vector3 point = position + direction * stats.attackRange;
            Gizmos.DrawLine(previousPoint, point);
            previousPoint = point;
        }
    }
}