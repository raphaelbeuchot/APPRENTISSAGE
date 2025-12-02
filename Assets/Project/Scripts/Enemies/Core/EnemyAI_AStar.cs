using UnityEngine;
using System.Collections;
using Pathfinding; // AJOUT A*

[RequireComponent(typeof(Rigidbody))]
public class EnemyAI_AStar : MonoBehaviour
{
    [Header("Enemy Stats")]
    public EnemyStats stats;
    public PlayerStats playerStats;

    private Vector3 lastKnownPlayerPosition;
    private bool isGoingToLastKnownPosition = false;
    [SerializeField] private float arrivalThreshold = 0.5f;

    [Header("References")]
    protected Rigidbody rb;
    public Transform targetHuman;
    protected GameManager gameManager;
    protected EnemyHealth health;
    protected IAttackBehavior attackBehavior;
    private AIPath aiPath; // REMPLACE NavMeshAgent
    private Seeker seeker; // AJOUT
    private BlinderWanderBehavior wanderBehavior;
    private EnemyPitInteractable pitInteractable;


    [Header("Pit Mode")]
    [HideInInspector] public bool isInPitMode = false;

    private EnemyHealthBarUI healthBarUI;
    public bool canMove = true;
    [HideInInspector] public bool isStunnedBySentinel = false;

    // Ajout pour persistance chase
    private float lostTargetTime = -999f;
    private bool wasChasing = false;

    [HideInInspector] public bool isForcedChase = false;

    
    private bool isPlayerInRange = false;
    protected float lastWanderTime = 0f;
    protected float wanderTimer = 0f;
    protected float lastAttackTime = 0f;
    protected Vector3 wanderDirection;
    protected float currentSpeed;
    protected bool isDead = false;
    [HideInInspector] public bool isDetectedBySentinel = false;

    private bool isBlinder = false;

    public enum State { Idle, Wandering, Chasing, Attacking, Dead }
    public State currentState = State.Idle;

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
        aiPath = GetComponent<AIPath>(); // REMPLACE GetComponent<NavMeshAgent>
        seeker = GetComponent<Seeker>(); // AJOUT
        wanderBehavior = GetComponent<BlinderWanderBehavior>();
        pitInteractable = GetComponent<EnemyPitInteractable>();

        isBlinder = stats.attackType == EnemyStats.AttackType.Blinder;

        if (aiPath != null)
        {
            aiPath.maxSpeed = stats.walkSpeed;
            aiPath.rotationSpeed = stats.rotationSpeed;

            
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
                Debug.LogWarning(gameObject.name + ": Hitter attack not yet implemented!");
                break;
            case EnemyStats.AttackType.Spitter:
                Debug.LogWarning(gameObject.name + ": Spitter attack not yet implemented!");
                break;
            case EnemyStats.AttackType.Blinder:
                attackBehavior = GetComponent<ChargeAttack>() ?? gameObject.AddComponent<ChargeAttack>();
                break;
            default:
                Debug.LogWarning(gameObject.name + ": Unknown attack type " + stats.attackType);
                break;
        }

        if (attackBehavior != null)
        {
            attackBehavior.Initialize(stats, playerStats, transform, rb);
            Debug.Log(gameObject.name + " initialized with " + stats.attackType + " attack behavior");
        }
    }

    protected virtual void Update()
    {
        if (stats == null || isDead) return;

        if (isStunnedBySentinel)
        {
            StopMovement();
            return;
        }

        if (!canMove)
        {
            if (aiPath != null) // REMPLACE agent.isOnNavMesh check
                aiPath.canMove = false; // REMPLACE agent.isStopped = true
            return;
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
        PlayerHealth player = FindObjectOfType<PlayerHealth>();
        if (player == null || player.IsDead())
            return;

        // DECLARE ICI UNE FOIS POUR TOUTE LA METHODE
        bool wasInRange = isPlayerInRange;
        float distToPlayer = Vector3.Distance(transform.position, player.transform.position);

        // ----- Gestion speciale pour le Blinder -----
        if (isBlinder)
        {
            // Le Blinder ne poursuit jamais le joueur
            targetHuman = null;
            if (currentState == State.Chasing || currentState == State.Attacking)
                currentState = State.Idle;

            // Affichage de la barre de vie si le joueur est dans le range defini
            if (distToPlayer <= stats.blinderHealthBarRange)
                health?.healthBarUI?.Show();
            else
                health?.healthBarUI?.Hide();

            return;
        }

        // ----- FORCED CHASE (apres bottle throw) -----
        if (isForcedChase && targetHuman != null)
        {
            currentState = State.Chasing;

            // Mise a jour barre de vie
            isPlayerInRange = distToPlayer <= 5f;

            if (isPlayerInRange && !wasInRange)
                health?.healthBarUI?.Show();
            else if (!isPlayerInRange && wasInRange)
                health?.healthBarUI?.Hide();

            return;
        }

        // ----- Detection classique pour les autres ennemis -----
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

                // Check Line of Sight
                Vector3 eyePos = transform.position + Vector3.up * 1.0f; // hauteur yeux zombie
                Vector3 targetEyePos = hit.transform.position + Vector3.up * 1.0f; // hauteur yeux du player

                Vector3 dir = (targetEyePos - eyePos).normalized;
                float dist = Vector3.Distance(eyePos, targetEyePos);

                // Si un obstacle bloque la vue
                if (Physics.Raycast(eyePos, dir, out RaycastHit wallHit, dist, stats.obstacleMask))
                {
                    // Si ce qu'on touche n'est pas le PLAYER  vue bloquée
                    if (!wallHit.collider.GetComponent<PlayerHealth>())
                    {
                        continue;
                    }
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
            lastKnownPlayerPosition = closestHuman.position; // MAJ constante
            isGoingToLastKnownPosition = false; // On le voit = pas besoin de la derniere position
            wasChasing = true;
            lostTargetTime = -999f;

            if (closestDistance <= stats.attackRange)
                currentState = State.Attacking;
            else
                currentState = State.Chasing;
        }
        else // Player invisible (hors range OU cache derriere obstacle)
        {
            if (wasChasing && !isForcedChase)
            {
                // On perd la vue : on va vers la derniere position connue
                targetHuman = null; // On ne le voit plus
                isGoingToLastKnownPosition = true; // Mode "chercher a la derniere position"
                currentState = State.Chasing; // On reste en chase (mais vers lastKnownPlayerPosition)

                Debug.Log($"{gameObject.name} a perdu le joueur, va chercher a {lastKnownPlayerPosition}");
            }
            else if (!isForcedChase)
            {
                targetHuman = null;
                wasChasing = false;
                isGoingToLastKnownPosition = false;

                if (currentState == State.Chasing || currentState == State.Attacking)
                    currentState = State.Idle;
            }
        }

        // CRITIQUE : Mise a jour de la barre de vie APRES toute la logique
        // (pour TOUS les ennemis non-Blinder)
        isPlayerInRange = distToPlayer <= 5f;
        // NOUVEAU : Ignorer distance check si zombie dans deep empty pit
        EnemyPitInteractable pitInt = GetComponent<EnemyPitInteractable>();
        bool ignoreDistance = pitInt != null && pitInt.shouldIgnoreHealthbarDistance;

        if (isPlayerInRange && !wasInRange)
            health?.healthBarUI?.Show();
        else if (!isPlayerInRange && wasInRange && !ignoreDistance)
            health?.healthBarUI?.Hide();
    }
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
        if (isBlinder) return;

        if (wanderBehavior != null) wanderBehavior.StopWandering();

        // CAS 1 : On voit le player (chase normal)
        if (targetHuman != null)
        {
            float distance = Vector3.Distance(transform.position, targetHuman.position);
            if (distance <= stats.attackRange)
            {
                currentState = State.Attacking;
                return;
            }

            if (isInPitMode)
                MoveInPitMode();
            else
            {
                Vector3 direction = (targetHuman.position - transform.position).normalized;
                MoveInDirection(direction, stats.chaseSpeed);
            }
        }
        // CAS 2 : On ne voit plus le player, on va a la derniere position connue
        else if (isGoingToLastKnownPosition)
        {
            float distanceToLastPos = Vector3.Distance(transform.position, lastKnownPlayerPosition);

            // Arrive a la derniere position : abandon, retour Idle
            if (distanceToLastPos <= arrivalThreshold)
            {
                Debug.Log($"{gameObject.name} arrive a la derniere position, rien trouve -> Idle");
                wasChasing = false;
                isGoingToLastKnownPosition = false;
                currentState = State.Idle;
                return;
            }

            // Avancer vers la derniere position connue
            if (isInPitMode)
                MoveInPitMode();
            else
            {
                Vector3 direction = (lastKnownPlayerPosition - transform.position).normalized;
                MoveInDirection(direction, stats.chaseSpeed);
            }
        }
        // CAS 3 : Ni cible ni position -> Idle
        else
        {
            currentState = State.Idle;
        }
    }

    protected virtual void HandleAttackingState()
    {
        if (isBlinder) return;

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

    protected virtual void MoveInDirection(Vector3 direction, float speed)
    {
        if (aiPath == null) return;

        aiPath.canMove = true;

        float finalSpeed = speed;

        // Appliquer ralentissement shallow water
        if (pitInteractable != null && pitInteractable.isInShallowWater)
        {
            finalSpeed = finalSpeed * pitInteractable.waterSlowdownMultiplier;
        }

        aiPath.maxSpeed = finalSpeed;

        if (targetHuman != null)
            aiPath.destination = targetHuman.position;
        else
            aiPath.destination = transform.position + direction * 3f;

        // AIPath gere rotation et mouvement automatiquement
        // On garde rotation manuelle pour matching ancien comportement
        direction.y = 0;
        direction.Normalize();

        if (direction.magnitude > 0.1f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * stats.rotationSpeed);
        }
    }

    protected virtual void MoveInPitMode()
    {
        Debug.Log($"[PitMode] {gameObject.name} MoveInPitMode called!");

        // Determiner la position cible
        Vector3 targetPosition;
        if (targetHuman != null)
        {
            targetPosition = targetHuman.position;
        }
        else if (isGoingToLastKnownPosition)
        {
            targetPosition = lastKnownPlayerPosition;
        }
        else
        {
            return; // Ni cible ni derniere position, on sort
        }

        Vector3 directionToTarget = (targetPosition - transform.position);
        directionToTarget.y = 0;
        directionToTarget.Normalize();

        if (directionToTarget.magnitude > 0.1f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(directionToTarget);
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRotation,
                Time.deltaTime * stats.rotationSpeed
            );
        }

        bool isChasing = (currentState == State.Chasing || currentState == State.Attacking);
        float pitMoveSpeed = (isChasing ? stats.chaseSpeed : stats.walkSpeed) * 2f;
        Vector3 moveDirection = transform.forward * pitMoveSpeed;
        rb.linearVelocity = new Vector3(moveDirection.x, rb.linearVelocity.y, moveDirection.z);

        Debug.Log($"[PitMode] Setting velocity to {moveDirection}, speed={pitMoveSpeed}");
    }


    protected void StopMovement()
    {
        if (aiPath != null)
        {
            aiPath.destination = transform.position; // REMPLACE agent.ResetPath()
            aiPath.canMove = false; // Stop le movement Astar
        }

        rb.linearVelocity = new Vector3(0, rb.linearVelocity.y, 0);
    }

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
            if (aiPath != null) // REMPLACE agent
            {
                aiPath.canMove = true; // REMPLACE agent.isStopped = false
                aiPath.maxSpeed = stats.walkSpeed; // REMPLACE agent.speed
            }
        }
    }

    public void SetDead(bool value)
    {
        isDead = value;
        if (value) currentState = State.Dead;
    }

    public void CancelLastKnownPositionSearch()
    {
        if (isGoingToLastKnownPosition)
        {
            Debug.Log($"{gameObject.name} annule la recherche (stun sentinelle)");
            targetHuman = null;
            wasChasing = false;
            isGoingToLastKnownPosition = false;
            currentState = State.Idle;
        }
    }

    public AIPath GetAIPath() // REMPLACE GetNavMeshAgent()
    {
        return aiPath;
    }

    public void EnablePitMode()
    {
        isInPitMode = true;
        if (aiPath != null)
        {
            aiPath.enabled = false; // Desactive SEULEMENT AIPath
        }
        // NE PAS desactiver this.enabled !
        Debug.Log($"[EnemyAI] {gameObject.name} PitMode enabled");
    }

    public void DisablePitMode()
    {
        isInPitMode = false;
        if (aiPath != null)
        {
            aiPath.enabled = true;
        }
        Debug.Log($"[EnemyAI] {gameObject.name} PitMode disabled");
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