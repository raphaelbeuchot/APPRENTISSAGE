using System.Collections;
using UnityEngine;

public class GrabAttack : MonoBehaviour, IAttackBehavior
{
    public Material redMaterial;

    private EnemyAI_AStar enemy; 
    private PlayerPhysicsMovement player;
    private Rigidbody playerRb;
    private Rigidbody enemyRb;
    private EnemyStats stats;
    public PlayerStats playerStats;
    private Transform enemyTransform;
    private GameManager gameManager;
    private MeleeAttackSystem playerMelee;
    private EnemyHealth enemyHealth;

    // Nouveau systeme de timings fixes
    private float[] grabDamageTimes = { 2f, 3.5f, 4.5f };
    private int nextDamageIndex = 0;
    private float grabTotalDuration = 5f;
    private float grabElapsedTime = 0f;

    public bool isGrabbing = false;
    public bool isInBourradeDuration = false;
    public bool isInBourradeCooldown = false;
    public bool isFakeGrabbing = false;
    private System.Collections.Generic.List<EnemyAI_AStar> fakeGrabbers = new System.Collections.Generic.List<EnemyAI_AStar>();
    public bool IsInBourrade() => isInBourradeDuration || isInBourradeCooldown;

    public void Initialize(EnemyStats stats, PlayerStats playerStats, Transform enemyTransform, Rigidbody enemyRigidbody)
    {
        fakeGrabbers = new System.Collections.Generic.List<EnemyAI_AStar>();

        this.stats = stats;
        this.playerStats = playerStats;
        this.enemyTransform = enemyTransform;
        this.enemyRb = enemyRigidbody;
        enemy = GetComponent<EnemyAI_AStar>();
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

            EnemyAI_AStar zombie = col.GetComponent<EnemyAI_AStar>();

            GrabAttack zombieGrab = col.GetComponent<GrabAttack>();
            ChargeAttack chargeAttack = col.GetComponent<ChargeAttack>();

            if (zombie != null)
            {
                if (zombieGrab != null && !zombieGrab.isGrabbing)
                {
                    fakeGrabbers.Add(zombie);
                    zombieGrab.isFakeGrabbing = true;
                }
                else if (chargeAttack != null && chargeAttack.CanAttack())
                {
                    fakeGrabbers.Add(zombie);
                }
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

        EnemyAI_AStar ai = GetComponent<EnemyAI_AStar>();

        if (player != null && player.IsSprinting())
            return;

        // === CHECK IMMUNITÉ GRABS ===
        if (player != null && player.isImmuneToGrab)
        {
            Debug.Log($"{gameObject.name} cannot grab - player is immune (climbing/falling)");
            return;
        }

        float dist = Vector3.Distance(transform.position, player.transform.position);
        if (dist > stats.attackRange) return;

        if (player.grabState == PlayerPhysicsMovement.GrabState.None)
        {
            StartCoroutine(GrabCoroutine());
        }
    }

    void StartGrab()
    {
        GameUIManager ui = FindObjectOfType<GameUIManager>();
        if (ui != null)
            ui.RegisterGrab(this);
    }

    IEnumerator GrabCoroutine()
    {
        isGrabbing = true;
        

            // NOUVEAU : Enregistrer le grab pour l'UI
            StartGrab();

            player.grabState = PlayerPhysicsMovement.GrabState.Grabbed;
            player.ForceStop();
            player.grabState = PlayerPhysicsMovement.GrabState.Grabbed;
        player.ForceStop();

        playerRb.constraints = RigidbodyConstraints.FreezeAll;
        enemyRb.constraints = RigidbodyConstraints.FreezeAll;

        if (playerMelee) playerMelee.OnGrabStart();

        BroomAttackSystem playerBroom = player.GetComponent<BroomAttackSystem>();
        if (playerBroom) playerBroom.OnGrabStart();

        int mashCount = 0;
        int required = player.stats.mashesToEscape;

        float elapsed = 0f;

        // Timings des dégâts intermédiaires
        float[] damageTimes = { 2f, 3.5f, 4f };
        int nextDamageIndex = 0;

        try
        {
            while (elapsed < stats.grabDuration && mashCount < required)
            {
                if (enemyHealth == null || enemyHealth.IsDead())
                {
                    EndGrab(false);
                    yield break;
                }

                UpdateFakeGrabbers();

                if (PlayerInputManager.Instance.MashEscapePressed)
                {
                    mashCount++;
                }

                player.grabProgress = (float)mashCount / required;
                elapsed += Time.deltaTime;

                // Dégâts intermédiaires
                if (nextDamageIndex < damageTimes.Length && elapsed >= damageTimes[nextDamageIndex])
                {
                    PlayerHealth ph = player.GetComponent<PlayerHealth>();
                    if (ph != null) ph.TakeDamage((int)stats.biteTickDamage);
                    nextDamageIndex++;
                }

                yield return null;
            }

            // Morsure finale à la fin du grab si le joueur n'a pas échappé
            if (mashCount < required)
            {
                PlayerHealth ph = player.GetComponent<PlayerHealth>();
                if (ph != null) ph.TakeDamage((int)stats.biteDamage);

                if (stats.attackSound != null)
                {
                    AudioSource.PlayClipAtPoint(stats.attackSound, transform.position);
                }
            }

            EndGrab(mashCount >= required);
        }
        finally
        {
            playerRb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            enemyRb.constraints = RigidbodyConstraints.FreezeRotation;

            if (player != null && player.grabState == PlayerPhysicsMovement.GrabState.Grabbed)
                player.grabState = PlayerPhysicsMovement.GrabState.None;

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
                player.lastGrabEndTime = Time.time; //  AJOUTE ÇA
            }

            if (playerMelee) playerMelee.OnGrabEnd();

            BroomAttackSystem playerBroom = player.GetComponent<BroomAttackSystem>();
            if (playerBroom) playerBroom.OnGrabEnd();
        }
    }

        void EndGrab(bool givePlayerRecoil = true)
    {
        grabElapsedTime = 0f;
        nextDamageIndex = 0;

        if (isGrabbing && givePlayerRecoil)
        {
            StartCoroutine(PlayerRecoilCoroutine());
        }
        else
        {
            if (playerMelee)
            {
                playerMelee.OnGrabEnd();
            }
            BroomAttackSystem playerBroom = player.GetComponent<BroomAttackSystem>();
            if (playerBroom) playerBroom.OnGrabEnd();
        }

        isGrabbing = false;
        StartCoroutine(BourradeZombie());

        foreach (EnemyAI_AStar fakeZombie in fakeGrabbers)
        {
            if (fakeZombie != null)
            {
                GrabAttack fakeGrab = fakeZombie.GetComponent<GrabAttack>();
                if (fakeGrab != null)
                {
                    fakeGrab.isFakeGrabbing = false;
                    fakeGrab.StartCoroutine(fakeGrab.BourradeZombie());
                }

                ChargeAttack chargeAttack = fakeZombie.GetComponent<ChargeAttack>();
                if (chargeAttack != null)
                {
                    Vector3 dir = (fakeZombie.transform.position - player.transform.position).normalized;
                    dir.y = 0f;
                    chargeAttack.ApplyBourrade(dir, playerStats.bourradeForce, stats.bourradeDuration);
                }
            }
        }

        fakeGrabbers.Clear();
    }

    IEnumerator BourradeZombie()
    {
        if (gameManager != null)
            gameManager.RemoveFromAlreadyShot(gameObject);

        Pathfinding.AIPath aiPath = GetComponent<Pathfinding.AIPath>();
        bool hadAIPath = aiPath != null;
        if (hadAIPath)
        {
            aiPath.enabled = false;
        }

        enemyRb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        isInBourradeDuration = true;
        isInBourradeCooldown = false;

        try
        {
            Vector3 dir = (transform.position - player.transform.position).normalized;
            dir.y = 0;

            enemyRb.linearVelocity = dir * playerStats.bourradeForce;

            Vector3 vel = enemyRb.linearVelocity;
            vel.y = 0;
            enemyRb.linearVelocity = vel;
            
            // ACTIVER LE FLAG KNOCKBACK
            if (enemyHealth != null)
            {
                enemyHealth.SetKnockbackState(stats.bourradeDuration);
            }

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

            if (hadAIPath && aiPath != null)
            {
                aiPath.enabled = true;
            }

            Debug.Log(gameObject.name + " > Bourrade ended cleanly");
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

        if (player != null && player.grabState != PlayerPhysicsMovement.GrabState.None)
            player.grabState = PlayerPhysicsMovement.GrabState.None;

        if (playerMelee) playerMelee.OnGrabEnd();

        Debug.Log(gameObject.name + " > ForceStop executed, back to normal");
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
