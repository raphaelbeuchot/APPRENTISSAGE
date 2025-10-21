using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PlayerPhysicsMovement : MonoBehaviour
{
    [Header("Movement")]
    public float speed = 5f;
    public float acceleration = 50f;
    public float deceleration = 50f;

    [Header("Freeze (RedLight)")]
    public float maxFreezeDuration = 1.5f;
    public KeyCode freezeKey = KeyCode.LeftShift;

    [Header("Camera")]
    public Transform cameraTransform;

    [Header("State")]
    public bool isRedLight = false;

    private Rigidbody rb;
    private Vector3 moveInput;
    private Vector3 currentVelocity;
    private bool isFreezing = false;
    private float freezeTimer = 0f;
    private float speedMultiplier = 1f; // Nouveau : multiplicateur de vitesse

    void Start()
    {
        rb = GetComponent<Rigidbody>();

        if (cameraTransform == null)
        {
            Camera mainCam = Camera.main;
            if (mainCam != null)
            {
                cameraTransform = mainCam.transform;
            }
            else
            {
                Debug.LogError("PlayerPhysicsMovement: Pas de camera trouvee!");
            }
        }
    }

    void Update()
    {
        if (!enabled) return;

        // Inputs de déplacement (ZQSD)
        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");

        moveInput = new Vector3(horizontal, 0, vertical).normalized;

        // Gestion du Freeze pendant RedLight
        if (isRedLight && Input.GetKey(freezeKey) && freezeTimer < maxFreezeDuration)
        {
            isFreezing = true;
            freezeTimer += Time.deltaTime;
        }
        else
        {
            isFreezing = false;
        }

        // Rotation du personnage vers la direction du mouvement
        if (moveInput.magnitude > 0.1f && !isFreezing)
        {
            Vector3 worldMoveDir = GetWorldMoveDirection(moveInput);
            Quaternion targetRotation = Quaternion.LookRotation(worldMoveDir);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 10f);
        }
    }

    void FixedUpdate()
    {
        if (!enabled) return;

        if (isFreezing)
        {
            rb.linearVelocity = Vector3.zero;
            return;
        }

        Vector3 worldMoveDir = GetWorldMoveDirection(moveInput);
        Vector3 targetVelocity = worldMoveDir * speed * speedMultiplier; // Applique le multiplicateur

        if (moveInput.magnitude > 0.1f)
        {
            currentVelocity = Vector3.MoveTowards(currentVelocity, targetVelocity, acceleration * Time.fixedDeltaTime);
        }
        else
        {
            currentVelocity = Vector3.MoveTowards(currentVelocity, Vector3.zero, deceleration * Time.fixedDeltaTime);
        }

        rb.linearVelocity = new Vector3(currentVelocity.x, rb.linearVelocity.y, currentVelocity.z);
    }

    Vector3 GetWorldMoveDirection(Vector3 input)
    {
        if (cameraTransform == null) return input;

        Vector3 forward = cameraTransform.forward;
        Vector3 right = cameraTransform.right;

        forward.y = 0;
        right.y = 0;

        forward.Normalize();
        right.Normalize();

        return forward * input.z + right * input.x;
    }

    public void DrainStamina()
    {
        freezeTimer = maxFreezeDuration;
    }

    public void ResetMovementState()
    {
        freezeTimer = 0f;
        isFreezing = false;
        currentVelocity = Vector3.zero;
        rb.linearVelocity = Vector3.zero;
    }

    // Nouveau : Setter pour le multiplicateur de vitesse
    public void SetSpeedMultiplier(float multiplier)
    {
        speedMultiplier = Mathf.Max(0f, multiplier); // Empêche les valeurs négatives
    }

    // Nouveau : Getter pour le multiplicateur
    public float GetSpeedMultiplier()
    {
        return speedMultiplier;
    }

    // Nouveau : Reset du multiplicateur
    public void ResetSpeedMultiplier()
    {
        speedMultiplier = 1f;
    }

    public bool IsFreezing()
    {
        return isFreezing;
    }

    public float GetFreezePercentage()
    {
        return freezeTimer / maxFreezeDuration;
    }
}