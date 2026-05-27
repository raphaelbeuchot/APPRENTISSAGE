using UnityEngine;
using System;
using System.Collections;

/// <summary>
/// Porte generique pilotee par un bouton.
///
/// Comportement :
///   - InteractBubble gere sa propre visibilite (detectionRange sur le composant)
///   - Press X (porte fermee, joueur en range) -> porte monte + CommentPanel.ShowPersistent(openMessage)
///   - Joueur recule au-dela de closeDistance -> porte redescend + CommentPanel.Hide()
///   - Events OnDoorOpened / OnDoorClosed pour brancher du comportement specifique
///
/// Setup :
///   - Ce script va sur le GO de la porte (mesh)
///   - Assigner interactBubble (composant sur le GO Bouton)
///   - La detection de range pour la bulle est geree par detectionRange sur InteractBubble
///   - startEnabled = false si la porte doit etre activee depuis l'exterieur (ex: TransitionRoom)
/// </summary>
public class DoorBasic : MonoBehaviour
{
    [Header("Bouton")]
    [Tooltip("Composant InteractBubble sur le GO Bouton — gere sa propre visibilite")]
    [SerializeField] private InteractBubble interactBubble;

    [Tooltip("Distance au-dela de laquelle la porte se referme")]
    [SerializeField] private float closeDistance = 4f;

    [Header("Ouverture / Fermeture")]
    [SerializeField] private float openDuration = 0.8f;
    [SerializeField] private float closeDuration = 0.8f;
    [SerializeField] private AnimationCurve openCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip openSound;
    [SerializeField] private AudioClip closeSound;

    [Header("Etat initial")]
    [Tooltip("Si false, appeler Enable() depuis l'exterieur pour activer la porte")]
    [SerializeField] private bool startEnabled = true;

    // ============================================
    // EVENTS
    // ============================================

    /// <summary>Fire quand la porte a fini de monter.</summary>
    public event Action OnDoorOpened;

    /// <summary>Fire quand la porte se referme apres que le joueur a recule.</summary>
    public event Action OnDoorClosed;

    /// <summary>Fire quand la porte se referme apres franchissement (ForceClose).</summary>
    public event Action OnDoorForceClosed;

    // ============================================
    // ETAT
    // ============================================

    public bool IsOpen          { get; private set; }
    public bool IsEnabled       { get; private set; }
    public bool IsAnimating     => isAnimating;

    /// <summary>Range de la bulle, lu depuis InteractBubble.</summary>
    public float InteractRange  => interactBubble != null ? interactBubble.DetectionRange : 0f;

    private bool isAnimating = false;
    private bool isFinalClose = false;
    private Vector3 closedPos;
    private Vector3 openPos;
    private Transform playerTransform;

    // ============================================
    // UNITY LIFECYCLE
    // ============================================

    private void Start()
    {
        closedPos = transform.position;
        openPos   = closedPos + Vector3.up * GetDoorHeight();

        IsEnabled = startEnabled;

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
            playerTransform = playerObj.transform;
        else
            Debug.LogWarning("[DoorBasic] Player introuvable (tag Player manquant ?)");
    }

    private void Update()
    {
        if (!IsEnabled || playerTransform == null) return;

        // Distance horizontale uniquement (XZ) -- la porte monte en Y, la distance 3D fausserait les checks
        float dist = Vector2.Distance(
            new Vector2(closedPos.x, closedPos.z),
            new Vector2(playerTransform.position.x, playerTransform.position.z));

        // InteractBubble gere sa propre visibilite -- pas d'appel Show/Hide ici

        // Press X (porte fermee) -> ouvrir
        if (!IsOpen && !isAnimating && dist <= InteractRange
            && PlayerInputManager.Instance != null
            && PlayerInputManager.Instance.InteractPressed)
        {
            Open();
        }

        // Reculer (porte ouverte) -> refermer
        if (IsOpen && !isAnimating && dist > closeDistance)
        {
            Close();
        }
    }

    // ============================================
    // API PUBLIQUE
    // ============================================

    public void Enable()
    {
        IsEnabled = true;
        Debug.Log("[DoorBasic] Activee.");
    }

    public void Disable()
    {
        IsEnabled = false;
        if (interactBubble != null) interactBubble.enabled = false;
        CommentPanel.Hide();
    }

    /// <summary>Ferme la porte apres franchissement — bulle desactivee, event OnDoorForceClosed.</summary>
    public void ForceClose()
    {
        if (!IsOpen || isAnimating) return;
        isFinalClose = true;
        Close();
    }

    // ============================================
    // OUVERTURE / FERMETURE
    // ============================================

    private void Open()
    {
        if (isAnimating || IsOpen) return;

        if (audioSource != null && openSound != null)
            audioSource.PlayOneShot(openSound);

        StartCoroutine(AnimateDoor(closedPos, openPos, openDuration, () =>
        {
            IsOpen = true;
            // Desactiver la bulle pendant que la porte est ouverte
            if (interactBubble != null)
            {
                interactBubble.Hide();
                interactBubble.enabled = false;
            }
            Debug.Log("[DoorBasic] Ouverte.");
            OnDoorOpened?.Invoke();
        }));
    }

    private void Close()
    {
        if (isAnimating || !IsOpen) return;
        IsOpen = false;

        CommentPanel.Hide();

        if (audioSource != null && closeSound != null)
            audioSource.PlayOneShot(closeSound);

        StartCoroutine(AnimateDoor(transform.position, closedPos, closeDuration, () =>
        {
            Debug.Log("[DoorBasic] Refermee.");
            if (isFinalClose)
            {
                // Joueur est passe : bulle reste off, event final
                isFinalClose = false;
                OnDoorForceClosed?.Invoke();
            }
            else
            {
                // Joueur a recule : bulle se reactivee, porte de nouveau utilisable
                if (interactBubble != null)
                    interactBubble.enabled = true;
                OnDoorClosed?.Invoke();
            }
        }));
    }

    private IEnumerator AnimateDoor(Vector3 from, Vector3 to, float duration, Action onComplete)
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
    // HELPERS
    // ============================================

    private float GetDoorHeight()
    {
        Renderer rend = GetComponentInChildren<Renderer>();
        if (rend != null) return rend.bounds.size.y;

        Collider col = GetComponentInChildren<Collider>();
        if (col != null) return col.bounds.size.y;

        Debug.LogWarning("[DoorBasic] Hauteur introuvable — 3u par defaut.");
        return 3f;
    }
}
