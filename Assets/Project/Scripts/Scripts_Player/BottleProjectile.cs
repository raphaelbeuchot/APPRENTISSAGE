using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class BottleProjectile : MonoBehaviour
{
    [Header("References")]
    public PlayerStats stats;
    public int savedAmmo;

    private Rigidbody rb;
    private bool hasHitEnemy = false;
    private AudioSource audioSource;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.spatialBlend = 0f;
        audioSource.playOnAwake = false;
    }
    void OnCollisionEnter(Collision collision)
    {
        Debug.Log("Bottle collision with: " + collision.gameObject.name + ", layer: " + LayerMask.LayerToName(collision.gameObject.layer));
        // Impact sur ennemi
        if (!hasHitEnemy && collision.gameObject.layer == LayerMask.NameToLayer("Zombie"))
        {
            hasHitEnemy = true;
            HitEnemy(collision.gameObject);
            // Son uniquement au hit ennemi
            if (audioSource != null && stats != null && stats.bottleImpactSound != null)
            {
                audioSource.PlayOneShot(stats.bottleImpactSound);
            }
        }
        // La bouteille reste au sol pour pickup
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
    }

    void HitEnemy(GameObject enemy)
    {
        // Degats
        EnemyHealth enemyHealth = enemy.GetComponent<EnemyHealth>();
        if (enemyHealth != null && stats != null)
        {
            enemyHealth.TakeMeleeDamage(EnemyHealth.AttackType.Bottle);
        }

        // Stun + Chase
        EnemyAI_AStar enemyAI_AStar = enemy.GetComponent<EnemyAI_AStar>();
        if (enemyAI_AStar != null)
        {
            StartCoroutine(StunAndChase_AStar(enemyAI_AStar));
        }
    }

    System.Collections.IEnumerator StunAndChase_AStar(EnemyAI_AStar enemyAI)
    {
        // Arreter mouvement
        enemyAI.canMove = false;
        Pathfinding.AIPath aiPath = enemyAI.GetAIPath();
        if (aiPath != null)
        {
            aiPath.canMove = false;
        }

        Debug.Log("Zombie stunned by bottle (A*)");

        // Stun duration
        yield return new WaitForSeconds(stats.bottleStunDuration);

        Debug.Log("Zombie waking up from stun (A*)");

        // Forcer la cible player
        PlayerPhysicsMovement player = FindObjectOfType<PlayerPhysicsMovement>();
        if (player != null)
        {
            enemyAI.targetHuman = player.transform;
            enemyAI.currentState = EnemyAI_AStar.State.Chasing;
            enemyAI.isForcedChase = true;
            Debug.Log("Target set to player, state = Chasing (A*)");
        }

        // Reactiver mouvement
        enemyAI.canMove = true;
        if (aiPath != null)
        {
            aiPath.canMove = true;
            if (player != null)
            {
                aiPath.destination = player.transform.position;
                Debug.Log("AIPath destination set to player position");
            }
        }

        Debug.Log(enemyAI.gameObject.name + " should be chasing now! (A*)");
    }

    
}