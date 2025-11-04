using UnityEngine;
using UnityEngine.AI;
using System.Collections;

[RequireComponent(typeof(Rigidbody))]
public class ChargeAttack : MonoBehaviour, IAttackBehavior
{
    [Header("References")]
    public BlinderStats stats;

    private EnemyAI enemyAI;
    private BlinderWanderBehavior wanderBehavior;
    private NavMeshAgent agent;
    private Rigidbody rb;
    private Transform player;

    // States
    private bool isCharging = false;
    private bool isStraightRunning = false;
    private Vector3 chargeTargetPosition;

    // Tunables (internal, safe defaults)
    private const float accelerateDuration = 0.7f;
    private const float straightDuration = 1.5f;
    private const float postFinishDelay = 0.3f;
    private const float knockAiDisableDuration = 1.5f;
    private const float zombiePushForceMultiplier = 1.0f; // multiplies stats.knockbackForce

    void Start()
    {
        if (stats == null)
        {
            Debug.LogError($"ChargeAttack on {gameObject.name}: BlinderStats not assigned!");
            return;
        }

        enemyAI = GetComponent<EnemyAI>();
        wanderBehavior = GetComponent<BlinderWanderBehavior>();
        agent = GetComponent<NavMeshAgent>();
        rb = GetComponent<Rigidbody>();
        player = GameObject.FindGameObjectWithTag("Player")?.transform;

        if (rb == null)
            Debug.LogError("ChargeAttack requires a Rigidbody on the same GameObject.");

        // Subscribe to melee audio event (if you use it)
        MeleeAudioManager.OnMeleeHit += OnMeleeHitHeard;
    }

    void OnDestroy()
    {
        MeleeAudioManager.OnMeleeHit -= OnMeleeHitHeard;
    }

    // Audio trigger: set the target position and start the charge
    void OnMeleeHitHeard(Vector3 soundPosition)
    {
        if (isCharging || isStraightRunning) return;

        float dist = Vector3.Distance(transform.position, soundPosition);
        if (dist > stats.audioDetectionRange) return;

        chargeTargetPosition = soundPosition;
        StartCharge();
    }

    // Public entry used by EnemyAI (if it wants to trigger the charge directly)
    public void StartChargePublic(Vector3 targetPos)
    {
        if (isCharging || isStraightRunning) return;
        chargeTargetPosition = targetPos;
        StartCharge();
    }

    // Start the charge sequence
    void StartCharge()
    {
        if (isCharging || isStraightRunning) return;

        isCharging = true;

        // Disable AI and wander
        if (enemyAI != null) enemyAI.enabled = false;
        if (wanderBehavior != null && wanderBehavior.IsWandering()) wanderBehavior.StopWandering();

        // Disable agent so NavMesh won't fight Rigidbody
        if (agent != null)
        {
            agent.isStopped = true;
            agent.enabled = false;
        }

        // Ensure RB is non-kinematic and under our control
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
        // Calculer direction UNE SEULE FOIS au debut
        Vector3 dir = (chargeTargetPosition - transform.position);
        dir.y = 0;
        if (dir.sqrMagnitude < 0.01f)
            dir = transform.forward;
        else
            dir.Normalize();

        // LOCK la direction
        Vector3 lockedDirection = dir;

        Debug.Log("Blinder charging, locked direction: " + lockedDirection);

        // Acceleration phase
        float elapsed = 0f;
        while (elapsed < accelerateDuration)
        {
            // Forcer rotation vers direction lockee
            transform.rotation = Quaternion.LookRotation(lockedDirection);

            float t = elapsed / accelerateDuration;
            float speed = Mathf.Lerp(stats.wanderSpeed, stats.chargeSpeed, t);
            if (rb != null)
            {
                Vector3 vel = lockedDirection * speed;
                vel.y = 0;
                rb.linearVelocity = vel;
            }
            elapsed += Time.deltaTime;
            yield return null;
        }

        // Full speed
        if (rb != null)
        {
            Vector3 vel = lockedDirection * stats.chargeSpeed;
            vel.y = 0;
            rb.linearVelocity = vel;
        }

        yield return StartCoroutine(ContinueStraight(lockedDirection));
    }

    IEnumerator ContinueStraight(Vector3 forwardDir)
    {
        if (!isCharging) yield break;

        isCharging = false;
        isStraightRunning = true;

        /*// Ignore collisions avec zombies pour ne pas d�vier
        int blinderLayer = gameObject.layer;
        int zombieLayer = LayerMask.NameToLayer("Zombie");
        if (zombieLayer >= 0) Physics.IgnoreLayerCollision(blinderLayer, zombieLayer, true);*/

        float elapsed = 0f;
        float maxDuration = 1.5f;
        bool stopTriggered = false;

        while (!stopTriggered)
        {
            // Forcer rotation
            transform.rotation = Quaternion.LookRotation(forwardDir);

            // Deplacer le Blinder
            if (rb != null)
            {
                Vector3 vel = forwardDir * stats.chargeSpeed;
                vel.y = 0;
                rb.linearVelocity = vel;
            }

            elapsed += Time.deltaTime;

            // Verifier si proche de l'origine du son
            float distToSound = Vector3.Distance(transform.position, chargeTargetPosition);
            if (distToSound < 0.5f)
            {
                stopTriggered = true;
            }

            // Stop si straightDuration max atteinte
            if (elapsed >= maxDuration)
            {
                stopTriggered = true;
            }

            yield return null;
        }

        // Commencer la d�c�l�ration jusqu'� l'arr�t
        float decelDuration = 1f; // temps de d�c�l�ration, ajustable
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

        /*// Restaurer collisions avec zombies
        if (zombieLayer >= 0) Physics.IgnoreLayerCollision(blinderLayer, zombieLayer, false);*/

        // Fin de charge
        FinishCharge();
    }


    void FinishCharge()
    {
        isStraightRunning = false;
        isCharging = false;

        // Re-enable agent & AI & wander
        if (agent != null)
        {
            agent.enabled = true;
            agent.isStopped = false;
        }

        if (enemyAI != null) enemyAI.enabled = true;
        if (wanderBehavior != null) wanderBehavior.StartWandering();
    }

    // Collision handling: push zombies and player, disable AI of hit zombies briefly
    void OnCollisionEnter(Collision collision)
    {
        if (!isStraightRunning && !isCharging) return;

        GameObject other = collision.gameObject;

        // Player
        if (other.CompareTag("Player"))
        {
            PlayerPhysicsMovement pm = other.GetComponent<PlayerPhysicsMovement>();

            Vector3 pushDir = (other.transform.position - transform.position).normalized;
            pushDir.y = 0;

            if (pm != null)
            {
                // Utiliser ApplyKnockback au lieu de AddForce
                Vector3 knockbackVel = pushDir * stats.knockbackForce;
                knockbackVel.y = 0;
                pm.ApplyKnockback(knockbackVel, 0.3f);
            }

            // NOUVEAU : Arreter la charge apres avoir touche le player
            if (isCharging || isStraightRunning)
            {
                StopAllCoroutines();
                StartCoroutine(SlowDownAndStop());
            }
        }

        // Other zombies
        if (other.layer == LayerMask.NameToLayer("Zombie"))
        {
            Rigidbody otherRb = other.GetComponent<Rigidbody>();
            EnemyAI otherAI = other.GetComponent<EnemyAI>();
            EnemyHealth otherHealth = other.GetComponent<EnemyHealth>();

            Vector3 pushDir = (other.transform.position - transform.position).normalized;
            pushDir.y = 0;

            if (otherRb != null)
            {
                // Même force que le player
                otherRb.AddForce(pushDir * stats.knockbackForce, ForceMode.VelocityChange);

                // Keep the Blinder moving forward (prevent bounce)
                if (rb != null)
                {
                    Vector3 vel = transform.forward * stats.chargeSpeed;
                    vel.y = 0;
                    rb.linearVelocity = vel;
                }
            }

            
        }

        // Walls / environment: stop the charge gracefully if needed
        int envLayer = LayerMask.NameToLayer("Environment");
        int defaultLayer = LayerMask.NameToLayer("Default");
        if (other.layer == envLayer || other.layer == defaultLayer)
        {
            // End the charge early on big obstacle
            StopAllCoroutines();
            if (rb != null) rb.linearVelocity = Vector3.zero;
            FinishCharge();
        }
    }

    // Appele par GrabAttack.EndGrab() pour les FGRB
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
                vel.y = 0;
                rb.linearVelocity = vel;
            }
            yield return null;
        }

        if (rb != null) rb.linearVelocity = Vector3.zero;

        FinishCharge();
    }
    IEnumerator BlinderBourradeCoroutine(Vector3 direction, float force, float duration)
    {
        // Arreter charge en cours
        if (isCharging || isStraightRunning)
        {
            StopAllCoroutines();
            isCharging = false;
            isStraightRunning = false;
        }

        // Desactiver AI temporairement
        if (enemyAI != null) enemyAI.enabled = false;
        if (agent != null)
        {
            agent.enabled = false;
        }

        // Appliquer bourrade
        if (rb != null)
        {
            Vector3 vel = direction * force;
            vel.y = 0;
            rb.linearVelocity = vel;
        }

        yield return new WaitForSeconds(duration);

        // Arreter
        if (rb != null) rb.linearVelocity = Vector3.zero;

        // Reactiver
        if (agent != null)
        {
            agent.enabled = true;
            agent.isStopped = false;
        }
        if (enemyAI != null) enemyAI.enabled = true;

        Debug.Log("Blinder bourrade ended");
    }
    // Appele quand le Blinder est frappe en melee
    public void OnDirectHit(Vector3 hitSourcePosition)
    {
        Debug.Log("Blinder hit by melee! Charging toward attacker!");

        // Si deja en charge, arreter et recharger vers nouvelle position
        if (isCharging || isStraightRunning)
        {
            StopAllCoroutines();
            isCharging = false;
            isStraightRunning = false;

            if (rb != null) rb.linearVelocity = Vector3.zero;
        }

        // Re-enable agent si necessaire
        if (agent != null && !agent.enabled)
        {
            agent.enabled = true;
        }

        // Demarrer nouvelle charge vers l'attaquant
        chargeTargetPosition = hitSourcePosition;
        StartCharge();
    }
    IEnumerator TemporaryDisableAI(EnemyAI ai, float duration)
    {
        if (ai == null) yield break;
        ai.enabled = false;
        yield return new WaitForSeconds(duration);
        if (ai != null) ai.enabled = true;
    }

    // =========================
    // IAttackBehavior interface
    // =========================
    public void Initialize(EnemyStats enemyStats, PlayerStats playerStats, Transform enemyTransform, Rigidbody enemyRigidbody)
    {
        // Not used for the Blinder, but required by interface
    }

    public void AttemptAttack(GameObject target)
    {
        // Blinder attack is triggered by sound. If AI wants to force it, we provide:
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

    public bool IsInSpecialState()
    {
        return isCharging || isStraightRunning;
    }

    public void ForceStop()
    {
        StopAllCoroutines();
        if (rb != null) rb.linearVelocity = Vector3.zero;
        isCharging = false;
        isStraightRunning = false;

        if (agent != null)
        {
            agent.enabled = true;
            agent.isStopped = false;
        }

        if (enemyAI != null) enemyAI.enabled = true;
        if (wanderBehavior != null) wanderBehavior.StartWandering();
    }
}
