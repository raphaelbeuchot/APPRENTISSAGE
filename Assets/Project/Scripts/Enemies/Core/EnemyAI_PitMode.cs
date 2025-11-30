using UnityEngine;

public class EnemyAI_PitMode : MonoBehaviour
{
    [Header("Pit Mode Settings")]
    public float moveSpeed = 0.6f;
    public float rotationSpeed = 120f;

    private Transform player;
    private Rigidbody rb;
    private EnemyHealth health;

    void OnEnable()
    {
        // Trouver le player
        PlayerHealth playerHealth = FindObjectOfType<PlayerHealth>();
        if (playerHealth != null)
        {
            player = playerHealth.transform;
        }

        rb = GetComponent<Rigidbody>();
        health = GetComponent<EnemyHealth>();

        Debug.Log($"[PitMode] {gameObject.name} activated PitMode");
    }

    void Update()
    {
        // Check si mort
        if (health != null && health.IsDead())
        {
            return;
        }

        // Check si player existe
        if (player == null)
        {
            PlayerHealth playerHealth = FindObjectOfType<PlayerHealth>();
            if (playerHealth != null)
            {
                player = playerHealth.transform;
            }
            else
            {
                return;
            }
        }

        // Rotation vers player
        Vector3 directionToPlayer = (player.position - transform.position);
        directionToPlayer.y = 0;
        directionToPlayer.Normalize();

        if (directionToPlayer.magnitude > 0.1f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(directionToPlayer);
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRotation,
                Time.deltaTime * rotationSpeed
            );
        }

        // Mouvement vers avant
        Vector3 moveDirection = transform.forward * moveSpeed;
        rb.linearVelocity = new Vector3(moveDirection.x, rb.linearVelocity.y, moveDirection.z);
    }

    void OnDisable()
    {
        // Reset velocity
        if (rb != null)
        {
            rb.linearVelocity = new Vector3(0, rb.linearVelocity.y, 0);
        }
    }
}