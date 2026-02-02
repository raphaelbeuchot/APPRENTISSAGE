using UnityEngine;
using System.Collections;

public class TestClimbDetection : MonoBehaviour
{
    [SerializeField] private float detectionRadius = 0.45f;
    [SerializeField] private LayerMask obstacleLayer;
    [SerializeField] private float alignmentThreshold = 0f;
    [SerializeField] private PlayerPhysicsMovement playerMovement;
    [SerializeField] private float rotationDuration = 0.5f;
    [Header("UI")]
    public ClimbPromptUI climbPrompt;
    [Header("Input Buffer")]
    [SerializeField] private float inputBufferDuration = 0.7f;
    private Vector3 bufferedInputDirection;
    private float lastInputTime = -999f;
    [Header("Animation")]
    [SerializeField] private Animator animator;

    private bool isClimbing = false;

    void Start()
    {
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }
    }

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

        // NOUVEAU : Empecher climb en chute
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb.linearVelocity.y < -0.05f) // Chute significative
        {
            if (climbPrompt != null)
                climbPrompt.Hide();
            return;
        }


        // 1. RECUPERER INPUT ET CALCULER DIRECTION
        Vector2 input = PlayerInputManager.Instance.MoveInput;

        // Check si on a une direction bufferee valide
        bool hasValidBuffer = (Time.time - lastInputTime) <= inputBufferDuration;

        // Si ni input actuel ni buffer valide, on sort
        if (input.magnitude < 0.1f && !hasValidBuffer)
        {
            if (climbPrompt != null)
                climbPrompt.Hide();
            return;
        }

        // Utiliser soit l'input actuel soit le buffer
        Vector3 inputDirection;
        if (input.magnitude > 0.1f)
        {
            // Calculer vraie direction basée sur input + caméra
            Vector3 camForward = Camera.main.transform.forward;
            Vector3 camRight = Camera.main.transform.right;
            camForward.y = 0f;
            camRight.y = 0f;
            camForward.Normalize();
            camRight.Normalize();

            inputDirection = (camForward * input.y + camRight * input.x).normalized;
        }
        else
        {
            inputDirection = bufferedInputDirection;
        }

        // 2. DETECTION OBSTACLE
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

        // 3. CHECK SI CLIMB POSSIBLE
        bool canClimb = (bestMatch != null && bestAlignment > alignmentThreshold);

        // 4. MEMORISER DANS BUFFER SI OBSTACLE DETECTE ET INPUT ACTUEL VALIDE
        if (canClimb && input.magnitude > 0.1f)
        {
            bufferedInputDirection = inputDirection;
            lastInputTime = Time.time;
        }

        // 5. AFFICHER/CACHER PROMPT
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

        // Recule le point de depart pour eviter de commencer dans le collider
        Vector3 rayOrigin = transform.position + Vector3.up * 0.5f - toObstacleRay * (detectionRadius * 0.5f); RaycastHit hitInfo;

        int humanLayer = LayerMask.NameToLayer("Human");
        int finalMask = obstacleLayer & ~(1 << humanLayer);

        Vector3 climbDirection = toObstacleRay;

        // SphereCast au lieu de Raycast
        float castDistance = detectionRadius * 1.5f;
        float castRadius = 0.15f; // Hardcodé
        if (Physics.SphereCast(rayOrigin, castRadius, toObstacleRay, out hitInfo, castDistance, finalMask))
        {
            // NOUVEAU : Vérifier que c'est bien le BON obstacle
            if (hitInfo.collider != bestCollider)
            {
                Debug.LogWarning($"[CLIMB] SphereCast a touche {hitInfo.collider.name} au lieu de {bestCollider.name} - CLIMB ANNULE");
                Debug.DrawRay(rayOrigin, toObstacleRay * hitInfo.distance, Color.yellow, 2f);

                isClimbing = false;
                playerMovement.isClimbing = false;
                playerMovement.canMove = true;
                playerMovement.enabled = true;
                return;
            }

            Debug.DrawRay(rayOrigin, toObstacleRay * hitInfo.distance, Color.green, 2f);
            climbDirection = -hitInfo.normal;
            climbDirection.y = 0f;
            climbDirection.Normalize();
            Debug.Log("[CLIMB] Normale trouvee: " + hitInfo.normal);
        }
        else
        {
            Debug.DrawRay(rayOrigin, toObstacleRay * castDistance, Color.red, 2f);
            Debug.LogWarning("[CLIMB] SphereCast rate, CLIMB ANNULE");

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

        // Déclencher l'animation de climb
        if (animator != null)
        {
            animator.SetBool("DoClimb", true);
            Debug.Log("[CLIMB] Animation DoClimb déclenchée");
        }

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

        // Arrêter l'animation de climb
        if (animator != null)
        {
            animator.SetBool("DoClimb", false);
            Debug.Log("[CLIMB] Animation DoClimb arrêtée");
        }

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
            // NOUVEAU : Simuler vitesse pour animation walk
            if (animator != null)
            {
                animator.SetFloat("SpeedZ", 0.5f); // Vitesse walk normale
            }
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

        // Arrêter l'animation si climb annulé
        if (animator != null)
        {
            animator.SetBool("DoClimb", false);
        }

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