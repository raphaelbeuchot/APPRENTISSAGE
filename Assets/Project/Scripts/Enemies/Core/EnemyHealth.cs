using UnityEngine;
using System;
using UnityEngine.AI;
using System.Collections;

public class EnemyHealth : MonoBehaviour
{
    [Header("Enemy Stats")]
    public EnemyStats stats;

    private DeathContext.DeathType lastDeathType = DeathContext.DeathType.Other;
    private Vector3 lastImpactDirection = Vector3.forward;
    private float lastImpactForce = 1f;

    private float currentHealth;
    private bool isDead = false;

    private bool isRecovering = false;
    private float recoverUntilTime = 0f;

    public event Action OnDeath;
    public event Action<float, float> OnHealthChanged;

    // Health bar
    public EnemyHealthBarUI healthBarUI;
    private EnemyHealthBarManager healthBarManager;

    void Start()
    {
        if (stats == null)
        {
            Debug.LogError("EnemyStats non assigne sur " + gameObject.name);
            return;
        }

        currentHealth = stats.maxHealth;
        SetupDeathEffect();
        SetupHealthBar();
    }

    void SetupHealthBar()
    {
        healthBarManager = FindObjectOfType<EnemyHealthBarManager>();
        if (healthBarManager != null)
        {
            GameObject barGO = Instantiate(healthBarManager.healthBarPrefab, healthBarManager.transform);
            healthBarUI = barGO.GetComponent<EnemyHealthBarUI>();
            healthBarManager.RegisterEnemy(transform, healthBarUI);
            healthBarUI.UpdateHealth(currentHealth, stats.maxHealth);
            healthBarUI.gameObject.SetActive(false);
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

        if (isRecovering && Time.time >= recoverUntilTime)
        {
            isRecovering = false;
            Debug.Log($"{gameObject.name} recovered from gunshot!");
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

    public void TakeMeleeDamage(float damage)
    {
        if (isDead) return;

        lastDeathType = DeathContext.DeathType.Melee;
        PlayerPhysicsMovement player = FindObjectOfType<PlayerPhysicsMovement>();
        if (player != null)
        {
            lastImpactDirection = (transform.position - player.transform.position).normalized;
        }
        lastImpactForce = damage;

        currentHealth -= damage;
        currentHealth = Mathf.Max(0f, currentHealth);

        Debug.Log($"{gameObject.name} took {damage} melee damage! Health: {currentHealth}/{stats.maxHealth}");

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

        NavMeshAgent agent = GetComponent<NavMeshAgent>();
        EnemyAI ai = GetComponent<EnemyAI>();

        float originalSpeed = 0f;
        if (agent != null)
        {
            originalSpeed = agent.speed;
            agent.isStopped = true;   // Stop la navigation
        }

        if (ai != null)
            ai.canMove = false;       // Booléen à ajouter dans EnemyAI pour stopper les actions

        Debug.Log($"{gameObject.name} stunned for 2 seconds");

        yield return new WaitForSeconds(2f);

        // Restauration après stun
        if (agent != null)
            agent.isStopped = false;

        if (ai != null)
            ai.canMove = true;

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
        Debug.Log($"{gameObject.name} is dead!");

        OnDeath?.Invoke();

        // Désinscrire la barre de vie
        if (healthBarManager != null)
        {
            healthBarManager.UnregisterEnemy(transform);
        }

        // Désactiver l'IA et les scripts de contrôle
        EnemyAI ai = GetComponent<EnemyAI>();
        if (ai != null) ai.enabled = false;

        GrabAttack grabAttack = GetComponent<GrabAttack>();
        if (grabAttack != null) grabAttack.enabled = false;

        NavMeshAgent agent = GetComponent<NavMeshAgent>();
        if (agent != null)
        {
            agent.isStopped = true;
            agent.enabled = false;
        }

        // Activer la physique pour que le corps soit poussable
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = false;  // laisse la physique agir
            rb.detectCollisions = true;
        }

        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            col.isTrigger = false;  // le corps devient solide
        }

        // Activer ragdoll si présent
        IDeathEffect[] deathEffects = GetComponents<IDeathEffect>();
        DeathContext context = new DeathContext
        {
            deathType = lastDeathType,
            impactDirection = lastImpactDirection,
            impactForce = lastImpactForce
        };
        if (deathEffects != null && deathEffects.Length > 0)
        {
            foreach (IDeathEffect effect in deathEffects)
            {
                effect.OnDeath(transform.position, context);
            }
        }

        // Event pour autres comportements à la mort
        IOnDeathBehavior[] deathBehaviors = GetComponents<IOnDeathBehavior>();
        if (deathBehaviors != null && deathBehaviors.Length > 0)
        {
            foreach (IOnDeathBehavior behavior in deathBehaviors)
            {
                behavior.OnEnemyDeath(transform.position);
            }
        }

        // Le corps reste dans la scène, prêt à être poussé
    }


    void UpdateSpeed()
    {
        EnemyAI ai = GetComponent<EnemyAI>();
        if (ai != null)
        {
            ai.UpdateSpeed(currentHealth, false);
        }
    }

    public bool IsDead() => isDead;
    public bool IsRecovering() => isRecovering;
    public float GetCurrentHealth() => currentHealth;
    public float GetMaxHealth() => stats.maxHealth;
    public float GetHealthPercentage() => currentHealth / stats.maxHealth;
    public int GetArmCount() => 2;
}