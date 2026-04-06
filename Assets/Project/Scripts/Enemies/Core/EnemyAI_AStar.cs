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
    private Animator animator;

    [Header("Epervier Mode")]
    [HideInInspector] public bool isKnockedDownByEpervier = false;
    [HideInInspector] public bool wasAlreadyShotDuringSweep = false;
    [HideInInspector] public bool pendingSweepShot = false;
    [HideInInspector] public bool isInStandupPhase = false;



    [Header("Pit Mode")]
    [HideInInspector] public bool isInPitMode = false;

    [Header("Rotating Platform Mode")]
    [HideInInspector] public bool isOnRotatingPlatform = false;
    private float centrifugalDrift = 0f;
    private float recoveryEndTime = 0f;
    private bool isRecoveringFromPlatform = false;

    [Header("Island Platform Mode")]
    [HideInInspector] public bool isOnIslandPlatform = false;
    private float islandCheckInterval = 0.2f;
    private float lastIslandCheckTime = 0f;

    [Header("Pathfinding Optimization")]
    [HideInInspector] public Vector3 lastPathDestination = Vector3.positiveInfinity;
    private float pathUpdateThreshold = 0.5f; // Distance min pour recalculer path

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
    public bool isDead = false;
    [HideInInspector] public bool isDetectedBySentinel = false;
    [Header("Rotation To Impact")]
    private Vector3 impactDirection = Vector3.zero;
    private float rotationToImpactEndTime = 0f;
    private bool isRotatingToImpact = false;

    private bool isBlinder = false;
    public RotatingPlatform currentRotatingPlatform;


    public enum State { Idle, Wandering, Chasing, Attacking, StunBySpray, OnRotatingPlatform, RotatingToImpact, OnIslandPlatform, Dead }
    public State currentState = State.Idle;

    protected virtual void Start()
    {
        if (stats == null)
        {
            return;
        }

        rb = GetComponent<Rigidbody>();
        gameManager = FindObjectOfType<GameManager>();
        health = GetComponent<EnemyHealth>();
        aiPath = GetComponent<AIPath>();
        seeker = GetComponent<Seeker>();
        wanderBehavior = GetComponent<BlinderWanderBehavior>();
        pitInteractable = GetComponent<EnemyPitInteractable>();
        animator = GetComponentInChildren<Animator>();
        animator.Play("Idle", 0, Random.value);

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
                attackBehavior = GetComponent<HitAttack>() ?? gameObject.AddComponent<HitAttack>();
                break;
            case EnemyStats.AttackType.Spitter:
                break;
                           
            default:
                break;
        }

        if (attackBehavior != null)
        {
            attackBehavior.Initialize(stats, playerStats, transform, rb);
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
            

            rb.angularVelocity = Vector3.zero;
            rb.constraints = RigidbodyConstraints.FreezeRotation;
        }

        if (isStunnedBySentinel)
        {
            if (health == null || !health.isInKnockback)
                StopMovement();
            if (currentState != State.OnRotatingPlatform)
                currentState = State.Idle;
            return;
        }

        if (health != null && health.IsStunnedBySpray())
        {
            currentState = State.StunBySpray;
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
            }
            else
            {
                StopMovement();
                return;
            }
        }
        if (Time.time - lastIslandCheckTime >= islandCheckInterval)
        {
            lastIslandCheckTime = Time.time;
            int islandLayer = LayerMask.GetMask("IslandPlatform");
            RaycastHit islandHit;
            CapsuleCollider cap = GetComponent<CapsuleCollider>();
            float halfHeight = cap != null ? cap.height / 2f : 1f;
            Vector3 origin = transform.position + Vector3.up * halfHeight + transform.forward * 0.2f;
            bool onIsland = Physics.Raycast(origin, Vector3.down, out islandHit, 3f, islandLayer);

            if (onIsland && !isOnIslandPlatform)
            {
                Debug.Log($"[Island] Enable island mode - hit={islandHit.collider?.name}");
                EnableIslandMode();
            }
            else if (!onIsland && isOnIslandPlatform)
            {
                Debug.Log($"[Island] Disable island mode");
                DisableIslandMode();
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
        if (isKnockedDownByEpervier || isInStandupPhase)
        {
            StopMovement();
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
            GrabAttack grab = attackBehavior as GrabAttack;
            bool inBourrade = grab != null && grab.isInBourradeDuration;
            if (!inBourrade)
                StopMovement();
            return;
        }

        if (currentState == State.Chasing && !isInPitMode)
        {
            Debug.Log($"[DEBUG] {name} canMove={canMove} isStunned={isStunnedBySentinel} isRecovering={isRecoveringFromPlatform} isAttacking={attackBehavior?.IsAttacking()} isSpecial={attackBehavior?.IsInSpecialState()} aiPathEnabled={aiPath?.enabled} aiPathCanMove={aiPath?.canMove}");
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
            case State.StunBySpray:
                HandleStunBySprayState();
                break;
            case State.OnRotatingPlatform:
                HandleOnRotatingPlatformState();
                break;
            case State.RotatingToImpact:
                HandleRotatingToImpactState();
                break;
            case State.OnIslandPlatform:
                HandleOnIslandPlatformState();
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
            if (isKnockedDownByEpervier) continue;
            if (isInStandupPhase) continue;
            if (gameManager != null && gameManager.zombieStunBySentinel) continue;
            if (isStunnedBySentinel) continue;
            GrabAttack grab = GetComponent<GrabAttack>();
            if (grab != null && (grab.isInWindup || Time.time - grab.windupEndTime < 0.5f)) continue;
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
    }

    private IEnumerator LookAroundCoroutine()
    {
        isLookingAround = true;
        StopMovement();


        Vector3 lookDirection = lastChaseDirection;
        lookDirection.y = 0;

        if (lookDirection.magnitude < 0.1f)
        {
            wasChasing = false;
            isGoingToLastKnownPosition = false;
            isLookingAround = false;
            currentState = State.Idle;
            yield break;
        }

        lookDirection.Normalize();

        Quaternion startRotation = transform.rotation;
        Quaternion targetRotation = Quaternion.LookRotation(lookDirection);


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

    }


    public virtual void DetectHumans()
    {
        GrabAttack grab = GetComponent<GrabAttack>();
        if (grab != null && grab.isLockedInIdle) return;

        if (grab != null && grab.isInWindup) return;

        if (currentState == State.StunBySpray)
            return;

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

        float effectiveDetectionRadius = (currentState == State.RotatingToImpact)
            ? stats.detectionRadius * 2f
            : stats.detectionRadius;

        Collider[] hits = Physics.OverlapSphere(transform.position, effectiveDetectionRadius, stats.targetLayer);
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

            if (!isOnIslandPlatform)
            {
                if (closestDistance <= stats.attackRange)
                    currentState = State.Attacking;
                else
                    currentState = State.Chasing;
            }
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
                        }
                        else
                        {
                            lastChaseDirection = directionToLastPos;
                        }
                    }

                    hasCalculatedAnticipatedPosition = true;
                }

                if (!isOnIslandPlatform)
                {
                    isGoingToLastKnownPosition = true;
                    currentState = State.Chasing;
                }
            }
            else if (!isForcedChase)
            {
                targetHuman = null;
                wasChasing = false;
                isGoingToLastKnownPosition = false;

                if (!isOnIslandPlatform && (currentState == State.Chasing || currentState == State.Attacking))
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
        if (isOnIslandPlatform)
        {
            currentState = State.OnIslandPlatform;
            return;
        }

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

    void OnCollisionEnter(Collision collision)
    {
        Debug.Log($"[Epervier] OnCollisionEnter - tag={collision.gameObject.tag} isDead={isDead} isKnockedDown={isKnockedDownByEpervier}");
        if (isDead) return;
        if (isKnockedDownByEpervier) return;
        if (!collision.gameObject.CompareTag("EpervierObstacle")) return;

        Debug.Log("[Epervier] Knockdown lance !");
        StartCoroutine(EpervierKnockdownCoroutine());
    }

    IEnumerator EpervierKnockdownCoroutine()
    {
        isKnockedDownByEpervier = true;
        canMove = false;
        StopMovement();
        if (aiPath != null) aiPath.enabled = false;

        if (animator != null)
        {
            animator.SetBool("CanStandUp", false);
            animator.SetTrigger("EpervierKnockdown");
        }

        yield return new WaitUntil(() =>
            animator.GetCurrentAnimatorStateInfo(0).IsName("Sweep") &&
            !animator.IsInTransition(0)
        );

        float sweepLength = animator.GetCurrentAnimatorStateInfo(0).length;
        yield return new WaitForSeconds(sweepLength);

        if (gameManager != null && gameManager.IsInRedLight() && !wasAlreadyShotDuringSweep)
        {
            gameManager.ExecuteSequenceShot(gameObject);
            yield return new WaitForSeconds(gameManager.sentinelSettings.stunZombieDuration);
        }

        isKnockedDownByEpervier = false;
        isInStandupPhase = true;

        if (animator != null)
            animator.SetBool("CanStandUp", true);

        yield return new WaitUntil(() =>
            animator.GetCurrentAnimatorStateInfo(0).IsName("Sad Idle") &&
            !animator.IsInTransition(0)
        );

        isInStandupPhase = false;
        wasAlreadyShotDuringSweep = false;
        lastPathDestination = Vector3.positiveInfinity;
        if (aiPath != null)
        {
            aiPath.enabled = true;
            aiPath.canMove = true;
        }
        canMove = true;
        if (gameManager != null)
            gameManager.ResetSequenceTarget(gameObject);
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
                HitAttack hitAttack = attackBehavior as HitAttack;

                if (grabAttack != null)
                {
                    grabAttack.StartWindup();
                }
                else if (hitAttack != null)
                {
                    hitAttack.StartWindup();
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

        // OPTIMISATION : Recalculer path UNIQUEMENT si destination change significativement
        Vector3 newDestination;
        if (targetHuman != null)
            newDestination = targetHuman.position;
        else
            newDestination = transform.position + direction * 3f;

        // Check si la destination a assez change
        if (Vector3.Distance(newDestination, lastPathDestination) > pathUpdateThreshold)
        {
            aiPath.destination = newDestination;
            lastPathDestination = newDestination;
        }

        // APRES
        Vector3 moveDir = aiPath.desiredVelocity;
        moveDir.y = 0;

        if (moveDir.magnitude > 0.1f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(moveDir);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * stats.rotationSpeed);
        }
    }

    protected virtual void MoveInPitMode()
    {

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

    }


    public void StopMovement()
    {
        if (aiPath != null)
        {
            aiPath.destination = transform.position;
            aiPath.canMove = false;
        }

        rb.linearVelocity = new Vector3(0, rb.linearVelocity.y, 0);
    }

    public void ResetChaseState()
    {
        wasChasing = false;
        isGoingToLastKnownPosition = false;
        targetHuman = null;
        lastPathDestination = Vector3.positiveInfinity;
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
    }

    public void DisablePitMode()
    {
        isInPitMode = false;
        if (aiPath != null)
        {
            aiPath.enabled = true;
        }
    }

    public void EnableRotatingPlatformMode(RotatingPlatform platform)
    {
        if (isDead) return;
        isOnRotatingPlatform = true;
        currentRotatingPlatform = platform;
        centrifugalDrift = 0f;
        currentState = State.OnRotatingPlatform;

        if (aiPath != null)
        {
            aiPath.enabled = false;
        }
        if (health != null) health.ShowSpiral();
        if (animator != null)
            animator.SetLayerWeight(1, 1f);
        if (animator != null)
            animator.SetBool("OnRotPlat", true);
    }

    public void DisableRotatingPlatformMode()
    {
        isOnRotatingPlatform = false;
        currentRotatingPlatform = null;

        isRecoveringFromPlatform = true;
        recoveryEndTime = Time.time + 1.5f;

        currentState = State.Idle;
        if (health != null) health.HideSpiral();
        if (animator != null)
            animator.SetLayerWeight(1, 0f);
        if (animator != null)
            animator.SetBool("OnRotPlat", false);
    }

    public void EnableIslandMode()
    {
        if (isDead) return;
        isOnIslandPlatform = true;
        currentState = State.OnIslandPlatform;
        if (aiPath != null)
            aiPath.enabled = false;
        Debug.Log($"[IslandMode] {name} enabled");
    }

    public void DisableIslandMode()
    {
        isOnIslandPlatform = false;
        if (currentState == State.OnIslandPlatform)
            currentState = State.Idle;
        if (aiPath != null)
            aiPath.enabled = true;
        Debug.Log($"[IslandMode] {name} disabled");
    }

    protected virtual void HandleOnRotatingPlatformState()
    {
        // La rotation est geree par RotatingPlatform.Update()
        StopMovement();
    }

    protected virtual void HandleOnIslandPlatformState()
    {
        if (targetHuman == null)
        {
            StopMovement();
            return;
        }

        float distance = Vector3.Distance(transform.position, targetHuman.position);
        if (distance <= stats.attackRange)
        {
            StopMovement();
            if (attackBehavior != null && attackBehavior.CanAttack())
            {
                if (Time.time - lastAttackTime >= stats.attackCooldown)
                {
                    lastAttackTime = Time.time;
                    GrabAttack grabAttack = attackBehavior as GrabAttack;
                    HitAttack hitAttack = attackBehavior as HitAttack;
                    if (grabAttack != null)
                        grabAttack.StartWindup();
                    else if (hitAttack != null)
                        hitAttack.StartWindup();
                }
            }
            return;
        }

        MoveOnIslandPlatform();
    }

    protected virtual void MoveOnIslandPlatform()
    {
        CapsuleCollider cap = GetComponent<CapsuleCollider>();
        float halfHeight = cap != null ? cap.height / 2f : 1f;
        int islandLayer = LayerMask.GetMask("IslandPlatform");

        Vector3 forwardCheckOrigin = transform.position + Vector3.up * halfHeight + transform.forward * 0.4f;
        bool groundAhead = Physics.Raycast(forwardCheckOrigin, Vector3.down, 2f, islandLayer);

        Vector3 directionToPlayer = (targetHuman.position - transform.position);
        directionToPlayer.y = 0;
        directionToPlayer.Normalize();

        if (!groundAhead)
        {
            // Tester gauche et droite
            Vector3 rightOrigin = transform.position + Vector3.up * halfHeight + transform.right * 0.4f;
            Vector3 leftOrigin = transform.position + Vector3.up * halfHeight - transform.right * 0.4f;

            bool groundRight = Physics.Raycast(rightOrigin, Vector3.down, 2f, islandLayer);
            bool groundLeft = Physics.Raycast(leftOrigin, Vector3.down, 2f, islandLayer);

            Vector3 strafeDir = Vector3.zero;

            if (groundRight && groundLeft)
            {
                // Les deux sont praticables, choisir celui qui rapproche le plus du joueur
                float dotRight = Vector3.Dot(transform.right, directionToPlayer);
                strafeDir = dotRight >= 0f ? transform.right : -transform.right;
            }
            else if (groundRight)
            {
                strafeDir = transform.right;
            }
            else if (groundLeft)
            {
                strafeDir = -transform.right;
            }
            else
            {
                // Bord total, on freeze
                rb.linearVelocity = new Vector3(0, rb.linearVelocity.y, 0);
                return;
            }

            // Orienter progressivement vers le joueur meme en strafant
            if (directionToPlayer.magnitude > 0.1f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(directionToPlayer);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * stats.rotationSpeed);
            }

            rb.linearVelocity = new Vector3(strafeDir.x * stats.walkSpeed, rb.linearVelocity.y, strafeDir.z * stats.walkSpeed);
            return;
        }

        // Sol devant, avancer normalement
        if (directionToPlayer.magnitude > 0.1f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(directionToPlayer);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * stats.rotationSpeed);
        }

        rb.linearVelocity = new Vector3(transform.forward.x * stats.walkSpeed, rb.linearVelocity.y, transform.forward.z * stats.walkSpeed);
    }

    protected virtual void HandleStunBySprayState()
    {
        StopMovement();
        if (health != null && health.GetSprayStunTimeRemaining() <= 0f)
        {
            if (isOnIslandPlatform)
                currentState = State.OnIslandPlatform;
            else
                currentState = State.Idle;
            lastPathDestination = Vector3.positiveInfinity;
            DetectHumans();
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
    protected virtual void HandleRotatingToImpactState()
    {
        StopMovement();
        if (Time.time >= rotationToImpactEndTime)
        {
            isRotatingToImpact = false;
            currentState = State.Idle;
            return;
        }
        if (impactDirection.magnitude > 0.1f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(impactDirection);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * stats.rotationSpeed);
        }
    }

    public void StartRotationToImpact(Vector3 impactDir, float duration)
    {
        impactDirection = impactDir;
        impactDirection.y = 0;
        impactDirection.Normalize();
        rotationToImpactEndTime = Time.time + duration;
        isRotatingToImpact = true;
        currentState = State.RotatingToImpact;
    }

    public void CancelRotationToImpact()
    {
        if (isRotatingToImpact)
        {
            isRotatingToImpact = false;
            impactDirection = Vector3.zero;

            if (currentState == State.RotatingToImpact)
            {
                currentState = State.Idle;
            }
        }
    }
    public void StartBlastStun(float duration)
    {
        StartCoroutine(BlastStunCoroutine(duration));
    }

    IEnumerator BlastStunCoroutine(float duration)
    {
        AIPath aiPath = GetAIPath();
        if (aiPath != null)
            aiPath.enabled = false;
        enabled = false;

        yield return new WaitForSeconds(duration);

        if (aiPath != null)
            aiPath.enabled = true;
        enabled = true;
        currentState = State.Idle;
        lastPathDestination = Vector3.positiveInfinity;
    }
    public void TriggerSweepFromBlast()
    {
        if (animator != null)
            animator.SetTrigger("EpervierKnockdown");
    }
    public void OnStandUpAnimationEvent()
    {
        if (wasAlreadyShotDuringSweep) return;
        if (gameManager == null) return;
        if (!gameManager.IsInRedLight()) return;

        gameManager.ForceScheduleShot(gameObject);
    }
    // Event animation a poser sur l'anim Sweep (feedback detection cas sweep)
    public void OnSweepDetected()
    {
        if (gameManager == null || !gameManager.IsInRedLight()) return;
        if (wasAlreadyShotDuringSweep) return;
        EnemyDetectionFeedback feedback = GetComponent<EnemyDetectionFeedback>();
        if (feedback != null) feedback.OnDetected();
    }

    public void OnStandUpStart()
    {
        if (isKnockedDownByEpervier) return;

        isInStandupPhase = true;

        if (wasAlreadyShotDuringSweep) return;
        if (gameManager == null) return;
        if (!gameManager.IsInRedLight()) return;

        pendingSweepShot = true;
        gameManager.NotifyStandUpDetected(gameObject);
    }

    public void OnStandUpComplete()
    {
        if (isKnockedDownByEpervier) return;

        if (!pendingSweepShot)
        {
            isInStandupPhase = false;
            return;
        }
        pendingSweepShot = false;

        if (gameManager == null || !gameManager.IsInRedLight())
        {
            isInStandupPhase = false;
            return;
        }

        if (animator != null) animator.speed = 0f;
        gameManager.ExecuteSequenceShot(gameObject);
        StartCoroutine(StandupStunCoroutine());
    }

    IEnumerator StandupStunCoroutine()
    {
        yield return new WaitForSeconds(gameManager.sentinelSettings.stunZombieDuration);
        if (animator != null) animator.speed = 1f;
        isInStandupPhase = false;
        if (gameManager != null)
            gameManager.ResetSequenceTarget(gameObject);
    }

    public void StartSweepSequence()
    {
        if (isDead) return;
        if (isKnockedDownByEpervier) return;
        StartCoroutine(EpervierKnockdownCoroutine());
    }
}