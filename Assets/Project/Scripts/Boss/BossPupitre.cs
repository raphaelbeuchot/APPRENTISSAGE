using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Pupitre d'epreuve du boss : le joueur s'approche, appuie sur Interact, le pupitre s'active une seule fois.
/// Rien a voir avec PupitreInteraction (pupitre de depart du niveau).
/// Effets propres a ce pupitre : a brancher dans onActivated (explosion, obstacle abattu, etc.).
/// Le BossPhaseManager ecoute Activated pour compter les pupitres de la phase.
/// Optionnel : une InteractBubble sur le meme objet (affichage du bouton), cachee a l'activation.
/// </summary>
public class BossPupitre : MonoBehaviour
{
    [Header("Interaction")]
    [SerializeField] private float interactionRange = 2f;

    [Header("Enfoncement a l'activation (0 = aucun)")]
    [SerializeField] private float sinkDistance = 0.3f;
    [SerializeField] private float sinkDuration = 0.3f;

    [Header("Effets de ce pupitre")]
    [SerializeField] private UnityEvent onActivated;

    /// <summary>Prevenu a l'activation (utilise par BossPhaseManager).</summary>
    public event System.Action<BossPupitre> Activated;

    public bool IsActivated { get; private set; }

    private Transform player;

    private void Start()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
            player = playerObj.transform;
        else
            Debug.LogWarning("[BossPupitre] Player introuvable : " + name);
    }

    private void Update()
    {
        if (IsActivated || player == null || PlayerInputManager.Instance == null) return;

        bool inRange = Vector3.Distance(transform.position, player.position) <= interactionRange;
        if (inRange && PlayerInputManager.Instance.InteractPressed)
            Activate();
    }

    /// <summary>Active le pupitre (aussi appelable depuis un autre script ou un evenement).</summary>
    public void Activate()
    {
        if (IsActivated) return;
        IsActivated = true;

        InteractBubble bubble = GetComponent<InteractBubble>();
        if (bubble != null)
        {
            bubble.Hide();
            bubble.enabled = false; // la bulle ne revient plus
        }

        if (sinkDistance > 0f)
            StartCoroutine(SinkCoroutine());

        Debug.Log("[BossPupitre] Active : " + name);
        onActivated?.Invoke();
        Activated?.Invoke(this);
    }

    private IEnumerator SinkCoroutine()
    {
        Vector3 startPos = transform.position;
        Vector3 targetPos = startPos + Vector3.down * sinkDistance;
        float elapsed = 0f;

        while (elapsed < sinkDuration)
        {
            elapsed += Time.deltaTime;
            transform.position = Vector3.Lerp(startPos, targetPos, elapsed / sinkDuration);
            yield return null;
        }
        transform.position = targetPos;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, interactionRange);
    }
}
