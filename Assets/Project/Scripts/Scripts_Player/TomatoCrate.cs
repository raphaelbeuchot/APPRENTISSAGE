using UnityEngine;

public class TomatoCrate : MonoBehaviour
{
    [Header("Interaction")]
    [SerializeField] private float interactionRange = 2f;

    private Transform playerTransform;
    private TomatoThrowSystem throwSystem;
    private InteractBubble interactBubble;

    private void Start()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            playerTransform = playerObj.transform;
            throwSystem = playerObj.GetComponent<TomatoThrowSystem>();
        }
        else
        {
            Debug.LogWarning("[TomatoCrate] Player introuvable");
        }

        interactBubble = GetComponent<InteractBubble>();
    }

    private void Update()
    {
        if (playerTransform == null) return;

        float distance = Vector3.Distance(transform.position, playerTransform.position);
        bool inRange = distance <= interactionRange;

        if (inRange && PlayerInputManager.Instance.InteractPressed)
        {
            if (throwSystem != null)
                throwSystem.Refill();
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, interactionRange);
    }
}