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
    public bool SprayAttackHeld { get; private set; }
    public bool BroomAttackPressed { get; private set; }
    public bool LockOnHeld { get; private set; }
    public bool ThrowBottlePressed { get; private set; }
    public bool InteractPressed { get; private set; }

    public bool InteractHeld { get; private set; }
    public bool ReloadPressed { get; private set; }
    public bool MashEscapePressed { get; private set; }
    public bool CrouchPressed { get; private set; }
    public bool SentinelCameraPressed { get; private set; }
    public bool ToggleCameraViewPressed { get; private set; }
    public bool CancelPressed { get; private set; }
    public bool PausePressed { get; private set; }
    public bool BroomLowActive { get; private set; }

    [Header("Broom Hold Threshold")]
    [SerializeField] private float broomHoldThreshold = 0.7f;

    private float broomPressStartTime;
    private bool broomIsHeld;

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
    private void Update()
    {
        if (broomIsHeld && !BroomLowActive && Time.time - broomPressStartTime >= broomHoldThreshold)
        {
            BroomLowActive = true;
            Debug.Log("BROOM LOW ACTIF");

        }
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

        inputActions.Player.SprayAttack.performed += OnSprayAttack;
        inputActions.Player.SprayAttack.canceled += OnSprayAttack;

        inputActions.Player.BroomAttack.started += ctx =>
        {
            broomIsHeld = true;
            broomPressStartTime = Time.time;
        };

        inputActions.Player.BroomAttack.canceled += ctx =>
        {
            broomIsHeld = false;
            if (BroomLowActive)
            {
                BroomLowActive = false;
            }
            else
            {
                float duration = Time.time - broomPressStartTime;
                if (duration < broomHoldThreshold)
                    BroomAttackPressed = true;
            }
        };

        inputActions.Player.LockOn.performed += ctx => LockOnHeld = true;
        inputActions.Player.LockOn.canceled += ctx => LockOnHeld = false;

        inputActions.Player.ThrowBottle.performed += ctx => ThrowBottlePressed = true;
        inputActions.Player.ThrowBottle.canceled += ctx => ThrowBottlePressed = false;

        inputActions.Player.Interact.performed += OnInteract;
        inputActions.Player.Interact.canceled += OnInteract;

        inputActions.Player.Reload.performed += ctx => ReloadPressed = true;
        inputActions.Player.Reload.canceled += ctx => ReloadPressed = false;

        inputActions.Player.MashEscape.performed += ctx => MashEscapePressed = true;
        inputActions.Player.MashEscape.canceled += ctx => MashEscapePressed = false;

        inputActions.Player.Crouch.performed += ctx => CrouchPressed = true;

        inputActions.Player.ToggleCameraView.performed += OnToggleCameraView;

        inputActions.Player.SentinelCamera.performed += ctx =>
        {
            Debug.Log("INPUT SENTINEL CAMERA PERFORMED!");
            SentinelCameraPressed = true;
        };
        inputActions.Player.SentinelCamera.canceled += ctx => SentinelCameraPressed = false;

        // NOUVEAU : Brancher Pause et Cancel
        inputActions.Player.Pause.performed += OnPause;
        inputActions.Player.Cancel.performed += OnCancel;
    }

    private void OnDisable()
    {
        // Se desabonner
        inputActions.Player.Movement.performed -= OnMovement;
        inputActions.Player.Movement.canceled -= OnMovement;

        inputActions.Player.Look.performed -= OnLook;
        inputActions.Player.Look.canceled -= OnLook;

        inputActions.Player.SprayAttack.performed -= OnSprayAttack;
        inputActions.Player.SprayAttack.canceled -= OnSprayAttack;

        inputActions.Player.Crouch.performed -= ctx => CrouchPressed = true;

        inputActions.Player.SentinelCamera.performed -= ctx => SentinelCameraPressed = true;
        inputActions.Player.SentinelCamera.canceled -= ctx => SentinelCameraPressed = false;

        inputActions.Player.ToggleCameraView.performed -= OnToggleCameraView;

        // NOUVEAU : Debrancher Pause et Cancel
        inputActions.Player.Pause.performed -= OnPause;
        inputActions.Player.Cancel.performed -= OnCancel;

        // Desactiver l'action map
        inputActions.Player.Disable();
    }

    private void OnCancel(InputAction.CallbackContext context)
    {
        if (context.performed)
            CancelPressed = true;
    }
    private void OnToggleCameraView(InputAction.CallbackContext context)
    {
        ToggleCameraViewPressed = true;
    }
    private void OnMovement(InputAction.CallbackContext context)
    {
        MoveInput = context.ReadValue<Vector2>();
    }

    private void OnLook(InputAction.CallbackContext context)
    {
        LookInput = context.ReadValue<Vector2>();
    }

    private void OnSprayAttack(InputAction.CallbackContext context)
    {
        if (context.performed)
            SprayAttackPressed = true;

        SprayAttackHeld = context.ReadValueAsButton();
    }
    private void OnPause(InputAction.CallbackContext context)
    {
        if (context.performed)
            PausePressed = true;
    }

    private void OnInteract(InputAction.CallbackContext context)
    {
        if (context.performed)
            InteractPressed = true;

        InteractHeld = context.ReadValueAsButton();
    }
    public void ForceBroomLowOff()
    {
        BroomLowActive = false;
        broomIsHeld = false;
    }

    private void LateUpdate()
    {
        // Reset des inputs "pressed" pour frame suivante
        SprayAttackPressed = false;
        // NE PAS reset SprayAttackHeld ici
        BroomAttackPressed = false;
        ThrowBottlePressed = false;
        InteractPressed = false;
        ReloadPressed = false;
        MashEscapePressed = false;
        CrouchPressed = false;
        SentinelCameraPressed = false;
        ToggleCameraViewPressed = false;
        CancelPressed = false;
        PausePressed = false;


    }
}