using UnityEngine;
using System;

public class PlayerHealth : MonoBehaviour
{
    [Header("Player Stats")]
    public PlayerStats stats; // Référence au ScriptableObject

    [Header("References")]
    [SerializeField] private PlayerPhysicsMovement movement;

    // État runtime
    private float currentHealth;
    private bool isDead = false;

    // Grace period (pour éviter detection pendant knockback)
    private bool isInKnockbackGrace = false;
    private float knockbackGraceEndTime = 0f;

    // Events
    public event Action<float, float> OnHealthChanged; // current, max
    public event Action OnDeath;
    public event Action OnCriticalHealth; // < 25%

    void Awake()
    {
        if (stats == null)
        {
            Debug.LogError("PlayerStats non assigné sur " + gameObject.name);
            return;
        }

        if (movement == null)
        {
            movement = GetComponent<PlayerPhysicsMovement>();
        }

        // Initialisation
        currentHealth = stats.maxHealth;

        // Informe le movement de la santé initiale
        if (movement != null)
        {
            movement.UpdateHealth(currentHealth);
        }
    }

    // 
    // DAMAGE SYSTEM
    // 

    /// <summary>
    /// Inflige des dégâts au joueur
    /// </summary>
    public void TakeDamage(float damage)
    {
        if (isDead) return;

        currentHealth -= damage;
        currentHealth = Mathf.Max(0f, currentHealth);

        Debug.Log($"Player took {damage} damage! Health: {currentHealth}/{stats.maxHealth}");

        // Event pour l'UI
        OnHealthChanged?.Invoke(currentHealth, stats.maxHealth);

        // Update la vitesse du mouvement
        if (movement != null)
        {
            movement.UpdateHealth(currentHealth);
        }

        // Check critical health
        if (GetHealthPercentage() <= 0.25f && !isDead)
        {
            OnCriticalHealth?.Invoke();
        }

        // Check mort
        if (currentHealth <= 0f)
        {
            Die();
        }
    }

    /// <summary>
    /// Variante : dégâts par tir de sentinelle
    /// </summary>
    public void TakeSentinelShot()
    {
        // Tu peux ajuster les dégâts sentinelle dans SentinelSettings
        // Pour l'instant on utilise une valeur par défaut
        TakeDamage(25f);
    }

    /// <summary>
    /// Variante : dégâts par morsure de zombie
    /// </summary>
    public void TakeZombieBite(float zombieDamage)
    {
        TakeDamage(zombieDamage);
    }

    /// <summary>
    /// Headshot = mort instantanée
    /// </summary>
    public void TakeHeadshot()
    {
        if (isDead) return;

        Debug.Log("HEADSHOT! Player is dead!");
        currentHealth = 0f;
        Die();
    }

    // 
    // HEALING SYSTEM
    // 

    /// <summary>
    /// Soigne le joueur (pour "The Doc")
    /// </summary>
    public void Heal(float amount)
    {
        if (isDead) return;
        if (!stats.canHeal) return; // Seulement si le perso peut heal

        currentHealth += amount;
        currentHealth = Mathf.Min(stats.maxHealth, currentHealth);

        Debug.Log($"Player healed {amount}! Health: {currentHealth}/{stats.maxHealth}");

        OnHealthChanged?.Invoke(currentHealth, stats.maxHealth);

        if (movement != null)
        {
            movement.UpdateHealth(currentHealth);
        }
    }

    /// <summary>
    /// Auto-régénération (pour "The Doc")
    /// </summary>
    void Update()
    {
        if (stats == null || isDead) return;

        // Auto-heal si le personnage a cette capacité
        if (stats.canHeal && currentHealth < stats.maxHealth)
        {
            Heal(stats.healingPerSecond * Time.deltaTime);
        }
    }

    // 
    // KNOCKBACK GRACE PERIOD
    // 

    public bool IsInKnockbackGracePeriod()
    {
        if (isInKnockbackGrace && Time.time >= knockbackGraceEndTime)
        {
            isInKnockbackGrace = false;
        }
        return isInKnockbackGrace;
    }

    public void StartKnockbackGracePeriod(float duration = 1f)
    {
        isInKnockbackGrace = true;
        knockbackGraceEndTime = Time.time + duration;
        Debug.Log($"Knockback grace period started for {duration}s");
    }

    public void ResetKnockbackGrace()
    {
        isInKnockbackGrace = false;
        knockbackGraceEndTime = 0f;
    }

    // 
    // DEATH
    // 

    void Die()
    {
        if (isDead) return;

        isDead = true;
        Debug.Log("PLAYER IS DEAD!");

        OnDeath?.Invoke();

        // Désactive les systèmes
        if (movement != null)
        {
            movement.enabled = false;
        }

        var meleeSystem = GetComponent<MeleeAttackSystem>();
        if (meleeSystem != null)
        {
            meleeSystem.enabled = false;
        }

        // TODO: Déclencher animation de mort, ragdoll, etc.
    }

    // 
    // GETTERS
    // 

    public float GetCurrentHealth() => currentHealth;
    public float GetMaxHealth() => stats.maxHealth;
    public float GetHealthPercentage() => currentHealth / stats.maxHealth;
    public bool IsDead() => isDead;
    public bool IsCritical() => GetHealthPercentage() <= 0.25f;
}