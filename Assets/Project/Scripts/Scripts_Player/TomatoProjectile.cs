using UnityEngine;
using System.Collections;

public class TomatoProjectile : MonoBehaviour
{
    private PlayerStats stats;
    private Vector3 targetPos;
    private bool hasImpacted = false;
    private Rigidbody rb;


    [SerializeField] private float tomatoSpeed = 12f;
    [SerializeField] private GameObject squishedTomatoPrefab;

    private static readonly int zombieLayer = -1;

    void Awake()
    {
        zombieLayerCached = LayerMask.NameToLayer("Zombie");
    }

    private int zombieLayerCached;

    public void Init(PlayerStats playerStats, Vector3 destination)
    {
        stats = playerStats;
        targetPos = destination + Vector3.up * 0.5f;
        rb = GetComponent<Rigidbody>();

        
        rb.linearVelocity = CalculateLaunchVelocity(transform.position, targetPos);
    }

    Vector3 CalculateLaunchVelocity(Vector3 origin, Vector3 target)
    {
        Vector3 displacementXZ = new Vector3(target.x - origin.x, 0f, target.z - origin.z);
        float distanceXZ = displacementXZ.magnitude;
        float flightTime = distanceXZ / tomatoSpeed;

        float displacementY = target.y - origin.y;
        Vector3 velocityXZ = displacementXZ / flightTime;
        float velocityY = (displacementY / flightTime) + (0.5f * Mathf.Abs(Physics.gravity.y) * flightTime);

        return velocityXZ + Vector3.up * velocityY;
    }

    void OnCollisionEnter(Collision collision)
    {
        if (hasImpacted) return;
        hasImpacted = true;

        if (stats != null && stats.bottleImpactSound != null)
        {
            GameObject tempAudio = new GameObject("TomatoImpactSound");
            AudioSource tempSource = tempAudio.AddComponent<AudioSource>();
            tempSource.spatialBlend = 0f;
            tempSource.PlayOneShot(stats.bottleImpactSound);
            Destroy(tempAudio, stats.bottleImpactSound.length);
        }

        if (collision.gameObject.layer == zombieLayerCached)
        {
            EnemyHealth enemyHealth = collision.gameObject.GetComponent<EnemyHealth>();
            if (enemyHealth != null && enemyHealth.IsAlive())
            {
                enemyHealth.TakeMeleeDamage(EnemyHealth.AttackType.Bottle);

                EnemyAI_AStar enemyAI = collision.gameObject.GetComponent<EnemyAI_AStar>();
                if (enemyAI != null)
                    enemyAI.StartCoroutine(StunAndChase(enemyAI));

                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.isKinematic = true;

                Collider col = GetComponent<Collider>();
                if (col != null)
                    col.enabled = false;

                transform.SetParent(collision.transform);

                StartCoroutine(WatchEnemyDeath(enemyHealth));
                return;
            }
        }

        // Cible lockable (panneau, prop de comptage)
        LockableTarget lockable = collision.gameObject.GetComponent<LockableTarget>();
        if (lockable == null)
            lockable = collision.gameObject.GetComponentInParent<LockableTarget>();

        if (lockable != null)
        {
            lockable.OnTomatoHit();

            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;

            Collider col = GetComponent<Collider>();
            if (col != null)
                col.enabled = false;

            transform.SetParent(lockable.transform);

            if (squishedTomatoPrefab != null)
            {
                Quaternion squishRot = Quaternion.Euler(90f, 0f, 0f);
                GameObject squished = Instantiate(squishedTomatoPrefab, transform.position, squishRot);
                squished.transform.SetParent(lockable.transform);
            }

            Destroy(gameObject);
            return;
        }

        // Sol ou tout autre layer : ecrasement
        SpawnSquished();
        Destroy(gameObject);
    }

    void SpawnSquished()
    {
        if (squishedTomatoPrefab == null) return;

        Vector3 spawnPos = new Vector3(transform.position.x, 0f, transform.position.z);
        Instantiate(squishedTomatoPrefab, spawnPos, Quaternion.identity);
    }

    IEnumerator WatchEnemyDeath(EnemyHealth enemyHealth)
    {
        while (enemyHealth != null && enemyHealth.IsAlive())
            yield return null;

        Destroy(gameObject);
    }

    IEnumerator StunAndChase(EnemyAI_AStar enemyAI)
    {
        enemyAI.canMove = false;
        Pathfinding.AIPath aiPath = enemyAI.GetComponent<Pathfinding.AIPath>();
        if (aiPath != null) aiPath.canMove = false;

        yield return new WaitForSeconds(stats.bottleStunDuration);

        PlayerPhysicsMovement player = FindObjectOfType<PlayerPhysicsMovement>();
        if (player != null)
        {
            enemyAI.targetHuman = player.transform;
            enemyAI.currentState = EnemyAI_AStar.State.Chasing;
            enemyAI.isForcedChase = true;
        }

        enemyAI.canMove = true;
        if (aiPath != null)
        {
            aiPath.canMove = true;
            if (player != null)
                aiPath.destination = player.transform.position;
        }
    }
}