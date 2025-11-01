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

        // Lancer la pulsation
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

        // Appliquer les dégâts
        currentHealth -= stats.sentinelDamageTaken;
        currentHealth = Mathf.Max(0f, currentHealth);

        Debug.Log($"{gameObject.name} shot by sentinel! Health: {currentHealth}/{stats.maxHealth}");
        OnHealthChanged?.Invoke(currentHealth, stats.maxHealth);

        // Vérifie la mort AVANT recovery
        if (currentHealth <= 0f)
        {
            Die();
            return;
        }

        // Recovery stun uniquement si vivant
        StartRecovery();

        // Mettre à jour la vitesse
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

        // Désactiver l'IA
        EnemyAI ai = GetComponent<EnemyAI>();
        if (ai != null)
            ai.enabled = false;

        // Désactiver le grab
        GrabAttack grabAttack = GetComponent<GrabAttack>();
        if (grabAttack != null)
            grabAttack.enabled = false;

        // Désactiver le NavMeshAgent
        NavMeshAgent agent = GetComponent<NavMeshAgent>();
        if (agent != null)
        {
            agent.isStopped = true;
            agent.enabled = false;
        }

        // Stopper la physique
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
            if (rb != null)
            {
                rb.isKinematic = false;
                rb.useGravity = true;
                rb.constraints = RigidbodyConstraints.None;
                rb.AddForce(Vector3.back * 2f, ForceMode.VelocityChange); // petit recul
            }

        // TODO: animation de mort, ragdoll, etc.

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