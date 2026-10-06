using UnityEngine;

public class TomatoCrate : MonoBehaviour
{
    [Header("Interaction")]
    [SerializeField] private float interactionRange = 2f;

    // Tous les joueurs de la scene (un seul en solo, un par joueur en multi).
    private TomatoThrowSystem[] throwSystems;
    private InteractBubble interactBubble;

    private void Start()
    {
        throwSystems = FindObjectsOfType<TomatoThrowSystem>();
        if (throwSystems.Length == 0)
            Debug.LogWarning("[TomatoCrate] Player introuvable");

        interactBubble = GetComponent<InteractBubble>();
    }

    private void Update()
    {
        foreach (TomatoThrowSystem throwSystem in throwSystems)
        {
            if (throwSystem == null) continue;

            float distance = Vector3.Distance(transform.position, throwSystem.transform.position);
            bool inRange = distance <= interactionRange;

            // Chaque joueur fait le plein avec son propre input (PlayerLocalInput en multi, singleton en solo).
            if (inRange && throwSystem.InputInteractPressed)
                throwSystem.Refill();
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, interactionRange);
    }
}
