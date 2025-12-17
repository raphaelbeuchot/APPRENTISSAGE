using UnityEngine;
using System.Collections;
using Pathfinding;

[RequireComponent(typeof(Rigidbody))]
public class ChargeAttack : MonoBehaviour, IAttackBehavior
{
    [Header("References")]
    public EnemyStats stats;
    private EnemyAI_AStar enemyAI;
    private BlinderWanderBehavior wanderBehavior;
    private AIPath aiPath;
    private Rigidbody rb;
    private Transform player;

    private float originalLinearDamping = 5f;
    private float originalAngularDamping = 5f;

    // States
    private bool isCharging = false;
    private bool isStraightRunning = false;
    private Vector3 chargeTargetPosition;

    // Tunables (internal, safe defaults)
    private const float accelerateDuration = 0.7f;
    private const float straightDuration = 1.5f;
    private const float postFinishDelay = 0.3f;
    private const float knockAiDisableDuration = 1.5f;
    private const float zombiePushForceMultiplier = 1.0f;

    void Start()
    {
        if (stats == null)
        {
            Debug.LogError($"ChargeAttack on {gameObject.name}: EnemyStats not assigned!"); return;
        }
        // Vérifier que c'est bien un Blinder
        if (stats.attackType != EnemyStats.AttackType.Blinder)
        {
            Debug.LogWarning($"ChargeAttack on {gameObject.name}: EnemyStats attackType should be Blinder!");
        }
        enemyAI = GetComponent<EnemyAI_AStar>();
        wanderBehavior = GetComponent<BlinderWanderBehavior>();
        aiPath = GetComponent<AIPath>();
        rb = GetComponent<Rigidbody>();

        // Sauvegarder les valeurs originales
        if (rb != null)
        {
            originalLinearDamping = rb.linearDamping;
            originalAngularDamping = rb.angularDamping;
        }

        player = GameObject.FindGameObjectWithTag("Player")?.transform;

        if (rb == null)
            Debug.LogError("ChargeAttack requires a Rigidbody on the same GameObject.");

        MeleeAudioManager.OnMeleeHit += OnMeleeHitHeard;
    }

    void OnDestroy()
    {
        MeleeAudioManager.OnMeleeHit -= OnMeleeHitHeard;
    }

    void OnMeleeHitHeard(Vector3 soundPosition)
    {
        Debug.Log($"BLINDER HEARD MELEE at {soundPosition}, distance: {Vector3.Distance(transform.position, soundPosition)}");

        // Ignorer si le son est sur nous
        float distToSound = Vector3.Distance(transform.position, soundPosition);
        if (distToSound < 1f)
        {
            Debug.Log("BLINDER: Sound is on me, ignoring (OnDirectHit handles it)");
            return;
        }

        if (isCharging || isStraightRunning) return;

        if (distToSound > stats.audioDetectionRange) return;

        Debug.Log($"BLINDER STARTING CHARGE!");
        chargeTargetPosition = soundPosition;
        StartCharge();
    }

    public void StartChargePublic(Vector3 targetPos)
    {
        if (isCharging || isStraightRunning) return;
        chargeTargetPosition = targetPos;
        StartCharge();
    }

    void StartCharge()
    {
        if (isCharging || isStraightRunning) return;

        isCharging = true;

        if (enemyAI != null) enemyAI.enabled = false;
        if (wanderBehavior != null && wanderBehavior.IsWandering()) wanderBehavior.StopWandering();

        if (aiPath != null)
        {
            aiPath.canMove = false;
        }

        if (rb != null)
        {
            rb.isKinematic = false;
            rb.linearDamping = 0f;
            rb.angularDamping = 0f;
        }

        StartCoroutine(AccelerateAndCharge());
    }

    IEnumerator AccelerateAndCharge()
    {
        Vector3 dir = (chargeTargetPosition - transform.position);
        dir.y = 0;
        if (dir.sqrMagnitude < 0.01f)
            dir = transform.forward;
        else
            dir.Normalize();

        Vector3 lockedDirection = dir;

        Debug.Log("Blinder charging, locked direction: " + lockedDirection);

        // Phase de rotation progressive (0.3s)
        float rotationDuration = 0.3f;
        float rotationElapsed = 0f;
        Quaternion startRotation = transform.rotation;
        Quaternion targetRotation = Quaternion.LookRotation(lockedDirection);

        while (rotationElapsed < rotationDuration)
        {
            rotationElapsed += Time.deltaTime;
            float t = rotationElapsed / rotationDuration;
            transform.rotation = Quaternion.Slerp(startRotation, targetRotation, t);
            yield return null;
        }

        // Force rotation finale
        transform.rotation = targetRotation;

        // Acceleration phase
        float elapsed = 0f;
        while (elapsed < accelerateDuration)
        {
            transform.rotation = Quaternion.LookRotation(lockedDirection);

            float t = elapsed / accelerateDuration;
            float speed = Mathf.Lerp(stats.blinderWanderSpeed, stats.blinderChargeSpeed, t);
            if (rb != null)
            {
                Vector3 vel = lockedDirection * speed;
                vel.y = Mathf.Min(rb.linearVelocity.y, 0f);
                rb.linearVelocity = vel;
            }
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (rb != null)
        {
            Vector3 vel = lockedDirection * stats.blinderChargeSpeed;
            vel.y = Mathf.Min(rb.linearVelocity.y, 0f);
            rb.linearVelocity = vel;
        }

        yield return StartCoroutine(ContinueStraight(lockedDirection));
    }

    IEnumerator ContinueStraight(Vector3 forwardDir)
    {
        if (!isCharging) yield break;

        isCharging = false;
        isStraightRunning = true;

        float elapsed = 0f;
        float maxDuration = 1.5f;
        bool stopTriggered = false;

        while (!stopTriggered)
        {
            transform.rotation = Quaternion.LookRotation(forwardDir);

            if (rb != null)
            {
                Vector3 vel = forwardDir * stats.blinderChargeSpeed;
                vel.y = Mathf.Min(rb.linearVelocity.y, 0f);
                rb.linearVelocity = vel;
            }

            elapsed += Time.deltaTime;

            float distToSound = Vector3.Distance(transform.position, chargeTargetPosition);
            if (distToSound < 0.5f)
            {
                stopTriggered = true;
            }

            if (elapsed >= maxDuration)
            {
                stopTriggered = true;
            }

            yield return null;
        }

        float decelDuration = 1f;
        float decelElapsed = 0f;
        Vector3 initialVelocity = rb != null ? rb.linearVelocity : Vector3.zero;

        while (decelElapsed < decelDuration)
        {
            decelElapsed += Time.deltaTime;
            float t = decelElapsed / decelDuration;
            if (rb != null)
                rb.linearVelocity = Vector3.Lerp(initialVelocity, Vector3.zero, t);
            yield return null;
        }

        if (rb != null)
            rb.linearVelocity = Vector3.zero;

        FinishCharge();
    }

    void FinishCharge()
    {
        isStraightRunning = false;
        isCharging = false;

        // Restaurer le damping
        if (rb != null)
        {
            rb.linearDamping = originalLinearDamping;
            rb.angularDamping = originalAngularDamping;
        }

        if (aiPath != null)
        {
            aiPath.canMove = true;
        }

        if (enemyAI != null) enemyAI.enabled = true;
        if (wanderBehavior != null) wanderBehavior.StartWandering();
    }

    void OnCollisionEnter(Collision collision)
    {
        if (!isStraightRunning && !isCharging) return;

        GameObject other = collision.gameObject;

        if (other.CompareTag("Player"))
        {
            PlayerPhysicsMovement pm = other.GetComponent<PlayerPhysicsMovement>();

            Vector3 pushDir = (other.transform.position - transform.position).normalized;
            pushDir.y = 0;

            if (pm != null)
            {
                Vector3 knockbackVel = pushDir * stats.blinderKnockbackForce;
                knockbackVel.y = 0;
                pm.ApplyKnockback(knockbackVel, 0.3f);
            }

            if (isCharging || isStraightRunning)
            {
                StopAllCoroutines();
                StartCoroutine(SlowDownAndStop());
            }
        }

        if (other.layer == LayerMask.NameToLayer("Zombie"))
        {
            Rigidbody otherRb = other.GetComponent<Rigidbody>();

            Vector3 pushDir = (other.transform.position - transform.position).normalized;
            pushDir.y = 0;

            if (otherRb != null)
            {
                otherRb.AddForce(pushDir * stats.blinderKnockbackForce, ForceMode.VelocityChange);

                if (rb != null)
                {
                    Vector3 vel = transform.forward * stats.blinderChargeSpeed;
                    vel.y = Mathf.Min(rb.linearVelocity.y, 0f);
                    rb.linearVelocity = vel;
                }
            }
        }

        int envLayer = LayerMask.NameToLayer("Environment");
        int defaultLayer = LayerMask.NameToLayer("Default");
        if (other.layer == envLayer || other.layer == defaultLayer)
        {
            StopAllCoroutines();
            if (rb != null) rb.linearVelocity = Vector3.zero;
            FinishCharge();
        }
    }

    public void ApplyBourrade(Vector3 bourradeDirection, float bourradeForce, float bourradeDuration)
    {
        StartCoroutine(BlinderBourradeCoroutine(bourradeDirection, bourradeForce, bourradeDuration));
    }

    IEnumerator SlowDownAndStop()
    {
        isCharging = false;
        isStraightRunning = false;

        float decelDuration = 0.8f;
        float elapsed = 0f;
        Vector3 initialVelocity = rb != null ? rb.linearVelocity : Vector3.zero;

        while (elapsed < decelDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / decelDuration;
            if (rb != null)
            {
                Vector3 vel = Vector3.Lerp(initialVelocity, Vector3.zero, t);
                vel.y = Mathf.Min(rb.linearVelocity.y, 0f);
                rb.linearVelocity = vel;
            }
            yield return null;
        }

        if (rb != null) rb.linearVelocity = Vector3.zero;

        FinishCharge();
    }

    IEnumerator BlinderBourradeCoroutine(Vector3 direction, float force, float duration)
    {
        if (isCharging || isStraightRunning)
        {
            StopAllCoroutines();
            isCharging = false;
            isStraightRunning = false;
        }

        if (enemyAI != null) enemyAI.enabled = false;
        if (aiPath != null)
        {
            aiPath.canMove = false;
        }

        if (rb != null)
        {
            Vector3 vel = direction * force;
            vel.y = 0;
            rb.linearVelocity = vel;
        }

        yield return new WaitForSeconds(duration);

        if (rb != null) rb.linearVelocity = Vector3.zero;

        if (aiPath != null)
        {
            aiPath.canMove = true;
        }
        if (enemyAI != null) enemyAI.enabled = true;

        Debug.Log("Blinder bourrade ended");
    }

    public void OnDirectHit(Vector3 hitSourcePosition)
    {
        Debug.Log("Blinder hit by melee! Will charge after knockback...");

        if (isCharging || isStraightRunning)
        {
            StopAllCoroutines();
            isCharging = false;
            isStraightRunning = false;

            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                // Restaurer damping si charge annulee
                rb.linearDamping = originalLinearDamping;
                rb.angularDamping = originalAngularDamping;
            }
        }

        if (aiPath != null && !aiPath.canMove)
        {
            aiPath.canMove = true;
        }

        chargeTargetPosition = hitSourcePosition;

        StartCoroutine(RechargeAfterKnockback());
    }

    IEnumerator RechargeAfterKnockback()
    {
        yield return new WaitForSeconds(stats.rechargeDelayAfterHit);

        Debug.Log("Blinder re-charging toward attacker!");
        StartCharge();
    }

    public void Initialize(EnemyStats enemyStats, PlayerStats playerStats, Transform enemyTransform, Rigidbody enemyRigidbody)
    {
    }

    public void AttemptAttack(GameObject target)
    {
        if (target != null)
            StartChargePublic(target.transform.position);
    }

    public bool CanAttack()
    {
        return !isCharging && !isStraightRunning;
    }

    public bool IsAttacking()
    {
        return isCharging || isStraightRunning;
    }

    public bool IsCharging()
    {
        return isCharging || isStraightRunning;
    }

    public bool IsInSpecialState()
    {
        return isCharging || isStraightRunning;
    }

    public void ForceStop()
    {
        StopAllCoroutines();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.linearDamping = originalLinearDamping;
            rb.angularDamping = originalAngularDamping;
        }
        isCharging = false;
        isStraightRunning = false;

        if (aiPath != null)
        {
            aiPath.canMove = true;
        }

        if (enemyAI != null) enemyAI.enabled = true;
        if (wanderBehavior != null) wanderBehavior.StartWandering();
    }
}