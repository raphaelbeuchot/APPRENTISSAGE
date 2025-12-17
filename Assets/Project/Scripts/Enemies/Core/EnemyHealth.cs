using UnityEngine;
using System;
using System.Collections;
using Pathfinding;

public class EnemyHealth : MonoBehaviour
{
    public enum AttackType { Spray, Broom, Bottle }


    [Header("Enemy Stats")]
    public EnemyStats stats;

    [Header("Knockback State")]
    public bool isInKnockback = false;
    private float knockbackEndTime = 0f;

    private DeathContext.DeathType lastDeathType = DeathContext.DeathType.Other;
    private Vector3 lastImpactDirection = Vector3.forward;
    private float lastImpactForce = 1f;

    [HideInInspector] public bool deathByPit = false;

    private float currentHealth;
    private bool isDead = false;

    private bool isRecovering = false;
    private float recoverUntilTime = 0f;

    private float originalLinearDamping;
    private float originalAngularDamping;
    private float originalMass;

    private Rigidbody rb;

    public event Action OnDeath;
    public event Action<float, float> OnHealthChanged;

    // Health bar
    public EnemyHealthBarUI healthBarUI;
    private EnemyHealthBarManager healthBarManager;

    // NOUVEAU : Spray stun system 2 phases
    private bool isInSprayWindow = false;
    private float sprayWindowTimer = 0f;
    private int sprayCount = 0;
    private float finalStunTimer = 0f;
    private float cumulativeStunPerSpray;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            originalLinearDamping = rb.linearDamping;
            originalAngularDamping = rb.angularDamping;
            originalMass = rb.mass;
        }
    }

    void Start()
    {
        if (stats == null)
        {
            Debug.LogError("EnemyStats non assigne sur " + gameObject.name);
            return;
        }

        currentHealth = stats.maxHealth;
        cumulativeStunPerSpray = stats.cumulativeStunPerSpray; // AJOUTE CETTE LIGNE
        SetupDeathEffect();
        SetupHealthBar();
    }

    void SetupHealthBar()
    {
        healthBarManager = FindObjectOfType<EnemyHealthBarManager>();
        if (healthBarManager != null && healthBarManager.canvas != null && healthBarManager.healthBarPrefab != null)
        {
            GameObject barGO = Instantiate(healthBarManager.healthBarPrefab, healthBarManager.canvas.transform);
            healthBarUI = barGO.GetComponent<EnemyHealthBarUI>();
            healthBarManager.RegisterEnemy(transform, healthBarUI);
            healthBarUI.UpdateHealth(currentHealth, stats.maxHealth);
            healthBarUI.Hide();
        }
        else
        {
            Debug.LogWarning($"SetupHealthBar failed on {gameObject.name}");
        }
    }

    void SetupDeathEffect()
    {
        if (stats == null) return;

        switch (stats.deathEffectType)
        {
            case EnemyStats.DeathEffectType.Ragdoll:
                var ragdoll = gameObject.AddComponent<RagdollDeathEffect>();
                ragdoll.baseRagdollForce = stats.ragdollForce;
                ragdoll.ragdollTorque = stats.ragdollTorque;
                ragdoll.meleeMultiplier = stats.meleeForceMultiplier;
                ragdoll.sentinelMultiplier = stats.sentinelForceMultiplier;
                Debug.Log($"Added RagdollDeathEffect to {gameObject.name}");
                break;

            case EnemyStats.DeathEffectType.Explosion:
                var explosion = gameObject.AddComponent<ExplosionDeathEffect>();
                explosion.explosionVFX = stats.explosionVFX;
                explosion.explosionSound = stats.explosionSound;
                explosion.soundVolume = stats.explosionSoundVolume;
                explosion.vfxScale = stats.explosionVFXScale;
                explosion.swellDuration = stats.swellDuration;  // NOUVEAU
                explosion.swellScale = stats.swellScale;        // NOUVEAU
                Debug.Log($"Added ExplosionDeathEffect to {gameObject.name}");
                break;

            case EnemyStats.DeathEffectType.None:
                Debug.Log($"No death effect for {gameObject.name}");
                break;
        }
    }

    void Update()
    {
        if (isDead) return;

        // NOUVEAU : Système spray 2 phases
        if (isInSprayWindow)
        {
            // Phase 1 : Fenêtre de spray
            sprayWindowTimer -= Time.deltaTime;

            if (sprayWindowTimer <= 0f)
            {
                // Timer expiré : passer en phase 2 (stun final)
                sprayWindowTimer = 0f;
                isInSprayWindow = false;
                finalStunTimer = sprayCount * cumulativeStunPerSpray;

                Debug.Log($"{gameObject.name} fenêtre expirée - {sprayCount} sprays reçus  Stun final {finalStunTimer}s");
            }
        }
        else if (finalStunTimer > 0f)
        {
            // Phase 2 : Stun final
            finalStunTimer -= Time.deltaTime;

            if (finalStunTimer <= 0f)
            {
                finalStunTimer = 0f;
                Debug.Log($"{gameObject.name} stun final terminé");
            }
        }

        if (isRecovering && Time.time >= recoverUntilTime)
        {
            isRecovering = false;
            Debug.Log($"{gameObject.name} recovered from gunshot!");
        }

        // Désactiver knockback state quand timer expiré
        if (isInKnockback && Time.time >= knockbackEndTime)
        {
            isInKnockback = false;
        }
    }

    public void SetKnockbackState(float duration)
    {
        isInKnockback = true;
        knockbackEndTime = Time.time + duration;
    }
    private void OnCollisionEnter(Collision collision)
    {
        Debug.Log($"[ENEMYHEALTH] {gameObject.name} collided with {collision.gameObject.name} (layer: {collision.gameObject.layer})");

        if (isDead) return;

        // Check layer obstacle
        if (collision.gameObject.layer != LayerMask.NameToLayer("Obstacle"))
        {
            Debug.Log("[ENEMYHEALTH] Not obstacle layer, returning");
            return;
        }

        Debug.Log("[ENEMYHEALTH] Is obstacle layer!");

        // CHECK : Seulement si en état forcé
        bool shouldBounce = false;

        // 1. Check knockback
        if (isInKnockback)
        {
            Debug.Log("[ENEMYHEALTH] isInKnockback = TRUE");
            shouldBounce = true;
        }

        /*// 2. Check Blinder
        ChargeAttack chargeAttack = GetComponent<ChargeAttack>();
        if (chargeAttack != null)
        {
            Debug.Log($"[ENEMYHEALTH] ChargeAttack found, isStraightRunning = {chargeAttack.isStraightRunning}");
            if (chargeAttack.isStraightRunning)
                shouldBounce = true;
        }
        else
        {
            Debug.Log("[ENEMYHEALTH] No ChargeAttack component");
        }*/

        // 3. Check bourrade
        GrabAttack grabAttack = GetComponent<GrabAttack>();
        if (grabAttack != null && grabAttack.isInBourradeDuration)
        {
            Debug.Log("[ENEMYHEALTH] isInBourradeDuration = TRUE");
            shouldBounce = true;
        }

        Debug.Log($"[ENEMYHEALTH] shouldBounce = {shouldBounce}");

        if (!shouldBounce)
            return;

        Debug.Log("[ENEMYHEALTH] APPLYING WALL BOUNCE!");

        float velocity = rb.linearVelocity.magnitude;
        Debug.Log($"[ENEMYHEALTH] velocity = {velocity}");

        if (velocity > 0f && collision.contacts.Length > 0)
        {
            Vector3 pushDirection = collision.contacts[0].normal;
            pushDirection.y = 0;
            pushDirection.Normalize();

            Debug.Log($"[ENEMYHEALTH] pushDirection = {pushDirection}");

            rb.AddForce(pushDirection * 100f, ForceMode.Impulse);
        }
    }
    private Coroutine pulseCoroutine;
    public float pulseScaleMultiplier = 1.2f;
    public float pulseDuration = 0.3f;

    private IEnumerator PulseCoroutine()
    {
        Vector3 originalScale = transform.localScale;
        Vector3 targetScale = originalScale * pulseScaleMultiplier;
        float elapsed = 0f;

        while (elapsed < pulseDuration / 2f)
        {
            transform.localScale = Vector3.Lerp(originalScale, targetScale, elapsed / (pulseDuration / 2f));
            elapsed += Time.deltaTime;
            yield return null;
        }

        elapsed = 0f;
        while (elapsed < pulseDuration / 2f)
        {
            transform.localScale = Vector3.Lerp(targetScale, originalScale, elapsed / (pulseDuration / 2f));
            elapsed += Time.deltaTime;
            yield return null;
        }

        transform.localScale = originalScale;
    }

    // Version pour dégâts directs (pits, environnement, etc.)
    // VERSION 1 : Pour attaques joueur (spray/broom/bottle)
    public void TakeMeleeDamage(AttackType attackType)
    {
        // Check si Bright Eyes et pas awake
        BrightEyesController brightEyes = GetComponent<BrightEyesController>();
        if (brightEyes != null && !brightEyes.IsAwake())
        {
            Debug.Log($"{gameObject.name} is sleeping, immune to damage");
            return;
        }

        if (isDead) return;

        lastDeathType = DeathContext.DeathType.Melee;
        PlayerPhysicsMovement player = FindObjectOfType<PlayerPhysicsMovement>();
        if (player != null)
        {
            lastImpactDirection = (transform.position - player.transform.position).normalized;
        }

        // Déterminer les dégâts selon le type d'attaque
        float damage = 0f;
        switch (attackType)
        {
            case AttackType.Spray:
                damage = stats.sprayDamageTaken;
                break;
            case AttackType.Broom:
                damage = stats.broomDamageTaken;
                break;
            case AttackType.Bottle:
                damage = stats.bottleDamageTaken;
                break;
        }

        lastImpactForce = damage;

        currentHealth -= damage;
        currentHealth = Mathf.Max(0f, currentHealth);

        Debug.Log($"{gameObject.name} took {damage} {attackType} damage! Health: {currentHealth}/{stats.maxHealth}");

        OnHealthChanged?.Invoke(currentHealth, stats.maxHealth);
        if (healthBarUI != null)
        {
            healthBarUI.UpdateHealth(currentHealth, stats.maxHealth);
        }
        UpdateSpeed();

        if (currentHealth <= 0f)
        {
            Die();
        }
    }

    // VERSION 2 : Pour dégâts directs (pits, environnement)
    public void TakeMeleeDamage(float damage)
    {
        // Check si Bright Eyes et pas awake
        BrightEyesController brightEyes = GetComponent<BrightEyesController>();
        if (brightEyes != null && !brightEyes.IsAwake())
        {
            Debug.Log($"{gameObject.name} is sleeping, immune to damage");
            return;
        }

        if (isDead) return;

        lastDeathType = DeathContext.DeathType.Other;
        lastImpactDirection = Vector3.down;
        lastImpactForce = damage;

        currentHealth -= damage;
        currentHealth = Mathf.Max(0f, currentHealth);

        Debug.Log($"{gameObject.name} took {damage} damage! Health: {currentHealth}/{stats.maxHealth}");

        OnHealthChanged?.Invoke(currentHealth, stats.maxHealth);
        if (healthBarUI != null)
        {
            healthBarUI.UpdateHealth(currentHealth, stats.maxHealth);
        }
        UpdateSpeed();

        if (currentHealth <= 0f)
        {
            Die();
        }
    }

    // Démarre un stun spray (Chase mode - pas de cumul)
    
    public void TakeSentinelShot(bool isHeadshot = false)
    {
        if (isDead) return;

        lastDeathType = DeathContext.DeathType.Sentinel;

        // Direction d'impact pour effets physiques
        GameManager gm = FindObjectOfType<GameManager>();
        if (gm != null && gm.sentinelEye != null)
            lastImpactDirection = (transform.position - gm.sentinelEye.position).normalized;

        lastImpactForce = stats.sentinelDamageTaken;

        // Pulse visuel
        if (pulseCoroutine != null)
            StopCoroutine(pulseCoroutine);
        pulseCoroutine = StartCoroutine(PulseCoroutine());

        // Headshot instant kill
        if (isHeadshot)
        {
            Debug.Log($"{gameObject.name} HEADSHOT! Instant death!");
            currentHealth = 0f;
            Die();
            return;
        }

        // Appliquer les dégâts
        currentHealth -= stats.sentinelDamageTaken;
        currentHealth = Mathf.Max(0f, currentHealth);

        Debug.Log($"{gameObject.name} shot by sentinel! Health: {currentHealth}/{stats.maxHealth}");
        OnHealthChanged?.Invoke(currentHealth, stats.maxHealth);
        if (healthBarUI != null)
            healthBarUI.UpdateHealth(currentHealth, stats.maxHealth);

        if (currentHealth <= 0f)
        {
            Die();
            return;
        }

        // Déclencher le stun de 2s pour TOUS les zombies, indépendamment du grab
        StartCoroutine(StunCoroutine());

        UpdateSpeed();
    }

    private IEnumerator StunCoroutine()
    {
        if (isDead) yield break;

        isRecovering = true;

        // NOUVEAU : Utiliser A AIPath au lieu de NavMeshAgent
        Pathfinding.AIPath aiPath = GetComponent<Pathfinding.AIPath>();
        EnemyAI_AStar zombieAI = GetComponent<EnemyAI_AStar>(); // CORRIGE : _AStar

        // AJOUT : Annuler la recherche de derniere position
        if (zombieAI != null)
        {
            zombieAI.CancelLastKnownPositionSearch();
        }

        float originalSpeed = 0f;
        if (aiPath != null)
        {
            originalSpeed = aiPath.maxSpeed;
            aiPath.canMove = false;   // Stop la navigation A
        }

        if (zombieAI != null)
            zombieAI.canMove = false;       // Stoppe les actions de l'AI

        Debug.Log($"{gameObject.name} stunned for 2 seconds");

        yield return new WaitForSeconds(2f);

        // Restauration apres stun
        if (aiPath != null)
        {
            aiPath.canMove = true;
            aiPath.maxSpeed = originalSpeed;
        }

        if (zombieAI != null)
            zombieAI.canMove = true;

        isRecovering = false;
        Debug.Log($"{gameObject.name} stun ended");
    }



    void StartRecovery()
    {
        isRecovering = true;
        recoverUntilTime = Time.time + 2f;

        GrabAttack grabAttack = GetComponent<GrabAttack>();
        if (grabAttack != null && grabAttack.IsGrabbing())
        {
            grabAttack.ForceStop();
        }

        Debug.Log($"{gameObject.name} starts recovery (stunned for 2s)");
    }

    void Die()
    {
        
            if (isDead) return;

            isDead = true;
            Debug.Log(string.Format("{0} is dead!", gameObject.name));

            OnDeath?.Invoke();

            // NOUVEAU : Forcer la barre visible meme apres mort si en pit
            EnemyPitInteractable pitInt = GetComponent<EnemyPitInteractable>();
            if (pitInt != null && pitInt.isFallingInPit && healthBarUI != null)
            {
                healthBarUI.Show();
                Debug.Log(string.Format("[EnemyHealth] Forced healthbar show for {0} after pit death", gameObject.name));
            }

        /// Desinscrire la barre de vie (SAUF si dans deep empty pit)
        bool isInDeepPit = pitInt != null && pitInt.shouldIgnoreHealthbarDistance;

        if (healthBarManager != null && !isInDeepPit)
        {
            healthBarManager.UnregisterEnemy(transform);
        }
        else if (isInDeepPit)
        {
            Debug.Log(string.Format("[EnemyHealth] {0} died in deep pit, keeping healthbar for 2s", gameObject.name));
        }

        // Desactiver l'IA et les scripts de controle
        EnemyAI_AStar ai = GetComponent<EnemyAI_AStar>(); // MODIFIE : _AStar
        if (ai != null) ai.enabled = false;

        GrabAttack grabAttack = GetComponent<GrabAttack>();
        if (grabAttack != null) grabAttack.enabled = false;

        // NOUVEAU : Desactiver AIPath au lieu de NavMeshAgent
        Pathfinding.AIPath aiPath = GetComponent<Pathfinding.AIPath>();
        if (aiPath != null)
        {
            aiPath.canMove = false;
            aiPath.enabled = false;
        }
        // SI MORT PAR PIT: Pas de ragdoll, comportement selon type de pit
        if (deathByPit)
        {
            Debug.Log(string.Format("[EnemyHealth] {0} died in pit - no ragdoll", gameObject.name));

            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = false;
                rb.useGravity = true;
            }

            // NE PAS mettre isTrigger = true pour Empty pits
            // Le collider doit rester solide pour s'arreter sur le floor
            // Pour Water/Lava, PitFillDamageController gerera la destruction

            // Detruire apres un delai (gere par PitFillDamageController)
            return;
        }

        // SINON: Mort normale avec ragdoll
        Debug.Log($"========== {gameObject.name} DIE() - MORT NORMALE ==========");

        Rigidbody rbNormal = GetComponent<Rigidbody>();
        if (rbNormal != null)
        {
            Debug.Log($"[Before] isKinematic={rbNormal.isKinematic}, constraints={rbNormal.constraints}");

            rbNormal.isKinematic = false;
            rbNormal.constraints = RigidbodyConstraints.None;
            rbNormal.linearDamping = originalLinearDamping;
            rbNormal.angularDamping = originalAngularDamping;
            rbNormal.mass = originalMass;
            rbNormal.detectCollisions = true;

            Debug.Log($"[After] isKinematic={rbNormal.isKinematic}, constraints={rbNormal.constraints}");
        }

        Collider colNormal = GetComponent<Collider>();
        if (colNormal != null)
        {
            colNormal.isTrigger = false;
        }

        Debug.Log($"About to call IDeathEffect on {gameObject.name}");

        // Appliquer les effets de mort (ragdoll, explosion, etc.)
        IDeathEffect[] deathEffects = GetComponents<IDeathEffect>();
        Debug.Log($"Found {deathEffects.Length} death effects");

        DeathContext context = new DeathContext
        {
            deathType = lastDeathType,
            impactDirection = lastImpactDirection,
            impactForce = lastImpactForce
        };

        bool hasExplosionEffect = false;

        if (deathEffects != null && deathEffects.Length > 0)
        {
            foreach (IDeathEffect effect in deathEffects)
            {
                Debug.Log($"Calling OnDeath on {effect.GetType().Name}");
                effect.OnDeath(transform.position, context);

                if (effect is ExplosionDeathEffect)
                {
                    hasExplosionEffect = true;
                }
            }
        }
        else
        {
            Debug.LogWarning($"NO DEATH EFFECTS FOUND on {gameObject.name}!");
        }

        if (deathEffects != null && deathEffects.Length > 0)
        {
            foreach (IDeathEffect effect in deathEffects)
            {
                effect.OnDeath(transform.position, context);

                // Verifier si c'est une explosion (Bloated)
                if (effect is ExplosionDeathEffect)
                {
                    hasExplosionEffect = true;
                }
            }
        }

        // Event pour autres comportements a la mort
        // SAUF si explosion (qui gerera elle-meme le spawn apres delai)
        if (!hasExplosionEffect)
        {
            IOnDeathBehavior[] deathBehaviors = GetComponents<IOnDeathBehavior>();
            if (deathBehaviors != null && deathBehaviors.Length > 0)
            {
                foreach (IOnDeathBehavior behavior in deathBehaviors)
                {
                    behavior.OnEnemyDeath(transform.position);
                }
            }
        }
    }

    void UpdateSpeed()
    {
        EnemyAI_AStar ai = GetComponent<EnemyAI_AStar>();
        if (ai != null)
        {
            ai.UpdateSpeed(currentHealth, false);
        }
    }

    public bool IsDead() => isDead;

    public bool IsAlive()
    {
        return currentHealth > 0f;
    }

    public void StartSprayWindow(float windowDuration)
    {
        isInSprayWindow = true;
        sprayWindowTimer = windowDuration;
        sprayCount = 1;
        finalStunTimer = 0f;

        Debug.Log($"{gameObject.name} START spray window - count=1, window={windowDuration}s");
    }

    public void ExtendSprayWindow(float windowDuration)
    {
        if (isInSprayWindow)
        {
            sprayWindowTimer = windowDuration; // Repousse le timer
            sprayCount++;

            Debug.Log($"{gameObject.name} EXTEND spray window - count={sprayCount}, window reset to {windowDuration}s");
        }
        else
        {
            // Au cas où on spray pendant le stun final (edge case)
            StartSprayWindow(windowDuration);
        }
    }
    public bool IsRecovering() => isRecovering;
    public float GetCurrentHealth() => currentHealth;
    public float GetMaxHealth() => stats.maxHealth;
    public float GetHealthPercentage() => currentHealth / stats.maxHealth;

    public float GetSprayStunTimeRemaining()
    {
        if (isInSprayWindow)
            return sprayWindowTimer;
        else
            return finalStunTimer;
    }

    
    public int GetArmCount() => 2;
}