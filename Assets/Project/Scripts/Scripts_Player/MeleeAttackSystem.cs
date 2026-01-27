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
    private int currentSprayAmmo;  // Chargeur actuel
    private int totalSprayAmmo;    // Réserve totale
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

    private float lastSprayTime = 0f;


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

        // CORRIGE : Reserve = Total - Chargeur initial
        currentSprayAmmo = stats.maxSprayAmmo;
        totalSprayAmmo = stats.totalSprayAmmoStart - stats.maxSprayAmmo;
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

        // Sortir crouch dès qu'on appuie sur spray
        if (PlayerInputManager.Instance.SprayAttackPressed)
        {
            if (movement != null)
                movement.ExitCrouch();
        }

        // Sortir crouch dès qu'on lance bouteille
        if (PlayerInputManager.Instance.ThrowBottlePressed)
        {
            if (movement != null)
                movement.ExitCrouch();
        }

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

        // === SPRAY = RB/R1 uniquement ===
        // Tap simple
        if (PlayerInputManager.Instance.SprayAttackPressed && CanAttack())
        {
            StartCoroutine(PerformAttack());
        }
        // Maintien continu
        else if (PlayerInputManager.Instance.SprayAttackHeld && CanAttack() && Time.time >= lastSprayTime + stats.sprayFireRate)
        {
            lastSprayTime = Time.time;
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

        // Calculer combien on peut recharger
        int neededAmmo = stats.maxSprayAmmo - currentSprayAmmo;  // Espace libre dans le chargeur
        int ammoToReload = Mathf.Min(neededAmmo, totalSprayAmmo);  // Prend ce qui est dispo dans la réserve

        // Transférer de la réserve au chargeur
        currentSprayAmmo += ammoToReload;
        totalSprayAmmo -= ammoToReload;

        Debug.Log($"Reloaded! Chargeur: {currentSprayAmmo}/{stats.maxSprayAmmo}, Reserve: {totalSprayAmmo}");
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
            // Rechargement auto si on a encore de la réserve
            if (!isReloading && totalSprayAmmo > 0)
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
        Debug.Log("SPRAY ATTACK!");

        // CONSOMMER LA MUNITION DIRECTEMENT (peu importe si on touche ou pas)
        currentSprayAmmo--;

        Collider[] hits = Physics.OverlapSphere(
            transform.position + Vector3.up * 1f,
            stats.attackRange,
            LayerMask.GetMask("Zombie", "Swarm")
        );

        bool hitSomething = false;

        foreach (Collider hit in hits)
        {
            if (hit.gameObject == gameObject) continue;

            Vector3 directionToTarget = (hit.transform.position - transform.position).normalized;
            directionToTarget.y = 0f;

            float angleToTarget = Vector3.Angle(transform.forward, directionToTarget);

            // Cone de spray
            if (angleToTarget > stats.meleeConeAngle / 2f)
            {
                continue;
            }

            // LINE-OF-SIGHT CHECK
            Vector3 rayOrigin = transform.position + Vector3.up * 0.5f;
            Vector3 targetPoint = hit.bounds.center;
            Vector3 directionToTarget3D = (targetPoint - rayOrigin).normalized;
            float distance = Vector3.Distance(rayOrigin, targetPoint);

            RaycastHit hitInfo;
            if (Physics.Raycast(rayOrigin,
                                directionToTarget3D,
                                out hitInfo,
                                distance,
                                LayerMask.GetMask("Obstacle")))
            {
                Debug.Log($"[SPRAY] {hit.name} is behind obstacle, ignored");
                continue;
            }

            // GESTION ZOMBIES
            EnemyHealth enemyHealth = hit.GetComponent<EnemyHealth>();
            if (enemyHealth != null && !enemyHealth.IsDead())
            {
                hitSomething = true;

                EnemyAI_AStar enemyAI_AStar = hit.GetComponent<EnemyAI_AStar>();
                Rigidbody targetRb = hit.GetComponent<Rigidbody>();

                // KNOCKBACK
                if (targetRb != null)
                {
                    Vector3 knockbackDir = (hit.transform.position - transform.position).normalized;
                    knockbackDir.y = 0;
                    targetRb.AddForce(knockbackDir * stats.knockbackForce, ForceMode.VelocityChange);
                    enemyHealth.SetKnockbackState(enemyHealth.stats.knockbackStunDuration);
                }

                // STUN SIMPLE
                if (enemyAI_AStar != null)
                {
                    enemyAI_AStar.currentState = EnemyAI_AStar.State.StunBySpray;
                    enemyHealth.ApplySprayStun(enemyHealth.stats.sprayStunDuration);
                }

                // DÉGÂTS
                enemyHealth.TakeMeleeDamage(EnemyHealth.AttackType.Spray);

                // ANNULER WINDUP GRAB
                GrabAttack grab = enemyHealth.GetComponent<GrabAttack>();
                if (grab != null && grab.isInWindup)
                {
                    grab.CancelWindup();
                    Debug.Log($"[SPRAY] Cancelled {enemyHealth.gameObject.name} grab windup");
                }

                // NOTIFIER BLINDERS
                MeleeAudioManager.TriggerMeleeHit(hit.transform.position);

                ChargeAttack chargeAttack = hit.GetComponent<ChargeAttack>();
                if (chargeAttack != null)
                {
                    chargeAttack.OnDirectHit(transform.position);
                }

                Debug.Log($"SPRAY HIT: {hit.gameObject.name}");
            }

            // GESTION SWARMS
            SwarmController_AStar swarmController = hit.GetComponent<SwarmController_AStar>();
            if (swarmController != null && swarmController.IsAlive())
            {
                hitSomething = true;
                float damage = swarmController.stats.sprayDamageTaken;
                swarmController.TakeDamage(damage);
                Debug.Log($"SPRAY HIT SWARM: {hit.gameObject.name} for {damage} damage");
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

        // AUDIO + VFX (toujours jouer, peu importe si on touche ou pas)
        if (audioSource != null)
        {
            // Son spray
            if (stats.sprayFrontSound != null)
            {
                audioSource.PlayOneShot(stats.sprayFrontSound);
            }

            // VFX spray - spawn sur premier ennemi touché
            if (hitSomething && stats.sprayVFX != null && hits.Length > 0)
            {
                foreach (Collider hit in hits)
                {
                    if (hit.gameObject == gameObject) continue;

                    EnemyHealth enemyHealth = hit.GetComponent<EnemyHealth>();
                    if (enemyHealth != null && !enemyHealth.IsDead())
                    {
                        Vector3 vfxPos = hit.bounds.center;
                        Instantiate(stats.sprayVFX, vfxPos, Quaternion.identity);
                        break;
                    }
                }
            }
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
        Debug.Log($"CanThrowBottle check: bottleThrown={bottleThrown}, lockSystem={lockSystem != null}, IsLocked={lockSystem?.IsLocked}, grabState={movement?.grabState}");

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
                enemyHealth.TakeMeleeDamage(EnemyHealth.AttackType.Bottle);
            }

            // Stun + force chase
            EnemyAI_AStar enemyAI_AStar = enemy.GetComponent<EnemyAI_AStar>();
            if (enemyAI_AStar != null)
            {
                StartCoroutine(StunAndChaseEnemy_AStar(enemyAI_AStar));
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

    System.Collections.IEnumerator StunAndChaseEnemy_AStar(EnemyAI_AStar enemyAI)
    {
        enemyAI.canMove = false;
        Pathfinding.AIPath aiPath = enemyAI.GetComponent<Pathfinding.AIPath>();
        if (aiPath != null)
        {
            aiPath.canMove = false;
        }

        yield return new WaitForSeconds(stats.bottleStunDuration);

        PlayerPhysicsMovement player = FindObjectOfType<PlayerPhysicsMovement>();
        if (player != null)
        {
            enemyAI.targetHuman = player.transform;
            enemyAI.currentState = EnemyAI_AStar.State.Chasing;
            enemyAI.isForcedChase = true;
        }

        enemyAI.canMove = true;
        if (aiPath != null)
        {
            aiPath.canMove = true;
            if (player != null)
            {
                aiPath.destination = player.transform.position;
            }
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
    public int GetTotalSprayAmmo() => totalSprayAmmo;

}