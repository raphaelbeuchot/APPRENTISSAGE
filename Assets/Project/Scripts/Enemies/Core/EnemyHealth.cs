using UnityEngine;
using System;

/// <summary>
/// Système de santé SIMPLIFIÉ pour tous les ennemis.
/// Pas de système de membres - juste santé, dégâts et mort.
/// </summary>
public class EnemyHealth : MonoBehaviour
{
    [Header("Enemy Stats")]
    public EnemyStats stats;

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
            Debug.LogError("EnemyStats non assigné sur " + gameObject.name);
            return;
        }

        currentHealth = stats.maxHealth;
    }

    void Update()
    {
        // Fin du recovery après tir
        if (isRecovering && Time.time >= recoverUntilTime)
        {
            isRecovering = false;
            Debug.Log($"{gameObject.name} recovered from gunshot!");
        }
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

        currentHealth -= damage;
        currentHealth = Mathf.Max(0f, currentHealth);

        Debug.Log($"{gameObject.name} took {damage} melee damage! Health: {currentHealth}/{stats.maxHealth}");

        // Event pour l'UI ou effets visuels
        OnHealthChanged?.Invoke(currentHealth, stats.maxHealth);

        // Mettre à jour la vitesse selon la santé
        UpdateSpeed();

        // Check mort
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

        // Headshot = mort instantanée
        if (isHeadshot)
        {
            Debug.Log($"{gameObject.name} HEADSHOT! Instant death!");
            currentHealth = 0f;
            Die();
            return;
        }

        // Dégâts normaux
        currentHealth -= stats.sentinelDamageTaken;
        currentHealth = Mathf.Max(0f, currentHealth);

        Debug.Log($"{gameObject.name} shot by sentinel! Health: {currentHealth}/{stats.maxHealth}");

        // Event
        OnHealthChanged?.Invoke(currentHealth, stats.maxHealth);

        // Recovery stun
        StartRecovery();

        // Mettre à jour la vitesse
        UpdateSpeed();

        // Check mort
        if (currentHealth <= 0f)
        {
            Die();
        }
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

        // Event
        OnDeath?.Invoke();

        // Désactiver l'AI
        EnemyAI ai = GetComponent<EnemyAI>();
        if (ai != null)
        {
            ai.enabled = false;
        }

        // Désactiver l'attaque
        GrabAttack grabAttack = GetComponent<GrabAttack>();
        if (grabAttack != null)
        {
            grabAttack.enabled = false;
        }

        // TODO: Animation de mort, ragdoll, etc.

        // Détruire après un délai
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