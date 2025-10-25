using UnityEngine;

/// <summary>
/// Mouvement du joueur avec système HYBRIDE :
/// - Responsive (pas d'inertie au démarrage)
/// - Conserve la physique (bourrade fonctionne)
/// - Arrêt net
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class PlayerPhysicsMovement : MonoBehaviour
{
    [Header("Movement Settings")]
    public PlayerStats stats;

    [Header("Acceleration (NEW)")]
    [Tooltip("Plus c'est élevé, plus le démarrage est rapide")]
    [Range(5f, 100f)]
    public float accelerationMultiplier = 15f;

    [Tooltip("Plus c'est élevé, plus l'arrêt est net")]
    [Range(5f, 100f)]
    public float decelerationMultiplier = 20f;

    [Header("Runtime Info")]
    public bool isRedLight = false;

    private Rigidbody rb;
    private float currentStamina;
    private bool isSprinting = false;
    private float currentMoveSpeed;

    void Start()
    {
        rb = GetComponent<Rigidbody>();

        if (stats == null)
        {
            Debug.LogError("PlayerStats non assigné!");
            return;
        }

        currentStamina = stats.maxStamina;
        currentMoveSpeed = stats.moveSpeed;

        // Configuration Rigidbody optimale pour le système hybride
        rb.linearDamping = 0f; // On gère la décélération manuellement
        rb.mass = 50f;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        // Freezer les rotations
        rb.constraints = RigidbodyConstraints.FreezeRotationX |
                        RigidbodyConstraints.FreezeRotationZ;
    }

    void Update()
    {
        if (stats == null) return;

        HandleStamina();
    }

    void FixedUpdate()
    {
        if (stats == null) return;

        HandleMovement();
    }

    // ============================================
    // MOUVEMENT HYBRIDE
    // ============================================

    void HandleMovement()
    {
        // Input
        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");
        Vector3 inputDirection = new Vector3(horizontal, 0f, vertical).normalized;

        // Calcul de la vitesse cible
        isSprinting = Input.GetKey(KeyCode.LeftShift) && currentStamina > 0f;

        // Calcul de la vitesse en fonction du sprint
        float targetSpeed = isSprinting
            ? stats.moveSpeed * stats.sprintSpeedMultiplier
            : stats.moveSpeed;

        Vector3 targetVelocity = inputDirection * targetSpeed;

        // Vélocité actuelle (horizontale seulement)
        Vector3 currentVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);

        // Calcul du changement de vélocité nécessaire
        Vector3 velocityChange = targetVelocity - currentVelocity;

        // === SYSTÈME HYBRIDE ===
        float multiplier;

        if (inputDirection.magnitude > 0.1f)
        {
            // Accélération
            multiplier = accelerationMultiplier;
        }
        else
        {
            // Décélération (freinage)
            multiplier = decelerationMultiplier;
        }

        // Application de la force
        Vector3 force = velocityChange * multiplier;

        // SÉCURITÉ : Limiter la force pour éviter la sur-correction
        float maxForce = 1000f; // Force maximale en unités
        if (force.magnitude > maxForce)
        {
            force = force.normalized * maxForce;
        }

        rb.AddForce(force, ForceMode.Acceleration);

        // Rotation vers la direction du mouvement
        if (inputDirection.magnitude > 0.1f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(inputDirection);
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRotation,
                Time.fixedDeltaTime * 15f
            );
        }

        currentMoveSpeed = currentVelocity.magnitude;
    }

    // ============================================
    // STAMINA
    // ============================================

    void HandleStamina()
    {
        if (isSprinting && currentStamina > 0f)
        {
            // Drain de la stamina
            currentStamina -= stats.staminaDrainPerSecond * Time.deltaTime;
            currentStamina = Mathf.Max(0f, currentStamina);
        }
        else if (!isSprinting && currentStamina < stats.maxStamina)
        {
            // Régénération de la stamina
            currentStamina += stats.staminaRegenPerSecond * Time.deltaTime;
            currentStamina = Mathf.Min(stats.maxStamina, currentStamina);
        }
    }

    // ============================================
    // UTILITAIRES
    // ============================================

    /// <summary>
    /// Arrête complètement le mouvement (pour grab, etc.)
    /// </summary>
    public void ForceStop()
    {
        rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
    }

    /// <summary>
    /// Applique un knockback (bourrade, explosion, etc.)
    /// </summary>
    public void ApplyKnockback(Vector3 direction, float force)
    {
        rb.AddForce(direction.normalized * force, ForceMode.Impulse);
    }

    // ============================================
    // GETTERS
    // ============================================

    public float GetCurrentStamina() => currentStamina;
    public float GetMaxStamina() => stats.maxStamina;
    public float GetStaminaPercentage() => currentStamina / stats.maxStamina;
    public bool IsSprinting() => isSprinting;
    public float GetCurrentSpeed() => currentMoveSpeed;

    // ============================================
    // MÉTHODE POUR PlayerHealth (COMPATIBILITÉ)
    // ============================================

    /// <summary>
    /// Appelée par PlayerHealth quand la santé change.
    /// Permet d'ajuster la vitesse selon la santé (optionnel).
    /// </summary>
    public void UpdateHealth(float currentHealth)
    {
        // Pour l'instant, on ne fait rien
        // Tu peux réduire la vitesse si tu veux :
        // float healthPercent = currentHealth / stats.maxHealth;
        // currentMoveSpeed *= healthPercent;
    }
}