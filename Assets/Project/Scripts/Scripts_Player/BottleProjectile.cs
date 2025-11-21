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
    }

    void OnCollisionEnter(Collision collision)
    {
        Debug.Log("Bottle collision with: " + collision.gameObject.name + ", layer: " + LayerMask.LayerToName(collision.gameObject.layer));
        
        // Impact sur ennemi
        if (!hasHitEnemy && collision.gameObject.layer == LayerMask.NameToLayer("Zombie"))
        {
            hasHitEnemy = true;
            HitEnemy(collision.gameObject);
        }

        // Son impact
        if (audioSource != null && stats != null && stats.bottleImpactSound != null)
        {
            AudioSource.PlayClipAtPoint(stats.bottleImpactSound, transform.position);
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
            enemyHealth.TakeMeleeDamage(stats.bottleThrowDamage);
        }

        // Stun + force chase
        EnemyAI enemyAI = enemy.GetComponent<EnemyAI>();
        if (enemyAI != null && stats != null)
        {
            StartCoroutine(StunAndChase(enemyAI));
        }
    }

    System.Collections.IEnumerator StunAndChase(EnemyAI enemyAI)
    {
        // Arreter mouvement
        enemyAI.canMove = false;
        UnityEngine.AI.NavMeshAgent agent = enemyAI.GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (agent != null && agent.isOnNavMesh)
        {
            agent.isStopped = true;
        }

        Debug.Log("Zombie stunned by bottle");

        // Stun duration
        yield return new WaitForSeconds(stats.bottleStunDuration);

        Debug.Log("Zombie waking up from stun");

        // Forcer la cible player
        PlayerPhysicsMovement player = FindObjectOfType<PlayerPhysicsMovement>();
        if (player != null)
        {
            enemyAI.targetHuman = player.transform;
            enemyAI.currentState = EnemyAI.State.Chasing;
            enemyAI.isForcedChase = true;

            Debug.Log("Target set to player, state = Chasing");
        }

        // Reactiver mouvement
        enemyAI.canMove = true;
        if (agent != null && agent.isOnNavMesh)
        {
            agent.isStopped = false;
            agent.SetDestination(player.transform.position);
            Debug.Log("NavMesh destination set to player position");
        }

        Debug.Log(enemyAI.gameObject.name + " should be chasing now!");
    }
}