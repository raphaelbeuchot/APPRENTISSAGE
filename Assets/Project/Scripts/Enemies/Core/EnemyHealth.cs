using UnityEngine;
using System;
using System.Collections;
using Pathfinding;

public class EnemyHealth : MonoBehaviour
{
    public enum AttackType { Spray, Broom, Bottle }


    [Header("Enemy Stats")]
    public EnemyStats stats;

    [SerializeField] private GameObject stunSpiralPrefab;
    private GameObject activeSpiral;

    [Header("Audio")]
    [Tooltip("Son joue quand le zombie meurt noye (non spatialise)")]
    public AudioClip crowdLaughterSound;

    [Header("Healthbar Display")]
    public bool recentlyHitBySentinel = false;
    private float sentinelHitDisplayDuration = 3f; // Durée d'affichage après hit sentinelle
    private Coroutine sentinelHitDisplayCoroutine;



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

    private float originalLinearDamping;
    private float originalAngularDamping;
    private float originalMass;

    private StunTimerUI stunTimerUI;
    private float maxTheoreticalStun = 0f;

    private Rigidbody rb;

    private GameObject activeStunSpiral;
    private Coroutine stunSpiralCoroutine;

    public bool destroyOnDeath = true;




    public event Action OnDeath;
    public event Action<float, float> OnHealthChanged;
    public event System.Action OnTakeDamage;


    // Health bar
    public EnemyHealthBarUI healthBarUI;
    private EnemyHealthBarManager healthBarManager;

    // SIMPLIFIÉ : Spray stun simple
    private float sprayStunTimer = 0f;

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

        float enemyHealthMult = ModifierApplier.Instance != null ? ModifierApplier.Instance.enemyMaxHealthMultiplier : 1f;
        currentHealth = stats.maxHealth * enemyHealthMult;
        maxTheoreticalStun = stats.sprayStunDuration;
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

        // SIMPLIFIÉ : Timer de stun spray
        if (sprayStunTimer > 0f)
        {
            sprayStunTimer -= Time.deltaTime;

            if (sprayStunTimer <= 0f)
            {
                sprayStunTimer = 0f;
                Debug.Log($"{gameObject.name} spray stun ended");
            }
        }

        

        // Desactiver knockback state quand timer expire
        if (isInKnockback && Time.time >= knockbackEndTime)
        {
            isInKnockback = false;
        }

        // Update UI timer
        if (stunTimerUI != null)
        {
            if (sprayStunTimer > 0f)
            {
                stunTimerUI.UpdateTimer(sprayStunTimer);
                if (!stunTimerUI.gameObject.activeSelf)
                    stunTimerUI.Show();
            }
            else
            {
                stunTimerUI.Hide();
            }
        }
        else if (sprayStunTimer > 0f && StunTimerManager.Instance != null)
        {
            GameObject timerObj = Instantiate(StunTimerManager.Instance.stunTimerPrefab, StunTimerManager.Instance.canvas.transform);
            stunTimerUI = timerObj.GetComponent<StunTimerUI>();
            StunTimerManager.Instance.RegisterEnemy(transform, stunTimerUI);
            stunTimerUI.Initialize(maxTheoreticalStun);
        }
    }

    public void ShowSpiral()
    {
        if (isDead) return; // AJOUT
        if (stunSpiralPrefab == null) return;
        if (activeSpiral != null) return;
        activeSpiral = Instantiate(stunSpiralPrefab, transform.position + Vector3.up * 1.8f, Quaternion.identity, transform);
    }

    public void HideSpiral()
    {
        if (activeSpiral != null)
        {
            Destroy(activeSpiral);
            activeSpiral = null;
        }

        // Nettoyage de securite : detruire tous les enfants avec "Spiral" dans le nom
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);
            if (child.name.Contains("Spiral"))
                Destroy(child.gameObject);
        }
    }

    public void SetKnockbackState(float duration)
    {
        isInKnockback = true;
        knockbackEndTime = Time.time + duration;
    }
    private void OnCollisionEnter(Collision collision)
    {

        if (isDead) return;

        // Check layer obstacle
        if (collision.gameObject.layer != LayerMask.NameToLayer("Obstacle"))
        {
            return;
        }


        // CHECK : Seulement si en état forcé
        bool shouldBounce = false;

        // 1. Check knockback
        if (isInKnockback)
        {
            Debug.Log("[ENEMYHEALTH] isInKnockback = TRUE");
            shouldBounce = true;
        }

        // 2. Check bourrade
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
        OnTakeDamage?.Invoke();
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
        OnTakeDamage?.Invoke();
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

    public void TakeSentinelShot(bool isHeadshot = false)
    {
        if (isDead) return;

        lastDeathType = DeathContext.DeathType.Sentinel;

        EnemyAI_AStar ai = GetComponent<EnemyAI_AStar>();
        if (ai != null && ai.isKnockedDownByEpervier)
            ai.wasAlreadyShotDuringEpervier = true;
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
        OnTakeDamage?.Invoke();

        Debug.Log($"{gameObject.name} shot by sentinel! Health: {currentHealth}/{stats.maxHealth}");
        OnHealthChanged?.Invoke(currentHealth, stats.maxHealth);
        if (healthBarUI != null)
            healthBarUI.UpdateHealth(currentHealth, stats.maxHealth);

        // NOUVEAU : Déclencher l'affichage temporaire de la barre de vie
        if (sentinelHitDisplayCoroutine != null)
            StopCoroutine(sentinelHitDisplayCoroutine);
        sentinelHitDisplayCoroutine = StartCoroutine(SentinelHitDisplayCoroutine());

        if (currentHealth <= 0f)
        {
            Die();
            return;
        }

        // ANNULER WINDUP GRAB SI EN COURS
        GrabAttack grabCheck = GetComponent<GrabAttack>();
        if (grabCheck != null && grabCheck.isInWindup)
        {
            grabCheck.CancelWindup();
            Debug.Log($"[SENTINEL] Cancelled {gameObject.name} grab windup");
        }
        // ANNULER WINDUP HITTER SI EN COURS

        HitAttack hitAttackCheck = GetComponent<HitAttack>();
        if (hitAttackCheck != null && (hitAttackCheck.isInWindup || hitAttackCheck.IsAttacking()))
        {
            hitAttackCheck.CancelAttack();
            Debug.Log($"[SENTINEL] Cancelled {gameObject.name} hit attack");
        }

        
        Animator enemyAnimator = GetComponentInChildren<Animator>();
        if (enemyAnimator != null)
            enemyAnimator.SetTrigger("HitReaction");
        StartCoroutine(StunCoroutine());

        UpdateSpeed();
        // Knockback non-létal
        if (rb != null)
        {
            Vector3 knockbackDir = lastImpactDirection;
            knockbackDir.y = 0f;
            knockbackDir.Normalize();
            SetKnockbackState(0.5f);
            rb.AddForce(knockbackDir * stats.sentinelKnockbackForce, ForceMode.Impulse);
        }
    }

    // Coroutine pour reset le flag (déjà présente en haut du script)
    private IEnumerator SentinelHitDisplayCoroutine()
    {
        recentlyHitBySentinel = true;
        yield return new WaitForSeconds(sentinelHitDisplayDuration);
        recentlyHitBySentinel = false;
    }

    private IEnumerator DelayedKnockback(Vector3 force)
    {
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();
        if (!isDead && rb != null)
            rb.AddForce(force, ForceMode.Impulse);
    }
    private IEnumerator StunCoroutine()
    {
        if (isDead) yield break;

        isRecovering = true;

        EnemyAI_AStar zombieAI = GetComponent<EnemyAI_AStar>();
        if (zombieAI != null)
            zombieAI.CancelLastKnownPositionSearch();

        yield return new WaitForSeconds(2f);

        isRecovering = false;
    }



   

    void Die()
    {

        if (isDead) return;

        isDead = true;
        HideSpiral();


        Debug.Log(string.Format("{0} is dead!", gameObject.name));
        OnDeath?.Invoke();
        
        ChainConstraint cc = GetComponent<ChainConstraint>();
        if (cc != null)
            cc.enabled = false;


        // AJOUTER ICI : Désinscrire le timer UI
        if (stunTimerUI != null && StunTimerManager.Instance != null)
        {
            StunTimerManager.Instance.UnregisterEnemy(transform);
            stunTimerUI = null;
        }

        // Notifier GameManager du kill (sauf si BrightEyes)
        BrightEyesController brightEyes = GetComponent<BrightEyesController>();
        if (brightEyes == null)
        {
            GameManager gm = FindObjectOfType<GameManager>();
            if (gm != null)
            {
                gm.OnEnemyKilled();
            }
        }

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
        EnemyAI_AStar ai = GetComponent<EnemyAI_AStar>();
        if (ai != null)
        {
            ai.SetDead(true); // Set isDead = true ET currentState = Dead
            ai.enabled = false;
        }

        GrabAttack grabAttack = GetComponent<GrabAttack>();
        if (grabAttack != null)
        {
            if (grabAttack.isInWindup)
                grabAttack.CancelWindup();
            grabAttack.enabled = false;
        }

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


            // Check si mort par noyade pour jouer crowd laughter
            if (pitInt != null && pitInt.GetCurrentPitZone() != null)
            {
                // CORRIGÉ : Chercher PitFill dans les enfants
                PitFill pitFill = pitInt.GetCurrentPitZone().GetComponentInChildren<PitFill>();

                if (pitFill != null && pitFill.fillType != null)
                {
                    if (pitFill.fillType.category == PitContentType.ContentCategory.Water)
                    {
                        // Mort par noyade : crowd laughter non spatialise
                        if (crowdLaughterSound != null)
                        {
                            AudioSource.PlayClipAtPoint(crowdLaughterSound, Camera.main.transform.position);
                        }

                    }
                }

            }

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
        DeadBodyPhysics deadBody = GetComponent<DeadBodyPhysics>();
        if (deadBody != null)
            deadBody.Activate();
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

    // SIMPLIFIÉ : Démarrer un stun spray simple
    public void ApplySprayStun(float duration)
    {
        sprayStunTimer = duration;
        Debug.Log($"{gameObject.name} spray stun applied - {duration}s");
    }

    public bool IsRecovering() => isRecovering;
    public float GetCurrentHealth() => currentHealth;
    public float GetMaxHealth() => stats.maxHealth;
    public float GetHealthPercentage() => currentHealth / stats.maxHealth;

    // SIMPLIFIÉ : Retourner le timer de stun actuel
    public float GetSprayStunTimeRemaining()
    {
        return sprayStunTimer;
    }

    public bool IsStunnedBySpray()
    {
        return sprayStunTimer > 0f;
    }

    public int GetArmCount() => 2;

    private void OnDestroy()
    {
        if (stunTimerUI != null && StunTimerManager.Instance != null)
        {
            StunTimerManager.Instance.UnregisterEnemy(transform);
        }
    }
}