using UnityEngine;
using System.Collections;

public class TestClimbDetection : MonoBehaviour
{
    [SerializeField] private float detectionRadius = 1.5f;
    [SerializeField] private LayerMask obstacleLayer;
    [SerializeField] private float alignmentThreshold = 0.3f;
    [SerializeField] private PlayerPhysicsMovement playerMovement;
    [SerializeField] private float vaultDepthThreshold = 0.6f;

    private bool isClimbing = false;

    void Update()
    {
        if (isClimbing)
            return;

        if (!PlayerInputManager.Instance.InteractPressed)
            return;

        Vector2 input = PlayerInputManager.Instance.MoveInput;
        if (input.magnitude < 0.1f)
            return;

        Vector3 inputDirection = transform.forward;
        inputDirection.y = 0f;
        inputDirection.Normalize();

        Collider[] hits = Physics.OverlapSphere(transform.position, detectionRadius, obstacleLayer);

        ClimbableObject bestMatch = null;
        Collider bestCollider = null;
        float bestAlignment = -1f;

        foreach (Collider hit in hits)
        {
            ClimbableObject climbable = hit.GetComponent<ClimbableObject>();
            if (climbable == null)
                continue;

            Vector3 toObstacle = (hit.bounds.center - transform.position).normalized;
            toObstacle.y = 0f;

            float alignment = Vector3.Dot(inputDirection, toObstacle);

            if (alignment > bestAlignment)
            {
                bestAlignment = alignment;
                bestMatch = climbable;
                bestCollider = hit;
            }
        }

        if (bestMatch != null && bestAlignment > alignmentThreshold)
        {
            if (bestMatch.climbType == null)
            {
                Debug.LogError("[CLIMB ERROR] " + bestMatch.name + " n'a pas de ClimbType SO assigné!");
                return;
            }

            float obstacleHeight = GetObstacleHeight(bestCollider);
            float obstacleDepth = bestMatch.climbType.obstacleDepth;

            // PIVOTER LE PLAYER PERPENDICULAIREMENT À LA FACE
Vector3 rayOrigin = transform.position + Vector3.up * 0.5f;
RaycastHit hitInfo;

if (Physics.Raycast(rayOrigin, inputDirection, out hitInfo, detectionRadius, obstacleLayer) && hitInfo.collider == bestCollider)
{
    Vector3 climbDirection = -hitInfo.normal;
    climbDirection.y = 0f;
    climbDirection.Normalize();
    transform.rotation = Quaternion.LookRotation(climbDirection);
    Debug.Log("[CLIMB] Player pivoté, normale: " + hitInfo.normal);
}
else
{
    Debug.LogWarning("[CLIMB] Raycast raté");
}

            // VAULT / PLATFORM distance
            float distance = (obstacleDepth < vaultDepthThreshold)
                ? obstacleDepth + 0.5f
                : 0.3f;

            float height = obstacleHeight;

            playerMovement.isClimbing = true;
            isClimbing = true;
            playerMovement.canMove = false;
            playerMovement.ForceStop();

            StartCoroutine(ClimbCoroutine(transform.position, height, distance, bestMatch.climbType));
        }
        else if (bestMatch != null)
        {
            Debug.Log("[CLIMB REFUSE] " + bestMatch.name + " - alignment: " + bestAlignment.ToString("F2"));
        }
    }

    private float GetObstacleHeight(Collider obstacleCollider)
    {
        float groundLevel = transform.position.y;
        float obstacleTop = obstacleCollider.bounds.max.y;
        return obstacleTop - groundLevel;
    }

    void ReEnableMovement()
    {
        playerMovement.canMove = true;
        Debug.Log("[CLIMB] Mouvement réactivé");
    }

    private IEnumerator ClimbCoroutine(Vector3 startPos, float height, float distance, ClimbType climbType)
    {
        Rigidbody rb = GetComponent<Rigidbody>();
        bool isVault = climbType.obstacleDepth < vaultDepthThreshold;
        float startY = transform.position.y;

        startPos = transform.position;

        playerMovement.enabled = false;

        RigidbodyConstraints oldConstraints = rb.constraints;
        rb.constraints = RigidbodyConstraints.FreezeRotation;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        // PHASE 1 : MONTEE VERTICALE
        Vector3 topPos = startPos + Vector3.up * height;
        float phase1Duration = climbType.phase1Duration;
        float elapsed = 0f;

        while (elapsed < phase1Duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / phase1Duration;
            Vector3 newPos = Vector3.Lerp(startPos, topPos, t);
            rb.MovePosition(newPos);
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            yield return new WaitForFixedUpdate();
        }
        rb.MovePosition(topPos);
        rb.linearVelocity = Vector3.zero;

        // PHASE 2 : AVANCEE HORIZONTALE
        Vector3 finalPos = topPos + transform.forward * distance;
        float phase2Duration = climbType.phase2Duration;
        elapsed = 0f;

        while (elapsed < phase2Duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / phase2Duration;
            Vector3 newPos = Vector3.Lerp(topPos, finalPos, t);
            rb.MovePosition(newPos);
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            yield return new WaitForFixedUpdate();
        }
        rb.MovePosition(finalPos);
        rb.linearVelocity = Vector3.zero;

        // Si VAULT : attendre la descente
        if (isVault)
        {
            Debug.Log("[CLIMB] VAULT détecté, attente descente...");
            yield return new WaitUntil(() => Mathf.Abs(transform.position.y - startY) < 0.2f);
            Debug.Log("[CLIMB] Descente terminée!");
        }

        // RESTAURER TOUT
        rb.constraints = oldConstraints;
        playerMovement.enabled = true;
        playerMovement.isClimbing = false;

        ReEnableMovement();

        // Attendre que E soit relâché
        yield return new WaitUntil(() => !PlayerInputManager.Instance.InteractPressed);

        isClimbing = false;
        Debug.Log("[CLIMB] Climb terminé, E relâché, prêt pour prochain climb");
    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
    }
}