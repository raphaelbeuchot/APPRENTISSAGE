using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class EnemyStepClimb : MonoBehaviour
{
    [Header("Step Climbing")]
    [SerializeField] private float maxStepHeight = 0.3f;
    [SerializeField] private float stepCheckDistance = 0.3f;
    [SerializeField] private float stepRayHeightOffset = 0.05f;

    private Rigidbody rb;
    private EnemyAI_AStar enemyAI;
    private EnemyHealth enemyHealth; // AJOUTER

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        enemyAI = GetComponent<EnemyAI_AStar>();
        enemyHealth = GetComponent<EnemyHealth>(); // AJOUTER
    }

    void FixedUpdate()
    {
        HandleStepClimb();
    }

    void HandleStepClimb()
    {
        if (enemyAI != null && (enemyAI.isDead || enemyAI.isInPitMode))
            return;

        Vector3 horizontalVel = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);

        if (horizontalVel.magnitude < 0.05f)
            return;

        Vector3 moveDirection = horizontalVel.normalized;

        Vector3 rayStart = transform.position + Vector3.up * stepRayHeightOffset;
        RaycastHit hitLower;

        if (Physics.Raycast(rayStart, moveDirection, out hitLower, stepCheckDistance, LayerMask.GetMask("Ground", "Obstacle")))
        {
            Vector3 upperRayStart = transform.position + moveDirection * stepCheckDistance + Vector3.up * maxStepHeight;
            RaycastHit hitUpper;

            if (Physics.Raycast(upperRayStart, Vector3.down, out hitUpper, maxStepHeight, LayerMask.GetMask("Ground", "Obstacle")))
            {
                float stepHeight = hitUpper.point.y - transform.position.y;

                if (stepHeight > 0.03f && stepHeight <= maxStepHeight)
                {
                    Vector3 newVel = rb.linearVelocity;
                    newVel.y = stepHeight * 20f;
                    rb.linearVelocity = newVel;
                }
            }
        }
    }
}