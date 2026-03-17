using System.Collections;
using UnityEngine;

public class GrabAttack : MonoBehaviour, IAttackBehavior
{
    private Material windupMaterial;

    private EnemyAI_AStar enemy;
    private PlayerPhysicsMovement player;
    private Rigidbody playerRb;
    private Rigidbody enemyRb;
    private EnemyStats stats;
    public PlayerStats playerStats;
    private Transform enemyTransform;
    private GameManager gameManager;
    private MeleeAttackSystem playerMelee;
    private EnemyHealth enemyHealth;
    private Animator animator;

    public bool isInWindup = false;
    private Coroutine windupCoroutine;

    public bool willBeGrabbed = false;
    public float windupEndTime = 0f;

    public bool isLockedInIdle = false;


    // Visuel windup
    private Renderer enemyRenderer;
    private Material originalMaterial;
    private Vector3 originalScale;

    // Nouveau systeme de timings fixes
    private float[] grabDamageTimes = { 2f, 3.5f, 4.5f };
    private int nextDamageIndex = 0;
    private float grabTotalDuration = 5f;
    private float grabElapsedTime = 0f;

    public bool isGrabbing = false;
    public bool isInBourradeDuration = false;
    public bool isInBourradeCooldown = false;
    public bool isFakeGrabbing = false;
    private bool windupAnimComplete = false;


    private System.Collections.Generic.List<EnemyAI_AStar> fakeGrabbers = new System.Collections.Generic.List<EnemyAI_AStar>();
    public bool IsInBourrade() => isInBourradeDuration || isInBourradeCooldown;

    public void Initialize(EnemyStats stats, PlayerStats playerStats, Transform enemyTransform, Rigidbody enemyRigidbody)
    {
        fakeGrabbers = new System.Collections.Generic.List<EnemyAI_AStar>();

        this.stats = stats;
        this.playerStats = playerStats;
        this.enemyTransform = enemyTransform;
        this.enemyRb = enemyRigidbody;
        enemy = GetComponent<EnemyAI_AStar>();
        enemyHealth = GetComponent<EnemyHealth>();
        player = FindFirstObjectByType<PlayerPhysicsMovement>();
        if (player) playerRb = player.GetComponent<Rigidbody>();
        gameManager = FindFirstObjectByType<GameManager>();
        if (player) playerMelee = player.GetComponent<MeleeAttackSystem>();

        isGrabbing = false;
        isInBourradeDuration = false;
        isInBourradeCooldown = false;
        isFakeGrabbing = false;

        // Setup visuel
        enemyRenderer = GetComponentInChildren<Renderer>();
        if (enemyRenderer != null)
            originalMaterial = new Material(enemyRenderer.material);
        windupMaterial = stats.windupMaterial;

        if (enemyRenderer != null)
        {
            originalMaterial = enemyRenderer.material;
        }
        originalScale = transform.localScale;
        animator = GetComponent<Animator>();
        if (animator != null)
            animator.SetLayerWeight(2, 1f);
    }

    void UpdateFakeGrabbers()
    {
        if (!player || player.grabState != PlayerPhysicsMovement.GrabState.Grabbed)
        {
            fakeGrabbers.Clear();
            return;
        }

        Collider[] nearbyZombies = Physics.OverlapSphere(
            player.transform.position,
            stats.fakeGrabRange,
            LayerMask.GetMask("Zombie")
        );

        fakeGrabbers.Clear();

        foreach (Collider col in nearbyZombies)
        {
            if (col.gameObject == gameObject) continue;

            EnemyAI_AStar zombie = col.GetComponent<EnemyAI_AStar>();

            GrabAttack zombieGrab = col.GetComponent<GrabAttack>();
            ChargeAttack chargeAttack = col.GetComponent<ChargeAttack>();

            if (zombie != null)
            {
                if (zombieGrab != null && !zombieGrab.isGrabbing)
                {
                    fakeGrabbers.Add(zombie);
                    zombieGrab.isFakeGrabbing = true;
                }
                else if (chargeAttack != null && chargeAttack.CanAttack())
                {
                    fakeGrabbers.Add(zombie);
                }
            }
        }
    }
    IEnumerator LockInIdleCoroutine(float duration)
    {
        isLockedInIdle = true;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (enemy != null)
            {
                enemy.currentState = EnemyAI_AStar.State.Idle;
                enemy.StopMovement();
            }
            elapsed += Time.deltaTime;
            yield return null;
        }
        if (enemy != null)
            enemy.ResetChaseState();
        isLockedInIdle = false;

        if (enemy != null)
        {
            enemy.DetectHumans();
            if (enemy.isOnIslandPlatform)
                enemy.currentState = EnemyAI_AStar.State.OnIslandPlatform;
        }
    }
    public bool CanAttack() => !isInBourradeDuration && !isInBourradeCooldown;
    public bool IsAttacking() => isGrabbing;
    public bool IsInSpecialState() => isInBourradeDuration || isInBourradeCooldown || isInWindup;
    public bool IsGrabbing() => isGrabbing;

    public void AttemptAttack(GameObject target)
    {
        if (!player || IsInBourrade() || isGrabbing) return;

        EnemyAI_AStar ai = GetComponent<EnemyAI_AStar>();

        IMovingPlatform playerPlatform = player.GetCurrentPlatform();
        Debug.Log($"[GRAB DEBUG] Player platform: {(playerPlatform != null ? playerPlatform.GetTransform().name : "NULL")}");

        // === CHECK IMMUNITE GRABS ===
        if (player != null && player.isImmuneToGrab)
        {
            Debug.Log($"{gameObject.name} cannot grab - player is immune (climbing/falling)");
            return;
        }
        // Pas de grab si player sur rotating platform
        if (playerPlatform != null && playerPlatform.GetTransform().GetComponent<RotatingPlatform>() != null)
        {
            Debug.Log($"{gameObject.name} cannot grab - player is on rotating platform");
            return;
        }


        float dist = Vector3.Distance(transform.position, player.transform.position);
        if (dist > stats.attackRange) return;

        // === LINE-OF-SIGHT CHECK ===
        Vector3 rayOrigin = transform.position + Vector3.up * 0.5f;
        Vector3 targetPoint = player.transform.position + Vector3.up * 0.5f;
        Vector3 directionToTarget = (targetPoint - rayOrigin).normalized;
        float distance = Vector3.Distance(rayOrigin, targetPoint);

        // DEBUG VISUEL - GARDE LA SCENE VIEW OUVERTE
        Debug.DrawRay(rayOrigin, directionToTarget * distance, Color.red, 2f);
        Debug.Log($"[GRAB DEBUG] Raycast from {rayOrigin} to {targetPoint}, distance={distance}");

        RaycastHit hitInfo;
        if (Physics.Raycast(rayOrigin,
                            directionToTarget,
                            out hitInfo,
                            distance,
                            LayerMask.GetMask("Obstacle")))
        {
            Debug.Log($"[GRAB] {gameObject.name} HIT OBSTACLE: {hitInfo.collider.name} at distance {hitInfo.distance}");
            return;
        }
        else
        {
            Debug.Log($"[GRAB] {gameObject.name} NO OBSTACLE DETECTED - grab allowed");
        }
        // === FIN LINE-OF-SIGHT CHECK ===

        if (player.grabState == PlayerPhysicsMovement.GrabState.None)
        {
            StartCoroutine(GrabCoroutine());
        }
    }

    public void StartWindup()
    {
        if (isInWindup || isGrabbing || IsInBourrade()) return;
        if (animator != null)
        windupCoroutine = StartCoroutine(WindupCoroutine());
    }

    IEnumerator WindupCoroutine()
    {
        isInWindup = true;
        if (animator != null)
            animator.SetTrigger("WindUpTrigger");

        windupAnimComplete = false;
        StartCoroutine(WindupPulseEffect());

        while (isInWindup && !windupAnimComplete)
        {
            if (enemyHealth == null || enemyHealth.IsDead())
            {
                isInWindup = false;
                RestoreVisual();
                enemy.currentState = EnemyAI_AStar.State.Idle;
                yield break;
            }

            if (enemyHealth.IsRecovering())
            {
                isInWindup = false;
                RestoreVisual();
                if (animator != null)
                {
                    animator.SetTrigger("WindupFailTrigger");
                    animator.SetLayerWeight(2, 0f);
                }
                enemy.currentState = EnemyAI_AStar.State.Idle;
                yield break;
            }
            if (player != null)
            {
                Vector3 dirToPlayerWindup = (player.transform.position - transform.position).normalized;
                dirToPlayerWindup.y = 0f;
                if (dirToPlayerWindup.magnitude > 0.1f)
                {
                    Quaternion targetRot = Quaternion.LookRotation(dirToPlayerWindup);
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * 8f);
                }
            }
            yield return null;
        }
        RestoreVisual();
        isInWindup = false;
        windupEndTime = Time.time;

        float finalDist = Vector3.Distance(transform.position, player.transform.position);
        bool playerFree = player.grabState == PlayerPhysicsMovement.GrabState.None;
        bool playerInRange = finalDist <= stats.attackRange;

        Vector3 dirToPlayer = (player.transform.position - transform.position).normalized;
        float angleToPlayer = Vector3.Angle(transform.forward, dirToPlayer);
        bool playerInCone = angleToPlayer <= stats.detectionAngle / 2f;

        // Juste avant le calcul de willBeGrabbed
        Vector3 losOrigin = transform.position + Vector3.up * 0.5f;
        Vector3 losTarget = player.transform.position + Vector3.up * 0.5f;
        Vector3 losDir = (losTarget - losOrigin).normalized;
        float losDist = Vector3.Distance(losOrigin, losTarget);

        bool hasLOS = !Physics.Raycast(losOrigin, losDir, losDist, LayerMask.GetMask("Obstacle"));

        willBeGrabbed = playerInRange && playerFree && playerInCone && hasLOS;
        if (!willBeGrabbed)
        {
            if (animator != null)
                animator.SetTrigger("WindupFailTrigger");
            StartCoroutine(LockInIdleCoroutine(1.5f));
            yield break;
        }

        // Grab confirme
        if (animator != null)
            animator.SetTrigger("GrabTrigger");
        Vector3 dirToPlayerSnap = (player.transform.position - transform.position).normalized;
        dirToPlayerSnap.y = 0f;
        transform.rotation = Quaternion.LookRotation(dirToPlayerSnap);
        Vector3 snapPosition = player.transform.position - dirToPlayerSnap * (stats.attackRange * 0.5f);
        snapPosition.y = transform.position.y;
        transform.position = snapPosition;
        StartCoroutine(GrabCoroutine());
    }

    public void OnWindupComplete()
    {
        windupAnimComplete = true;
    }

    IEnumerator WindupPulseEffect()
    {
        float elapsed = 0f;
        float pulseSpeed = 3f;

        while (!windupAnimComplete)
        {
            elapsed += Time.deltaTime;

            float scaleMultiplier = 1f + Mathf.Sin(elapsed * pulseSpeed) * 0.1f;
            transform.localScale = originalScale * scaleMultiplier;

            if (enemyRenderer != null && windupMaterial != null)
                enemyRenderer.material = windupMaterial;

            yield return null;
        }

        RestoreVisual();
    }

    void RestoreVisual()
    {
        transform.localScale = originalScale;

        if (enemyRenderer != null && originalMaterial != null)
            enemyRenderer.material = originalMaterial;
    }

    public void CancelWindup()
    {
        if (!isInWindup) return;

        isInWindup = false;
        windupAnimComplete = true; // STOPPE WindupPulseEffect
        if (windupCoroutine != null)
        {
            StopCoroutine(windupCoroutine);
            windupCoroutine = null;
        }
        if (animator != null)
            animator.SetTrigger("WindupFailTrigger");
        RestoreVisual();

        if (enemy != null)
        {
            enemy.currentState = EnemyAI_AStar.State.Idle;
            enemy.lastPathDestination = Vector3.positiveInfinity;
        }
    }

    void StartGrab()
    {
        GameUIManager ui = FindObjectOfType<GameUIManager>();
        if (ui != null)
            ui.RegisterGrab(this);
    }

    IEnumerator GrabCoroutine()
    {
        animator.SetTrigger("GrabTrigger");
        isGrabbing = true;


        // NOUVEAU : Enregistrer le grab pour l'UI
        StartGrab();

        player.grabState = PlayerPhysicsMovement.GrabState.Grabbed;
        player.ForceStop();
        player.grabState = PlayerPhysicsMovement.GrabState.Grabbed;
        player.ForceStop();

        // APRÈS
        playerRb.constraints = RigidbodyConstraints.FreezeAll;
        enemyRb.constraints = RigidbodyConstraints.FreezeRotation;
        Vector3 grabOffset = enemyTransform.position - player.transform.position;


        if (playerMelee) playerMelee.OnGrabStart();

        BroomAttackSystem playerBroom = player.GetComponent<BroomAttackSystem>();
        if (playerBroom) playerBroom.OnGrabStart();

        int mashCount = 0;
        int required = player.stats.mashesToEscape;

        float elapsed = 0f;

        // Timings des degats intermediaires
        float[] damageTimes = { 2f, 3.5f, 4f };
        int nextDamageIndex = 0;

        try
        {
            while (elapsed < stats.grabDuration && mashCount < required)
            {
                if (enemyHealth == null || enemyHealth.IsDead())
                {
                    EndGrab(false);
                    yield break;
                }
                // Annuler grab si player touché par sentinelle
                if (gameManager != null && gameManager.stunBySentinel)
                {
                    Debug.Log($"{gameObject.name} grab cancelled - player shot by sentinel");
                    EndGrab(false);
                    yield break;
                }
                if (enemy != null && enemy.isStunnedBySentinel)
                {
                    Debug.Log($"{gameObject.name} grab cancelled - shot by sentinel during grab");
                    EndGrab(false);
                    yield break;
                }
                UpdateFakeGrabbers();

                if (PlayerInputManager.Instance.MashEscapePressed)
                {
                    mashCount++;
                }

                player.grabProgress = (float)mashCount / required;
                elapsed += Time.deltaTime;

                // Degats intermediaires
                if (nextDamageIndex < damageTimes.Length && elapsed >= damageTimes[nextDamageIndex])
                {
                    PlayerHealth ph = player.GetComponent<PlayerHealth>();
                    if (ph != null) ph.TakeDamage((int)stats.biteTickDamage);
                    nextDamageIndex++;
                }
                // COLLAGE AU PLAYER
                // Si une plateforme (ou autre) déplace le player, l'enemy maintient
                // sa position relative en utilisant MovePosition (respecte la physique).
                Vector3 desiredEnemyPos = player.transform.position + grabOffset;
                if (Vector3.Distance(enemyRb.position, desiredEnemyPos) > 0.01f)
                {
                    enemyRb.MovePosition(desiredEnemyPos);
                }
                //

                yield return null;
            }

            // Morsure finale a la fin du grab si le joueur n'a pas echappe
            if (mashCount < required)
            {
                PlayerHealth ph = player.GetComponent<PlayerHealth>();
                if (ph != null) ph.TakeDamage((int)stats.biteDamage);

                if (stats.attackSound != null)
                {
                    AudioSource.PlayClipAtPoint(stats.attackSound, transform.position);
                }
            }

            EndGrab(mashCount >= required);
        }
        finally
        {
            playerRb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            enemyRb.constraints = RigidbodyConstraints.FreezeRotation;

            if (player != null && player.grabState == PlayerPhysicsMovement.GrabState.Grabbed)
                player.grabState = PlayerPhysicsMovement.GrabState.None;
           

            isGrabbing = false;
        }
    }


    IEnumerator PlayerRecoilCoroutine()
    {
        player.grabState = PlayerPhysicsMovement.GrabState.Recoil;
        Vector3 recoilDir = (player.transform.position - transform.position).normalized;
        recoilDir.y = 0;

        playerRb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

        try
        {
            yield return null;

            playerRb.linearVelocity = recoilDir * player.stats.recoilForce;
            Vector3 vel = playerRb.linearVelocity;
            vel.y = 0f;
            playerRb.linearVelocity = vel;

            yield return new WaitForSeconds(0.3f);
        }
        finally
        {
            if (player != null && player.grabState == PlayerPhysicsMovement.GrabState.Recoil)
            {
                player.grabState = PlayerPhysicsMovement.GrabState.None;
                player.lastGrabEndTime = Time.time;
            }

            if (playerMelee) playerMelee.OnGrabEnd();

            BroomAttackSystem playerBroom = player.GetComponent<BroomAttackSystem>();
            if (playerBroom) playerBroom.OnGrabEnd();
        }
    }

    void EndGrab(bool givePlayerRecoil = true)
    {
        if (animator != null)
            animator.SetTrigger("GrabEndTrigger");
        grabElapsedTime = 0f;
        nextDamageIndex = 0;

        if (isGrabbing && givePlayerRecoil)
        {
            StartCoroutine(PlayerRecoilCoroutine());
        }

        else
        {
            StartCoroutine(PlayerReleaseRecoilCoroutine()); // Utilise la nouvelle coroutine
        }

        isGrabbing = false;
        StartCoroutine(BourradeZombie());

        foreach (EnemyAI_AStar fakeZombie in fakeGrabbers)
        {
            if (fakeZombie != null)
            {
                GrabAttack fakeGrab = fakeZombie.GetComponent<GrabAttack>();
                if (fakeGrab != null)
                {
                    fakeGrab.isFakeGrabbing = false;
                    fakeGrab.StartCoroutine(fakeGrab.BourradeZombie());
                }

                ChargeAttack chargeAttack = fakeZombie.GetComponent<ChargeAttack>();
                if (chargeAttack != null)
                {
                    Vector3 dir = (fakeZombie.transform.position - player.transform.position).normalized;
                    dir.y = 0f;
                    chargeAttack.ApplyBourrade(dir, playerStats.bourradeForce, stats.bourradeDuration);
                }
            }
        }

        fakeGrabbers.Clear();
    }

    IEnumerator PlayerReleaseRecoilCoroutine()
    {
        player.grabState = PlayerPhysicsMovement.GrabState.Recoil;
        Vector3 recoilDir = (player.transform.position - transform.position).normalized;
        recoilDir.y = 0;

        playerRb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

        try
        {
            yield return null;

            playerRb.linearVelocity = recoilDir * 2f; // Force de 50 au lieu de 100
            Vector3 vel = playerRb.linearVelocity;
            vel.y = 0f;
            playerRb.linearVelocity = vel;

            yield return new WaitForSeconds(0.3f);
        }
        finally
        {
            if (player != null && player.grabState == PlayerPhysicsMovement.GrabState.Recoil)
            {
                player.grabState = PlayerPhysicsMovement.GrabState.None;
                player.lastGrabEndTime = Time.time;
            }

            if (playerMelee) playerMelee.OnGrabEnd();

            BroomAttackSystem playerBroom = player.GetComponent<BroomAttackSystem>();
            if (playerBroom) playerBroom.OnGrabEnd();
        }
    }

    IEnumerator BourradeZombie()
    {
        if (gameManager != null)
            gameManager.RemoveFromAlreadyShot(gameObject);

        Pathfinding.AIPath aiPath = GetComponent<Pathfinding.AIPath>();
        bool hadAIPath = aiPath != null;
        if (hadAIPath)
        {
            aiPath.enabled = false;
        }

        enemyRb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        isInBourradeDuration = true;
        if (animator != null)
            animator.SetTrigger("BourradeTrigger");
        isInBourradeCooldown = false;

        try
        {
            Vector3 dir = (transform.position - player.transform.position).normalized;
            dir.y = 0;
            yield return null;
            enemyRb.linearVelocity = dir * playerStats.bourradeForce;

            Vector3 vel = enemyRb.linearVelocity;
            vel.y = 0;
            enemyRb.linearVelocity = vel;

            // ACTIVER LE FLAG KNOCKBACK
            if (enemyHealth != null)
            {
                enemyHealth.SetKnockbackState(stats.bourradeDuration);
            }

            yield return new WaitForSeconds(stats.bourradeDuration);

            isInBourradeDuration = false;
            isInBourradeCooldown = true;
            if (animator != null)
                animator.SetBool("isChasing", false);
            enemyRb.linearVelocity = Vector3.zero;

            yield return new WaitForSeconds(stats.bourradeCooldown);
        }
        finally
        {
            isInBourradeDuration = false;
            isInBourradeCooldown = false;
            enemyRb.linearVelocity = Vector3.zero;
            enemyRb.constraints = RigidbodyConstraints.FreezeRotation;
            if (hadAIPath && aiPath != null)
            {
                aiPath.enabled = true;
            }

            // Force Idle pour eviter walk flash
            if (enemy != null && !enemy.isDead)
                StartCoroutine(LockInIdleCoroutine(1.5f));

            if (animator != null)
                animator.SetBool("isChasing", false);

            Debug.Log(gameObject.name + " > Bourrade ended cleanly");
        }
    }

    public void ForceStop()
    {
        StopAllCoroutines();
        if (animator != null)
            animator.SetTrigger("GrabEndTrigger");

        isGrabbing = false;
        isInBourradeDuration = false;
        isInBourradeCooldown = false;

        if (enemyRb != null)
        {
            enemyRb.linearVelocity = Vector3.zero;
            enemyRb.constraints = RigidbodyConstraints.FreezeRotation;
        }

        if (player != null && player.grabState != PlayerPhysicsMovement.GrabState.None)
            player.grabState = PlayerPhysicsMovement.GrabState.None;

        if (playerMelee) playerMelee.OnGrabEnd();

        Debug.Log(gameObject.name + " > ForceStop executed, back to normal");
    }

    IEnumerator FlashRedMaterialCoroutine(Renderer renderer, Material redMat, float duration)
    {
        if (renderer == null || redMat == null) yield break;

        Material originalMat = renderer.material;
        float elapsed = 0f;
        bool toggle = false;

        while (elapsed < duration)
        {
            renderer.material = toggle ? redMat : originalMat;
            toggle = !toggle;
            elapsed += 0.1f;
            yield return new WaitForSeconds(0.1f);
        }

        renderer.material = originalMat;
    }
}