using UnityEngine;

// Socle de prise du balai : le balai est un accessoire d'episode (voir STRUCTURE_CAMPAGNE.md).
// Le niveau demarre sans balai (case Has Broom decochee dans le LevelCatalog), le joueur le prend ici.
// Solo uniquement : en multi on a toujours le balai, pas de socle.
public class BroomPedestal : MonoBehaviour
{
    [Header("Interaction")]
    [SerializeField] private float interactionRange = 2f;

    [Header("Visual")]
    [Tooltip("Balai pose sur le socle, cache une fois pris")]
    [SerializeField] private GameObject broomVisual;
    [Tooltip("Polish du balai en attente (particules, lumiere...) a eteindre a la prise. Inutile pour les enfants du Broom Visual, deja eteints avec lui")]
    [SerializeField] private GameObject[] hideOnTake;

    [Header("Feedback de prise")]
    [Tooltip("Particules de ramassage, activees a la prise (Play On Awake). Detachees vers le socle avant activation : peuvent etre rangees n'importe ou")]
    [SerializeField] private GameObject pickupEffect;
    [Tooltip("Joue sur l'AudioSource du GameManager")]
    [SerializeField] private AudioClip pickupSound;

    private PlayerLoadout loadout;
    private InteractBubble interactBubble;
    private AudioSource audioSource;
    private bool taken;

    private void Start()
    {
        loadout = FindObjectOfType<PlayerLoadout>();
        if (loadout == null)
            Debug.LogWarning("[BroomPedestal] PlayerLoadout introuvable");

        interactBubble = GetComponent<InteractBubble>();

        GameManager gameManager = FindObjectOfType<GameManager>();
        if (gameManager != null)
            audioSource = gameManager.GetComponent<AudioSource>();
        if (audioSource == null && pickupSound != null)
            Debug.LogWarning("[BroomPedestal] AudioSource introuvable sur GameManager");

        // L'effet ne doit se jouer qu'a la prise, meme s'il est laisse actif dans la scene
        if (pickupEffect != null) pickupEffect.SetActive(false);
    }

    private void Update()
    {
        if (taken || loadout == null || PlayerInputManager.Instance == null) return;

        float distance = Vector3.Distance(transform.position, loadout.transform.position);
        if (distance <= interactionRange && PlayerInputManager.Instance.InteractPressed)
            Take();
    }

    private void Take()
    {
        taken = true;
        loadout.ApplyLoadout(true, loadout.hasSpray);

        // Detache avant d'eteindre quoi que ce soit : rangee sous le balai, l'effet serait eteint avec lui
        if (pickupEffect != null)
        {
            pickupEffect.transform.SetParent(transform, true);
            pickupEffect.SetActive(true);
        }
        if (audioSource != null && pickupSound != null)
            audioSource.PlayOneShot(pickupSound);

        if (broomVisual != null) broomVisual.SetActive(false);
        foreach (GameObject go in hideOnTake)
            if (go != null) go.SetActive(false);
        if (interactBubble != null)
        {
            interactBubble.Hide();
            interactBubble.enabled = false;
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, interactionRange);
    }
}
