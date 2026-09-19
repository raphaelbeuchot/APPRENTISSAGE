using UnityEngine;
using UnityEngine.InputSystem;

// Input propre a un joueur en multi local. Autonome : cree sa propre copie des actions et la restreint
// aux devices choisis (pas de PlayerInput ni de pairing), donc utilisable sur un joueur pose dans la scene.
// Volontairement minimal : deplacement, sprint, accroupi.
// Les valeurs "Pressed" suivent la meme semantique que le singleton (vraies uniquement la frame de l'appui).
public class PlayerLocalInput : MonoBehaviour
{
    public enum DeviceKind { KeyboardMouse, Gamepad }

    [SerializeField] private DeviceKind deviceKind = DeviceKind.KeyboardMouse;
    [SerializeField] private int gamepadIndex = 0;

    private PlayerInputActions actions;

    public Vector2 MoveInput => actions.Player.Movement.ReadValue<Vector2>();
    public bool SprintPressed => actions.Player.Sprint.WasPressedThisFrame();
    public bool CrouchPressed => actions.Player.Crouch.WasPressedThisFrame();

    private void Awake()
    {
        actions = new PlayerInputActions();
    }

    private void OnEnable()
    {
        SetDevices(ResolveDevices());
        actions.Player.Enable();
    }

    private void OnDisable()
    {
        actions.Player.Disable();
    }

    private void OnDestroy()
    {
        actions.Dispose();
    }

    // Point d'entree pour un futur ecran d'assignation manette -> joueur.
    public void SetDevices(InputDevice[] devices)
    {
        actions.devices = devices;
    }

    private InputDevice[] ResolveDevices()
    {
        if (deviceKind == DeviceKind.KeyboardMouse)
        {
            var devices = new System.Collections.Generic.List<InputDevice>();
            if (Keyboard.current != null) devices.Add(Keyboard.current);
            if (Mouse.current != null) devices.Add(Mouse.current);
            return devices.ToArray();
        }

        if (gamepadIndex < Gamepad.all.Count)
            return new InputDevice[] { Gamepad.all[gamepadIndex] };

        Debug.LogWarning($"[PlayerLocalInput] {name} : pas de manette a l'index {gamepadIndex}, aucun device assigne", this);
        return new InputDevice[0];
    }
}
