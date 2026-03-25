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

    public event Action<float, float> OnHealthChanged;
    public event Action OnDeath;
    public event Action OnCriticalHealth;

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

    public void TakeDamage(float damage)
    {
        if (isDead) return;
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
        TestClimbDetection climbDetection = GetComponent<TestClimbDetection>();
        if (climbDetection != null)
            climbDetection.CancelClimb();
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            Vector3 knockbackDir = (transform.position - sentinelPosition);
            knockbackDir.y = 0f;
            knockbackDir.Normalize();
            rb.AddForce(knockbackDir * stats.sentinelKnockbackForce, ForceMode.Impulse);
        }
        TakeDamage(settings.playerDamage);
    }

    public void TakeZombieBite(float zombieDamage)
    {
        TakeDamage(zombieDamage);
    }

    public void TakeHeadshot()
    {
        if (isDead) return;
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
        Debug.Log("PLAYER IS DEAD!");
        OnDeath?.Invoke();
        if (movement != null)
            movement.enabled = false;
        var meleeSystem = GetComponent<MeleeAttackSystem>();
        if (meleeSystem != null)
            meleeSystem.enabled = false;
    }

    public float GetCurrentHealth() => currentHealth;
    public float GetHealthPercentage() => currentHealth / GetMaxHealth();
    public bool IsDead() => isDead;
    public bool IsCritical() => GetHealthPercentage() <= 0.25f;
}