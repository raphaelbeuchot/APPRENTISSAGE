using UnityEngine;
using System.Collections;
using UnityEngine.AI;

/// <summary>
/// Generic AI for all enemies.
/// Handles: Idle, Wander, Chase, and delegates attacks to IAttackBehavior.
/// Works with all enemy types (Grabber, Hitter, Spitter, Blinder, etc.)
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class EnemyAI : MonoBehaviour
{
    [Header("Enemy Stats")]
    public EnemyStats stats;
    public PlayerStats playerStats;

    [Header("References")]
    protected Rigidbody rb;
    protected Transform targetHuman;
    protected GameManager gameManager;
    protected EnemyHealth health;
    protected IAttackBehavior attackBehavior;
    protected NavMeshAgent agent;
    private BlinderWanderBehavior wanderBehavior; // Added reference for Blinder

    // State variables
    
    private EnemyHealthBarUI healthBarUI;
    public bool canMove = true;

    private bool isPlayerInRange = false;
    protected float lastWanderTime = 0f;
    protected float wanderTimer = 0f;
    protected float lastAttackTime = 0f;
    protected Vector3 wanderDirection;
    protected float currentSpeed;
    protected bool isDead = false;
    [HideInInspector] public bool isDetectedBySentinel = false;

    protected enum State { Idle, Wandering, Chasing, Attacking, Dead }
    protected State currentState = State.Idle;

    protected virtual void Start()
    {
        if (stats == null)
        {
            Debug.LogError("EnemyStats not assigned on " + gameObject.name);
            return;
        }

        rb = GetComponent<Rigidbody>();
        gameManager = FindObjectOfType<GameManager>();
        health = GetComponent<EnemyHealth>();
        agent = GetComponent<NavMeshAgent>();
        wanderBehavior = GetComponent<BlinderWanderBehavior>(); // Initialize wander behavior

        if (agent != null)
        {
            agent.speed = stats.walkSpeed;
            agent.angularSpeed = stats.rotationSpeed;
        }

        InitializeAttackBehavior();
        StartCoroutine(DetectionLoop());
    }

    void InitializeAttackBehavior()
    {
        switch (stats.attackType)
        {
            case EnemyStats.AttackType.Grabber:
                attackBehavior = GetComponent<GrabAttack>() ?? gameObject.AddComponent<GrabAttack>();
                break;

            case EnemyStats.AttackType.Hitter:
                Debug.LogWarning($"{gameObject.name}: Hitter attack not yet implemented!");
                break;

            case EnemyStats.AttackType.Spitter:
                Debug.LogWarning($"{gameObject.name}: Spitter attack not yet implemented!");
                break;

            case EnemyStats.AttackType.Blinder:
                attackBehavior = GetComponent<ChargeAttack>() ?? gameObject.AddComponent<ChargeAttack>();
                break;

            default:
                Debug.LogWarning($"{gameObject.name}: Unknown attack type {stats.attackType}");
                break;
        }

        if (attackBehavior != null)
        {
            attackBehavior.Initialize(stats, playerStats, transform, rb);
            Debug.Log($"{gameObject.name} initialized with {stats.attackType} attack behavior");
        }
    }

    protected virtual void Update()
    {
        if (stats == null || isDead) return;

        if (!canMove)
        {
            agent.isStopped = true;
            return; // Bloque toutes les actions pendant le stun
        }

        if (health != null && health.IsDead())
        {
            StopMovement();
            currentState = State.Dead;
            isDead = true;
            return;
        }

        if (gameManager != null && gameManager.zombieStunBySentinel)
        {
            if (attackBehavior == null || !attackBehavior.IsInSpecialState())
            {
                StopMovement();
                return;
            }
        }

        if (attackBehavior != null && attackBehavior.IsAttacking())
        {
            StopMovement();
            return;
        }

        if (attackBehavior != null && attackBehavior.IsInSpecialState())
        {
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
            case State.Dead:
                StopMovement();
                break;
        }
    }

    // ============================================
    // DETECTION
    // ============================================

    protected IEnumerator DetectionLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(stats.detectionCheckInterval);
            if (isDead) continue;
            if (gameManager != null && gameManager.zombieStunBySentinel) continue;
            DetectHumans();
        }
    }

    protected virtual void DetectHumans()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, stats.detectionRadius, stats.targetLayer);
        Transform closestHuman = null;
        float closestDistance = Mathf.Infinity;

        foreach (Collider hit in hits)
        {
            PlayerHealth humanHealth = hit.GetComponent<PlayerHealth>();
            if (humanHealth != null && !humanHealth.IsDead())
            {
                float distance = Vector3.Distance(transform.position, hit.transform.position);
                Vector3 directionToTarget = (hit.transform.position - transform.position).normalized;
                float angle = Vector3.Angle(transform.forward, directionToTarget);

                if (angle > stats.detectionAngle / 2f)
                    continue;

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
            if (closestDistance <= stats.attackRange)
                currentState = State.Attacking;
            else
                currentState = State.Chasing;
        }
        else
        {
            targetHuman = null;
            if (currentState == State.Chasing || currentState == State.Attacking)
                currentState = State.Idle;
        }

        // Update health bar visibility (separate 6m range, 360 degrees)
        PlayerHealth player = FindObjectOfType<PlayerHealth>();
        bool wasInRange = isPlayerInRange;

        if (player != null && !player.IsDead())
        {
            float distToPlayer = Vector3.Distance(transform.position, player.transform.position);
            isPlayerInRange = (distToPlayer <= 5f);
        }
        else
        {
            isPlayerInRange = false;
        }

        if (isPlayerInRange && !wasInRange)
        {
            health?.healthBarUI?.Show();
        }
        else if (!isPlayerInRange && wasInRange)
        {
            health?.healthBarUI?.Hide();
        }
    }

    // ============================================
    // STATES
    // ============================================

    protected virtual void HandleIdleState()
    {
        StopMovement();

        if (wanderBehavior != null && !wanderBehavior.IsWandering())
        {
            if (Time.time - lastWanderTime >= stats.wanderInterval)
            {
                lastWanderTime = Time.time;
                if (Random.value < stats.idleWanderChance)
                {
                    wanderBehavior.StartWandering();
                    currentState = State.Wandering;
                }
            }
        }
    }

    protected virtual void HandleWanderingState()
    {
        if (wanderBehavior == null) return;

        if (!wanderBehavior.IsWandering())
        {
            currentState = State.Idle;
        }
    }

    protected virtual void HandleChasingState()
    {
        if (wanderBehavior != null) wanderBehavior.StopWandering();

        if (agent != null && agent.isOnNavMesh)
        {
            agent.speed = stats.chaseSpeed;
        }

        if (targetHuman == null)
        {
            currentState = State.Idle;
            return;
        }

        float distance = Vector3.Distance(transform.position, targetHuman.position);
        if (distance <= stats.attackRange)
        {
            currentState = State.Attacking;
            return;
        }

        Vector3 direction = (targetHuman.position - transform.position).normalized;
        MoveInDirection(direction, stats.chaseSpeed);
    }

    protected virtual void HandleAttackingState()
    {
        if (wanderBehavior != null) wanderBehavior.StopWandering();

        if (targetHuman == null)
        {
            currentState = State.Idle;
            return;
        }

        float distance = Vector3.Distance(transform.position, targetHuman.position);
        if (distance > stats.attackRange * 1.5f)
        {
            currentState = State.Chasing;
            return;
        }

        Vector3 direction = (targetHuman.position - transform.position).normalized;
        direction.y = 0;

        if (direction.magnitude > 0.1f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * stats.rotationSpeed);
        }

        if (attackBehavior != null && attackBehavior.CanAttack())
        {
            if (Time.time - lastAttackTime >= stats.attackCooldown)
            {
                lastAttackTime = Time.time;
                attackBehavior.AttemptAttack(targetHuman.gameObject);
            }
        }
    }

    // ============================================
    // MOVEMENT
    // ============================================

    protected virtual void MoveInDirection(Vector3 direction, float speed)
    {
        if (agent == null || !agent.isOnNavMesh) return;

        agent.speed = speed;

        if (targetHuman != null)
        {
            agent.SetDestination(targetHuman.position);
        }
        else
        {
            agent.SetDestination(transform.position + direction * 3f);
        }

        direction.y = 0;
        direction.Normalize();

        if (direction.magnitude > 0.1f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * stats.rotationSpeed);

            Vector3 velocity = direction * speed;
            rb.linearVelocity = new Vector3(velocity.x, rb.linearVelocity.y, velocity.z);
        }
    }

    protected void StopMovement()
    {
        if (agent != null && agent.isOnNavMesh)
            agent.ResetPath();

        rb.linearVelocity = new Vector3(0, rb.linearVelocity.y, 0);
    }

    // ============================================
    // PUBLIC METHODS
    // ============================================

    public void UpdateSpeed(float currentHealth, bool isCrawler)
    {
        if (stats == null) return;

        bool isChasing = (currentState == State.Chasing || currentState == State.Attacking);
        currentSpeed = stats.GetAdjustedSpeed(currentHealth, isChasing, isCrawler);
    }

    public void ResetAfterBourrade()
    {
        if (!isDead)
        {
            currentState = State.Chasing;
            if (agent != null)
            {
                agent.isStopped = false;
                agent.speed = stats.walkSpeed;
            }
        }
    }

    public void SetDead(bool value)
    {
        isDead = value;
        if (value) currentState = State.Dead;
    }

    protected virtual void OnDrawGizmosSelected()
    {
        if (stats == null) return;

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, stats.detectionRadius);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, stats.attackRange);
    }
}
