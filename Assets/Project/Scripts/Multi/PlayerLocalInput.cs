using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// Input propre a un joueur en multi local. Autonome : cree sa propre copie des actions et la restreint
// aux devices choisis (pas de PlayerInput ni de pairing), donc utilisable sur un joueur pose dans la scene.
// Volontairement minimal : deplacement, sprint, accroupi.
// Les valeurs "Pressed" suivent la meme semantique que le singleton (vraies uniquement la frame de l'appui).
// Manettes : un joueur "Gamepad" sans manette prend la prochaine manette branchee/rebranchee qu'aucun
// autre joueur n'utilise, et libere la sienne si elle se deconnecte.
public class PlayerLocalInput : MonoBehaviour
{
    public enum DeviceKind { KeyboardMouse, Gamepad }

    [SerializeField] private DeviceKind deviceKind = DeviceKind.KeyboardMouse;
    [SerializeField] private int gamepadIndex = 0;

    // Toutes les instances actives, pour ne jamais prendre une manette deja utilisee par un autre joueur.
    private static readonly List<PlayerLocalInput> activeInstances = new List<PlayerLocalInput>();

    private PlayerInputActions actions;
    private Gamepad currentGamepad;

    public Vector2 MoveInput => actions.Player.Movement.ReadValue<Vector2>();
    public bool SprintPressed => actions.Player.Sprint.WasPressedThisFrame();
    public bool CrouchPressed => actions.Player.Crouch.WasPressedThisFrame();
    public bool ToggleCameraViewPressed => actions.Player.ToggleCameraView.WasPressedThisFrame();
    public bool MashEscapePressed => actions.Player.MashEscape.WasPressedThisFrame();
    // Tap seul : pas de balai bas (BroomLow) en multi, comme le reste du mouvement local.
    public bool BroomAttackPressed => actions.Player.BroomAttack.WasPressedThisFrame();

    private void Awake()
    {
        actions = new PlayerInputActions();
    }

    private void OnEnable()
    {
        activeInstances.Add(this);
        InputSystem.onDeviceChange += OnDeviceChange;
        SetDevices(ResolveDevices());
        actions.Player.Enable();
    }

    private void OnDisable()
    {
        activeInstances.Remove(this);
        InputSystem.onDeviceChange -= OnDeviceChange;
        currentGamepad = null;
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
        currentGamepad = null;
        foreach (InputDevice device in devices)
        {
            if (device is Gamepad gamepad)
            {
                currentGamepad = gamepad;
                break;
            }
        }
    }

    private InputDevice[] ResolveDevices()
    {
        if (deviceKind == DeviceKind.KeyboardMouse)
        {
            var devices = new List<InputDevice>();
            if (Keyboard.current != null) devices.Add(Keyboard.current);
            if (Mouse.current != null) devices.Add(Mouse.current);
            return devices.ToArray();
        }

        if (gamepadIndex < Gamepad.all.Count && !IsClaimedByOther(Gamepad.all[gamepadIndex]))
            return new InputDevice[] { Gamepad.all[gamepadIndex] };

        Debug.LogWarning($"[PlayerLocalInput] {name} : pas de manette a l'index {gamepadIndex}, en attente d'une manette", this);
        return new InputDevice[0];
    }

    private void OnDeviceChange(InputDevice device, InputDeviceChange change)
    {
        if (deviceKind != DeviceKind.Gamepad) return;
        if (!(device is Gamepad gamepad)) return;

        switch (change)
        {
            case InputDeviceChange.Removed:
            case InputDeviceChange.Disconnected:
                if (gamepad == currentGamepad)
                {
                    SetDevices(new InputDevice[0]);
                    Debug.Log($"[PlayerLocalInput] {name} : manette deconnectee, en attente d'une manette", this);
                }
                break;

            case InputDeviceChange.Added:
            case InputDeviceChange.Reconnected:
                if (currentGamepad == null && !IsClaimedByOther(gamepad))
                {
                    SetDevices(new InputDevice[] { gamepad });
                    Debug.Log($"[PlayerLocalInput] {name} : manette {gamepad.displayName} assignee", this);
                }
                break;
        }
    }

    private bool IsClaimedByOther(Gamepad gamepad)
    {
        foreach (PlayerLocalInput other in activeInstances)
        {
            if (other != this && other.currentGamepad == gamepad) return true;
        }
        return false;
    }
}
