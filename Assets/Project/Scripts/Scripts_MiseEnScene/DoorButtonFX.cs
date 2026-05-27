using UnityEngine;

/// <summary>
/// FX visuels du bouton associe a une DoorBasic.
/// Gere 3 etats materiaux : idle, porte ouverte, porte refermee.
///
/// Setup :
///   - Ce script va sur le GO Bouton
///   - Assigner door -> le composant DoorBasic de la porte
///   - Assigner buttonRenderer -> le Renderer du mesh bouton
///   - Assigner les 3 materiaux dans l'Inspector
/// </summary>
public class DoorButtonFX : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private DoorBasic door;
    [SerializeField] private Renderer buttonRenderer;

    [Header("Materiaux")]
    [Tooltip("Etat par defaut — bouton actif en attente")]
    [SerializeField] private Material materialIdle;

    [Tooltip("Etat quand la porte est ouverte")]
    [SerializeField] private Material materialOpen;

    [Tooltip("Etat quand la porte s'est refermee apres avoir ete ouverte")]
    [SerializeField] private Material materialClosed;

    // ============================================
    // UNITY LIFECYCLE
    // ============================================

    private void Start()
    {
        if (door != null)
        {
            door.OnDoorOpened      += HandleDoorOpened;
            door.OnDoorClosed      += HandleDoorClosed;
            door.OnDoorForceClosed += HandleDoorForceClosed;
        }
        else
        {
            Debug.LogWarning("[DoorButtonFX] DoorBasic non assigne.");
        }

        ApplyMaterial(materialIdle);
    }

    private void OnDestroy()
    {
        if (door != null)
        {
            door.OnDoorOpened      -= HandleDoorOpened;
            door.OnDoorClosed      -= HandleDoorClosed;
            door.OnDoorForceClosed -= HandleDoorForceClosed;
        }
    }

    // ============================================
    // HANDLERS
    // ============================================

    private void HandleDoorOpened()
    {
        ApplyMaterial(materialOpen);
    }

    private void HandleDoorClosed()
    {
        // Joueur a recule : porte encore utilisable -> retour idle
        ApplyMaterial(materialIdle);
    }

    private void HandleDoorForceClosed()
    {
        // Joueur est passe : porte definitivement fermee -> materialClosed
        ApplyMaterial(materialClosed);
    }

    // ============================================
    // HELPERS
    // ============================================

    private void ApplyMaterial(Material mat)
    {
        if (mat != null && buttonRenderer != null)
            buttonRenderer.material = mat;
    }
}
