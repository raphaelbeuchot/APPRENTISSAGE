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

        // Initialize spray ammo
        currentSprayAmmo = stats.maxSprayAmmo;  // Chargeur plein
        totalSprayAmmo = stats.totalSprayAmmoStart;
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

            // MODIFIÉ : Autoriser spray cone (140°) OU backstab cone (60° derrière)
            bool inSprayCone = angleToTarget <= stats.meleeConeAngle / 2f;
            bool inBackstabCone = angleToTarget <= 30f; // 60° cone = ±30°

            if (!inSprayCone && !inBackstabCone)
            {
                continue;
            }

            // GESTION ZOMBIES
            EnemyHealth enemyHealth = hit.GetComponent<EnemyHealth>();
            if (enemyHealth != null && !enemyHealth.IsDead())
            {
                hitSomething = true;

                EnemyAI_AStar enemyAI_AStar = hit.GetComponent<EnemyAI_AStar>();
                Rigidbody targetRb = hit.GetComponent<Rigidbody>();

                // === CHECK BACKSTAB CONDITIONS ===
                float distanceToEnemy = Vector3.Distance(transform.position, hit.transform.position);
                bool isBackstab = false;

                if (distanceToEnemy <= 1f) // Range backstab 1m
                {
                    // Check : Joueur dans les 60° du DOS de l'ennemi
                    Vector3 dirPlayerFromEnemy = (transform.position - hit.transform.position).normalized;
                    dirPlayerFromEnemy.y = 0f;
                    float angleFromEnemyBack = Vector3.Angle(-hit.transform.forward, dirPlayerFromEnemy);

                    if (angleFromEnemyBack <= 30f) // 60° cone = ±30°
                    {
                        isBackstab = true;
                    }
                }

                if (isBackstab)
                {
                    // === BACKSTAB ===
                    // PAS de consommation munition
                    wasFrontAttack = false;

                    // Knockback
                    if (targetRb != null)
                    {
                        Vector3 knockbackDir = (hit.transform.position - transform.position).normalized;
                        knockbackDir.y = 0;
                        targetRb.AddForce(knockbackDir * stats.backstabKnockbackForce, ForceMode.Impulse);
                    }

                    // Désactiver AI temporairement
                    if (enemyAI_AStar != null)
                    {
                        enemyAI_AStar.enabled = false;
                        StartCoroutine(ReenableAIAfterBackstab(enemyAI_AStar, enemyHealth.stats.backstabStunDuration));
                    }

                    // Dégâts
                    enemyHealth.TakeMeleeDamage(EnemyHealth.AttackType.Spray);

                    // Son backstab
                    if (audioSource != null && stats.sprayBackSound != null)
                    {
                        audioSource.PlayOneShot(stats.sprayBackSound);
                    }

                    Debug.Log($"BACKSTAB: {hit.gameObject.name} (no ammo used)");
                }
                else
                {
                    // === SPRAY NORMAL ===

                    // NOUVEAU : Check si joueur dans dos ennemi (même à distance > 1m)
                    Vector3 dirPlayerFromEnemy = (transform.position - hit.transform.position).normalized;
                    dirPlayerFromEnemy.y = 0f;
                    float angleFromEnemyBack = Vector3.Angle(-hit.transform.forward, dirPlayerFromEnemy);

                    if (angleFromEnemyBack <= 30f) // Dans le dos de l'ennemi (60° cone)
                    {
                        // MISS - skip cet ennemi
                        Debug.Log($"MISS: {hit.gameObject.name} - player in enemy back cone but too far for backstab");
                        continue; // Skip au prochain ennemi
                    }
                    // Consommer munition
                    if (!sprayUsed)
                    {
                        currentSprayAmmo--;
                        sprayUsed = true;
                        wasFrontAttack = true;
                    }

                    // CHECK ÉTAT ACTUEL DU ZOMBIE
                    if (enemyAI_AStar != null)
                    {
                        if (enemyAI_AStar.currentState == EnemyAI_AStar.State.Chasing)
                        {
                            // === CAS 1 : ENNEMI EN CHASE ===
                            // Stop brutal, pas de recul
                            if (targetRb != null)
                            {
                                targetRb.linearVelocity = new Vector3(0, targetRb.linearVelocity.y, 0);
                            }

                            // Passe en StunBySpray
                            enemyAI_AStar.currentState = EnemyAI_AStar.State.StunBySpray;
                            enemyHealth.SetSprayStunTime(enemyHealth.stats.stunSprayDuration);

                            Debug.Log($"SPRAY CHASE: {hit.gameObject.name} -> StunBySpray (no recoil)");
                        }
                        else if (enemyAI_AStar.currentState == EnemyAI_AStar.State.StunBySpray)
                        {
                            // === CAS 2 : ENNEMI DÉJÀ EN STUNBYSPRAY ===
                            // Recul + Cumul timer
                            if (targetRb != null)
                            {
                                Vector3 knockbackDir = (hit.transform.position - transform.position).normalized;
                                knockbackDir.y = 0;
                                Vector3 currentVel = targetRb.linearVelocity;
                                Vector3 desiredVel = knockbackDir * stats.knockbackForce;
                                Vector3 velocityChange = desiredVel - currentVel;
                                targetRb.AddForce(velocityChange, ForceMode.VelocityChange);
                            }

                            // Cumul timer
                            enemyHealth.AddSprayStunTime(enemyHealth.stats.stunSprayDuration);

                            Debug.Log($"SPRAY STUN: {hit.gameObject.name} recoil + cumul timer ({enemyHealth.GetSprayStunTimeRemaining()}s)");
                        }
                        else
                        {
                            // === CAS 3 : ENNEMI EN IDLE/WANDERING ===
                            // Recul + Passe en StunBySpray
                            if (targetRb != null)
                            {
                                Vector3 knockbackDir = (hit.transform.position - transform.position).normalized;
                                knockbackDir.y = 0;
                                Vector3 currentVel = targetRb.linearVelocity;
                                Vector3 desiredVel = knockbackDir * stats.knockbackForce;
                                Vector3 velocityChange = desiredVel - currentVel;
                                targetRb.AddForce(velocityChange, ForceMode.VelocityChange);
                            }

                            // Passe en StunBySpray
                            enemyAI_AStar.currentState = EnemyAI_AStar.State.StunBySpray;
                            enemyHealth.SetSprayStunTime(enemyHealth.stats.stunSprayDuration);

                            Debug.Log($"SPRAY IDLE: {hit.gameObject.name} -> StunBySpray (with recoil)");
                        }
                    }

                    // Dégâts
                    enemyHealth.TakeMeleeDamage(EnemyHealth.AttackType.Spray);
                }

                // Notifier Blinders (commun spray/backstab)
                MeleeAudioManager.TriggerMeleeHit(hit.transform.position);

                // Check Blinder
                ChargeAttack chargeAttack = hit.GetComponent<ChargeAttack>();
                if (chargeAttack != null)
                {
                    chargeAttack.OnDirectHit(transform.position);
                }
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

        EnemyAI_AStar zombieAI_AStar = target.GetComponent<EnemyAI_AStar>();

        // Pas besoin de redisable, deja fait dans DetectAndHit

        yield return new WaitForSeconds(2f);

        // Reactiver
        if (zombieAI_AStar != null && target != null)
        {
            if (zombieAI_AStar.isInPitMode)
            {
                // En PitMode : juste reactiver AI (AIPath reste disabled)
                zombieAI_AStar.enabled = true;
            }
            else
            {
                // Hors pit : reactiver AI + AIPath
                Pathfinding.AIPath aiPath = target.GetComponent<Pathfinding.AIPath>();
                if (aiPath != null)
                {
                    aiPath.enabled = true;
                }
                zombieAI_AStar.enabled = true;
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

    IEnumerator ReenableAIAfterBackstab(EnemyAI_AStar ai, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (ai != null)
        {
            ai.enabled = true;
        }
    }

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