using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;

// Socle du balai : le balai est un accessoire d'episode (voir STRUCTURE_CAMPAGNE.md).
// Prise : le niveau demarre sans balai (case Has Broom decochee dans le LevelCatalog), le joueur le prend ici.
// Depot : le niveau demarre avec le balai, le joueur le pose ici (le pupitre peut l'exiger : Must Put Away Broom),
// puis le socle s'enfonce dans le sol. Sur un restart, le socle est deja enfonce et le joueur n'a pas de balai.
// Solo uniquement : en multi on a toujours le balai, pas de socle.
public class BroomPedestal : MonoBehaviour
{
    public enum PedestalMode { Pickup, Deposit }

    [Header("Mode")]
    [SerializeField] private PedestalMode mode = PedestalMode.Pickup;
    public PedestalMode Mode => mode;

    [Header("Interaction")]
    [SerializeField] private float interactionRange = 2f;

    [Header("Visual")]
    [Tooltip("Balai pose sur le socle : cache une fois pris (Prise), affiche une fois depose (Depot)")]
    [SerializeField] private GameObject broomVisual;
    [Tooltip("Polish du socle en attente (particules, lumiere...) a eteindre a l'utilisation. Inutile pour les enfants du Broom Visual en mode Prise, deja eteints avec lui")]
    [FormerlySerializedAs("hideOnTake")]
    [SerializeField] private GameObject[] hideOnUse;

    [Header("Feedback")]
    [Tooltip("Particules jouees a la prise ou au depot (Play On Awake). Detachees du socle avant activation : peuvent etre rangees n'importe ou")]
    [FormerlySerializedAs("pickupEffect")]
    [SerializeField] private GameObject useEffect;
    [Tooltip("Joue sur l'AudioSource du GameManager")]
    [FormerlySerializedAs("pickupSound")]
    [SerializeField] private AudioClip useSound;

    [Header("Depot : enfoncement")]
    [Tooltip("Attente apres le depot avant que le socle s'enfonce")]
    [SerializeField] private float sinkDelay = 2f;
    [SerializeField] private float sinkDistance = 1f;
    [SerializeField] private float sinkDuration = 0.5f;

    private PlayerLoadout loadout;
    private InteractBubble interactBubble;
    private AudioSource audioSource;
    private bool used;
    private bool isRestart;

    private void Awake()
    {
        // Lu en Awake : CountdownManager efface la cle dans son Start
        isRestart = PlayerPrefs.GetInt("AutoStartCountdown", 0) == 1;
    }

    private void Start()
    {
        loadout = FindObjectOfType<PlayerLoadout>();
        if (loadout == null)
            Debug.LogWarning("[BroomPedestal] PlayerLoadout introuvable");

        interactBubble = GetComponent<InteractBubble>();

        GameManager gameManager = FindObjectOfType<GameManager>();
        if (gameManager != null)
            audioSource = gameManager.GetComponent<AudioSource>();
        if (audioSource == null && useSound != null)
            Debug.LogWarning("[BroomPedestal] AudioSource introuvable sur GameManager");

        // L'effet ne doit se jouer qu'a l'utilisation, meme s'il est laisse actif dans la scene
        if (useEffect != null) useEffect.SetActive(false);

        if (mode == PedestalMode.Deposit)
        {
            if (broomVisual != null) broomVisual.SetActive(false);
            // Restart : on reapparait sans balai, socle deja enfonce
            if (isRestart) SetDepositedInstant();
        }
    }

    private void Update()
    {
        if (used || loadout == null || PlayerInputManager.Instance == null) return;

        float distance = Vector3.Distance(transform.position, loadout.transform.position);
        if (distance > interactionRange || !PlayerInputManager.Instance.InteractPressed) return;

        if (mode == PedestalMode.Pickup)
            Take();
        else if (loadout.hasBroom)
            Deposit();
    }

    private void Take()
    {
        used = true;
        loadout.ApplyLoadout(true, loadout.hasSpray);

        PlayFeedback();
        if (broomVisual != null) broomVisual.SetActive(false);
        HideIdleVisuals();
    }

    private void Deposit()
    {
        used = true;
        loadout.ApplyLoadout(false, loadout.hasSpray);

        PlayFeedback();
        if (broomVisual != null) broomVisual.SetActive(true);
        HideIdleVisuals();
        StartCoroutine(SinkCoroutine());
    }

    private void SetDepositedInstant()
    {
        used = true;
        if (loadout != null) loadout.ApplyLoadout(false, loadout.hasSpray);

        if (broomVisual != null) broomVisual.SetActive(true);
        HideIdleVisuals();
        transform.position += Vector3.down * sinkDistance;
    }

    // Detache avant d'eteindre ou d'enfoncer quoi que ce soit : rangee sous le balai ou le socle,
    // l'effet serait eteint ou enfonce avec lui
    private void PlayFeedback()
    {
        if (useEffect != null)
        {
            useEffect.transform.SetParent(transform.parent, true);
            useEffect.SetActive(true);
        }
        if (audioSource != null && useSound != null)
            audioSource.PlayOneShot(useSound);
    }

    private void HideIdleVisuals()
    {
        foreach (GameObject go in hideOnUse)
            if (go != null) go.SetActive(false);
        if (interactBubble != null)
        {
            interactBubble.Hide();
            interactBubble.enabled = false;
        }
    }

    // Meme enfoncement que le pupitre (PupitreInteraction.SinkPupitreCoroutine)
    private IEnumerator SinkCoroutine()
    {
        yield return new WaitForSeconds(sinkDelay);

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
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, interactionRange);
    }
}
