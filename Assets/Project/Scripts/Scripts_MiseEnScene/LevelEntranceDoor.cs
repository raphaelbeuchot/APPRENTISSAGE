using UnityEngine;
using System.Collections;

/// <summary>
/// Porte d'entree automatique en debut de niveau.
/// S'ouvre seule apres le fade-in de la scene, se referme quand le joueur passe le trigger.
///
/// Setup :
///   - Ce script sur le meme GO que DoorBasic
///   - Assigner door -> DoorBasic (meme GO, startEnabled = false)
///   - Placer un GO enfant "EnterTrigger" avec Collider trigger + LevelEntranceTrigger.cs
/// </summary>
public class LevelEntranceDoor : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private DoorBasic door;

    [Header("Timing")]
    [Tooltip("Delai en secondes apres le fade-in avant que la porte s'ouvre")]
    [SerializeField] private float delayAfterFade = 0.5f;

    // ============================================
    // UNITY LIFECYCLE
    // ============================================

    private void Start()
    {
        if (SceneFader.Instance != null && SceneFader.Instance.IsFading)
            SceneFader.Instance.OnFadeOutComplete += OnSceneReady;
        else
            StartCoroutine(OpenAfterDelay());
    }

    private void OnDestroy()
    {
        if (SceneFader.Instance != null)
            SceneFader.Instance.OnFadeOutComplete -= OnSceneReady;
    }

    // ============================================
    // API PUBLIQUE
    // ============================================

    /// <summary>Appele par LevelEntranceTrigger quand le joueur franchit le seuil.</summary>
    public void OnPlayerEntered()
    {
        door?.ForceClose();
    }

    // ============================================
    // SEQUENCE
    // ============================================

    private void OnSceneReady()
    {
        SceneFader.Instance.OnFadeOutComplete -= OnSceneReady;
        StartCoroutine(OpenAfterDelay());
    }

    private IEnumerator OpenAfterDelay()
    {
        yield return new WaitForSeconds(delayAfterFade);
        door?.AutoOpen();
    }
}
