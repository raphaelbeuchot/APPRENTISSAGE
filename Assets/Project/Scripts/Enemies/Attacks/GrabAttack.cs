using System.Collections;
using UnityEngine;

public class GrabAttack : MonoBehaviour, IAttackBehavior
{
    public Material redMaterial;

    private EnemyAI enemy;
    private PlayerPhysicsMovement player;
    private Rigidbody playerRb;
    private Rigidbody enemyRb;
    private EnemyStats stats;
    public PlayerStats playerStats;
    private Transform enemyTransform;
    private GameManager gameManager;
    private MeleeAttackSystem playerMelee;
    private EnemyHealth enemyHealth;

    public bool isGrabbing = false;
    public bool isInBourradeDuration = false;
    public bool isInBourradeCooldown = false;
    public bool isFakeGrabbing = false;
    private System.Collections.Generic.List<EnemyAI> fakeGrabbers = new System.Collections.Generic.List<EnemyAI>();
    public bool IsInBourrade() => isInBourradeDuration || isInBourradeCooldown;

    public void Initialize(EnemyStats stats, PlayerStats playerStats, Transform enemyTransform, Rigidbody enemyRigidbody)
    {
        fakeGrabbers = new System.Collections.Generic.List<EnemyAI>();

        this.stats = stats;
        this.playerStats = playerStats;
        this.enemyTransform = enemyTransform;
        this.enemyRb = enemyRigidbody;
        enemy = GetComponent<EnemyAI>();
        enemyHealth = GetComponent<EnemyHealth>();
        player = FindFirstObjectByType<PlayerPhysicsMovement>();
        if (player) playerRb = player.GetComponent<Rigidbody>();
        gameManager = FindFirstObjectByType<GameManager>();
        if (player) playerMelee = player.GetComponent<MeleeAttackSystem>();

        isGrabbing = false;
        isInBourradeDuration = false;
        isInBourradeCooldown = false;
        isFakeGrabbing = false;
    }

    void UpdateFakeGrabbers()
    {
        if (!player || player.grabState != PlayerPhysicsMovement.GrabState.Grabbed)
        {
            fakeGrabbers.Clear();
            return;
        }

        Collider[] nearbyZombies = Physics.OverlapSphere(
            player.transform.position,
            stats.fakeGrabRange,
            LayerMask.GetMask("Zombie")
        );

        fakeGrabbers.Clear();

        foreach (Collider col in nearbyZombies)
        {
            if (col.gameObject == gameObject) continue;

            EnemyAI zombie = col.GetComponent<EnemyAI>();
            GrabAttack zombieGrab = col.GetComponent<GrabAttack>();

            if (zombie != null && zombieGrab != null && !zombieGrab.isGrabbing)
            {
                fakeGrabbers.Add(zombie);
                zombieGrab.isFakeGrabbing = true;
            }
        }
    }

    public bool CanAttack() => !isInBourradeDuration && !isInBourradeCooldown;
    public bool IsAttacking() => isGrabbing;
    public bool IsInSpecialState() => isInBourradeDuration || isInBourradeCooldown;
    public bool IsGrabbing() => isGrabbing;

    public void AttemptAttack(GameObject target)
    {
        if (!player || IsInBourrade() || isGrabbing) return;

        EnemyAI ai = GetComponent<EnemyAI>();
        if (ai != null && ai.isDetectedBySentinel)
        {
            return; // Sentinelle targeting: cancel grab
        }

        if (player != null && player.IsSprinting())
            return;

        float dist = Vector3.Distance(transform.position, player.transform.position);
        if (dist > stats.attackRange) return;

        if (player.grabState == PlayerPhysicsMovement.GrabState.None)
        {
            StartCoroutine(GrabCoroutine());
        }
    }

    IEnumerator GrabCoroutine()
    {
        isGrabbing = true;
        player.grabState = PlayerPhysicsMovement.GrabState.Grabbed;
        player.ForceStop();

        playerRb.constraints = RigidbodyConstraints.FreezeAll;
        enemyRb.constraints = RigidbodyConstraints.FreezeAll;

        if (playerMelee) playerMelee.OnGrabStart();

        float elapsed = 0f;
        int mashCount = 0;
        int required = player.stats.mashesToEscape;
        float maxTime = player.stats.grabEscapeTimeWindow;

        try
        {
            while (elapsed < maxTime && mashCount < required)
            {
                // Safety: if enemy dies, release immediately
                if (enemyHealth == null || enemyHealth.IsDead())
                {
                    Debug.Log("Grab interrupted: enemy dead - NO RECOIL");
                    EndGrab(false); // PAS de recoil player
                    yield break;
                }

                UpdateFakeGrabbers();

                if (Input.GetKeyDown(KeyCode.Space))
                {
                    mashCount++;
                    Debug.Log($"MASH {mashCount}/{required}");
                }

                player.grabProgress = (float)mashCount / required;
                elapsed += Time.deltaTime;
                yield return null;
            }

            bool escaped = mashCount >= required;
            Debug.Log(escaped ? "GRAB ESCAPE" : "GRAB RELEASE");

            if (!escaped)
            {
                PlayerHealth ph = player.GetComponent<PlayerHealth>();
                if (ph) ph.TakeDamage(stats.biteDamage);
            }

            EndGrab(true); // Recoil normal
        }
        finally
        {
            // TOUJOURS executer meme si StopAllCoroutines
            if (player != null && playerRb != null)
            {
                playerRb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            }

            if (enemyRb != null)
            {
                enemyRb.constraints = RigidbodyConstraints.FreezeRotation;
            }

            if (player != null && player.grabState == PlayerPhysicsMovement.GrabState.Grabbed)
            {
                player.grabState = PlayerPhysicsMovement.GrabState.None;
            }

            isGrabbing = false;
        }
    }

    IEnumerator PlayerRecoilCoroutine()
    {
        player.grabState = PlayerPhysicsMovement.GrabState.Recoil;
        Vector3 recoilDir = (player.transform.position - transform.position).normalized;
        recoilDir.y = 0;

        playerRb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

        try
        {
            yield return null;

            playerRb.linearVelocity = recoilDir * player.stats.recoilForce;
            Vector3 vel = playerRb.linearVelocity;
            vel.y = 0f;
            playerRb.linearVelocity = vel;

            yield return new WaitForSeconds(0.3f);
        }
        finally
        {
            if (player != null && player.grabState == PlayerPhysicsMovement.GrabState.Recoil)
            {
                player.grabState = PlayerPhysicsMovement.GrabState.None;
            }

            if (playerMelee) playerMelee.OnGrabEnd();
        }
    }

    void EndGrab(bool givePlayerRecoil = true)
    {
        if (isGrabbing && givePlayerRecoil)
        {
            StartCoroutine(PlayerRecoilCoroutine());
        }

        isGrabbing = false;
        StartCoroutine(BourradeZombie());

        foreach (EnemyAI fakeZombie in fakeGrabbers)
        {
            if (fakeZombie != null)
            {
                GrabAttack fakeGrab = fakeZombie.GetComponent<GrabAttack>();
                if (fakeGrab != null)
                {
                    fakeGrab.isFakeGrabbing = false;
                    fakeGrab.StartCoroutine(fakeGrab.BourradeZombie());
                }
            }
        }

        fakeGrabbers.Clear();
    }

    IEnumerator BourradeZombie()
    {
        enemyRb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        isInBourradeDuration = true;
        isInBourradeCooldown = false;

        try
        {
            Vector3 dir = (transform.position - player.transform.position).normalized;
            dir.y = 0f;

            enemyRb.linearVelocity = dir * playerStats.bourradeForce;

            Vector3 vel = enemyRb.linearVelocity;
            vel.y = 0f;
            enemyRb.linearVelocity = vel;

            yield return new WaitForSeconds(stats.bourradeDuration);

            isInBourradeDuration = false;
            isInBourradeCooldown = true;
            enemyRb.linearVelocity = Vector3.zero;

            yield return new WaitForSeconds(stats.bourradeCooldown);
        }
        finally
        {
            isInBourradeDuration = false;
            isInBourradeCooldown = false;
            enemyRb.linearVelocity = Vector3.zero;
            enemyRb.constraints = RigidbodyConstraints.FreezeRotation;

            EnemyAI ai = GetComponent<EnemyAI>();
            if (ai != null)
                ai.ResetAfterBourrade();

            // Retirer de alreadyShot pour pouvoir etre tire a nouveau
            if (gameManager != null)
                gameManager.RemoveFromAlreadyShot(gameObject);

            Debug.Log($"{gameObject.name} > Bourrade ended cleanly");
        }
    }

    public void ForceStop()
    {
        StopAllCoroutines();

        isGrabbing = false;
        isInBourradeDuration = false;
        isInBourradeCooldown = false;

        if (enemyRb != null)
        {
            enemyRb.linearVelocity = Vector3.zero;
            enemyRb.constraints = RigidbodyConstraints.FreezeRotation;
        }

        EnemyAI ai = GetComponent<EnemyAI>();
        if (ai != null)
            ai.ResetAfterBourrade();

        if (player != null && player.grabState != PlayerPhysicsMovement.GrabState.None)
            player.grabState = PlayerPhysicsMovement.GrabState.None;

        if (playerMelee) playerMelee.OnGrabEnd();

        Debug.Log($"{gameObject.name} > ForceStop executed, back to normal");
    }

    IEnumerator FlashRedMaterialCoroutine(Renderer renderer, Material redMat, float duration)
    {
        if (renderer == null || redMat == null) yield break;

        Material originalMat = renderer.material;
        float elapsed = 0f;
        bool toggle = false;

        while (elapsed < duration)
        {
            renderer.material = toggle ? redMat : originalMat;
            toggle = !toggle;
            elapsed += 0.1f;
            yield return new WaitForSeconds(0.1f);
        }

        renderer.material = originalMat;
    }
}