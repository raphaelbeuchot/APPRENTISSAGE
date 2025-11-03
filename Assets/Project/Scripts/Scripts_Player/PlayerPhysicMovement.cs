using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PlayerPhysicsMovement : MonoBehaviour
{
    [Header("Player Stats")]
    public PlayerStats stats;

    [Header("Camera")]
    public Transform cameraTransform;

    [Header("State")]
    public bool isRedLight = false;
    public bool canMove;

    // === SWARM EFFECTS (sans grab) ===
    private float swarmSlowdownMultiplier = 1f;
    private bool isInSwarmVision = false;

    public bool IsSprinting() => isSprinting;

    // Grab States
    public enum GrabState { None, Grabbed, Recoil }
    public GrabState grabState = GrabState.None;
    public float grabProgress = 0f;
    public bool isBeingGrabbed => grabState == GrabState.Grabbed;

    // Variables runtime
    private Rigidbody rb;
    private Vector3 moveInput;
    private Vector3 currentVelocity;
    private bool isSprinting = false;
    private float currentSpeed;

    // Stamina runtime
    private float currentStamina;
    private float lastSprintTime;

    // Health
    private float currentHealth;

    // Reference
    public GameManager gameManager;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

        // Camera
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
            Debug.LogError("PlayerStats non assigne sur " + gameObject.name);
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

        // Securite anti-freeze
        if (!canMove && grabState == GrabState.None && !gameManager.stunBySentinel)
            canMove = true;
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
            currentStamina -= stats.staminaDrainPerSecond * Time.deltaTime;
            currentStamina = Mathf.Max(0f, currentStamina);

            if (currentStamina <= 0f)
            {
                isSprinting = false;
            }
        }
        else
        {
            if (Time.time - lastSprintTime >= stats.staminaRegenDelay)
            {
                currentStamina += stats.staminaRegenPerSecond * Time.deltaTime;
                currentStamina = Mathf.Min(stats.maxStamina, currentStamina);
            }
        }
    }

    public void HandleMovement()
    {
        Vector3 moveDirection = Vector3.zero;

        if (moveInput.magnitude < 0.1f)
        {
            currentVelocity = Vector3.Lerp(currentVelocity, Vector3.zero, 5f * stats.moveSpeed * Time.fixedDeltaTime);
        }
        else
        {
            moveDirection = GetCameraRelativeMovement(moveInput);
            float targetSpeed = CalculateSpeed();
            Vector3 targetVelocity = moveDirection * targetSpeed;
            currentVelocity = Vector3.Lerp(currentVelocity, targetVelocity, stats.moveSpeed * Time.fixedDeltaTime);
        }

        if (moveInput.magnitude > 0.1f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, 10f * Time.fixedDeltaTime);
        }

        rb.linearVelocity = new Vector3(currentVelocity.x, rb.linearVelocity.y, currentVelocity.z);
    }

    float CalculateSpeed()
    {
        float baseSpeed = stats.GetAdjustedSpeed(currentHealth);

        if (isSprinting)
        {
            baseSpeed *= stats.sprintSpeedMultiplier;
        }

        // Slowdown nuees
        baseSpeed *= swarmSlowdownMultiplier;

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

    // ============================================
    // PUBLIC METHODS
    // ============================================

    public void UpdateHealth(float newHealth)
    {
        currentHealth = newHealth;
    }

    public void ForceStop()
    {
        moveInput = Vector3.zero;
        currentVelocity = Vector3.zero;
        rb.linearVelocity = Vector3.zero;
    }

    public float GetCurrentStamina()
    {
        return currentStamina;
    }

    public float GetMaxStamina()
    {
        return stats.maxStamina;
    }

    // ============================================
    // SWARM EFFECTS (simples, pas de grab)
    // ============================================

    public void ApplySwarmSlowdown(float multiplier)
    {
        swarmSlowdownMultiplier = multiplier;
    }

    public void RemoveSwarmSlowdown()
    {
        swarmSlowdownMultiplier = 1f;
    }

    public void ApplySwarmVision(bool active)
    {
        isInSwarmVision = active;
        Debug.Log($"Swarm vision effect: {active}");
    }
}