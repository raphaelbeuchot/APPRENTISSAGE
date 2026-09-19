using UnityEngine;
using UnityEngine.InputSystem;

// Input propre a un joueur en multi local : lit les actions de SON PlayerInput (device apparie),
// pas le singleton global. Volontairement minimal : deplacement, sprint, accroupi.
// Les valeurs "Pressed" suivent la meme semantique que le singleton (vraies uniquement la frame de l'appui).
[RequireComponent(typeof(PlayerInput))]
public class PlayerLocalInput : MonoBehaviour
{
    private PlayerInput playerInput;
    private InputAction moveAction;
    private InputAction sprintAction;
    private InputAction crouchAction;

    public int PlayerIndex => playerInput.playerIndex;

    public Vector2 MoveInput => moveAction.ReadValue<Vector2>();
    public bool SprintPressed => sprintAction.WasPressedThisFrame();
    public bool CrouchPressed => crouchAction.WasPressedThisFrame();

    private void Awake()
    {
        playerInput = GetComponent<PlayerInput>();
        moveAction = playerInput.actions.FindAction("Movement", true);
        sprintAction = playerInput.actions.FindAction("Sprint", true);
        crouchAction = playerInput.actions.FindAction("Crouch", true);
    }
}
