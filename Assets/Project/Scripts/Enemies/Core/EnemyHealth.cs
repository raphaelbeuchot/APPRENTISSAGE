using UnityEngine;
using System;
using UnityEngine.AI;
using System.Collections;


/// <summary>
/// Système de santé SIMPLIFIÉ pour tous les ennemis.
/// Pas de système de membres - juste santé, dégâts et mort.
/// </summary>
public class EnemyHealth : MonoBehaviour
{
    [Header("Enemy Stats")]
    public EnemyStats stats;

    // Death context tracking
    private DeathContext.DeathType lastDeathType = DeathContext.DeathType.Other;
    private Vector3 lastImpactDirection = Vector3.forward;
    private float lastImpactForce = 1f;

    // Santé
    private float currentHealth;
    private bool isDead = false;

    // Recovery après tir de sentinelle
    private bool isRecovering = false;
    private float recoverUntilTime = 0f;

    // Events (pour l'UI, les effets visuels, etc.)
    public event Action OnDeath;
    public event Action<float, float> OnHealthChanged; // current, max

    // ============================================
    // INITIALISATION
    // ============================================

    void Start()
    {
        if (stats == null)
        {
            Debug.LogError("EnemyStats non assigne sur " + gameObject.name);
            return;
        }

        currentHealth = stats.maxHealth;

        // AJOUTE CA A LA FIN DE START :
        SetupDeathEffect();
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
        if (isDead) return; // stop tout

        // Fin du recovery après tir
        if (isRecovering && Time.time >= recoverUntilTime)
        {
            isRecovering = false;
            Debug.Log($"{gameObject.name} recovered from gunshot!");
        }
    }

    // ============================================
    // PULSATION
    // ============================================
    private Coroutine pulseCoroutine;
    public float pulseScaleMultiplier = 1.2f; // combien la taille augmente
    public float pulseDuration = 0.3f; // durée de la pulsation

    private IEnumerator PulseCoroutine()
    {
        Vector3 originalScale = transform.localScale;
        Vector3 targetScale = originalScale * pulseScaleMultiplier;
        float elapsed = 0f;

        // Phase d'expansion
        while (elapsed < pulseDuration / 2f)
        {
            transform.localScale = Vector3.Lerp(originalScale, targetScale, elapsed / (pulseDuration / 2f));
            elapsed += Time.deltaTime;
            yield return null;
        }

        // Phase de retour
        elapsed = 0f;
        while (elapsed < pulseDuration / 2f)
        {
            transform.localScale = Vector3.Lerp(targetScale, originalScale, elapsed / (pulseDuration / 2f));
            elapsed += Time.deltaTime;
            yield return null;
        }

        transform.localScale = originalScale;
    }




    // ============================================
    // SYSTÈME DE DÉGÂTS
    // ============================================

    /// <summary>
    /// Dégâts par melee attack du joueur
    /// </summary>
    public void TakeMeleeDamage(float damage)
    {
        if (isDead) return;

        // Track death context
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
        UpdateSpeed();

        if (currentHealth <= 0f)
        {
            Die();
        }
    }

    /// <summary>
    /// Dégâts par tir de sentinelle
    /// </summary>
    public void TakeSentinelShot(bool isHeadshot = false)
    {
        if (isDead) return;

        // Track death context
        lastDeathType = DeathContext.DeathType.Sentinel;
        GameManager gm = FindObjectOfType<GameManager>();
        if (gm != null && gm.sentinelEye != null)
        {
            lastImpactDirection = (transform.position - gm.sentinelEye.position).normalized;
        }
        lastImpactForce = stats.sentinelDamageTaken;

        if (pulseCoroutine != null)
            StopCoroutine(pulseCoroutine);
        pulseCoroutine = StartCoroutine(PulseCoroutine());

        if (isHeadshot)
        {
            Debug.Log($"{gameObject.name} HEADSHOT! Instant death!");
            currentHealth = 0f;
            Die();
            return;
        }

        currentHealth -= stats.sentinelDamageTaken;
        currentHealth = Mathf.Max(0f, currentHealth);

        Debug.Log($"{gameObject.name} shot by sentinel! Health: {currentHealth}/{stats.maxHealth}");
        OnHealthChanged?.Invoke(currentHealth, stats.maxHealth);

        if (currentHealth <= 0f)
        {
            Die();
            return;
        }

        StartRecovery();
        UpdateSpeed();
    }

    /// <summary>
    /// Démarre le recovery après un tir (stun temporaire)
    /// </summary>
    void StartRecovery()
    {
        isRecovering = true;
        recoverUntilTime = Time.time + 2f;

        // Libérer la cible si grab en cours
        GrabAttack grabAttack = GetComponent<GrabAttack>();
        if (grabAttack != null && grabAttack.IsGrabbing())
        {
            grabAttack.ForceStop();
        }

        Debug.Log($"{gameObject.name} starts recovery (stunned for 2s)");
    }

    // ============================================
    // MORT
    // ============================================

    void Die()
    {
        if (isDead) return;

        isDead = true;
        Debug.Log($"{gameObject.name} is dead!");

        OnDeath?.Invoke();

        EnemyAI ai = GetComponent<EnemyAI>();
        if (ai != null)
            ai.enabled = false;

        GrabAttack grabAttack = GetComponent<GrabAttack>();
        if (grabAttack != null)
            grabAttack.enabled = false;

        NavMeshAgent agent = GetComponent<NavMeshAgent>();
        if (agent != null)
        {
            agent.isStopped = true;
            agent.enabled = false;
        }

        // Preparer contexte de mort
        DeathContext context = new DeathContext
        {
            deathType = lastDeathType,
            impactDirection = lastImpactDirection,
            impactForce = lastImpactForce
        };

        // Appeler death effects
        IDeathEffect[] deathEffects = GetComponents<IDeathEffect>();
        if (deathEffects != null && deathEffects.Length > 0)
        {
            foreach (IDeathEffect effect in deathEffects)
            {
                effect.OnDeath(transform.position, context);
            }
        }

        // Appeler death behaviors (spawn swarm)
        IOnDeathBehavior[] deathBehaviors = GetComponents<IOnDeathBehavior>();
        if (deathBehaviors != null && deathBehaviors.Length > 0)
        {
            foreach (IOnDeathBehavior behavior in deathBehaviors)
            {
                behavior.OnEnemyDeath(transform.position);
            }
        }

        Destroy(gameObject, 3f);
    }


    // ============================================
    // MISE À JOUR DE LA VITESSE
    // ============================================

    /// <summary>
    /// Met à jour la vitesse de l'ennemi selon sa santé actuelle
    /// </summary>
    void UpdateSpeed()
    {
        EnemyAI ai = GetComponent<EnemyAI>();
        if (ai != null)
        {
            ai.UpdateSpeed(currentHealth, false); // false = pas crawler
        }
    }

    // ============================================
    // GETTERS
    // ============================================

    public bool IsDead() => isDead;
    public bool IsRecovering() => isRecovering;
    public float GetCurrentHealth() => currentHealth;
    public float GetMaxHealth() => stats.maxHealth;
    public float GetHealthPercentage() => currentHealth / stats.maxHealth;

    /// <summary>
    /// Pour la compatibilité avec l'ancien système de grab
    /// (nombre de bras pour calculer les mashes requis)
    /// Par défaut : 2 bras
    /// </summary>
    public int GetArmCount() => 2;
}