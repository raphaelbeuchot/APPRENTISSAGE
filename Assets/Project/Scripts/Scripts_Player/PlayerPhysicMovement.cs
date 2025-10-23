using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PlayerPhysicsMovement : MonoBehaviour
{
    [Header("Player Stats")]
    public PlayerStats stats; // ← LA SEULE RÉFÉRENCE NÉCESSAIRE !

    [Header("Camera")]
    public Transform cameraTransform;

    [Header("State")]
    public bool isRedLight = false;
    public bool canMove;

    // Variables runtime (état actuel)
    private Rigidbody rb;
    private Vector3 moveInput;
    private Vector3 currentVelocity;
    private bool isSprinting = false;
    private float currentSpeed;

    // Stamina runtime
    private float currentStamina;
    private float lastSprintTime;

    // Health (pour ajuster la vitesse)
    private float currentHealth;
    //réference
    public GameManager gameManager;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();

        // Initialisation depuis les stats
        if (stats != null)
        {
            currentHealth = stats.maxHealth;
            currentStamina = stats.maxStamina;
        }
        else
        {
            Debug.LogError("PlayerStats non assigné sur " + gameObject.name);
        }
    }

    void Update()
    {
        if (stats == null) return;

        HandleInput();
        HandleStamina();
    }

    void FixedUpdate()
    {
        if (stats == null) return;

        HandleMovement();
    }

    void HandleInput()
    {
        // Input de mouvement
        if (!gameManager.stunBySentinel)
        {
            float h = Input.GetAxisRaw("Horizontal");
            float v = Input.GetAxisRaw("Vertical");
            moveInput = new Vector3(h, 0f, v).normalized;
        }
        else
        {
            moveInput = Vector3.zero;
        }

        // Sprint (uniquement si stamina disponible)
        if (Input.GetKey(KeyCode.LeftShift) && currentStamina > 0f && moveInput.magnitude > 0.1f)
        {
            isSprinting = true;
            lastSprintTime = Time.time;
        }
        else
        {
            isSprinting = false;
        }
    }

    void HandleStamina()
    {
        if (isSprinting)
        {
            // Drain de stamina
            currentStamina -= stats.staminaDrainPerSecond * Time.deltaTime;
            currentStamina = Mathf.Max(0f, currentStamina);

            // Arrêter le sprint si plus de stamina
            if (currentStamina <= 0f)
            {
                isSprinting = false;
            }
        }
        else
        {
            // Régénération avec délai
            if (Time.time - lastSprintTime >= stats.staminaRegenDelay)
            {
                currentStamina += stats.staminaRegenPerSecond * Time.deltaTime;
                currentStamina = Mathf.Min(stats.maxStamina, currentStamina);
            }
        }
    }

    void HandleMovement()
    {
        if (moveInput.magnitude < 0.1f)
        {
            // Deceleration
            currentVelocity = Vector3.Lerp(currentVelocity, Vector3.zero, stats.moveSpeed * Time.fixedDeltaTime);
        }
        else
        {
            // Direction relative à la caméra
            Vector3 moveDirection = GetCameraRelativeMovement(moveInput);

            // Calcul de la vitesse
            float targetSpeed = CalculateSpeed();
            Vector3 targetVelocity = moveDirection * targetSpeed;

            // Acceleration
            currentVelocity = Vector3.Lerp(currentVelocity, targetVelocity, stats.moveSpeed * Time.fixedDeltaTime);
        }

        // Application du mouvement
        rb.linearVelocity = new Vector3(currentVelocity.x, rb.linearVelocity.y, currentVelocity.z);
    }

    float CalculateSpeed()
    {
        // 1. Vitesse de base ajustée selon la santé
        float baseSpeed = stats.GetAdjustedSpeed(currentHealth);

        // 2. Application du sprint si actif
        if (isSprinting)
        {
            baseSpeed *= stats.sprintSpeedMultiplier;
        }

        return baseSpeed;
    }

    Vector3 GetCameraRelativeMovement(Vector3 input)
    {
        if (cameraTransform == null)
            return input;

        Vector3 forward = cameraTransform.forward;
        Vector3 right = cameraTransform.right;

        forward.y = 0f;
        right.y = 0f;

        forward.Normalize();
        right.Normalize();

        return (forward * input.z + right * input.x).normalized;
    }

    // ═══════════════════════════════════════════════════════════
    // PUBLIC METHODS - Pour être appelés par d'autres scripts
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Met à jour la santé actuelle (appelé par PlayerHealth)
    /// </summary>
    public void UpdateHealth(float newHealth)
    {
        currentHealth = newHealth;
    }

    /// <summary>
    /// Force le joueur à s'arrêter (utilisé pour grab, etc.)
    /// </summary>
    public void ForceStop()
    {
        moveInput = Vector3.zero;
        currentVelocity = Vector3.zero;
        rb.linearVelocity = Vector3.zero;
    }

    /// <summary>
    /// Getter pour la stamina (pour l'UI)
    /// </summary>
    public float GetCurrentStamina()
    {
        return currentStamina;
    }

    /// <summary>
    /// Getter pour la stamina max (pour l'UI)
    /// </summary>
    public float GetMaxStamina()
    {
        return stats.maxStamina;
    }
}