using UnityEngine;
using System.Collections;

/// <summary>
/// Porte de la TransitionRoom post-victoire.
///
/// Comportement :
///   - Inactive jusqu'a Enable() (appele par PostVictorySequencer apres EndCurtainRise)
///   - Joueur s'approche -> InteractBubble
///   - Press X -> porte monte + affiche continueUI
///   - Joueur recule au-dela de closeDistance -> porte redescend
///   - Joueur franchit l'enterTrigger -> fade out -> niveau suivant
///
/// Setup :
///   - Placer un GO enfant vide "EnterTrigger" avec Collider trigger dans l'encadrement
///   - Assigner sceneFadeOut (CanvasGroup noir sur Canvas world-space ou overlay)
/// </summary>
public class TransitionRoomDoor : MonoBehaviour
{
    [Header("Interaction")]
    [Tooltip("Distance pour afficher l'InteractBubble et accepter le press X")]
    [SerializeField] private float interactRange = 2.5f;

    [Tooltip("Distance au-dela de laquelle la porte se referme")]
    [SerializeField] private float closeDistance = 4f;

    [SerializeField] private InteractBubble interactBubble;

    [Header("Ouverture / Fermeture")]
    [SerializeField] private float openDuration = 0.8f;
    [SerializeField] private AnimationCurve openCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip openSound;
    [SerializeField] private AudioClip closeSound;

    [Header("Continue UI")]
    [Tooltip("GO ou Canvas a afficher quand la porte est ouverte (ex: texte 'Continue')")]
    [SerializeField] private GameObject continueUI;

    [Header("Chargement")]
    [Tooltip("SceneFadeOut pour le fondu avant le chargement")]
    [SerializeField] private SceneFadeOut sceneFadeOut;

    [Tooltip("Trigger a franchir pour declencher le chargement (enfant de ce GO ou GO separe)")]
    [SerializeField] private Collider enterTrigger;

    // ============================================
    // ETAT INTERNE
    // ============================================

    private bool isEnabled = false;
    private bool isOpen = false;
    private bool isAnimating = false;
    private bool hasTriggeredLoad = false;

    private Vector3 closedPos;
    private Vector3 openPos;
    private Transform playerTransform;

    // ============================================
    // UNITY LIFECYCLE
    // ============================================

    private void Start()
    {
        closedPos = transform.position;
        openPos = closedPos + Vector3.up * GetDoorHeight();

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
            playerTransform = playerObj.transform;
        else
            Debug.LogWarning("[TransitionDoor] Player introuvable (tag Player manquant ?)");

        if (continueUI != null) continueUI.SetActive(false);
    }

    private void Update()
    {
        if (!isEnabled || hasTriggeredLoad || playerTransform == null) return;

        float dist = Vector3.Distance(transform.position, playerTransform.position);

        // Afficher / masquer la bulle
        if (!isOpen && interactBubble != null)
        {
            if (dist <= interactRange) interactBubble.Show();
            else                       interactBubble.Hide();
        }

        // Press X -> ouvrir
        if (!isOpen && !isAnimating && dist <= interactRange
            && PlayerInputManager.Instance != null
            && PlayerInputManager.Instance.InteractPressed)
        {
            Open();
        }

        // Reculer -> refermer
        if (isOpen && !isAnimating && dist > closeDistance)
        {
            Close();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!isEnabled || hasTriggeredLoad || !isOpen) return;
        if (!other.CompareTag("Player")) return;

        hasTriggeredLoad = true;
        StartCoroutine(LoadNextLevel());
    }

    // ============================================
    // API PUBLIQUE
    // ============================================

    public void Enable()
    {
        isEnabled = true;
        Debug.Log("[TransitionDoor] Activee.");
    }

    // ============================================
    // OUVERTURE / FERMETURE
    // ============================================

    private void Open()
    {
        if (isAnimating || isOpen) return;
        StartCoroutine(AnimateDoor(closedPos, openPos, openDuration, onComplete: () =>
        {
            isOpen = true;
            if (interactBubble != null) interactBubble.Hide();
            if (continueUI != null) continueUI.SetActive(true);
            Debug.Log("[TransitionDoor] Ouverte.");
        }));

        if (audioSource != null && openSound != null)
            audioSource.PlayOneShot(openSound);
    }

    private void Close()
    {
        if (isAnimating || !isOpen) return;
        isOpen = false;
        if (continueUI != null) continueUI.SetActive(false);

        StartCoroutine(AnimateDoor(transform.position, closedPos, openDuration, onComplete: null));

        if (audioSource != null && closeSound != null)
            audioSource.PlayOneShot(closeSound);

        Debug.Log("[TransitionDoor] Refermee.");
    }

    private IEnumerator AnimateDoor(Vector3 from, Vector3 to, float duration, System.Action onComplete)
    {
        isAnimating = true;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = openCurve.Evaluate(Mathf.Clamp01(elapsed / duration));
            transform.position = Vector3.Lerp(from, to, t);
            yield return null;
        }

        transform.position = to;
        isAnimating = false;
        onComplete?.Invoke();
    }

    // ============================================
    // CHARGEMENT NIVEAU SUIVANT
    // ============================================

    private IEnumerator LoadNextLevel()
    {
        Debug.Log("[TransitionDoor] Franchissement — chargement niveau suivant.");

        if (sceneFadeOut != null)
        {
            // SceneFadeOut gere le fade ET le LoadingScreen
            LevelManager lm = FindObjectOfType<LevelManager>();
            if (lm != null)
            {
                // Recuperer l'index du niveau suivant via LevelManager
                // puis passer par SceneFadeOut pour le fade
                lm.LoadNextLevel();
            }
            else
            {
                sceneFadeOut.FadeToScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex + 1);
            }
        }
        else
        {
            LevelManager lm = FindObjectOfType<LevelManager>();
            if (lm != null) lm.LoadNextLevel();
        }

        yield break;
    }

    // ============================================
    // HELPERS
    // ============================================

    private float GetDoorHeight()
    {
        Renderer rend = GetComponentInChildren<Renderer>();
        if (rend != null) return rend.bounds.size.y;

        Collider col = GetComponentInChildren<Collider>();
        if (col != null) return col.bounds.size.y;

        Debug.LogWarning("[TransitionDoor] Hauteur introuvable — 3u par defaut.");
        return 3f;
    }
}
