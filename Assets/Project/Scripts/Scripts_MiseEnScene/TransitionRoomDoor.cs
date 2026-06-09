using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

/// <summary>
/// Porte de la TransitionRoom post-victoire.
/// S'appuie sur DoorBasic pour toute la mecanique bouton/porte.
///
/// Flow specifique :
///   1. Enable() appele par PostVictorySequencer
///   2. Joueur ouvre la porte (Press X via DoorBasic)
///   3. Joueur franchit le DoorEnterTrigger -> OnPlayerEntered()
///   4. Porte se ferme (ForceClose)
///   5. Attente delayAfterClose
///   6. Fade out + loading screen + niveau suivant
///
/// Setup :
///   - Ce script sur le meme GO que DoorBasic
///   - Assigner door -> DoorBasic (meme GO)
///   - Placer un GO enfant "EnterTrigger" avec Collider trigger + DoorEnterTrigger.cs
/// </summary>
public class TransitionRoomDoor : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private DoorBasic door;

    [Header("Message")]
    [Tooltip("Texte affiche dans CommentPanel quand la porte s'ouvre")]
    [SerializeField] private string doorOpenMessage = "Continue";

    [Header("Chargement")]
    [Tooltip("Delai en secondes apres la fermeture de la porte avant le fade")]
    [SerializeField] private float delayAfterClose = 1f;

    // ============================================
    // ETAT
    // ============================================

    private bool hasTriggeredLoad = false;

    // ============================================
    // UNITY LIFECYCLE
    // ============================================

    private void Start()
    {
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

    // ============================================
    // API PUBLIQUE
    // ============================================

    /// <summary>Appele par PostVictorySequencer une fois le rideau leve.</summary>
    public void Enable()
    {
        if (door != null) door.Enable();
    }

    /// <summary>Appele par DoorEnterTrigger quand le joueur franchit le seuil.</summary>
    public void OnPlayerEntered()
    {
        if (hasTriggeredLoad) return;
        hasTriggeredLoad = true;
        StartCoroutine(LoadSequence());
    }

    // ============================================
    // HANDLERS
    // ============================================

    private void HandleDoorOpened()
    {
        if (!string.IsNullOrEmpty(doorOpenMessage))
            CommentPanel.ShowPersistent(doorOpenMessage);
    }

    // ============================================
    // SEQUENCE DE CHARGEMENT
    // ============================================

    private IEnumerator LoadSequence()
    {
        Debug.Log("[TransitionRoomDoor] Joueur entre — sequence de chargement.");

        // Fermer la porte derriere le joueur
        door?.ForceClose();

        // Attendre la fin de l'animation de fermeture
        if (door != null)
            yield return new WaitUntil(() => !door.IsOpen && !door.IsAnimating);

        // Delai avant fade
        yield return new WaitForSeconds(delayAfterClose);

        // Fade + niveau suivant via SceneFader (loading cache par la porte d'entree)
        int nextScene = GetNextSceneIndex();
        if (nextScene != -1 && SceneFader.Instance != null)
        {
            SceneFader.Instance.FadeToSceneAsync(nextScene);
        }
        else
        {
            // Fallback sans fade
            LevelManager lm = FindObjectOfType<LevelManager>();
            if (lm != null) lm.LoadNextLevel();
            else Debug.LogWarning("[TransitionRoomDoor] LevelManager introuvable.");
        }
    }

    // ============================================
    // HELPERS
    // ============================================

    private int GetNextSceneIndex()
    {
        if (LevelProgressionManager.Instance != null)
            return LevelProgressionManager.Instance.GetNextLevelSceneIndex(
                SceneManager.GetActiveScene().buildIndex);

        Debug.LogWarning("[TransitionRoomDoor] LevelProgressionManager introuvable — fallback buildIndex+1.");
        return SceneManager.GetActiveScene().buildIndex + 1;
    }
}
