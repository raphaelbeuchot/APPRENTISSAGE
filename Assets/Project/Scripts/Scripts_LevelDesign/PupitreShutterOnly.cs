using UnityEngine;

public class PupitreShutterOnly : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private MetalShutterSafe metalShutter;

    [Header("Interaction")]
    [SerializeField] private float interactionRange = 1f;

    private Transform player;
    private bool hasActivated = false;

    private void Start()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
            player = playerObj.transform;
    }

    private void Update()
    {
        if (hasActivated || player == null) return;

        float distance = Vector3.Distance(transform.position, player.position);

        if (distance <= interactionRange && PlayerInputManager.Instance.InteractPressed)
        {
            Activate();
        }
    }

    private void Activate()
    {
        hasActivated = true;

        GetComponent<InteractBubble>()?.Hide();

        if (metalShutter != null)
            metalShutter.StartOpening();
        else
            Debug.LogWarning("[PupitreShutterOnly] Aucun MetalShutter assigné !");
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, interactionRange);
    }
}