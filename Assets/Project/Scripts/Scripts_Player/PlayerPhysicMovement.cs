using System.Collections.Generic;
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
    public bool IsSprinting() => isSprinting;

    // Grab States
    public enum GrabState { None, Grabbed, Recoil }
    public GrabState grabState = GrabState.None;
    public float grabProgress = 0f;
    public bool isBeingGrabbed => grabState == GrabState.Grabbed;

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
        // Freeze rotation X et Z
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;


        // Caméra
        if (cameraTransform == null)
        {
            GameObject camObj = GameObject.Find("MainCamera");
            if (camObj != null)
                cameraTransform = camObj.transform;
        }

        // GameManager
        if (gameManager == null)
        {
            gameManager = FindAnyObjectByType<GameManager>();
        }

        // Stats
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

    void Start()
    {
        if (gameManager == null)
        {
            gameManager = FindObjectOfType<GameManager>();
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

        // Ne pas bouger si en recoil
        if (grabState == GrabState.Recoil) return;


        HandleMovement();
    }

    void HandleInput()
    {
        // Bloquer inputs si grabbed ou en recoil
        if (grabState != GrabState.None || gameManager.stunBySentinel)
        {
            moveInput = Vector3.zero;
            return;
        }

        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");
        moveInput = new Vector3(h, 0f, v).normalized;

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

    public void HandleMovement()
    {
        Vector3 moveDirection = Vector3.zero; // ← AJOUTE CETTE LIGNE

        if (moveInput.magnitude < 0.1f)
        {
            // Deceleration
            currentVelocity = Vector3.Lerp(currentVelocity, Vector3.zero, 5f * stats.moveSpeed * Time.fixedDeltaTime);
        }
        else
        {
            // Direction relative à la caméra
            moveDirection = GetCameraRelativeMovement(moveInput);

            // Calcul de la vitesse
            float targetSpeed = CalculateSpeed();
            Vector3 targetVelocity = moveDirection * targetSpeed;

            // Acceleration
            currentVelocity = Vector3.Lerp(currentVelocity, targetVelocity, stats.moveSpeed * Time.fixedDeltaTime);
        }

        // Rotation vers la direction du mouvement
        if (moveInput.magnitude > 0.1f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, 10f * Time.fixedDeltaTime);
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

        Debug.Log($"Input: {input}, Forward: {forward}, Right: {right}"); // ← ICI

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