using UnityEngine;
using System.Collections;

public class TestClimbDetection : MonoBehaviour
{
    [SerializeField] private float detectionRadius = 1.5f;
    [SerializeField] private LayerMask obstacleLayer;
    [SerializeField] private float alignmentThreshold = 0.3f;
    [SerializeField] private PlayerPhysicsMovement playerMovement;
    [SerializeField] private float vaultDepthThreshold = 0.6f;
    [SerializeField] private float rotationDuration = 0.3f;

    private bool isClimbing = false;

    void Update()
    {
        if (isClimbing)
            return;

        // === BLOQUER CLIMB SI GRABBED ===
        if (playerMovement.grabState != PlayerPhysicsMovement.GrabState.None)
        {
            Debug.LogWarning($"[Climb] BLOQUÉ - grabState = {playerMovement.grabState}");
            return;
        }

        if (!PlayerInputManager.Instance.InteractPressed)
            return;

        Debug.LogWarning("[Climb] E PRESSÉ - grabState = " + playerMovement.grabState);

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
                Debug.LogError("[CLIMB ERROR] " + bestMatch.name + " n'a pas de ClimbType SO assigne!");
                return;
            }

            float obstacleHeight = GetObstacleHeight(bestCollider);
            float obstacleDepth = bestMatch.climbType.obstacleDepth;

            // Calculer direction vers obstacle
            Vector3 toObstacle = (bestCollider.bounds.center - transform.position).normalized;
            toObstacle.y = 0f;
            toObstacle.Normalize();

            // RAYCAST POUR OBTENIR LA NORMALE DE LA FACE
            Vector3 rayOrigin = transform.position + Vector3.up * 0.5f;
            RaycastHit hitInfo;

            int humanLayer = LayerMask.NameToLayer("Human");
            int finalMask = obstacleLayer & ~(1 << humanLayer);

            Vector3 climbDirection = toObstacle;

            if (Physics.Raycast(rayOrigin, toObstacle, out hitInfo, detectionRadius, finalMask))
            {
                Debug.DrawRay(rayOrigin, toObstacle * hitInfo.distance, Color.green, 2f);
                climbDirection = -hitInfo.normal;
                climbDirection.y = 0f;
                climbDirection.Normalize();
                Debug.Log("[CLIMB] Normale trouvee: " + hitInfo.normal);

                Debug.Log("[CLIMB] Normale trouvee: " + hitInfo.normal);

                // VERIFICATION FACE SI RESTRICTION
                if (bestMatch.onlyClimbFromLongSide)
                {
                    BoxCollider boxCol = bestCollider as BoxCollider;
                    if (boxCol != null)
                    {
                        Vector3 size = boxCol.size;

                        // Déterminer axe long/court (ignorant Y)
                        bool xIsLonger = size.x > size.z;

                        // Transformer la normale dans l'espace local de l'obstacle
                        Vector3 localNormal = bestCollider.transform.InverseTransformDirection(hitInfo.normal);
                        localNormal.y = 0f;

                        // Vérifier si on tape la face courte
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
                Debug.DrawRay(rayOrigin, toObstacle * detectionRadius, Color.red, 2f);
                Debug.LogWarning("[CLIMB] Raycast rate, CLIMB ANNULE");

                // RESTAURER TOUT
                isClimbing = false;
                playerMovement.isClimbing = false;
                playerMovement.canMove = true;
                playerMovement.enabled = true;
                return;
            }

            // VAULT / PLATFORM distance
            float distance = (obstacleDepth < vaultDepthThreshold)
                ? obstacleDepth + 0.5f
                : 0.3f;

            float height = obstacleHeight;

            // TOUT DEBRANCHER IMMEDIATEMENT
            isClimbing = true;
            playerMovement.isClimbing = true;
            playerMovement.canMove = false;
            playerMovement.ResetAllInputs();
            playerMovement.enabled = false;

            // ANNULER CROUCH SI ACTIF
            if (playerMovement.IsCrouching())
            {
                playerMovement.ExitCrouch();
            }

            playerMovement.enabled = false;

            // LANCER ROTATION PUIS CLIMB
            StartCoroutine(RotateAndClimbCoroutine(climbDirection, transform.position, height, distance, bestMatch.climbType));
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
        Debug.Log("[CLIMB] Mouvement reactive");
    }

    private IEnumerator RotateAndClimbCoroutine(Vector3 targetDirection, Vector3 startPos, float height, float distance, ClimbType climbType)
    {
        // PHASE ROTATION
        Quaternion startRotation = transform.rotation;
        Quaternion targetRotation = Quaternion.LookRotation(targetDirection);
        float elapsed = 0f;

        Debug.Log("[CLIMB] Debut rotation vers: " + targetDirection);

        while (elapsed < rotationDuration)
        {
            // === CHECK SI GRABBED PENDANT LA ROTATION ===
            if (playerMovement.grabState != PlayerPhysicsMovement.GrabState.None)
            {
                Debug.LogWarning("[CLIMB] ANNULÉ - Player grabbed pendant la rotation!");

                // Annuler tout
                isClimbing = false;
                playerMovement.isClimbing = false;
                playerMovement.enabled = true;
                playerMovement.canMove = true;
                yield break; // SORTIR de la coroutine
            }

            elapsed += Time.deltaTime;
            float t = elapsed / rotationDuration;
            transform.rotation = Quaternion.Slerp(startRotation, targetRotation, t);
            yield return null;
        }

        transform.rotation = targetRotation;
        Debug.Log("[CLIMB] Rotation terminee!");

        // MAINTENANT LANCER LE CLIMB
        yield return StartCoroutine(ClimbCoroutine(startPos, height, distance, climbType));
    }

    private IEnumerator ClimbCoroutine(Vector3 startPos, float height, float distance, ClimbType climbType)
    {
        Rigidbody rb = GetComponent<Rigidbody>();
        bool isVault = climbType.obstacleDepth < vaultDepthThreshold;
        float startY = transform.position.y;
        startPos = transform.position;

        // === BLOQUER GRABS PENDANT TOUT LE CLIMB ===
        playerMovement.isImmuneToGrab = true;

        // === DÉSACTIVER COLLISIONS ZOMBIES SI VAULT ===
        int playerLayer = gameObject.layer;
        int zombieLayer = LayerMask.NameToLayer("Zombie");
        if (isVault)
        {
            Physics.IgnoreLayerCollision(playerLayer, zombieLayer, true);
            Debug.Log("[CLIMB] Vault détecté - collisions zombies désactivées");
        }

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
            Debug.Log("[CLIMB] VAULT detecte, attente descente...");

            // Timeout de 3 secondes au cas où
            float descenteTimer = 0f;
            float maxDescenteTime = 3f;

            while (Mathf.Abs(transform.position.y - startY) >= 0.2f && descenteTimer < maxDescenteTime)
            {
                descenteTimer += Time.deltaTime;
                yield return null;
            }

            if (descenteTimer >= maxDescenteTime)
            {
                Debug.LogWarning("[CLIMB] Timeout descente vault - forçage au sol");
            }

            Debug.Log("[CLIMB] Descente terminee!");
        }

        // === RÉACTIVER COLLISIONS ZOMBIES ===
        if (isVault)
        {
            Physics.IgnoreLayerCollision(playerLayer, zombieLayer, false);
            Debug.Log("[CLIMB] Collisions zombies réactivées");
        }

        // RESTAURER TOUT
        rb.constraints = oldConstraints;
        playerMovement.enabled = true;
        playerMovement.isClimbing = false;

        // === RETIRER IMMUNITÉ GRABS ===
        playerMovement.isImmuneToGrab = false;

        ReEnableMovement();

        // Attendre que E soit relache
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