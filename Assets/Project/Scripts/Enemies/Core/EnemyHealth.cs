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
    private float sentinelHitDisplayDuration = 3f;
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

    // SIMPLIFIE : Spray stun simple
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

        currentHealth = GetMaxHealth();
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
            healthBarUI.Initialize(GetBaseMaxHealth(), GetMaxHealth());
            healthBarUI.UpdateHealth(currentHealth, GetBaseMaxHealth(), GetMaxHealth());
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
                ragdoll.corpseData = stats.corpseData;
                ragdoll.InitializeRagdoll();
                break;
            case EnemyStats.DeathEffectType.Explosion:
                // TODO: implementer quand le Kamikaze sera code
                Debug.Log("ExplosionDeathEffect pas encore implemente");
                break;
            case EnemyStats.DeathEffectType.None:
                break;
        }
    }

    void Update()
    {
        if (isDead) return;

        if (sprayStunTimer > 0f)
        {
            sprayStunTimer -= Time.deltaTime;

            if (sprayStunTimer <= 0f)
            {
                sprayStunTimer = 0f;
                Debug.Log($"{gameObject.name} spray stun ended");
            }
        }

        if (isInKnockback && Time.time >= knockbackEndTime)
        {
            isInKnockback = false;
        }

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
        if (isDead) return;
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

        if (collision.gameObject.layer != LayerMask.NameToLayer("Obstacle"))
        {
            return;
        }

        bool shouldBounce = false;

        if (isInKnockback)
        {
            shouldBounce = true;
        }

        


        if (!shouldBounce)
            return;


        float velocity = rb.linearVelocity.magnitude;

        if (velocity > 0f && collision.contacts.Length > 0)
        {
            Vector3 pushDirection = collision.contacts[0].normal;
            pushDirection.y = 0;
            pushDirection.Normalize();


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

    public void TakeMeleeDamage(AttackType attackType)
    {
        

        if (isDead) return;

        lastDeathType = DeathContext.DeathType.Melee;
        PlayerPhysicsMovement player = FindObjectOfType<PlayerPhysicsMovement>();
        if (player != null)
        {
            lastImpactDirection = (transform.position - player.transform.position).normalized;
        }

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


        OnHealthChanged?.Invoke(currentHealth, GetMaxHealth());
        if (healthBarUI != null)
            healthBarUI.UpdateHealth(currentHealth, GetBaseMaxHealth(), GetMaxHealth());

        UpdateSpeed();

        if (currentHealth <= 0f)
            Die();
    }

    public void TakeMeleeDamage(float damage)
    {
        BrightEyesController brightEyes = GetComponent<BrightEyesController>();
       

        if (isDead) return;

        lastDeathType = DeathContext.DeathType.Other;
        lastImpactDirection = Vector3.down;
        lastImpactForce = damage;

        currentHealth -= damage;
        OnTakeDamage?.Invoke();
        currentHealth = Mathf.Max(0f, currentHealth);


        OnHealthChanged?.Invoke(currentHealth, GetMaxHealth());
        if (healthBarUI != null)
            healthBarUI.UpdateHealth(currentHealth, GetBaseMaxHealth(), GetMaxHealth());

        UpdateSpeed();

        if (currentHealth <= 0f)
            Die();
    }

    public void TakeSentinelShot(bool isHeadshot = false)
    {
        if (isDead) return;

        lastDeathType = DeathContext.DeathType.Sentinel;

        EnemyAI_AStar ai = GetComponent<EnemyAI_AStar>();
        if (ai != null && ai.isKnockedDownByEpervier)
            ai.wasAlreadyShotDuringSweep = true;

        GameManager gm = FindObjectOfType<GameManager>();
        if (gm != null && gm.sentinelEye != null)
            lastImpactDirection = (transform.position - gm.sentinelEye.position).normalized;

        lastImpactForce = stats.sentinelDamageTaken;

        if (pulseCoroutine != null)
            StopCoroutine(pulseCoroutine);
        pulseCoroutine = StartCoroutine(PulseCoroutine());

        if (isHeadshot)
        {
            currentHealth = 0f;
            Die();
            return;
        }

        currentHealth -= stats.sentinelDamageTaken;
        currentHealth = Mathf.Max(0f, currentHealth);
        OnTakeDamage?.Invoke();

        OnHealthChanged?.Invoke(currentHealth, GetMaxHealth());
        if (healthBarUI != null)
            healthBarUI.UpdateHealth(currentHealth, GetBaseMaxHealth(), GetMaxHealth());

        if (sentinelHitDisplayCoroutine != null)
            StopCoroutine(sentinelHitDisplayCoroutine);
        sentinelHitDisplayCoroutine = StartCoroutine(SentinelHitDisplayCoroutine());

        if (currentHealth <= 0f)
        {
            Die();
            return;
        }

       

        HitAttack hitAttackCheck = GetComponent<HitAttack>();
        if (hitAttackCheck != null && (hitAttackCheck.isInWindup || hitAttackCheck.IsAttacking()))
        {
            hitAttackCheck.CancelAttack();
        }

        Animator enemyAnimator = GetComponentInChildren<Animator>();
        if (enemyAnimator != null)
            enemyAnimator.SetTrigger("HitReaction");
        StartCoroutine(StunCoroutine());

        UpdateSpeed();

        if (rb != null)
        {
            Vector3 knockbackDir = lastImpactDirection;
            knockbackDir.y = 0f;
            knockbackDir.Normalize();
            SetKnockbackState(0.5f);
            rb.AddForce(knockbackDir * stats.sentinelKnockbackForce, ForceMode.Impulse);
        }
    }

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

        OnDeath?.Invoke();

        ChainConstraint cc = GetComponent<ChainConstraint>();
        if (cc != null)
            cc.enabled = false;

        if (stunTimerUI != null && StunTimerManager.Instance != null)
        {
            StunTimerManager.Instance.UnregisterEnemy(transform);
            stunTimerUI = null;
        }

        BrightEyesController brightEyes = GetComponent<BrightEyesController>();
        if (brightEyes == null)
        {
            GameManager gm = FindObjectOfType<GameManager>();
            if (gm != null)
            {
                gm.OnEnemyKilled();
            }
        }

        EnemyPitInteractable pitInt = GetComponent<EnemyPitInteractable>();
        if (pitInt != null && pitInt.isFallingInPit && healthBarUI != null)
        {
            healthBarUI.Show();
        }

        bool isInDeepPit = pitInt != null && pitInt.shouldIgnoreHealthbarDistance;

        if (healthBarManager != null && !isInDeepPit)
        {
            healthBarManager.UnregisterEnemy(transform);
        }
        else if (isInDeepPit)
        {
        }

        EnemyAI_AStar ai = GetComponent<EnemyAI_AStar>();
        if (ai != null)
        {
            ai.SetDead(true);
            ai.enabled = false;
        }

       

        Pathfinding.AIPath aiPath = GetComponent<Pathfinding.AIPath>();
        if (aiPath != null)
        {
            aiPath.canMove = false;
            aiPath.enabled = false;
        }

        if (deathByPit)
        {
            if (pitInt != null && pitInt.GetCurrentPitZone() != null)
            {
                PitFill pitFill = pitInt.GetCurrentPitZone().GetComponentInChildren<PitFill>();

                if (pitFill != null && pitFill.fillType != null)
                {
                    if (pitFill.fillType.category == PitContentType.ContentCategory.Water)
                    {
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

            return;
        }

        Debug.Log($"========== {gameObject.name} DIE() - MORT NORMALE ==========");

        Collider colNormal = GetComponent<Collider>();
        if (colNormal != null)
        {
            colNormal.isTrigger = false;
        }

        Debug.Log($"About to call IDeathEffect on {gameObject.name}");

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

                if (effect is ExplosionDeathEffect)
                {
                    hasExplosionEffect = true;
                }
            }
        }

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

            int corpseLayer = LayerMask.NameToLayer("Corpse");
            gameObject.layer = corpseLayer;
            foreach (Transform child in GetComponentsInChildren<Transform>())
                child.gameObject.layer = corpseLayer;
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

    public void ApplySprayStun(float duration)
    {
        sprayStunTimer = duration;
        Debug.Log($"{gameObject.name} spray stun applied - {duration}s");
    }

    public bool IsRecovering() => isRecovering;
    public float GetBaseMaxHealth() => stats.maxHealth;
    public float GetMaxHealth()
    {
        float multiplier = ModifierApplier.Instance != null ? ModifierApplier.Instance.enemyMaxHealthMultiplier : 1f;
        return stats.maxHealth * multiplier;
    }
    public float GetCurrentHealth() => currentHealth;
    public float GetHealthPercentage() => currentHealth / GetMaxHealth();

    public float GetSprayStunTimeRemaining()
    {
        return sprayStunTimer;
    }

    public bool IsStunnedBySpray()
    {
        return sprayStunTimer > 0f;
    }

    public int GetArmCount() => 2;

    public void TakeSentinelShotDuringSequence()
    {
        if (isDead) return;

        lastDeathType = DeathContext.DeathType.Sentinel;

        EnemyAI_AStar ai = GetComponent<EnemyAI_AStar>();
        if (ai != null) ai.wasAlreadyShotDuringSweep = true;

        GameManager gm = FindObjectOfType<GameManager>();
        if (gm != null && gm.sentinelEye != null)
            lastImpactDirection = (transform.position - gm.sentinelEye.position).normalized;

        lastImpactForce = stats.sentinelDamageTaken;

        if (pulseCoroutine != null) StopCoroutine(pulseCoroutine);
        pulseCoroutine = StartCoroutine(PulseCoroutine());

        currentHealth -= stats.sentinelDamageTaken;
        currentHealth = Mathf.Max(0f, currentHealth);
        OnTakeDamage?.Invoke();

        OnHealthChanged?.Invoke(currentHealth, GetMaxHealth());
        if (healthBarUI != null)
            healthBarUI.UpdateHealth(currentHealth, GetBaseMaxHealth(), GetMaxHealth());

        if (sentinelHitDisplayCoroutine != null) StopCoroutine(sentinelHitDisplayCoroutine);
        sentinelHitDisplayCoroutine = StartCoroutine(SentinelHitDisplayCoroutine());

        if (currentHealth <= 0f)
            Die();
        // Pas de HitReaction, pas de StunCoroutine, pas de knockback physique
    }

    private void OnDestroy()
    {
        if (stunTimerUI != null && StunTimerManager.Instance != null)
        {
            StunTimerManager.Instance.UnregisterEnemy(transform);
        }
    }
}