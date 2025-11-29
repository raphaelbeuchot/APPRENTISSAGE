using UnityEngine;

public class TestClimbDetection : MonoBehaviour
{
    [SerializeField] private float detectionRadius = 1.5f;
    [SerializeField] private LayerMask obstacleLayer;
    [SerializeField] private float alignmentThreshold = 0.3f;
    [SerializeField] private PlayerPhysicsMovement playerMovement;

    private bool isClimbing = false;

    void Update()
    {
        // Si en train de climb, attendre que E soit relache
        if (isClimbing)
        {
            if (!PlayerInputManager.Instance.InteractPressed)
            {
                isClimbing = false;
                Debug.Log("[CLIMB] E relache, pret pour prochain climb");
            }
            return;
        }

        if (!PlayerInputManager.Instance.InteractPressed)
        {
            return;
        }

        Vector2 input = PlayerInputManager.Instance.MoveInput;

        if (input.magnitude < 0.1f)
        {
            return;
        }

        Vector3 inputDirection = transform.forward;
        inputDirection.y = 0f;
        inputDirection.Normalize();

        Collider[] hits = Physics.OverlapSphere(transform.position, detectionRadius, obstacleLayer);

        ClimbableObject bestMatch = null;
        float bestAlignment = -1f;

        foreach (Collider hit in hits)
        {
            ClimbableObject climbable = hit.GetComponent<ClimbableObject>();
            if (climbable == null) continue;

            Vector3 toObstacle = (hit.transform.position - transform.position).normalized;
            toObstacle.y = 0f;
            float alignment = Vector3.Dot(inputDirection, toObstacle);

            if (alignment > bestAlignment)
            {
                bestAlignment = alignment;
                bestMatch = climbable;
            }
        }

        if (bestMatch != null && bestAlignment > alignmentThreshold)
        {
            Debug.Log("[CLIMB OK] " + bestMatch.name + " - TELEPORTATION");

            playerMovement.canMove = false;
            playerMovement.ForceStop();

            Vector3 newPosition = transform.position + transform.forward * 1f + Vector3.up * 1f;
            transform.position = newPosition;

            isClimbing = true;
            Invoke("ReEnableMovement", 0.5f);
        }
        else if (bestMatch != null)
        {
            Debug.Log("[CLIMB REFUSE] " + bestMatch.name + " - alignment: " + bestAlignment.ToString("F2") + " (seuil: " + alignmentThreshold + ")");
        }
        else
        {
            Debug.Log("[CLIMB] Aucun obstacle climbable detecte");
        }
    }

    void ReEnableMovement()
    {
        playerMovement.canMove = true;
        Debug.Log("[CLIMB] Mouvement reactive (attends release E)");
    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
    }
}