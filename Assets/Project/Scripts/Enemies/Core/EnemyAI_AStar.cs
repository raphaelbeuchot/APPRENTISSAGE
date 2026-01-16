using UnityEngine;
using System.Collections;
using Pathfinding;

[RequireComponent(typeof(Rigidbody))]
public class EnemyAI_AStar : MonoBehaviour
{

    private bool hasCalculatedAnticipatedPosition = false;
    private Vector3 lastChaseDirection = Vector3.zero;
    private bool isLookingAround = false;


    [Header("Enemy Stats")]
    public EnemyStats stats;
    public PlayerStats playerStats;

    // Wall staring detection
    private float wallStaringTimer = 0f;
    private float wallStaringThreshold = 5f;
    private float wallDetectionDistance = 1f;

    private Vector3 lastKnownPlayerPosition;
    private bool isGoingToLastKnownPosition = false;
    [SerializeField] private float arrivalThreshold = 0.5f;

    [Header("References")]
    protected Rigidbody rb;
    public Transform targetHuman;
    protected GameManager gameManager;
    protected EnemyHealth health;
    protected IAttackBehavior attackBehavior;
    private AIPath aiPath;
    private Seeker seeker;
    private BlinderWanderBehavior wanderBehavior;
    private EnemyPitInteractable pitInteractable;


    [Header("Pit Mode")]
    [HideInInspector] public bool isInPitMode = false;

    [Header("Rotating Platform Mode")]
    [HideInInspector] public bool isOnRotatingPlatform = false;
    private RotatingPlatform currentRotatingPlatform;
    private float centrifugalDrift = 0f;
    private float recoveryEndTime = 0f;
    private bool isRecoveringFromPlatform = false;

    private EnemyHealthBarUI healthBarUI;
    public bool canMove = true;
    [HideInInspector] public bool isStunnedBySentinel = false;

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

    public enum State { Idle, Wandering, Chasing, Attacking, PreGrab, StunBySpray, OnRotatingPlatform, Dead }
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
        aiPath = GetComponent<AIPath>();
        seeker = GetComponent<Seeker>();
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

        if (currentState == State.Idle)
        {
            RaycastHit hit;
            if (Physics.Raycast(transform.position + Vector3.up * 0.5f, transform.forward, out hit, wallDetectionDistance, LayerMask.GetMask("Obstacle")))
            {
                wallStaringTimer += Time.deltaTime;

                if (wallStaringTimer >= wallStaringThreshold)
                {
                    float randomAngle = Random.Range(90f, 270f);
                    StartCoroutine(SmoothRotateAway(randomAngle));
                    wallStaringTimer = 0f;
                }
            }
            else
            {
                wallStaringTimer = 0f;
            }
        }
        else
        {
            wallStaringTimer = 0f;
        }

        if (rb != null && rb.angularVelocity.magnitude > 10f)
        {
            Debug.LogError($"[ROTATION FOLLE] {gameObject.name} - Angular velocity: {rb.angularVelocity.magnitude:F2}");
            Debug.LogError($"State: {currentState}, Constraints: {rb.constraints}");
            Debug.LogError($"AIPath enabled: {aiPath?.enabled}, canMove: {canMove}");

            rb.angularVelocity = Vector3.zero;
            rb.constraints = RigidbodyConstraints.FreezeRotation;
        }

        if (isStunnedBySentinel)
        {
            StopMovement();
            return;
        }

        // Recuperation apres sortie rotating platform
        if (isRecoveringFromPlatform)
        {
            if (Time.time >= recoveryEndTime)
            {
                isRecoveringFromPlatform = false;
                if (aiPath != null)
                {
                    aiPath.enabled = true;
                }
                Debug.Log($"{gameObject.name} recovered from rotating platform");
            }
            else
            {
                StopMovement();
                return;
            }
        }

        if (!canMove)
        {
            if (aiPath != null)
                aiPath.canMove = false;
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
            case State.PreGrab:
                HandlePreGrabState();
                break;
            case State.StunBySpray:
                HandleStunBySprayState();
                break;
            case State.OnRotatingPlatform:
                HandleOnRotatingPlatformState();
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

    private IEnumerator SmoothRotateAway(float angle)
    {
        Quaternion startRotation = transform.rotation;
        Quaternion targetRotation = startRotation * Quaternion.Euler(0, angle, 0);

        float elapsed = 0f;
        float rotationDuration = 1f;

        while (elapsed < rotationDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / rotationDuration;
            transform.rotation = Quaternion.Slerp(startRotation, targetRotation, t);
            yield return null;
        }

        transform.rotation = targetRotation;
        Debug.Log($"{gameObject.name} finished smooth rotation away from wall");
    }

    private IEnumerator LookAroundCoroutine()
    {
        isLookingAround = true;
        StopMovement();

        Debug.Log($"{gameObject.name} lastChaseDirection = {lastChaseDirection}");

        Vector3 lookDirection = lastChaseDirection;
        lookDirection.y = 0;

        if (lookDirection.magnitude < 0.1f)
        {
            Debug.LogWarning($"{gameObject.name} lastChaseDirection invalide, abandon rotation");
            wasChasing = false;
            isGoingToLastKnownPosition = false;
            isLookingAround = false;
            currentState = State.Idle;
            yield break;
        }

        lookDirection.Normalize();

        Quaternion startRotation = transform.rotation;
        Quaternion targetRotation = Quaternion.LookRotation(lookDirection);

        Debug.Log($"{gameObject.name} startRotation = {startRotation.eulerAngles}, targetRotation = {targetRotation.eulerAngles}");

        float elapsed = 0f;
        float rotationDuration = 1f;

        while (elapsed < rotationDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / rotationDuration;
            transform.rotation = Quaternion.Slerp(startRotation, targetRotation, t);
            yield return null;
        }

        transform.rotation = targetRotation;

        yield return new WaitForSeconds(0.8f);

        wasChasing = false;
        isGoingToLastKnownPosition = false;
        isLookingAround = false;
        currentState = State.Idle;

        Debug.Log($"{gameObject.name} finished looking around, returning to Idle");
    }


    protected virtual void DetectHumans()
    {
        if (currentState == State.StunBySpray)
            return;
        
        // Ignorer detection si sur plateforme rotative
        if (currentState == State.OnRotatingPlatform)
            return;

        PlayerHealth player = FindObjectOfType<PlayerHealth>();
        if (player == null || player.IsDead())
            return;

        bool wasInRange = isPlayerInRange;
        float distToPlayer = Vector3.Distance(transform.position, player.transform.position);

        if (isBlinder)
        {
            targetHuman = null;
            if (currentState == State.Chasing || currentState == State.Attacking)
                currentState = State.Idle;

            if (distToPlayer <= stats.blinderHealthBarRange)
                health?.healthBarUI?.Show();
            else
                health?.healthBarUI?.Hide();

            return;
        }

        if (isForcedChase && targetHuman != null)
        {
            currentState = State.Chasing;

            isPlayerInRange = distToPlayer <= 5f;

            if (isPlayerInRange && !wasInRange)
                health?.healthBarUI?.Show();
            else if (!isPlayerInRange && wasInRange)
                health?.healthBarUI?.Hide();

            return;
        }

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

                Vector3 eyePos = transform.position + Vector3.up * 1.0f;
                Vector3 targetEyePos = hit.transform.position + Vector3.up * 1.0f;

                Vector3 dir = (targetEyePos - eyePos).normalized;
                float dist = Vector3.Distance(eyePos, targetEyePos);

                if (Physics.Raycast(eyePos, dir, out RaycastHit wallHit, dist, stats.obstacleMask))
                {
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
            lastKnownPlayerPosition = closestHuman.position;
            isGoingToLastKnownPosition = false;
            wasChasing = true;
            lostTargetTime = -999f;
            hasCalculatedAnticipatedPosition = false;

            if (closestDistance <= stats.attackRange)
                currentState = State.Attacking;
            else
                currentState = State.Chasing;
        }
        else
        {
            if (wasChasing && !isForcedChase)
            {
                targetHuman = null;

                if (!hasCalculatedAnticipatedPosition)
                {
                    Vector3 directionToLastPos = (lastKnownPlayerPosition - transform.position).normalized;
                    lastKnownPlayerPosition = lastKnownPlayerPosition + directionToLastPos * 0.75f;

                    Rigidbody playerRb = player.GetComponent<Rigidbody>();
                    if (playerRb != null)
                    {
                        Vector3 playerVelocity = playerRb.linearVelocity;
                        playerVelocity.y = 0;

                        if (playerVelocity.magnitude > 0.5f)
                        {
                            lastChaseDirection = playerVelocity.normalized;
                            Debug.Log($"{gameObject.name} Memorise direction mouvement player : {lastChaseDirection}");
                        }
                        else
                        {
                            lastChaseDirection = directionToLastPos;
                        }
                    }

                    hasCalculatedAnticipatedPosition = true;
                    Debug.Log($"{gameObject.name} position anticipee calculee : {lastKnownPlayerPosition}");
                }

                isGoingToLastKnownPosition = true;
                currentState = State.Chasing;
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

        isPlayerInRange = distToPlayer <= 5f;
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

        if (isLookingAround)
        {
            StopMovement();
            return;
        }

        if (wanderBehavior != null) wanderBehavior.StopWandering();

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
        else if (isGoingToLastKnownPosition)
        {
            float distanceToLastPos = Vector3.Distance(transform.position, lastKnownPlayerPosition);

            if (distanceToLastPos <= arrivalThreshold)
            {
                Debug.Log($"{gameObject.name} arrive a la derniere position, rotation de recherche...");
                StartCoroutine(LookAroundCoroutine());
                return;
            }

            if (isInPitMode)
                MoveInPitMode();
            else
            {
                Vector3 direction = (lastKnownPlayerPosition - transform.position).normalized;
                MoveInDirection(direction, stats.chaseSpeed);
            }
        }
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

                GrabAttack grabAttack = attackBehavior as GrabAttack;
                if (grabAttack != null)
                {
                    currentState = State.PreGrab;
                    grabAttack.StartWindup();
                }
                else
                {
                    attackBehavior.AttemptAttack(targetHuman.gameObject);
                }
            }
        }
    }

    protected virtual void MoveInDirection(Vector3 direction, float speed)
    {
        if (aiPath == null) return;

        aiPath.canMove = true;

        float finalSpeed = speed;

        if (pitInteractable != null && pitInteractable.isInShallowWater)
        {
            finalSpeed = finalSpeed * pitInteractable.waterSlowdownMultiplier;
        }

        aiPath.maxSpeed = finalSpeed;

        if (targetHuman != null)
            aiPath.destination = targetHuman.position;
        else
            aiPath.destination = transform.position + direction * 3f;

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
            return;
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
            aiPath.destination = transform.position;
            aiPath.canMove = false;
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
            if (aiPath != null)
            {
                aiPath.canMove = true;
                aiPath.maxSpeed = stats.walkSpeed;
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

    public AIPath GetAIPath()
    {
        return aiPath;
    }

    public void EnablePitMode()
    {
        isInPitMode = true;
        if (aiPath != null)
        {
            aiPath.enabled = false;
        }
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

    public void EnableRotatingPlatformMode(RotatingPlatform platform)
    {
        isOnRotatingPlatform = true;
        currentRotatingPlatform = platform;
        centrifugalDrift = 0f;
        currentState = State.OnRotatingPlatform;

        if (aiPath != null)
        {
            aiPath.enabled = false;
        }

        Debug.Log($"[EnemyAI] {gameObject.name} OnRotatingPlatform mode enabled");
    }

    public void DisableRotatingPlatformMode()
    {
        isOnRotatingPlatform = false;
        currentRotatingPlatform = null;

        isRecoveringFromPlatform = true;
        recoveryEndTime = Time.time + 1.5f;

        currentState = State.Idle;

        Debug.Log($"[EnemyAI] {gameObject.name} OnRotatingPlatform mode disabled, recovering...");
    }

    protected virtual void HandleOnRotatingPlatformState()
    {
        // La rotation est geree par RotatingPlatform.Update()
        StopMovement();
    }

    protected virtual void HandleStunBySprayState()
    {
        StopMovement();

        if (health != null && health.GetSprayStunTimeRemaining() <= 0f)
        {
            currentState = State.Idle;
            Debug.Log($"{gameObject.name} exited StunBySpray state");
        }
    }

    protected virtual void HandlePreGrabState()
    {
        if (wanderBehavior != null) wanderBehavior.StopWandering();

        if (targetHuman != null)
        {
            Vector3 direction = (targetHuman.position - transform.position).normalized;
            direction.y = 0;

            if (direction.magnitude > 0.1f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(direction);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * stats.rotationSpeed);
            }
        }

        GrabAttack grab = attackBehavior as GrabAttack;
        if (grab != null && !grab.isInWindup)
        {
            currentState = State.Chasing;
        }
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