using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInputManager : MonoBehaviour
{
    public static PlayerInputManager Instance { get; private set; }

    [Header("Input Action Asset")]
    [SerializeField] private PlayerInputActions inputActions;

    // Properties publiques pour que les autres scripts puissent lire les inputs
    public Vector2 MoveInput { get; private set; }
    public Vector2 LookInput { get; private set; }
    public bool SprintPressed { get; private set; }
    public bool SprayAttackPressed { get; private set; }
    public bool BroomAttackPressed { get; private set; }
    public bool LockOnHeld { get; private set; }
    public bool ThrowBottlePressed { get; private set; }
    public bool InteractPressed { get; private set; }
    public bool ReloadPressed { get; private set; }

    public bool MashEscapePressed { get; private set; }

    public bool CrouchPressed { get; private set; }

    public bool SentinelCameraPressed { get; private set; }



    private void Awake()
    {
        // Singleton
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        // Initialiser l'Input Action Asset
        inputActions = new PlayerInputActions();
    }
    
    private void OnEnable()
    {
        // Activer l'action map Player
        inputActions.Player.Enable();

        // S'abonner aux actions
        inputActions.Player.Movement.performed += OnMovement;
        inputActions.Player.Movement.canceled += OnMovement;

        inputActions.Player.Look.performed += OnLook;
        inputActions.Player.Look.canceled += OnLook;

        inputActions.Player.Sprint.performed += ctx => SprintPressed = true;
        inputActions.Player.Sprint.canceled += ctx => SprintPressed = false;

        inputActions.Player.SprayAttack.performed += ctx => SprayAttackPressed = true;
        inputActions.Player.SprayAttack.canceled += ctx => SprayAttackPressed = false;

        inputActions.Player.BroomAttack.performed += ctx => BroomAttackPressed = true;
        inputActions.Player.BroomAttack.canceled += ctx => BroomAttackPressed = false;

        inputActions.Player.LockOn.performed += ctx => LockOnHeld = true;
        inputActions.Player.LockOn.canceled += ctx => LockOnHeld = false;

        inputActions.Player.ThrowBottle.performed += ctx => ThrowBottlePressed = true;
        inputActions.Player.ThrowBottle.canceled += ctx => ThrowBottlePressed = false;

        inputActions.Player.Interact.performed += ctx => InteractPressed = true;
        inputActions.Player.Interact.canceled += ctx => InteractPressed = false;

        inputActions.Player.Reload.performed += ctx => ReloadPressed = true;
        inputActions.Player.Reload.canceled += ctx => ReloadPressed = false;

        inputActions.Player.MashEscape.performed += ctx => MashEscapePressed = true;
        inputActions.Player.MashEscape.canceled += ctx => MashEscapePressed = false;

        inputActions.Player.Crouch.performed += ctx => CrouchPressed = true;

        inputActions.Player.SentinelCamera.performed += ctx =>
        {
            Debug.Log("INPUT SENTINEL CAMERA PERFORMED!");
            SentinelCameraPressed = true;
        }; inputActions.Player.SentinelCamera.canceled += ctx => SentinelCameraPressed = false;

    }

    private void OnDisable()
    {
        // Se désabonner
        inputActions.Player.Movement.performed -= OnMovement;
        inputActions.Player.Movement.canceled -= OnMovement;

        inputActions.Player.Look.performed -= OnLook;
        inputActions.Player.Look.canceled -= OnLook;

        // Désactiver l'action map
        inputActions.Player.Disable();

        inputActions.Player.Crouch.performed -= ctx => CrouchPressed = true;

        inputActions.Player.SentinelCamera.performed -= ctx => SentinelCameraPressed = true;
        inputActions.Player.SentinelCamera.canceled -= ctx => SentinelCameraPressed = false;
    }

    private void OnMovement(InputAction.CallbackContext context)
    {
        MoveInput = context.ReadValue<Vector2>();
    }

    private void OnLook(InputAction.CallbackContext context)
    {
        LookInput = context.ReadValue<Vector2>();
    }

    private void LateUpdate()
    {
        // Reset des inputs "pressed" pour frame suivante
        SprayAttackPressed = false;
        BroomAttackPressed = false;
        ThrowBottlePressed = false;
        InteractPressed = false;
        ReloadPressed = false;
        MashEscapePressed = false;
        CrouchPressed = false;
        SentinelCameraPressed = false;


    }
}