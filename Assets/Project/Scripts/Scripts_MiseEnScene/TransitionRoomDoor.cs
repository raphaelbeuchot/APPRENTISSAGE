using UnityEngine;

/// <summary>
/// Porte de la TransitionRoom post-victoire.
/// Quand le joueur est a portee et appuie sur la touche d'interaction,
/// execute l'action assignee (niveau suivant ou restart).
///
/// Setup :
///   - Choisir l'action dans l'Inspector (NextLevel / Restart)
///   - Assigner interactBubble (InteractBubble sur ce GO ou un enfant)
///   - Activer la porte via Enable() une fois que la dalle a atterri
/// </summary>
public class TransitionRoomDoor : MonoBehaviour
{
    public enum DoorAction { NextLevel, Restart }

    [Header("Configuration")]
    [SerializeField] private DoorAction action = DoorAction.NextLevel;

    [Tooltip("Rayon de detection du joueur (en unites monde)")]
    [SerializeField] private float interactRange = 2.5f;

    [Tooltip("InteractBubble associe a cette porte")]
    [SerializeField] private InteractBubble interactBubble;

    private Transform playerTransform;
    private bool playerInRange = false;
    private bool activated = false;

    // Desactivee par defaut — activee par PostVictorySequencer quand la dalle a pose
    private bool isEnabled = false;

    // ============================================
    // UNITY LIFECYCLE
    // ============================================

    private void Start()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
            playerTransform = playerObj.transform;
        else
            Debug.LogWarning("[TransitionRoomDoor] Player introuvable (tag Player manquant ?)");
    }

    private void Update()
    {
        if (!isEnabled || activated || playerTransform == null) return;

        float dist = Vector3.Distance(transform.position, playerTransform.position);
        bool inRange = dist <= interactRange;

        // Afficher / masquer la bulle selon la proximite
        if (inRange != playerInRange)
        {
            playerInRange = inRange;
            if (interactBubble != null)
            {
                if (inRange) interactBubble.Show();
                else         interactBubble.Hide();
            }
        }

        // Detecter l'appui sur la touche d'interaction
        if (inRange && IsInteractPressed())
            Activate();
    }

    // ============================================
    // API PUBLIQUE
    // ============================================

    /// <summary>Appele par PostVictorySequencer une fois la dalle posee.</summary>
    public void Enable()
    {
        isEnabled = true;
        Debug.Log($"[TransitionRoomDoor] Activee : {action}");
    }

    // ============================================
    // HELPERS
    // ============================================

    private bool IsInteractPressed()
    {
        return Input.GetButtonDown("Submit")
            || Input.GetKeyDown(KeyCode.E)
            || Input.GetKeyDown(KeyCode.F);
    }

    private void Activate()
    {
        activated = true;

        if (interactBubble != null)
            interactBubble.Hide();

        LevelManager lm = FindObjectOfType<LevelManager>();
        if (lm == null)
        {
            Debug.LogError("[TransitionRoomDoor] LevelManager introuvable.");
            return;
        }

        Debug.Log($"[TransitionRoomDoor] Action : {action}");

        switch (action)
        {
            case DoorAction.NextLevel:
                lm.LoadNextLevel();
                break;

            case DoorAction.Restart:
                lm.RestartLevel();
                break;
        }
    }
}
