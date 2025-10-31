using UnityEngine;
using System.Collections;
using UnityEngine.AI;

/// <summary>
/// AI générique pour tous les ennemis.
/// Gère: Idle, Wander, Chase, et délègue l'attaque à IAttackBehavior.
/// Compatible avec tous les types d'ennemis (Grabber, Hitter, Spitter, etc.)
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

    // État
    protected float lastWanderTime = 0f;
    protected float wanderTimer = 0f;
    protected float lastAttackTime = 0f;
    protected Vector3 wanderDirection;
    protected float currentSpeed;
    protected bool isDead = false;

    protected enum State { Idle, Wandering, Chasing, Attacking, Dead }
    protected State currentState = State.Idle;

    protected virtual void Start()
    {
        if (stats == null)
        {
            Debug.LogError("EnemyStats non assigné sur " + gameObject.name);
            return;
        }

        rb = GetComponent<Rigidbody>();
        gameManager = FindObjectOfType<GameManager>();
        health = GetComponent<EnemyHealth>();



        // Initialiser le comportement d'attaque selon le type
        InitializeAttackBehavior();

        StartCoroutine(DetectionLoop());

        agent = GetComponent<NavMeshAgent>();
        if (agent != null) {
            agent.speed = stats.walkSpeed;
            agent.angularSpeed = stats.rotationSpeed;
            
                }
        

    }

    /// <summary>
    /// Initialise le comportement d'attaque selon le type défini dans EnemyStats
    /// </summary>
    void InitializeAttackBehavior()
    {
        switch (stats.attackType)
        {
            case EnemyStats.AttackType.Grabber:
                attackBehavior = gameObject.GetComponent<GrabAttack>();
                if (attackBehavior == null)
                {
                    attackBehavior = gameObject.AddComponent<GrabAttack>();
                }
                break;

            case EnemyStats.AttackType.Hitter:
                // TODO: Implémenter MeleeHitAttack
                Debug.LogWarning($"{gameObject.name}: Hitter attack not yet implemented!");
                break;

            case EnemyStats.AttackType.Spitter:
                // TODO: Implémenter RangedAttack
                Debug.LogWarning($"{gameObject.name}: Spitter attack not yet implemented!");
                break;

            default:
                Debug.LogWarning($"{gameObject.name}: Unknown attack type {stats.attackType}");
                break;
        }

        // Initialiser le comportement d'attaque
        if (attackBehavior != null)
        {
            attackBehavior.Initialize(stats, playerStats, transform, rb);
            Debug.Log($"{gameObject.name} initialized with {stats.attackType} attack behavior");
        }
    }

    protected virtual void Update()
    {
        if (stats == null || isDead) return;

        // Vérifier si mort
        if (health != null && health.IsDead())
        {
            StopMovement();
            currentState = State.Dead;
            isDead = true;
            return;
        }

        // Arrêter si stun par sentinelle
        if (gameManager != null && gameManager.zombieStunBySentinel)
        {
            StopMovement();
            return;
        }

        // Arrêter si en train d'attaquer ou dans un état spécial
        if (attackBehavior != null && attackBehavior.IsAttacking())
        {
            StopMovement();
            return;
        }

        // Ne pas toucher au mouvement si en bourrade (velocity contrôlée par la coroutine)
        if (attackBehavior != null && attackBehavior.IsInSpecialState())
        {
            return; // Ne pas appeler StopMovement !
        }

        // États normaux
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
    // DÉTECTION
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

                // CHECK ANGLE
                Vector3 directionToTarget = (hit.transform.position - transform.position).normalized;
                float angle = Vector3.Angle(transform.forward, directionToTarget);

                if (angle > stats.detectionAngle / 2f)
                    continue; // Hors du cone, ignore

                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestHuman = hit.transform;
                }

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
    }

    // ============================================
    // ÉTATS
    // ============================================

    protected virtual void HandleIdleState()
    {
        StopMovement();
        if (Time.time - lastWanderTime >= stats.wanderInterval)
        {
            lastWanderTime = Time.time;
            if (Random.value < stats.idleWanderChance)
                StartWandering();
        }
    }

    protected virtual void HandleWanderingState()
    {

        if (agent != null && agent.isOnNavMesh)
        {
            agent.speed = stats.walkSpeed;
        }
        
        wanderTimer += Time.deltaTime;
        if (wanderTimer >= stats.wanderDuration)
        {
            currentState = State.Idle;
            wanderTimer = 0f;
            StopMovement();
            return;
        }
        MoveInDirection(wanderDirection, stats.walkSpeed);
    }

    protected virtual void HandleChasingState()
    {
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

        // Orienter l'ennemi vers sa cible
        Vector3 direction = (targetHuman.position - transform.position).normalized;
        direction.y = 0;

        if (direction.magnitude > 0.1f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * stats.rotationSpeed);
        }

        // Tentative d'attaque via le comportement modulaire
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
    // MOUVEMENT
    // ============================================

    protected virtual void StartWandering()
    {
        currentState = State.Wandering;
        wanderTimer = 0f;
        wanderDirection = new Vector3(Random.Range(-1f, 1f), 0, Random.Range(-1f, 1f)).normalized;
    }

    protected virtual void MoveInDirection(Vector3 direction, float speed)
    {

        if (agent != null && agent.isOnNavMesh)
        {
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

    // ============================================
    // GIZMOS
    // ============================================

    protected virtual void OnDrawGizmosSelected()
    {
        if (stats == null) return;

        // Rayon de détection
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, stats.detectionRadius);

        // Portée d'attaque
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, stats.attackRange);
    }
}