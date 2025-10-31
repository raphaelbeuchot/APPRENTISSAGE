using System.Collections;
using UnityEngine;

public class GrabAttack : MonoBehaviour, IAttackBehavior
{
    private EnemyAI enemy;
    private PlayerPhysicsMovement player;
    private Rigidbody playerRb;
    private Rigidbody enemyRb;
    private EnemyStats stats;
    public PlayerStats playerStats;
    private Transform enemyTransform;
    private GameManager gameManager;
    private MeleeAttackSystem playerMelee;

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

        // Empecher grab si player sprinte
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
        // SETUP
        isGrabbing = true;
        player.grabState = PlayerPhysicsMovement.GrabState.Grabbed;
        player.ForceStop();

        // Freeze TOUT
        playerRb.constraints = RigidbodyConstraints.FreezeAll;
        enemyRb.constraints = RigidbodyConstraints.FreezeAll;

        if (playerMelee) playerMelee.OnGrabStart();

        float elapsed = 0f;
        int mashCount = 0;
        int required = player.stats.mashesToEscape;
        float maxTime = player.stats.grabEscapeTimeWindow;

        // GRAB LOOP
        while (elapsed < maxTime && mashCount < required)
        {
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

        // ESCAPE ou RELEASE ?
        bool escaped = mashCount >= required;
        Debug.Log(escaped ? "GRAB ESCAPE" : "GRAB RELEASE");

        if (!escaped)
        {
            PlayerHealth ph = player.GetComponent<PlayerHealth>();
            if (ph) ph.TakeDamage(stats.biteDamage);
        }

        

        EndGrab();
    }

    IEnumerator PlayerRecoilCoroutine()
    {
        player.grabState = PlayerPhysicsMovement.GrabState.Recoil;
        Vector3 recoilDir = (player.transform.position - transform.position).normalized;
        recoilDir.y = 0;

        // Unfreeze JUSTE la position (pas encore la rotation)
        // Unfreeze position, garde rotation XZ freeze
        playerRb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

        // Attendre 1 frame
        yield return null;

        // MAINTENANT appliquer la velocity
        playerRb.linearVelocity = recoilDir * player.stats.recoilForce;

        // Force Y a 0
        Vector3 vel = playerRb.linearVelocity;
        vel.y = 0f;
        playerRb.linearVelocity = vel;

        yield return new WaitForSeconds(0.3f);

        // TIR SENTINEL
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
        
        player.grabState = PlayerPhysicsMovement.GrabState.None;


        if (playerMelee) playerMelee.OnGrabEnd();
    }

    void EndGrab()
    {

        // RECOIL PLAYER
        StartCoroutine(PlayerRecoilCoroutine());

        // BOURRADE GRB (ce zombie)
        isGrabbing = false;
        StartCoroutine(BourradeZombie());

        // BOURRADE FAKE GRABBERS
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
        // --- SETUP ---
        enemyRb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        isInBourradeDuration = true;
        isInBourradeCooldown = false;

        try
        {
            // === PHASE BOURRADE ===
            Vector3 dir = (transform.position - player.transform.position).normalized;
            dir.y = 0f;

            enemyRb.linearVelocity = dir * playerStats.bourradeForce;

            Vector3 vel = enemyRb.linearVelocity;
            vel.y = 0f;
            enemyRb.linearVelocity = vel;

            yield return new WaitForSeconds(stats.bourradeDuration);

            // === PHASE COOLDOWN ===
            isInBourradeDuration = false;
            isInBourradeCooldown = true;
            enemyRb.linearVelocity = Vector3.zero;

            yield return new WaitForSeconds(stats.bourradeCooldown);
        }
        finally
        {
            // --- CLEANUP --- (toujours exécuté, même si la coroutine est stoppée)
            isInBourradeDuration = false;
            isInBourradeCooldown = false;
            enemyRb.linearVelocity = Vector3.zero;
            enemyRb.constraints = RigidbodyConstraints.FreezeRotation;

            // Réactiver le zombie
            EnemyAI ai = GetComponent<EnemyAI>();
            if (ai != null)
                ai.ResetAfterBourrade();

            Debug.Log($"{gameObject.name} > Bourrade terminé proprement");
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

        Debug.Log($"{gameObject.name} > ForceStop exécuté, retour à état normal");
    }

}