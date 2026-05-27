using UnityEngine;

/// <summary>
/// Porte de la TransitionRoom post-victoire.
/// S'appuie sur DoorBasic pour toute la mecanique bouton/porte.
///
/// Comportement specifique :
///   - Inactive jusqu'a Enable() (appele par PostVictorySequencer)
///   - A l'ouverture : CommentPanel.ShowPersistent("Continue")
///   - Press X porte ouverte -> LevelManager.LoadNextLevel()
///     (Phase 2 : animation joueur qui entre dans la porte avant le fade)
///
/// Setup :
///   - Ce script va sur le meme GO que DoorBasic (ou un GO parent)
///   - Assigner door -> le composant DoorBasic
///   - Laisser openMessage vide dans DoorBasic (c'est TransitionRoomDoor qui gere le message)
///   - startEnabled = false dans DoorBasic
/// </summary>
public class TransitionRoomDoor : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private DoorBasic door;

    // ============================================
    // ETAT
    // ============================================

    private bool hasTriggeredLoad = false;
    private Transform playerTransform;

    // ============================================
    // UNITY LIFECYCLE
    // ============================================

    private void Start()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
            playerTransform = playerObj.transform;
        else
            Debug.LogWarning("[TransitionRoomDoor] Player introuvable.");

        if (door != null)
            door.OnDoorOpened += HandleDoorOpened;
        else
            Debug.LogWarning("[TransitionRoomDoor] DoorBasic non assigne.");
    }

    private void OnDestroy()
    {
        if (door != null)
            door.OnDoorOpened -= HandleDoorOpened;
    }

    private void Update()
    {
        if (hasTriggeredLoad || playerTransform == null || door == null) return;
        if (!door.IsOpen || !door.IsEnabled) return;

        float dist = Vector2.Distance(
            new Vector2(door.transform.position.x, door.transform.position.z),
            new Vector2(playerTransform.position.x, playerTransform.position.z));

        if (dist <= door.InteractRange
            && PlayerInputManager.Instance != null
            && PlayerInputManager.Instance.InteractPressed)
        {
            TriggerLoad();
        }
    }

    // ============================================
    // API PUBLIQUE
    // ============================================

    /// <summary>Appele par PostVictorySequencer une fois le rideau leve.</summary>
    public void Enable()
    {
        if (door != null) door.Enable();
    }

    // ============================================
    // HANDLERS
    // ============================================

    private void HandleDoorOpened()
    {
        CommentPanel.ShowPersistent("Continue");
    }

    // ============================================
    // CHARGEMENT
    // ============================================

    private void TriggerLoad()
    {
        hasTriggeredLoad = true;
        Debug.Log("[TransitionRoomDoor] Press X porte ouverte — chargement niveau suivant.");

        LevelManager lm = FindObjectOfType<LevelManager>();
        if (lm != null)
            lm.LoadNextLevel();
        else
            Debug.LogWarning("[TransitionRoomDoor] LevelManager introuvable.");
    }
}
