using UnityEngine;
using System;

public class PlayerHealth : MonoBehaviour
{
    [Header("Debug")]
    [SerializeField] private bool debugOverride = false;
    [SerializeField] private float debugMultiplier = 1.5f;

    [Header("Player Stats")]
    public PlayerStats stats;
    public SentinelSettings settings;

    [Header("References")]
    [SerializeField] private PlayerPhysicsMovement movement;

    private float currentHealth;
    private bool isDead = false;

    private bool isInKnockbackGrace = false;
    private float knockbackGraceEndTime = 0f;
    private Vector3 lastDamageDirection = Vector3.back;


    public event Action<float, float> OnHealthChanged;
    public event Action OnDeath;
    public event Action OnCriticalHealth;
    // Au tout debut de la mort (OnDeath, lui, arrive a la fin de la DeathSequence). Parametre : LastDeathCause.
    public event Action<DeathCause> OnDying;

    // Cause du coup fatal (null = generique). Remise a null au Revive.
    public DeathCause LastDeathCause { get; private set; }

    void Awake()
    {
        if (stats == null)
        {
            Debug.LogError("PlayerStats non assigne sur " + gameObject.name);
            return;
        }
        if (movement == null)
            movement = GetComponent<PlayerPhysicsMovement>();
        currentHealth = stats.maxHealth;
        if (movement != null)
            movement.UpdateHealth(currentHealth);
    }

    void Start()
    {
        currentHealth = GetMaxHealth();
        if (movement != null)
            movement.UpdateHealth(currentHealth);
        OnHealthChanged?.Invoke(currentHealth, GetMaxHealth());
    }

    public float GetBaseMaxHealth() => stats.maxHealth;

    public float GetMaxHealth()
    {
        if (debugOverride)
            return stats.maxHealth * debugMultiplier;

        float multiplier = ModifierApplier.Instance != null
            ? ModifierApplier.Instance.playerMaxHealthMultiplier
            : 1f;
        return stats.maxHealth * multiplier;
    }

    public void TakeDamage(float damage, DeathCause deathCause = null)
    {
        if (isDead) return;
        if (DevFlags.Invincible) return;
        LastDeathCause = deathCause;
        currentHealth -= damage;
        currentHealth = Mathf.Max(0f, currentHealth);
        Debug.Log($"Player took {damage} damage! Health: {currentHealth}/{GetMaxHealth()}");
        OnHealthChanged?.Invoke(currentHealth, GetMaxHealth());
        if (movement != null)
            movement.UpdateHealth(currentHealth);
        if (GetHealthPercentage() <= 0.25f && !isDead)
            OnCriticalHealth?.Invoke();
        if (currentHealth <= 0f)
            Die();
    }

    public void TakeSentinelShot(Vector3 sentinelPosition)
    {
        LevelStatsTracker.Instance?.RegisterSentinelShot();

        TestClimbDetection climbDetection = GetComponent<TestClimbDetection>();
        if (climbDetection != null)
            climbDetection.CancelClimb();

        Vector3 knockbackDir = (transform.position - sentinelPosition);
        knockbackDir.y = 0f;
        knockbackDir.Normalize();
        lastDamageDirection = knockbackDir;   // <-- nouveau

        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
            rb.AddForce(knockbackDir * stats.sentinelKnockbackForce, ForceMode.Impulse);

        float damage = OptionsManager.Instance != null
            ? OptionsManager.Instance.GetSentinelDamage(settings.playerDamage, GetMaxHealth())
            : settings.playerDamage;
        TakeDamage(damage);
    }

    public void TakeZombieBite(float zombieDamage)
    {
        TakeDamage(zombieDamage);
    }

    public void TakeHeadshot()
    {
        Kill(null);
    }

    // Mort instantanee, ignore l'invincibilite (DevFlags) : chute hors limites, defaite en course...
    public void Kill(DeathCause deathCause)
    {
        if (isDead) return;
        LastDeathCause = deathCause;
        currentHealth = 0f;
        Die();
    }

    public void Heal(float amount)
    {
        if (isDead) return;
        if (!stats.canHeal) return;
        currentHealth += amount;
        currentHealth = Mathf.Min(GetMaxHealth(), currentHealth);
        Debug.Log($"Player healed {amount}! Health: {currentHealth}/{GetMaxHealth()}");
        OnHealthChanged?.Invoke(currentHealth, GetMaxHealth());
        if (movement != null)
            movement.UpdateHealth(currentHealth);
    }

    void Update()
    {
        if (stats == null || isDead) return;
        if (stats.canHeal && currentHealth < GetMaxHealth())
            Heal(stats.healingPerSecond * Time.deltaTime);
    }

    public bool IsInKnockbackGracePeriod()
    {
        if (isInKnockbackGrace && Time.time >= knockbackGraceEndTime)
            isInKnockbackGrace = false;
        return isInKnockbackGrace;
    }

    public void StartKnockbackGracePeriod(float duration = 1f)
    {
        isInKnockbackGrace = true;
        knockbackGraceEndTime = Time.time + duration;
    }

    public void ResetKnockbackGrace()
    {
        isInKnockbackGrace = false;
        knockbackGraceEndTime = 0f;
    }

    void Die()
    {
        if (isDead) return;
        isDead = true;
        Debug.Log("PLAYER IS DEAD! Cause : " + (LastDeathCause != null ? LastDeathCause.name : "generique"));
        OnDying?.Invoke(LastDeathCause);

        DeathSequence deathSequence = GetComponent<DeathSequence>();
        Debug.Log("DeathSequence found: " + (deathSequence != null ? "OUI" : "NON - FALLBACK"));

        if (deathSequence != null)
        {
            Debug.Log("Appel DeathSequence.Play()");
            deathSequence.Play(lastDamageDirection);
        }
        else
        {
            Debug.Log("FALLBACK : OnDeath invoque directement");
            OnDeath?.Invoke();
            if (movement != null) movement.enabled = false;
            var meleeSystem = GetComponent<MeleeAttackSystem>();
            if (meleeSystem != null) meleeSystem.enabled = false;
        }
    }
    public void TriggerOnDeath()
    {
        OnDeath?.Invoke();
    }

    // Multi : remet le joueur en vie (respawn). Jamais appele en solo.
    public void Revive()
    {
        isDead = false;
        LastDeathCause = null;
        currentHealth = GetMaxHealth();
        ResetKnockbackGrace();
        if (movement != null)
            movement.UpdateHealth(currentHealth);
        OnHealthChanged?.Invoke(currentHealth, GetMaxHealth());
    }

    public float GetCurrentHealth() => currentHealth;
    public float GetHealthPercentage() => currentHealth / GetMaxHealth();
    public bool IsDead() => isDead;
    public bool IsCritical() => GetHealthPercentage() <= 0.25f;
}