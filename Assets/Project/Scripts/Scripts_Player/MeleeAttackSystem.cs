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
    private TargetLockSystem lockSystem;


    // State runtime
    private bool isAttacking = false;
    private float lastAttackTime = 0f;

    // Grab state tracking
    private bool isGrabbed = false;

    // Spray ammo system
    private int currentSprayAmmo;
    private bool isReloading = false;
    private float reloadStartTime;

    private Vector3 originalScale;
    private AudioSource audioSource;

    // Visual
    private enum AttackArm { Left, Right }
    private AttackArm currentArm = AttackArm.Left;

    // Bottle throw system
    private bool bottleThrown = false;
    private int savedSprayAmmo = 0;
    private GameObject thrownBottleInstance;

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
        lockSystem = GetComponent<TargetLockSystem>();


        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        originalScale = transform.localScale;

        // Initialize spray ammo
        currentSprayAmmo = stats.maxSprayAmmo;
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

        HandleReload();

        // Check bottle pickup
        if (bottleThrown && thrownBottleInstance != null)
        {
            float distance = Vector3.Distance(transform.position, thrownBottleInstance.transform.position);
            if (distance <= 1.5f && PlayerInputManager.Instance.InteractPressed)
            {
                PickupBottle();
            }
        }

        // === SPRAY = RB/R1 uniquement (SprayAttackPressed) ===
        if (PlayerInputManager.Instance.SprayAttackPressed && CanAttack())
        {
            StartCoroutine(PerformAttack());
        }

        // === THROW BOTTLE = RT/R2 + lock-on (ThrowBottlePressed) ===
        if (PlayerInputManager.Instance.ThrowBottlePressed && CanThrowBottle())
        {
            ThrowBottle();
        }
    }
    void HandleReload()
    {
        if (PlayerInputManager.Instance.ReloadPressed && currentSprayAmmo < stats.maxSprayAmmo && !isReloading)
        {
            StartReload();
        }

        if (isReloading && Time.time >= reloadStartTime + stats.sprayReloadTime)
        {
            CompleteReload();
        }
    }

    void StartReload()
    {
        isReloading = true;
        reloadStartTime = Time.time;
        Debug.Log("Reloading spray...");
    }

    void CompleteReload()
    {
        isReloading = false;
        currentSprayAmmo = stats.maxSprayAmmo;
        Debug.Log("Spray reloaded!");
    }
    bool CanAttack()
    {

        // Cannot attack if bottle is thrown
        if (bottleThrown)
        {
            Debug.Log("Cannot attack: bottle is thrown, pick it up first!");
            return false;
        }
        // Check spray ammo
        if (currentSprayAmmo <= 0)
        {
            if (!isReloading)
            {
                StartReload();
            }
            Debug.Log("Cannot attack: no spray ammo!");
            return false;
        }

        if (isReloading)
        {
            Debug.Log("Cannot attack: reloading!");
            return false;
        }
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
        /*
        // CHECK STAMINA
        if (movement != null && movement.GetCurrentStamina() < stats.meleeStaminaCost)
        {
            Debug.Log("Cannot attack: not enough stamina!");
            return false;
        }
        */
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
            /*// CONSOMMER LA STAMINA
            if (movement != null)
            {
                float currentStamina = movement.GetCurrentStamina();
                movement.UpdateStamina(currentStamina - stats.meleeStaminaCost);
            }
            */

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
        bool sprayUsed = false;
        bool wasFrontAttack = false;

        foreach (Collider hit in hits)
        {
            if (hit.gameObject == gameObject) continue;

            Vector3 directionToTarget = (hit.transform.position - transform.position).normalized;
            directionToTarget.y = 0f;

            float angleToTarget = Vector3.Angle(transform.forward, directionToTarget);

            if (angleToTarget > stats.meleeConeAngle / 2f)
            {
                continue;
            }

            // GESTION ZOMBIES
            EnemyHealth enemyHealth = hit.GetComponent<EnemyHealth>();
            if (enemyHealth != null && !enemyHealth.IsDead())
            {
                hitSomething = true;

                // CHECK SI ENNEMI AWARE DU PLAYER
                EnemyAI enemyAI = hit.GetComponent<EnemyAI>();
                bool isPshitAttack = false;

                if (enemyAI != null)
                {
                    isPshitAttack = (enemyAI.currentState == EnemyAI.State.Chasing ||
                                    enemyAI.currentState == EnemyAI.State.Attacking);
                }

                // Consume spray ammo only once if pshit attack
                if (isPshitAttack && !sprayUsed)
                {
                    currentSprayAmmo--;
                    sprayUsed = true;
                    wasFrontAttack = true;
                    Debug.Log("PSHIT SPRAY used! Ammo: " + currentSprayAmmo + "/" + stats.maxSprayAmmo);

                    if (currentSprayAmmo <= 0 && !isReloading)
                    {
                        StartReload();
                    }
                }
                // Back attack: no ammo consumption but mark for sound
                if (!isPshitAttack)
                {
                    wasFrontAttack = false;
                    sprayUsed = true;
                }

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

                // Notifier les Blinders
                MeleeAudioManager.TriggerMeleeHit(hit.transform.position);

                // Check si c'est un Blinder
                ChargeAttack chargeAttack = hit.GetComponent<ChargeAttack>();
                if (chargeAttack != null)
                {
                    chargeAttack.OnDirectHit(transform.position);
                }
                else
                {
                    StartCoroutine(KnockdownTarget(hit.gameObject));
                }

                Debug.Log(gameObject.name + " hit " + hit.gameObject.name + " for " + damage + " damage!");
            }

            // GESTION NUEES
            SwarmController swarm = hit.GetComponent<SwarmController>();
            if (swarm != null)
            {
                hitSomething = true;
                float damage = stats.GetAdjustedDamage();
                swarm.TakeDamage(damage);
                Debug.Log(gameObject.name + " hit swarm for " + damage + " damage!");
            }

            // GESTION BRIGHT EYES
            BrightEyesController brightEyes = hit.GetComponent<BrightEyesController>();
            if (brightEyes != null && brightEyes.IsAlive() && !brightEyes.IsFlameExtinguished())
            {
                hitSomething = true;
                brightEyes.ExtinguishFlame();
                Debug.Log(gameObject.name + " extinguished " + hit.gameObject.name + "'s flame!");
            }
        }

        // Jouer le bon son + VFX
        if (audioSource != null)
        {
            if (sprayUsed)
            {
                // PSHIT SPRAY
                if (wasFrontAttack && stats.sprayFrontSound != null)
                {
                    audioSource.PlayOneShot(stats.sprayFrontSound);
                }
                else if (!wasFrontAttack && stats.sprayBackSound != null)
                {
                    audioSource.PlayOneShot(stats.sprayBackSound);
                }

                // Spawn VFX spray
                if (stats.sprayVFX != null)
                {
                    Vector3 spawnPos = transform.position + transform.forward * 1f + Vector3.up * 1f;
                    Instantiate(stats.sprayVFX, spawnPos, transform.rotation);
                }
            }
            else
            {
                // SPRAY A VIDE
                if (stats.sprayMissSound != null)
                {
                    audioSource.PlayOneShot(stats.sprayMissSound);
                }
            }
        }
    }


    IEnumerator KnockdownTarget(GameObject target)
    {
        // Skip knockdown si Blinder
        ChargeAttack chargeAttack = target.GetComponent<ChargeAttack>();
        if (chargeAttack != null)
        {
            yield break;
        }

        EnemyAI zombieAI = target.GetComponent<EnemyAI>();

        // NOUVEAU : Désactive NavMesh pendant le knockdown
        UnityEngine.AI.NavMeshAgent agent = target.GetComponent<UnityEngine.AI.NavMeshAgent>();
        bool hadAgent = agent != null;
        if (hadAgent && agent.isOnNavMesh)
        {
            agent.enabled = false;
        }

        if (zombieAI != null)
        {
            zombieAI.enabled = false;
        }

        yield return new WaitForSeconds(2f);

        // NOUVEAU : Réactive NavMesh
        if (hadAgent && agent != null)
        {
            agent.enabled = true;
        }

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

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, stats.attackRange);
    }
    public int GetCurrentSprayAmmo() => currentSprayAmmo;
    public int GetMaxSprayAmmo() => stats.maxSprayAmmo;
    public bool IsReloading() => isReloading;
    public float GetReloadProgress() => isReloading ? (Time.time - reloadStartTime) / stats.sprayReloadTime : 0f;

    bool CanThrowBottle()
    {
        if (bottleThrown)
        {
            Debug.Log("Cannot throw: bottle already thrown!");
            return false;
        }

        if (lockSystem == null || !lockSystem.IsLocked)
        {
            return false;
        }

        if (isGrabbed || movement.grabState != PlayerPhysicsMovement.GrabState.None)
        {
            return false;
        }

        if (health != null && health.IsDead())
        {
            return false;
        }

        return true;
    }

    void ThrowBottle()
    {
        if (stats.bottlePrefab == null)
        {
            Debug.LogError("Bottle prefab not assigned!");
            return;
        }

        if (lockSystem.CurrentTarget == null)
        {
            Debug.LogError("No target locked!");
            return;
        }

        // SAVE target avant de unlock
        Transform target = lockSystem.CurrentTarget;

        // Save current ammo
        savedSprayAmmo = currentSprayAmmo;
        bottleThrown = true;

        // Unlock target
        lockSystem.UnlockTarget();

        // Raycast vers la cible sauvegardée
        Vector3 origin = transform.position + Vector3.up * 1.5f;
        Vector3 direction = (target.position - origin).normalized;

        RaycastHit hit;
        if (Physics.Raycast(origin, direction, out hit, 50f, LayerMask.GetMask("Zombie")))
        {
            Debug.Log("Bottle raycast hit: " + hit.collider.gameObject.name);

            // Hit enemy
            GameObject enemy = hit.collider.gameObject;
            EnemyHealth enemyHealth = enemy.GetComponent<EnemyHealth>();
            if (enemyHealth != null && stats != null)
            {
                enemyHealth.TakeMeleeDamage(stats.bottleThrowDamage);
            }

            // Stun + force chase
            EnemyAI enemyAI = enemy.GetComponent<EnemyAI>();
            if (enemyAI != null)
            {
                StartCoroutine(StunAndChaseEnemy(enemyAI));
            }

            // Spawn bottle au sol pres de l'ennemi
            Vector3 bottleSpawnPos = hit.point;
            bottleSpawnPos.y = 0.5f;
            thrownBottleInstance = Instantiate(stats.bottlePrefab, bottleSpawnPos, Quaternion.identity);

            // Setup bottle
            BottleProjectile bottle = thrownBottleInstance.GetComponent<BottleProjectile>();
            if (bottle != null)
            {
                bottle.stats = stats;
                bottle.savedAmmo = savedSprayAmmo;
            }

            // Play sounds
            if (audioSource != null && stats.bottleThrowSound != null)
            {
                audioSource.PlayOneShot(stats.bottleThrowSound);
            }
            if (stats.bottleImpactSound != null)
            {
                AudioSource.PlayClipAtPoint(stats.bottleImpactSound, hit.point);
            }
        }

        Debug.Log("Bottle thrown! Ammo saved: " + savedSprayAmmo);
    }

    System.Collections.IEnumerator StunAndChaseEnemy(EnemyAI enemyAI)
    {
        enemyAI.canMove = false;
        UnityEngine.AI.NavMeshAgent agent = enemyAI.GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (agent != null && agent.isOnNavMesh)
        {
            agent.isStopped = true;
        }

        yield return new WaitForSeconds(stats.bottleStunDuration);

        PlayerPhysicsMovement player = FindObjectOfType<PlayerPhysicsMovement>();
        if (player != null)
        {
            enemyAI.targetHuman = player.transform;
            enemyAI.currentState = EnemyAI.State.Chasing;
            enemyAI.isForcedChase = true;
        }

        enemyAI.canMove = true;
        if (agent != null && agent.isOnNavMesh)
        {
            agent.isStopped = false;
            agent.SetDestination(player.transform.position);
        }

        Debug.Log(enemyAI.gameObject.name + " is now chasing after bottle hit!");
    }

    public bool IsBottleThrown() => bottleThrown;

    void PickupBottle()
    {
        if (thrownBottleInstance == null) return;

        // Restore ammo
        currentSprayAmmo = savedSprayAmmo;
        bottleThrown = false;

        // Destroy bottle
        Destroy(thrownBottleInstance);
        thrownBottleInstance = null;

        Debug.Log("Bottle picked up! Ammo restored: " + currentSprayAmmo);
    }

    public GameObject GetThrownBottle() => thrownBottleInstance;

}