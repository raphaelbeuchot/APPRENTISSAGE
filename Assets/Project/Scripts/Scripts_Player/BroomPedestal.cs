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

    private PlayerLoadout loadout;
    private InteractBubble interactBubble;
    private bool taken;

    private void Start()
    {
        loadout = FindObjectOfType<PlayerLoadout>();
        if (loadout == null)
            Debug.LogWarning("[BroomPedestal] PlayerLoadout introuvable");

        interactBubble = GetComponent<InteractBubble>();
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

        if (broomVisual != null) broomVisual.SetActive(false);
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
