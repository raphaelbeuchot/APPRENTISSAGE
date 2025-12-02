using UnityEngine;
using System.Collections;
public class TestClimbDetection : MonoBehaviour
{
    [SerializeField] private float detectionRadius = 1.5f;
    [SerializeField] private LayerMask obstacleLayer;
    [SerializeField] private float alignmentThreshold = 0.3f;
    [SerializeField] private PlayerPhysicsMovement playerMovement;
    [SerializeField] private float vaultDepthThreshold = 0.6f; // Seuil Vault vs Platform


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
        Collider bestCollider = null; // AJOUT: garder reference au collider
        float bestAlignment = -1f;

        foreach (Collider hit in hits)
        {
            ClimbableObject climbable = hit.GetComponent<ClimbableObject>();
            if (climbable == null) continue;

            Vector3 toObstacle = (hit.bounds.center - transform.position).normalized;
            toObstacle.y = 0f;

            float alignment = Vector3.Dot(inputDirection, toObstacle);

            if (alignment > bestAlignment)
            {
                bestAlignment = alignment;
                bestMatch = climbable;
                bestCollider = hit; // AJOUT: sauvegarder le collider
            }
        }

        if (bestMatch != null && bestAlignment > alignmentThreshold)
        {
            if (bestMatch.climbType == null)
            {
                Debug.LogError("[CLIMB ERROR] " + bestMatch.name + " n'a pas de ClimbType SO assigne!");
                return;
            }

            // AJOUT: Calcul hauteur reelle
            float obstacleHeight = GetObstacleHeight(bestCollider);
            Debug.Log("[CLIMB DETECTION] " + bestMatch.name + " - Hauteur reelle: " + obstacleHeight.ToString("F2") + "m");

            // AJOUT: Lecture profondeur depuis SO
            float obstacleDepth = bestMatch.climbType.obstacleDepth;
            Debug.Log("[CLIMB DETECTION] Profondeur obstacle (SO): " + obstacleDepth.ToString("F2") + "m");

            // AJOUT: Determiner type franchissement
            string climbCategory = (obstacleDepth < vaultDepthThreshold) ? "VAULT" : "PLATFORM";
            Debug.Log("[CLIMB TYPE] " + climbCategory + " (seuil: " + vaultDepthThreshold + "m)");

            playerMovement.canMove = false;
            playerMovement.ForceStop();


            // Calculer distance selon type
            float distance;
            if (obstacleDepth < vaultDepthThreshold)
            {
                // VAULT : passer de l'autre cote (profondeur + marge)
                distance = obstacleDepth + 0.5f;
                Debug.Log("[CLIMB CALCUL] VAULT - Distance: profondeur (" + obstacleDepth.ToString("F2") + ") + 0.5m = " + distance.ToString("F2") + "m");
            }
            else
            {
                // PLATFORM : monter dessus (distance courte fixe)
                distance = 0.3f;
                Debug.Log("[CLIMB CALCUL] PLATFORM - Distance fixe: " + distance.ToString("F2") + "m");
            }

            // Hauteur = exactement le sommet de l'obstacle (pose fesses dessus)
            float height = obstacleHeight;
            Debug.Log("[CLIMB CALCUL] Hauteur cible: " + height.ToString("F2") + "m (sommet obstacle)");

            Debug.Log("[CLIMB CALCUL] Hauteur obstacle: " + obstacleHeight.ToString("F2") + "m -> Hauteur climb: " + height.ToString("F2") + "m");

            Vector3 startPos = transform.position;
            Vector3 targetPos = transform.position + transform.forward * distance + Vector3.up * height;

            // Lancer coroutine animation
            StartCoroutine(ClimbCoroutine(transform.position, height, distance, bestMatch.climbType));

            isClimbing = true;
        }
        else if (bestMatch != null)
        {
            Debug.Log("[CLIMB REFUSE] " + bestMatch.name + " - alignment: " + bestAlignment.ToString("F2") + " (seuil: " + alignmentThreshold + ") <<< TROP FAIBLE");
        }
    }

    private float GetObstacleHeight(Collider obstacleCollider)
    {
        // Position du sol (Y du player)
        float groundLevel = transform.position.y;

        // Hauteur maximale de l'obstacle
        float obstacleTop = obstacleCollider.bounds.max.y;

        // Hauteur reelle = difference
        float height = obstacleTop - groundLevel;

        return height;
    }

    void ReEnableMovement()
    {
        playerMovement.canMove = true;
        Debug.Log("[CLIMB] Mouvement reactive (attends release E)");
    }

    private IEnumerator ClimbCoroutine(Vector3 startPos, float height, float distance, ClimbType climbType)
    {
        Rigidbody rb = GetComponent<Rigidbody>();

        // DESACTIVER PlayerPhysicsMovement completement
        playerMovement.enabled = false;

        // FREEZE RIGIDBODY COMPLET
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

        // RESTAURER TOUT
        rb.constraints = oldConstraints;
        playerMovement.enabled = true;

        ReEnableMovement();
    }
    void OnDrawGizmos()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
    }
}