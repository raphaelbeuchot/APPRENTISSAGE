using UnityEngine;
using UnityEngine.InputSystem;

// Multi : quel appareil joue P1 (gauche) et P2 (droite). Pose par MultiStartUI au lancement d'un niveau,
// lu par PlayerLocalInput au chargement. Statique : survit aux rechargements de scene (restart garde
// l'assignation), comme MultiMatchScore. Sans assignation (scene lancee directement en editeur),
// PlayerLocalInput garde son reglage d'Inspector.
// Regle : P1 = l'appareil qui a valide le menu. P2 = l'autre manette s'il y en a une, sinon le clavier
// (P1 manette) ; la premiere manette (P1 clavier), ou aucune -> P2 attend une manette.
public static class MultiDeviceAssignment
{
    public static bool HasAssignment { get; private set; }
    public static bool Player1IsKeyboard { get; private set; }
    public static Gamepad Player1Gamepad { get; private set; }
    public static bool Player2IsKeyboard { get; private set; }
    public static Gamepad Player2Gamepad { get; private set; }

    // Appareil qui a appuye sur "valider" cette frame (A d'une manette, ou Entree/Espace), null si inconnu.
    public static InputDevice DetectConfirmDevice()
    {
        foreach (Gamepad gamepad in Gamepad.all)
        {
            if (gamepad.buttonSouth.wasPressedThisFrame)
                return gamepad;
        }

        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && (keyboard.enterKey.wasPressedThisFrame
                                 || keyboard.numpadEnterKey.wasPressedThisFrame
                                 || keyboard.spaceKey.wasPressedThisFrame))
            return keyboard;

        return null;
    }

    public static void AssignFromConfirm(InputDevice confirmDevice)
    {
        if (confirmDevice == null)
        {
            Debug.LogWarning("[MultiDeviceAssignment] Appareil de validation inconnu : reglages d'Inspector conserves");
            HasAssignment = false;
            return;
        }

        Player1Gamepad = confirmDevice as Gamepad;
        Player1IsKeyboard = Player1Gamepad == null;
        Player2Gamepad = null;
        Player2IsKeyboard = false;

        foreach (Gamepad gamepad in Gamepad.all)
        {
            if (gamepad != Player1Gamepad)
            {
                Player2Gamepad = gamepad;
                break;
            }
        }

        // P1 manette sans 2e manette : P2 au clavier. P1 clavier sans manette : P2 en attente d'une manette.
        if (Player2Gamepad == null && !Player1IsKeyboard)
            Player2IsKeyboard = true;

        HasAssignment = true;
        Debug.Log($"[MultiDeviceAssignment] P1 = {Describe(Player1IsKeyboard, Player1Gamepad)}, P2 = {Describe(Player2IsKeyboard, Player2Gamepad)}");
    }

    private static string Describe(bool isKeyboard, Gamepad gamepad)
    {
        if (isKeyboard) return "clavier";
        return gamepad != null ? gamepad.displayName : "manette (en attente)";
    }
}
