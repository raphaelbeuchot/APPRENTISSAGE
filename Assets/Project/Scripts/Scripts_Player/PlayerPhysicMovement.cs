using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PlayerPhysicsMovement : MonoBehaviour
{
    [Header("Player Stats")]
    public PlayerStats stats;

    private Transform currentParent = null;

    [Header("Grab Immunity")]
    [HideInInspector] public bool isImmuneToGrab = false;
    [HideInInspector] public float lastGrabEndTime = -999f; 


    [Header("Camera")]
    public Transform cameraTransform;
    public TargetLockSystem lockSystem;

    [Header("State")]
    public bool isRedLight = false;
    public bool canMove;

    // === BRIGHT EYES ATTRACTION ===
    private Transform brightEyesAttractor;
    private float brightEyesForce;
    private float brightEyesSlowdown = 1f;

    // === SWARM EFFECTS (sans grab) ===
    private float swarmSlowdownMultiplier = 1f;
    private float waterSlowdownMultiplier = 1f; // NOUVEAU

    private bool isInSwarmVision = false;

    [Header("Crouch")]
    private bool isCrouching = false;
    [SerializeField] private Transform playerMesh; // Assigner mesh dans Inspector
    private CapsuleCollider capsuleCollider;
    private float originalColliderHeight;
    private Vector3 originalColliderCenter;
    private Vector3 originalMeshScale;

    [Header("Step Climbing")]  // <--- AJOUTER ICI
    [SerializeField] private float maxStepHeight = 0.4f;
    [SerializeField] private float stepCheckDistance = 0.1f;
    [SerializeField] private float stepRayHeightOffset = 0.1f;
    private float lastStepClimbTime = 0f;



    private Material originalMaterial;

    public bool isClimbing = false;



    // Grab States
    public enum GrabState { None, Grabbed, Recoil, Knockdown }
    public GrabState grabState = GrabState.None;
    public float grabProgress = 0f;
    public bool isBeingGrabbed => grabState == GrabState.Grabbed;

    // Variables runtime
    private Rigidbody rb;
    private Vector3 moveInput;
    private Vector3 currentVelocity;
    private float currentSpeed;

    // Stamina runtime
    private float currentStamina;

    // Health
    private float currentHealth;

    // Dash
    [HideInInspector] public bool isDashing = false;
    private float lastDashTime = -999f;
    private Vector3 dashDirection;

    // Reference
    public GameManager gameManager;

    // === FREEZE SYSTEM ===
    public bool isFrozen = false;
    private float freezeHoldTime = 0f;

    [Header("Freeze Feedback")]
    [SerializeField] private Renderer playerRenderer;
    [SerializeField] private Material normalMaterial;
    [SerializeField] private Material freezeMaterial;

    //Animations
    private Animator animator;

    // === MOVING PLATFORM SUPPORT ===
    private IMovingPlatform currentPlatform;
    private Vector3 lastPlatformPosition;

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
        

        // Renderer auto si non assigné
        if (playerRenderer == null)
            playerRenderer = GetComponentInChildren<Renderer>();

        if (playerRenderer != null)
        {
            originalMaterial = playerRenderer.material;
        }
    }

    void Start()
    {
        animator = GetComponentInChildren<Animator>();

        


        if (gameManager == null)
        {
            gameManager = FindObjectOfType<GameManager>();
        }

        if (playerRenderer != null && normalMaterial == null)
        {
            normalMaterial = playerRenderer.sharedMaterial;
        }

        capsuleCollider = GetComponent<CapsuleCollider>();
        if (capsuleCollider != null)
        {
            originalColliderHeight = capsuleCollider.height;
            originalColliderCenter = capsuleCollider.center;
        }

        if (playerMesh != null)
        {
            originalMeshScale = playerMesh.localScale;
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

        if (PlayerInputManager.Instance.CrouchPressed)
        {
            if (isCrouching)
                ExitCrouch();
            else
                EnterCrouch();
        }

        // Sortie crouch si dash
        if (PlayerInputManager.Instance.SprintPressed && isCrouching)
        {
            ExitCrouch();
        }

        if (animator != null)
        {
            // Calculer la vitesse dans le référentiel LOCAL du personnage
            Vector3 localVelocity = transform.InverseTransformDirection(rb.linearVelocity);

            // SpeedX = vitesse latérale (gauche/droite)
            // SpeedZ = vitesse avant/arrière
            animator.SetFloat("SpeedX", localVelocity.x);
            animator.SetFloat("SpeedZ", localVelocity.z);
            animator.SetBool("IsCrouching", isCrouching);
        }

        // === IMMUNITÉ GRABS ===
        if (!isClimbing) // Ne pas override l'immunité du climb
        {
            isImmuneToGrab = !IsGrounded() || isDashing; // En l'air OU en dash = immune
        }
    }


    void FixedUpdate()
    {
        if (stats == null) return;
        if (grabState == GrabState.Recoil || grabState == GrabState.Knockdown) return;
        if (isFrozen) return;

        // AJOUTER CETTE LIGNE :
        if (isDashing) return; // Skip tout pendant le dash

        HandleStepClimb();
        HandleMovement();
    }

    void HandleInput()
    {
        if (grabState != GrabState.None || gameManager.stunBySentinel || !canMove || isClimbing)
        {
            moveInput = Vector3.zero;
            return;
        }

        if (isFrozen)
        {
            moveInput = Vector3.zero;
            return;
        }

        Vector2 inputVector = PlayerInputManager.Instance.MoveInput;

        // NOUVEAU : Garder la magnitude AVANT de normaliser
        float inputMagnitude = inputVector.magnitude;

        // Normaliser juste pour la direction
        Vector3 direction = new Vector3(inputVector.x, 0f, inputVector.y).normalized;

        // Multiplier par la magnitude originale pour garder l'intensité du stick
        moveInput = direction * Mathf.Clamp01(inputMagnitude);

        // Dash
        if (PlayerInputManager.Instance.SprintPressed && CanDash())
        {
            Vector3 dashDir = GetCameraRelativeMovement(moveInput);
            StartCoroutine(DashCoroutine(dashDir));
        }
    }

    void HandleStamina()
    {
        // Regen passive (pas de condition sprint)
        currentStamina += stats.staminaRegenPerSecond * Time.deltaTime;
        currentStamina = Mathf.Min(stats.maxStamina, currentStamina);
    }

    void HandleStepClimb()
    {
        // AJOUTER ICI : check conditions bloquantes
        if (isClimbing || grabState != GrabState.None || moveInput.magnitude < 0.1f)
            return;

        Vector3 moveDirection = GetCameraRelativeMovement(moveInput);

        Vector3 rayStart = transform.position + Vector3.up * stepRayHeightOffset;
        RaycastHit hitLower;

        if (Physics.Raycast(rayStart, moveDirection, out hitLower, stepCheckDistance, LayerMask.GetMask("Ground", "Obstacle")))
        {
            Vector3 upperRayStart = transform.position + moveDirection * stepCheckDistance + Vector3.up * maxStepHeight;
            RaycastHit hitUpper;

            if (Physics.Raycast(upperRayStart, Vector3.down, out hitUpper, maxStepHeight, LayerMask.GetMask("Ground", "Obstacle")))
            {
                float stepHeight = hitUpper.point.y - transform.position.y;

                if (stepHeight > 0.05f && stepHeight <= maxStepHeight)
                {
                    // CALCULER VELOCITY Y PROPORTIONNELLE
                    Vector3 currentVel = rb.linearVelocity;
                    float horizontalSpeed = new Vector3(currentVel.x, 0f, currentVel.z).magnitude;

                    float ratio = stepHeight / stepCheckDistance;
                    float targetVelocityY = stepHeight * 20f;

                    // APPLIQUER VELOCITY Y
                    Vector3 newVelocity = rb.linearVelocity;
                    newVelocity.y = targetVelocityY;
                    rb.linearVelocity = newVelocity;

                    return; // Step climb actif, Y reste unfreeze
                }
            }
        }

        
    }


    public void ApplyKnockdown(Vector3 knockbackDirection, float force, float duration)
    {
        if (grabState == GrabState.Grabbed)
        {
            GrabAttack[] allGrabs = FindObjectsByType<GrabAttack>(FindObjectsSortMode.None);
            foreach (GrabAttack grab in allGrabs)
            {
                if (grab.isGrabbing)
                {
                    grab.ForceStop();
                }
            }
        }

        StartCoroutine(KnockdownCoroutine(knockbackDirection, force, duration));
    }

    public void ApplyKnockback(Vector3 knockbackVelocity, float stunDuration = 0.3f)
    {
        StartCoroutine(KnockbackCoroutine(knockbackVelocity, stunDuration));
    }

    public void ApplyProgressivePush(Vector3 direction, float force, float duration)
    {
        StartCoroutine(ProgressivePushCoroutine(direction, force, duration));
    }

    IEnumerator ProgressivePushCoroutine(Vector3 direction, float force, float duration)
    {
        // Désactiver le script complètement (comme le knockback)
        enabled = false;

        float elapsed = 0f;
        float forcePerFrame = force / duration;

        while (elapsed < duration)
        {
            // Appliquer une fraction de la force chaque frame
            rb.AddForce(direction * forcePerFrame * Time.deltaTime, ForceMode.VelocityChange);

            elapsed += Time.deltaTime;
            yield return null;
        }

        // Réactiver le script
        enabled = true;
    }

    IEnumerator KnockbackCoroutine(Vector3 knockbackVel, float duration)
    {
        enabled = false;
        knockbackVel.y = rb.linearVelocity.y;
        rb.linearVelocity = knockbackVel;
        yield return new WaitForSeconds(duration);
        enabled = true;
    }

    IEnumerator KnockdownCoroutine(Vector3 direction, float force, float duration)
    {
        grabState = GrabState.Knockdown;
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        rb.linearVelocity = direction * force;
        yield return new WaitForSeconds(duration);
        if (grabState == GrabState.Knockdown)
        {
            grabState = GrabState.None;
        }
    }

    public void HandleMovement()
    {
        Vector3 moveDirection = Vector3.zero;

        if (moveInput.magnitude < 0.1f)
        {
            // Arret plus rapide
            currentVelocity = Vector3.Lerp(currentVelocity, Vector3.zero, 15f * Time.fixedDeltaTime);
        }
        else
        {
            moveDirection = GetCameraRelativeMovement(moveInput);
            float targetSpeed = CalculateSpeed();
            Vector3 targetVelocity = moveDirection * targetSpeed;
            // Acceleration plus reactive pour eviter le drift
            currentVelocity = Vector3.Lerp(currentVelocity, targetVelocity, 20f * Time.fixedDeltaTime);
        }

        if (lockSystem != null && lockSystem.IsLocked)
        {
            Vector3 targetDirection = lockSystem.GetTargetDirection();
            if (targetDirection != Vector3.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(targetDirection);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, 40f * Time.fixedDeltaTime);
            }
        }
        else
        {
            if (moveInput.magnitude > 0.1f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, 40f * Time.fixedDeltaTime);
            }
        }

        // Appliquer velocity de base
        Vector3 finalVelocity = new Vector3(currentVelocity.x, rb.linearVelocity.y, currentVelocity.z);

        // === BRIGHT EYES ATTRACTION (force radiale continue) ===
        if (brightEyesAttractor != null)
        {
            Vector3 directionToAttractor = (brightEyesAttractor.position - transform.position).normalized;
            directionToAttractor.y = 0f;
            finalVelocity += directionToAttractor * brightEyesForce;
        }

        // === MOVING PLATFORM SUPPORT ===
        DetectMovingPlatform();
        if (currentPlatform != null)
        {
            RotatingPlatform rotPlatform = currentPlatform.GetTransform().GetComponent<RotatingPlatform>();

            if (rotPlatform != null)
            {
                // ROTATING PLATFORM
                if (moveInput.magnitude < 0.1f)
                {
                    // Idle : teleportation (reste solidaire)
                    Vector3 pivotPoint = rotPlatform.GetTransform().position + new Vector3(
                        rotPlatform.settings.pivotOffset.x,
                        0f,
                        rotPlatform.settings.pivotOffset.y
                    );

                    float angleThisFrame = rotPlatform.settings.rotationSpeed * Time.fixedDeltaTime;
                    if (!rotPlatform.settings.clockwise)
                        angleThisFrame = -angleThisFrame;

                    Vector3 directionFromPivot = transform.position - pivotPoint;
                    directionFromPivot = Quaternion.Euler(0f, angleThisFrame, 0f) * directionFromPivot;
                    transform.position = pivotPoint + directionFromPivot;

                    // AJOUTER CETTE LIGNE : Rotation de l'orientation
                    transform.Rotate(Vector3.up, angleThisFrame);
                }
                else
                {
                    // Bouge : velocite tangentielle (lutte/boost)
                    Vector3 tangentialVel = rotPlatform.GetTangentialVelocityAtPoint(transform.position);
                    finalVelocity.x += tangentialVel.x * rotPlatform.settings.playerInfluence;
                    finalVelocity.z += tangentialVel.z * rotPlatform.settings.playerInfluence;
                }
            }
            else
            {
                // ROAMING OBSTACLE : toujours teleportation (idle OU en mouvement)
                Vector3 platformCurrentPos = currentPlatform.GetTransform().position;
                Vector3 platformDelta = platformCurrentPos - lastPlatformPosition;
                platformDelta.y = 0f;

                transform.position += platformDelta;
                lastPlatformPosition = platformCurrentPos;
            }
        }


        rb.linearVelocity = finalVelocity;
    }
    float CalculateSpeed()
    {
        float baseSpeed = stats.GetAdjustedSpeed(currentHealth);
        float stickMagnitude = moveInput.magnitude;
        baseSpeed *= stickMagnitude;

       

        // AJOUTE CES LIGNES :
        if (isCrouching)
        {
            baseSpeed *= stats.crouchSpeedMultiplier;
        }

        baseSpeed *= swarmSlowdownMultiplier;
        baseSpeed *= brightEyesSlowdown;
        baseSpeed *= waterSlowdownMultiplier;

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

    public void ApplyBrightEyesAttraction(Transform attractor, float force, float slowdown)
    {
        brightEyesAttractor = attractor;
        brightEyesForce = force;
        brightEyesSlowdown = slowdown;
    }

    public void RemoveBrightEyesAttraction()
    {
        brightEyesAttractor = null;
        brightEyesForce = 0f;
        brightEyesSlowdown = 1f;
    }
    public void UpdateStamina(float newStamina)
    {
        currentStamina = Mathf.Clamp(newStamina, 0f, stats.maxStamina);
    }

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

    public void ApplySwarmSlowdown(float multiplier)
    {
        swarmSlowdownMultiplier = multiplier;
    }

    public void RemoveSwarmSlowdown()
    {
        swarmSlowdownMultiplier = 1f;
    }

    public void ApplyWaterSlowdown(float multiplier)
    {
        waterSlowdownMultiplier = multiplier;
    }

    public void RemoveWaterSlowdown()
    {
        waterSlowdownMultiplier = 1f;
    }
    public void ApplySwarmVision(bool active)
    {
        isInSwarmVision = active;


    }

    void EnterCrouch()
    {
        isCrouching = true;

        // SUPPRIME TOUTE LA PARTIE MESH (lignes playerMesh.localScale)

        // GARDE ET CORRIGE le collider (détection sentinelle)
        if (capsuleCollider != null)
        {
            float newHeight = originalColliderHeight * 0.5f; //  CORRIGE ICI
            capsuleCollider.height = newHeight;
            capsuleCollider.center = new Vector3(
                originalColliderCenter.x,
                newHeight * 0.5f,
                originalColliderCenter.z
            );
        }
    }

    public void ExitCrouch()
    {
        if (!isCrouching) return;

        isCrouching = false;

        // SUPPRIME TOUTE LA PARTIE MESH

        // GARDE la restauration collider
        if (capsuleCollider != null)
        {
            capsuleCollider.height = originalColliderHeight;
            capsuleCollider.center = originalColliderCenter;
        }
    }

    public void ResetAllInputs()
    {
        moveInput = Vector3.zero;
        currentVelocity = Vector3.zero;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
    }
    public bool IsCrouching()
    {
        return isCrouching;
    }
    public Vector3 GetMoveInput()
    {
        Vector2 input = PlayerInputManager.Instance.MoveInput;
        Vector3 input3D = new Vector3(input.x, 0f, input.y);
        return transform.TransformDirection(input3D);
    }
    // === FREEZE FUNCTIONS ===
    private Color originalColor; // <--- ajoute cette ligne dans tes variables privées

    void StartFreeze()
    {
        isFrozen = true;
        canMove = false;
        rb.linearVelocity = Vector3.zero;

        if (playerRenderer != null)
        {
            // On sauvegarde la couleur du matériau (pas le matériau lui-même)
            originalColor = playerRenderer.material.color;

            // Et on le rend bleu
            playerRenderer.material.color = Color.blue;
        }

        Debug.Log("Player is now FROZEN");
    }

    void EndFreeze()
    {
        isFrozen = false;
        canMove = true;

        if (playerRenderer != null)
        {
            // On restaure simplement la couleur sauvegardée
            playerRenderer.material.color = originalColor;
        }

        Debug.Log("Player UNFROZEN");
    }

    private bool IsGrounded()
    {
        float rayLength = 0.3f;
        Vector3 rayStart = transform.position + Vector3.up * 0.1f; // Légèrement au-dessus des pieds
        return Physics.Raycast(rayStart, Vector3.down, rayLength, LayerMask.GetMask("Ground"));
    }


    void DetectMovingPlatform()
    {
        if (!IsGrounded())
        {
            currentPlatform = null;
            return;
        }

        float rayLength = 0.3f;
        Vector3 rayStart = transform.position + Vector3.up * 0.1f;
        RaycastHit hit;

        if (Physics.Raycast(rayStart, Vector3.down, out hit, rayLength, LayerMask.GetMask("Ground")))
        {
            IMovingPlatform platform = hit.collider.GetComponent<IMovingPlatform>();

            if (platform != null)
            {
                if (currentPlatform != platform)
                {
                    // Nouvelle plateforme
                    currentPlatform = platform;
                    lastPlatformPosition = platform.GetTransform().position;
                }
            }
            else
            {
                currentPlatform = null;
            }
        }
        else
        {
            currentPlatform = null;
        }
    }
    bool CanDash()
    {
        if (isDashing) return false;
        if (isClimbing) return false;
        if (isCrouching) return false;
        if (grabState != GrabState.None) return false;
        if (gameManager.stunBySentinel) return false;
        if (moveInput.magnitude < 0.1f) return false; // Pas de dash sur place
        if (currentStamina < stats.dashStaminaCost) return false;
        if (Time.time < lastDashTime + stats.dashCooldown) return false;

        return true;
    }

    IEnumerator DashCoroutine(Vector3 direction)
    {
        // Consommer stamina
        currentStamina -= stats.dashStaminaCost;
        currentStamina = Mathf.Max(0f, currentStamina);

        // Setup dash
        isDashing = true;
        // AJOUTER CES 2 LIGNES :
        if (animator != null)
            animator.SetTrigger("DoDash");

        dashDirection = direction.normalized;
        float dashSpeed = stats.dashDistance / stats.dashDuration;
        

        // Bloquer inputs normaux
        canMove = false;

        float elapsed = 0f;
        while (elapsed < stats.dashDuration)
        {
            // Forcer velocity dans dashDirection
            Vector3 dashVelocity = dashDirection * dashSpeed;
            dashVelocity.y = rb.linearVelocity.y; // Garder gravité
            rb.linearVelocity = dashVelocity;

            elapsed += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }

        // Fin dash
        isDashing = false;
        canMove = true;
        lastDashTime = Time.time;
    }

}
