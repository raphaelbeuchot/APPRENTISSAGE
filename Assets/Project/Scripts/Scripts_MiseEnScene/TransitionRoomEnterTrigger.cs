using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Trigger a placer dans la TransitionRoom.
/// Quand le joueur entre, desactive le Global Volume des lights-out (PostVictorySequencer).
///
/// Setup :
///   - GO avec Collider trigger + ce script
///   - Assigner lightsOutVolume -> le Volume cree par PostVictorySequencer (5b)
/// </summary>
public class TransitionRoomEnterTrigger : MonoBehaviour
{
    [SerializeField] private Volume lightsOutVolume;

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        if (lightsOutVolume != null)
        {
            lightsOutVolume.gameObject.SetActive(false);
            Debug.Log("[TransitionRoomTrigger] Lights-out volume desactive.");
        }
    }
}
