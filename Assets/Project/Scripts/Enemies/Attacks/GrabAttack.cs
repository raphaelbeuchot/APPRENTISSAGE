using System.Collections;
using UnityEngine;

public class GrabAttack : MonoBehaviour, IAttackBehavior
{
    private EnemyAI enemy;
    private PlayerPhysicsMovement player;
    private Rigidbody playerRb;
    private Rigidbody enemyRb;
    private EnemyStats stats;
    private Transform enemyTransform;
    private GameManager gameManager;
    private MeleeAttackSystem playerMelee;

    public bool isGrabbing = false;
    public bool isInBourradeDuration = false;
    public bool isInBourradeCooldown = false;
    public bool IsInBourrade() => isInBourradeDuration || isInBourradeCooldown;

    public void Initialize(EnemyStats stats, Transform enemyTransform, Rigidbody enemyRigidbody)
    {
        this.stats = stats;
        this.enemyTransform = enemyTransform;
        this.enemyRb = enemyRigidbody;
        enemy = GetComponent<EnemyAI>();
        player = FindFirstObjectByType<PlayerPhysicsMovement>();
        if (player) playerRb = player.GetComponent<Rigidbody>();
        gameManager = FindFirstObjectByType<GameManager>();
        if (player) playerMelee = player.GetComponent<MeleeAttackSystem>();
        isGrabbing = false;
        isInBourradeDuration = false;
        isInBourradeCooldown = false;
    }

    public bool CanAttack() => !isInBourradeDuration && !isInBourradeCooldown;
    public bool IsAttacking() => isGrabbing;
    public bool IsInSpecialState() => isInBourradeDuration || isInBourradeCooldown;
    public bool IsGrabbing() => isGrabbing;

    public void AttemptAttack(GameObject target)
    {
        if (!player || IsInBourrade() || isGrabbing) return;

        float dist = Vector3.Distance(transform.position, player.transform.position);
        if (dist > stats.attackRange) return;

        if (player.grabState == PlayerPhysicsMovement.GrabState.None)
        {
            StartCoroutine(GrabCoroutine());
        }
    }

    IEnumerator GrabCoroutine()
    {
        // SETUP
        isGrabbing = true;
        player.grabState = PlayerPhysicsMovement.GrabState.Grabbed;
        player.ForceStop();


        // Désactiver melee attack
        if (playerMelee) playerMelee.OnGrabStart();

        float elapsed = 0f;
        int mashCount = 0;
        int required = player.stats.mashesToEscape;
        float maxTime = player.stats.grabEscapeTimeWindow;

        // GRAB LOOP
        while (elapsed < maxTime && mashCount < required)
        {
            if (Input.GetKeyDown(KeyCode.Space))
            {
                mashCount++;
                Debug.Log($"MASH {mashCount}/{required}");
            }

            player.grabProgress = (float)mashCount / required;
            elapsed += Time.deltaTime;
            yield return null;
        }

        // ESCAPE ou RELEASE ?
        bool escaped = mashCount >= required;
        Debug.Log(escaped ? "GRAB ESCAPE" : "GRAB RELEASE");

        if (!escaped)
        {
            // MORSURE
            PlayerHealth ph = player.GetComponent<PlayerHealth>();
            if (ph) ph.TakeDamage(stats.biteDamage);
        }

        // RECOIL PLAYER
        player.grabState = PlayerPhysicsMovement.GrabState.Recoil;
        Vector3 recoilDir = (player.transform.position - transform.position).normalized;
        recoilDir.y = 0;
        playerRb.AddForce(recoilDir * stats.bourradeForce * 0.6f, ForceMode.VelocityChange);
        Debug.Log($"RECOIL applied: {recoilDir * stats.bourradeForce * 0.6f}");

        isGrabbing = false;

        // BOURRADE ZOMBIE
        StartCoroutine(BourradeZombie());

        // CLEANUP PLAYER
        yield return new WaitForSeconds(0.3f);
        // TIR SENTINEL À LA FIN DU RECOIL
        if (gameManager.IsInRedLight() && player.grabState == PlayerPhysicsMovement.GrabState.Recoil)
        {
            PlayerHealth ph = player.GetComponent<PlayerHealth>();
            if (ph && !ph.IsDead())
            {
                Vector3 sentinelPos = gameManager.sentinelEye != null ? gameManager.sentinelEye.position : gameManager.transform.position;
                gameManager.StartCoroutine(gameManager.ShootPlayerAtEndOfRecoil(player.gameObject, ph, sentinelPos, player.transform.position));
            }
        }

        if (player.grabState == PlayerPhysicsMovement.GrabState.Recoil)
            player.grabState = PlayerPhysicsMovement.GrabState.None;

        // Réactiver melee attack
        if (playerMelee) playerMelee.OnGrabEnd();

    }

    IEnumerator BourradeZombie()
    {
        isInBourradeDuration = true;
        isInBourradeCooldown = false;

        Vector3 dir = (transform.position - player.transform.position).normalized;
        dir.y = 0;
        enemyRb.linearVelocity = dir * stats.bourradeForce;
        Debug.Log("BOURRADE DURATION - vulnerable au tir");

        yield return new WaitForSeconds(stats.bourradeDuration);

        // FIN DURATION, DÉBUT COOLDOWN
        isInBourradeDuration = false;
        isInBourradeCooldown = true;
        enemyRb.linearVelocity = Vector3.zero;
        Debug.Log("BOURRADE COOLDOWN - immunisé au tir");

        yield return new WaitForSeconds(stats.bourradeCooldown);

        isInBourradeDuration = false;
        isInBourradeCooldown = false;
        Debug.Log("BOURRADE ended");
    }

    public void ForceStop()
    {
        StopAllCoroutines();
        isGrabbing = false;
        isInBourradeDuration = false;
        isInBourradeCooldown = false;
    }
}