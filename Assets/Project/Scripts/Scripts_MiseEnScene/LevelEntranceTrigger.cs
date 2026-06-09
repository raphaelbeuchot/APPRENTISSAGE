using UnityEngine;

/// <summary>
/// Trigger a placer dans l'encadrement de la porte d'entree.
/// Ferme la porte quand le joueur franchit le seuil.
///
/// Setup :
///   - GO enfant avec Collider trigger + ce script
///   - Assigner door -> LevelEntranceDoor du GO parent
/// </summary>
public class LevelEntranceTrigger : MonoBehaviour
{
    [SerializeField] private LevelEntranceDoor door;

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        door?.OnPlayerEntered();
        enabled = false;
    }
}
