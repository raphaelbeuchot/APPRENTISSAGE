using UnityEngine;
using UnityEngine.InputSystem;

public class MultiTestFinishLine : MonoBehaviour
{
    public static int WinnerPlayerIndex { get; private set; } = -1;

    private void OnTriggerEnter(Collider other)
    {
        if (WinnerPlayerIndex != -1) return;

        PlayerInput playerInput = other.GetComponent<PlayerInput>();
        if (playerInput == null) return;

        WinnerPlayerIndex = playerInput.playerIndex;
        Debug.Log($"[MultiTest] Player {WinnerPlayerIndex} a gagne !");
    }
}
