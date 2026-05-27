using UnityEngine;

/// <summary>
/// Trigger a placer dans l'encadrement de la porte (GO enfant).
/// Quand le joueur franchit le seuil, declenche la sequence de chargement.
///
/// Setup :
///   - GO enfant avec Collider trigger + ce script
///   - Assigner door -> le composant TransitionRoomDoor
/// </summary>
public class DoorEnterTrigger : MonoBehaviour
{
    [SerializeField] private TransitionRoomDoor door;

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        door?.OnPlayerEntered();
    }
}
