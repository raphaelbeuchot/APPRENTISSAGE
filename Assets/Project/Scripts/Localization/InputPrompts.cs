using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.InputSystem;

// Touches affichees dans les textes, selon le dernier appareil utilise (manette ou clavier/souris).
// Dans la Sheet, le bouton s'ecrit avec le nom de l'action de PlayerInputActions entre crochets :
//   "Press [Sprint] to Lunge"  ->  "Press A to Lunge" (manette)  /  "Press Left Shift to Lunge" (clavier)
// Loc.Get / Loc.Text font le remplacement ; LocalizedText se rafraichit sur DeviceChanged.
//
// Manette : noms Xbox fixes (table plus bas). Clavier : nom donne par Unity (suit la disposition AZERTY / QWERTY).
// Un nom peut etre traduit par une ligne de la Sheet "input.<controle>" (ex. input.leftShift -> Maj gauche).
// Nom entre crochets qui n'est pas une action -> laisse tel quel.
public static class InputPrompts
{
    public enum Device { Gamepad, Keyboard }

    public static event Action DeviceChanged;

    private static Device current;
    private static bool ready;
    private static PlayerInputActions actions;
    private static readonly Dictionary<string, string> cache = new Dictionary<string, string>();
    private static readonly Regex token = new Regex(@"\[([A-Za-z]+)\]");

    private static readonly Dictionary<string, string> xboxNames = new Dictionary<string, string>
    {
        { "buttonSouth", "A" }, { "buttonEast", "B" }, { "buttonWest", "X" }, { "buttonNorth", "Y" },
        { "leftShoulder", "LB" }, { "rightShoulder", "RB" },
        { "leftTrigger", "LT" }, { "rightTrigger", "RT" },
        { "leftStick", "Left Stick" }, { "rightStick", "Right Stick" },
        { "leftStick/x", "Left Stick" }, { "rightStick/x", "Right Stick" },
        { "leftStickPress", "LS" }, { "rightStickPress", "RS" },
        { "start", "Menu" }, { "select", "View" }, { "dpad", "D-Pad" },
    };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        ready = false;
        DeviceChanged = null;
        cache.Clear();
        InputSystem.onActionChange -= OnActionChange;
    }

    public static Device Current
    {
        get { EnsureReady(); return current; }
    }

    // Remplace chaque [Action] par sa touche sur l'appareil courant
    public static string Apply(string text)
    {
        if (string.IsNullOrEmpty(text) || text.IndexOf('[') < 0) return text;
        EnsureReady();
        return token.Replace(text, m => KeyFor(m.Groups[1].Value) ?? m.Value);
    }

    private static void EnsureReady()
    {
        if (ready) return;
        ready = true;
        // Tant que rien n'a ete touche : manette si une manette est branchee
        current = Gamepad.current != null ? Device.Gamepad : Device.Keyboard;
        if (Application.isPlaying)
            InputSystem.onActionChange += OnActionChange;
    }

    private static void OnActionChange(object obj, InputActionChange change)
    {
        if (change != InputActionChange.ActionPerformed) return;
        InputAction action = obj as InputAction;
        InputDevice device = action?.activeControl?.device;
        if (device == null) return;

        Device next;
        if (device is Gamepad) next = Device.Gamepad;
        else if (device is Keyboard || device is Mouse) next = Device.Keyboard;
        else return;

        if (next == current) return;
        current = next;
        cache.Clear();
        DeviceChanged?.Invoke();
    }

    // Touche de l'action sur l'appareil courant, null si l'action n'existe pas
    private static string KeyFor(string actionName)
    {
        string cacheKey = actionName + "|" + current + "|" + Loc.Current;
        if (cache.TryGetValue(cacheKey, out string found)) return found;

        if (actions == null) actions = new PlayerInputActions();
        InputAction action = actions.asset.FindAction(actionName);
        string result = action != null ? FindBinding(action) : null;
        if (action == null)
            Debug.LogWarning("[InputPrompts] Action inconnue : [" + actionName + "]");

        cache[cacheKey] = result;
        return result;
    }

    // Premiere touche de l'action pour l'appareil courant
    private static string FindBinding(InputAction action)
    {
        var bindings = action.bindings;
        for (int i = 0; i < bindings.Count; i++)
        {
            InputBinding b = bindings[i];
            if (b.isPartOfComposite) continue;

            // Composite (ex. ZQSD) : l'appareil se lit sur sa premiere partie
            string path = b.isComposite && i + 1 < bindings.Count ? bindings[i + 1].effectivePath : b.effectivePath;
            if (!MatchesCurrent(path)) continue;

            string control = ControlName(path);
            if (!b.isComposite && Loc.Has("input." + control)) return Loc.Get("input." + control);
            if (current == Device.Gamepad && !b.isComposite && xboxNames.TryGetValue(control, out string xbox)) return xbox;
            return action.GetBindingDisplayString(i);
        }
        return "?";
    }

    private static bool MatchesCurrent(string path)
    {
        if (string.IsNullOrEmpty(path)) return false;
        if (current == Device.Gamepad) return path.StartsWith("<Gamepad>");
        return path.StartsWith("<Keyboard>") || path.StartsWith("<Mouse>");
    }

    // "<Keyboard>/leftShift" -> "leftShift"
    private static string ControlName(string path)
    {
        int slash = path.IndexOf('/');
        return slash >= 0 ? path.Substring(slash + 1) : path;
    }
}
