using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Rigidbody))]
public class ZombieAI : MonoBehaviour
{
    [Header("Detection")]
    [SerializeField] private float detectionRadius = 8f;
    [SerializeField] private float detectionCheckInterval = 0.3f;
    [SerializeField] private LayerMask humanLayer;

    [Header("Movement")]
    [SerializeField] private float walkSpeed = 1.5f;
    [SerializeField] private float chaseSpeed = 2.5f;
    [SerializeField] private float rotationSpeed = 3f;

    [Header("Attack")]
    [SerializeField] private float attackRange = 1.5f;
    [SerializeField] private float attackCooldown = 2f;

    [Header("Behavior")]
    [SerializeField] private float idleWanderChance = 0.2f;
    [SerializeField] private float wanderInterval = 3f;
    [SerializeField] private float wanderDuration = 1f;

    private Rigidbody rb;
    private ZombieHealth health;
    private ZombieGrabSystem grabSystem;

    private Transform targetHuman;
    private Vector3 wanderDirection;
    private float lastAttackTime = 0f;
    private float lastWanderTime = 0f;
    private float wanderTimer = 0f;
    private bool isStunnedByShot = false;
    private float stunnedUntilTime = 0f;

    private enum State { Idle, Wandering, Chasing, Attacking }
    private State currentState = State.Idle;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        health = GetComponent<ZombieHealth>();
        grabSystem = GetComponent<ZombieGrabSystem>();

        detectionRadius += Random.Range(-1f, 1f);
        walkSpeed += Random.Range(-0.3f, 0.3f);
        chaseSpeed += Random.Range(-0.5f, 0.5f);

        StartCoroutine(DetectionLoop());
    }

    void Update()
    {
        if (health != null && health.IsDead())
        {
            StopMovement();
            return;
        }

        if (isStunnedByShot && Time.time >= stunnedUntilTime)
        {
            isStunnedByShot = false;
        }

        if (isStunnedByShot)
        {
            StopMovement();
            return;
        }

        // MODIFICATION 1: Ne pas traiter les états si le zombie est en train de grab
        if (grabSystem != null && grabSystem.IsGrabbing())
        {
            StopMovement();
            return;
        }

        switch (currentState)
        {
            case State.Idle:
                HandleIdleState();
                break;
            case State.Wandering:
                HandleWanderingState();
                break;
            case State.Chasing:
                HandleChasingState();
                break;
            case State.Attacking:
                HandleAttackingState();
                break;
        }
    }

    IEnumerator DetectionLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(detectionCheckInterval);

            if (health != null && health.IsDead()) continue;
            if (isStunnedByShot) continue;

            // MODIFICATION 2: Ne pas détecter pendant le grab
            if (grabSystem != null && grabSystem.IsGrabbing()) continue;

            DetectHumans();
        }
    }

    void DetectHumans()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, detectionRadius, humanLayer);

        Transform closestHuman = null;
        float closestDistance = Mathf.Infinity;

        foreach (Collider hit in hits)
        {
            HumanHealth humanHealth = hit.GetComponent<HumanHealth>();
            if (humanHealth != null && !humanHealth.IsDead())
            {
                float distance = Vector3.Distance(transform.position, hit.transform.position);

                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestHuman = hit.transform;
                }
            }
        }

        if (closestHuman != null)
        {
            targetHuman = closestHuman;

            if (closestDistance <= attackRange)
            {
                currentState = State.Attacking;
            }
            else
            {
                currentState = State.Chasing;
            }
        }
        else
        {
            targetHuman = null;
            if (currentState == State.Chasing || currentState == State.Attacking)
            {
                currentState = State.Idle;
            }
        }
    }

    void HandleIdleState()
    {
        StopMovement();

        if (Time.time - lastWanderTime >= wanderInterval)
        {
            lastWanderTime = Time.time;

            if (Random.value < idleWanderChance)
            {
                StartWandering();
            }
        }
    }

    void HandleWanderingState()
    {
        wanderTimer += Time.deltaTime;

        if (wanderTimer >= wanderDuration)
        {
            currentState = State.Idle;
            wanderTimer = 0f;
            StopMovement();
            return;
        }

        MoveInDirection(wanderDirection, walkSpeed);
    }

    void HandleChasingState()
    {
        if (targetHuman == null)
        {
            currentState = State.Idle;
            return;
        }

        float distance = Vector3.Distance(transform.position, targetHuman.position);

        if (distance <= attackRange)
        {
            currentState = State.Attacking;
            return;
        }

        Vector3 direction = (targetHuman.position - transform.position).normalized;
        MoveInDirection(direction, GetCurrentSpeed());
    }

    void HandleAttackingState()
    {
        if (targetHuman == null)
        {
            currentState = State.Idle;
            return;
        }

        float distance = Vector3.Distance(transform.position, targetHuman.position);

        if (distance > attackRange * 1.5f)
        {
            currentState = State.Chasing;
            return;
        }

        Vector3 direction = (targetHuman.position - transform.position).normalized;
        direction.y = 0;
        if (direction.magnitude > 0.1f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * rotationSpeed);
        }

        // MODIFICATION 3: Vérifier si le zombie peut grab avant d'attaquer
        if (Time.time - lastAttackTime >= attackCooldown)
        {
            if (grabSystem != null && grabSystem.CanGrab())
            {
                AttemptAttack();
            }
            else if (grabSystem == null)
            {
                // Fallback si pas de grab system
                AttemptAttack();
            }
            // Si le zombie ne peut pas grab (cooldown), il attend sans attaquer
        }
        else
        {
            StopMovement();
        }
    }

    void AttemptAttack()
    {
        if (targetHuman == null) return;

        lastAttackTime = Time.time;

        // Utiliser le système de grab au lieu de dégâts instantanés
        if (grabSystem != null && grabSystem.CanGrab())  // MODIFICATION 4: Double check CanGrab
        {
            grabSystem.AttemptGrab(targetHuman.gameObject);
        }
        else if (grabSystem == null)
        {
            // Fallback si pas de grab system
            HumanHealth humanHealth = targetHuman.GetComponent<HumanHealth>();
            if (humanHealth != null && !humanHealth.IsDead())
            {
                humanHealth.TakeDamage();
                Debug.Log($"{gameObject.name} attacked {targetHuman.name}!");
            }
        }
    }

    void StartWandering()
    {
        currentState = State.Wandering;
        wanderTimer = 0f;
        wanderDirection = new Vector3(Random.Range(-1f, 1f), 0, Random.Range(-1f, 1f)).normalized;
    }

    void MoveInDirection(Vector3 direction, float speed)
    {
        direction.y = 0;
        direction.Normalize();

        if (direction.magnitude > 0.1f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * rotationSpeed);

            Vector3 velocity = direction * speed;
            rb.linearVelocity = new Vector3(velocity.x, rb.linearVelocity.y, velocity.z);
        }
    }

    void StopMovement()
    {
        rb.linearVelocity = new Vector3(0, rb.linearVelocity.y, 0);
    }

    float GetCurrentSpeed()
    {
        if (health == null) return chaseSpeed;

        if (health.IsCrawling())
        {
            return chaseSpeed * 0.3f;
        }
        else
        {
            int injuries = 0;
            if (!health.HasLeftArm()) injuries++;
            if (!health.HasRightArm()) injuries++;
            if (!health.HasLeftLeg()) injuries++;
            if (!health.HasRightLeg()) injuries++;

            float speedReduction = 1f - (injuries * 0.15f);
            return chaseSpeed * Mathf.Max(0.3f, speedReduction);
        }
    }

    public void StunByGunshot(float duration)
    {
        isStunnedByShot = true;
        stunnedUntilTime = Time.time + duration;
        StopMovement();

        // MODIFICATION 5: Forcer la libération si en grab
        if (grabSystem != null && grabSystem.IsGrabbing())
        {
            grabSystem.ForceRelease();
        }
    }

    public bool IsStunnedByShot() => isStunnedByShot;

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);

        if (targetHuman != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(transform.position, targetHuman.position);
        }

        // MODIFICATION 6: Indicateur visuel du cooldown de grab
        if (grabSystem != null && !grabSystem.CanGrab())
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawCube(transform.position + Vector3.up * 2.5f, Vector3.one * 0.3f);
        }
    }
}