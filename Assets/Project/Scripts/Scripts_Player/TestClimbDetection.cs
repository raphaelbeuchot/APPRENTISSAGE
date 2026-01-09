using UnityEngine;
using System.Collections;

public class TestClimbDetection : MonoBehaviour
{
    [SerializeField] private float detectionRadius = 1.5f;
    [SerializeField] private LayerMask obstacleLayer;
    [SerializeField] private float alignmentThreshold = 0.3f;
    [SerializeField] private PlayerPhysicsMovement playerMovement;
    [SerializeField] private float rotationDuration = 0.3f;
    [Header("UI")]
    public ClimbPromptUI climbPrompt;

    private bool isClimbing = false;

    void Update()
    {
        if (isClimbing)
        {
            if (climbPrompt != null)
                climbPrompt.Hide();
            return;
        }

        if (playerMovement.grabState != PlayerPhysicsMovement.GrabState.None)
        {
            if (climbPrompt != null)
                climbPrompt.Hide();
            return;
        }

        // Input direction
        Vector2 input = PlayerInputManager.Instance.MoveInput;
        if (input.magnitude < 0.1f)
        {
            if (climbPrompt != null)
                climbPrompt.Hide();
            return;
        }

        Vector3 inputDirection = transform.forward;
        inputDirection.y = 0f;
        inputDirection.Normalize();

        // Detection obstacle
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

        // Check si climb possible
        bool canClimb = (bestMatch != null && bestAlignment > alignmentThreshold);

        // Afficher/cacher prompt
        if (climbPrompt != null)
        {
            if (canClimb)
                climbPrompt.Show();
            else
                climbPrompt.Hide();
        }

        // Si pas possible, stop ici
        if (!canClimb)
            return;

        // Si E pas presse, stop ici
        if (!PlayerInputManager.Instance.InteractPressed)
            return;

        // === CLIMB DECLENCHE ===
        Debug.LogWarning("[Climb] E PRESSE - grabState = " + playerMovement.grabState);

        // Cacher prompt
        if (climbPrompt != null)
            climbPrompt.Hide();

        // Verification ClimbType
        if (bestMatch.climbType == null)
        {
            Debug.LogError("[CLIMB ERROR] " + bestMatch.name + " n'a pas de ClimbType SO assigne!");
            return;
        }

        float obstacleHeight = GetObstacleHeight(bestCollider);

        Vector3 toObstacleRay = (bestCollider.bounds.center - transform.position).normalized;
        toObstacleRay.y = 0f;
        toObstacleRay.Normalize();

        Vector3 rayOrigin = transform.position + Vector3.up * 0.5f;
        RaycastHit hitInfo;

        int humanLayer = LayerMask.NameToLayer("Human");
        int finalMask = obstacleLayer & ~(1 << humanLayer);

        Vector3 climbDirection = toObstacleRay;

        if (Physics.Raycast(rayOrigin, toObstacleRay, out hitInfo, detectionRadius, finalMask))
        {
            Debug.DrawRay(rayOrigin, toObstacleRay * hitInfo.distance, Color.green, 2f);
            climbDirection = -hitInfo.normal;
            climbDirection.y = 0f;
            climbDirection.Normalize();
            Debug.Log("[CLIMB] Normale trouvee: " + hitInfo.normal);

            if (bestMatch.onlyClimbFromLongSide)
            {
                BoxCollider boxCol = bestCollider as BoxCollider;
                if (boxCol != null)
                {
                    Vector3 size = boxCol.size;
                    bool xIsLonger = size.x > size.z;
                    Vector3 localNormal = bestCollider.transform.InverseTransformDirection(hitInfo.normal);
                    localNormal.y = 0f;

                    bool hittingShortSide = (xIsLonger && Mathf.Abs(localNormal.z) > Mathf.Abs(localNormal.x)) ||
                                            (!xIsLonger && Mathf.Abs(localNormal.x) > Mathf.Abs(localNormal.z));

                    if (hittingShortSide)
                    {
                        Debug.LogWarning("[CLIMB] Face courte detectee, CLIMB REFUSE (onlyClimbFromLongSide = true)");
                        return;
                    }

                    Debug.Log("[CLIMB] Face longue detectee, OK pour climb");
                }
            }
        }
        else
        {
            Debug.DrawRay(rayOrigin, toObstacleRay * detectionRadius, Color.red, 2f);
            Debug.LogWarning("[CLIMB] Raycast rate, CLIMB ANNULE");

            isClimbing = false;
            playerMovement.isClimbing = false;
            playerMovement.canMove = true;
            playerMovement.enabled = true;
            return;
        }

        float distance = bestMatch.climbType.moveDistanceForward;
        float height = obstacleHeight;

        isClimbing = true;
        playerMovement.isClimbing = true;
        playerMovement.canMove = false;
        playerMovement.ResetAllInputs();
        playerMovement.enabled = false;

        if (playerMovement.IsCrouching())
        {
            playerMovement.ExitCrouch();
        }

        StartCoroutine(RotateAndClimbCoroutine(climbDirection, transform.position, height, distance, bestMatch.climbType));
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
        Debug.Log("[CLIMB] Mouvement reactive");
    }

    private IEnumerator RotateAndClimbCoroutine(Vector3 targetDirection, Vector3 startPos, float height, float distance, ClimbType climbType)
    {
        Quaternion startRotation = transform.rotation;
        Quaternion targetRotation = Quaternion.LookRotation(targetDirection);
        float elapsed = 0f;

        Debug.Log("[CLIMB] Debut rotation vers: " + targetDirection);

        while (elapsed < rotationDuration)
        {
            if (playerMovement.grabState != PlayerPhysicsMovement.GrabState.None)
            {
                Debug.LogWarning("[CLIMB] ANNULE - Player grabbed pendant la rotation!");

                isClimbing = false;
                playerMovement.isClimbing = false;
                playerMovement.enabled = true;
                playerMovement.canMove = true;
                yield break;
            }

            elapsed += Time.deltaTime;
            float t = elapsed / rotationDuration;
            transform.rotation = Quaternion.Slerp(startRotation, targetRotation, t);
            yield return null;
        }

        transform.rotation = targetRotation;
        Debug.Log("[CLIMB] Rotation terminee!");

        yield return StartCoroutine(ClimbCoroutine(startPos, height, distance, climbType));
    }

    private IEnumerator ClimbCoroutine(Vector3 startPos, float height, float distance, ClimbType climbType)
    {
        Rigidbody rb = GetComponent<Rigidbody>();
        bool isVault = climbType.isVault;
        float startY = transform.position.y;
        startPos = transform.position;

        playerMovement.isImmuneToGrab = true;

        int playerLayer = gameObject.layer;
        int zombieLayer = LayerMask.NameToLayer("Zombie");
        if (isVault)
        {
            Physics.IgnoreLayerCollision(playerLayer, zombieLayer, true);
            Debug.Log("[CLIMB] Vault detecte - collisions zombies desactivees");
        }

        RigidbodyConstraints oldConstraints = rb.constraints;
        rb.constraints = RigidbodyConstraints.FreezeRotation;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

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

        if (isVault)
        {
            Debug.Log("[CLIMB] VAULT detecte, attente descente...");

            float descenteTimer = 0f;
            float maxDescenteTime = 3f;

            while (Mathf.Abs(transform.position.y - startY) >= 0.2f && descenteTimer < maxDescenteTime)
            {
                descenteTimer += Time.deltaTime;
                yield return null;
            }

            if (descenteTimer >= maxDescenteTime)
            {
                Debug.LogWarning("[CLIMB] Timeout descente vault - forcage au sol");
            }

            Debug.Log("[CLIMB] Descente terminee!");
        }

        if (isVault)
        {
            Physics.IgnoreLayerCollision(playerLayer, zombieLayer, false);
            Debug.Log("[CLIMB] Collisions zombies reactivees");
        }

        rb.constraints = oldConstraints;
        playerMovement.enabled = true;
        playerMovement.isClimbing = false;

        playerMovement.isImmuneToGrab = false;

        ReEnableMovement();

        yield return new WaitUntil(() => !PlayerInputManager.Instance.InteractPressed);

        isClimbing = false;
        Debug.Log("[CLIMB] Climb termine, E relache, pret pour prochain climb");
    }

    public bool IsClimbing()
    {
        return isClimbing;
    }

    public void CancelClimb()
    {
        if (!isClimbing)
            return;

        Debug.LogWarning("[CLIMB] CLIMB ANNULE par tir sentinelle!");

        StopAllCoroutines();

        Rigidbody rb = GetComponent<Rigidbody>();
        rb.constraints = RigidbodyConstraints.FreezeRotation;

        playerMovement.enabled = true;
        playerMovement.isClimbing = false;
        playerMovement.canMove = true;

        isClimbing = false;
    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
    }
}