using UnityEngine;
using System.Collections;

public class BroomAttackSystem : MonoBehaviour
{
    [Header("Player Stats")]
    public PlayerStats stats;

    [Header("References")]
    private Rigidbody rb;
    private PlayerHealth health;
    private PlayerPhysicsMovement movement;
    private AudioSource audioSource;

    [Header("Impact Effects")]
    [SerializeField] private GameObject[] broomImpactEffects;



    private bool isAttacking = false;
   
    private bool isGrabbed = false;
    private Animator animator;
    void Start()
    {
        if (stats == null)
        {
            Debug.LogError("PlayerStats non assigne sur " + gameObject.name);
            return;
        }
        

        rb = GetComponent<Rigidbody>();
        health = GetComponent<PlayerHealth>();
        movement = GetComponent<PlayerPhysicsMovement>();
        animator = GetComponentInChildren<Animator>();

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();
    }

    void Update()
    {
        if (stats == null) return;


        if (PlayerInputManager.Instance.BroomAttackPressed && CanAttack())
        {
            StartCoroutine(PerformBroomAttack());
        }
    }

    bool CanAttack()
    {
        if (isGrabbed)
        {
            Debug.Log("Cannot broom attack: player is grabbed!");
            return false;
        }

        if (movement != null && movement.grabState == PlayerPhysicsMovement.GrabState.Recoil)
        {
            return false;
        }
        if (PlayerInputManager.Instance.BroomLowActive)
        {
            return false;
        }
        // CHECK STAMINA
        if (movement != null && movement.GetCurrentStamina() < stats.broomStaminaCost)
        {
            Debug.Log("Cannot broom attack: not enough stamina!");
            return false;
        }

        

        if (isAttacking)
        {
            Debug.Log("Cannot broom attack: already attacking");
            return false;
        }

        if (health != null && health.IsDead())
        {
            Debug.Log("Cannot broom attack: player is dead");
            return false;
        }

        if (movement != null && !movement.enabled)
        {
            Debug.Log("Cannot broom attack: movement disabled");
            return false;
        }

        return true;
    }

    IEnumerator PerformBroomAttack()
    {
        isAttacking = true;

        if (movement != null)
            movement.ExitCrouch();

        // CONSOMMER LA STAMINA
        if (movement != null)
        {
            float currentStamina = movement.GetCurrentStamina();
            movement.UpdateStamina(currentStamina - stats.broomStaminaCost);
        }

        // ACTIVER LE LAYER UPPER BODY + TRIGGER
        if (animator != null)
        {
            animator.SetLayerWeight(1, 1f);
            animator.SetTrigger("BroomAttack");
        }

        

        // ATTENDRE TOUTE LA DUREE DE L'ANIMATION
        yield return new WaitForSeconds(stats.broomAttackDuration);

        // Plus de lerp forcé, laisser l'Animator gérer la transition
        isAttacking = false;

        // Reset SEULEMENT si nécessaire pour éviter conflits spray
        if (animator != null)
        {
            animator.Rebind();
            animator.Update(0f);
        }
    }

    public void OnBroomHit()
    {
        bool hitSomething = false;

        Collider[] hits = Physics.OverlapSphere(
            transform.position + Vector3.up * 1f,
            stats.broomRange,
            LayerMask.GetMask("Zombie", "Swarm")
        );

        foreach (Collider hit in hits)
        {
            if (hit.gameObject == gameObject) continue;

            Vector3 directionToTarget = (hit.transform.position - transform.position).normalized;
            directionToTarget.y = 0f;

            float angleToTarget = Vector3.Angle(transform.forward, directionToTarget);

            if (angleToTarget > stats.broomConeAngle / 2f)
            {
                continue;
            }

            Vector3 rayOrigin = transform.position + Vector3.up * 0.5f;
            Vector3 targetPoint = hit.bounds.center;
            Vector3 directionToTarget3D = (targetPoint - rayOrigin).normalized;
            float distance = Vector3.Distance(rayOrigin, targetPoint);

            RaycastHit hitInfo;
            if (Physics.Raycast(rayOrigin,
                                directionToTarget3D,
                                out hitInfo,
                                distance,
                                LayerMask.GetMask("Obstacle")))
            {
                Debug.Log($"[BROOM] {hit.name} est derriere un obstacle, ignore");
                continue;
            }

            EnemyHealth enemyHealth = hit.GetComponent<EnemyHealth>();
            if (enemyHealth != null && !enemyHealth.IsDead())
            {
                hitSomething = true;

                if (broomImpactEffects != null && broomImpactEffects.Length > 0)
                {
                    int randomIndex = Random.Range(0, broomImpactEffects.Length);
                    Instantiate(broomImpactEffects[randomIndex], hit.bounds.center, Quaternion.identity);
                }

                Vector3 knockbackDir = (hit.transform.position - transform.position).normalized;
                knockbackDir.y = 0;

                Rigidbody targetRb = hit.GetComponent<Rigidbody>();
                if (targetRb != null)
                {
                    float knockbackMult = ModifierApplier.Instance != null ? ModifierApplier.Instance.broomKnockbackMultiplier : 1f;
                    targetRb.AddForce(knockbackDir * stats.broomKnockbackForce * knockbackMult, ForceMode.VelocityChange);
                    enemyHealth.SetKnockbackState(0.8f);
                }

                enemyHealth.TakeMeleeDamage(EnemyHealth.AttackType.Broom);

                GrabAttack grab = enemyHealth.GetComponent<GrabAttack>();
                if (grab != null && grab.isInWindup)
                {
                    grab.CancelWindup();
                    Debug.Log($"[BROOM] Cancelled {enemyHealth.gameObject.name} grab windup");
                }

                HitAttack hitAttack = enemyHealth.GetComponent<HitAttack>();
                if (hitAttack != null && (hitAttack.isInWindup || hitAttack.IsAttacking()))
                {
                    hitAttack.CancelAttack();
                    Debug.Log($"[BROOM] Cancelled {enemyHealth.gameObject.name} hit attack");
                }

                EnemyAI_AStar zombieAI = hit.GetComponent<EnemyAI_AStar>();
                if (zombieAI != null && zombieAI.currentState == EnemyAI_AStar.State.RotatingToImpact)
                {
                    zombieAI.CancelRotationToImpact();
                    Debug.Log($"[BROOM] Cancelled {enemyHealth.gameObject.name} rotation to impact");
                }

                if (audioSource != null && stats.broomHitSound != null)
                {
                    audioSource.PlayOneShot(stats.broomHitSound);
                }

                EnemyAI_AStar zombieAI_AStar_temp = hit.GetComponent<EnemyAI_AStar>();
                if (zombieAI_AStar_temp != null)
                {
                    zombieAI_AStar_temp.enabled = false;
                }

                BlinderWanderBehavior wanderBehavior = hit.GetComponent<BlinderWanderBehavior>();
                if (wanderBehavior != null)
                {
                    wanderBehavior.StopWandering();
                }

                MeleeAudioManager.TriggerMeleeHit(hit.transform.position);
                StartCoroutine(KnockdownTarget(hit.gameObject, knockbackDir));
            }

            SwarmController_AStar swarmAStar = hit.GetComponent<SwarmController_AStar>();
            if (swarmAStar != null)
            {
                hitSomething = true;
                swarmAStar.TakeDamage(swarmAStar.stats.broomDamageTaken);
                Debug.Log(gameObject.name + " BROOM hit swarm (A*) for " + swarmAStar.stats.broomDamageTaken + " damage!");
            }
            else
            {
                SwarmController swarm = hit.GetComponent<SwarmController>();
                if (swarm != null)
                {
                    hitSomething = true;
                    swarm.TakeDamage(swarm.stats.broomDamageTaken);
                    Debug.Log(gameObject.name + " BROOM hit swarm for " + swarm.stats.broomDamageTaken + " damage!");
                }
            }

            
        }

        // BLINDERS
        ChargeAttack[] allBlinders = FindObjectsOfType<ChargeAttack>();
        foreach (ChargeAttack blinder in allBlinders)
        {
            if (blinder.stats != null)
            {
                float distanceToPlayer = Vector3.Distance(blinder.transform.position, transform.position);
                if (distanceToPlayer <= blinder.stats.audioDetectionRange)
                    blinder.OnDirectHit(transform.position);
            }
        }

        // BOMBES
        Collider[] bombHits = Physics.OverlapSphere(
            transform.position + Vector3.up * 1f,
            stats.broomBombRange,
            LayerMask.GetMask("Bomb")
        );

        foreach (Collider hit in bombHits)
        {
            BombProjectile bomb = hit.GetComponent<BombProjectile>();
            if (bomb == null) continue;

            Vector3 directionToBomb = (hit.transform.position - transform.position).normalized;
            directionToBomb.y = 0f;
            float angleToBomb = Vector3.Angle(transform.forward, directionToBomb);
            if (angleToBomb > stats.broomBombConeAngle / 2f) continue;

            Vector3 kickDir = (hit.transform.position - transform.position).normalized;
            bomb.KickBack(kickDir, stats.broomKnockbackForce);
            hitSomething = true;
        }

        // CORPSES
        Collider[] corpseHits = Physics.OverlapSphere(
            transform.position + Vector3.up * 0.3f,
            stats.broomRange + 0.5f,
            LayerMask.GetMask("Corpse")
        );

        bool corpseHitSoundPlayed = false;

        foreach (Collider hit in corpseHits)
        {
            Vector3 dir = (hit.transform.position - transform.position).normalized;
            dir.y = 0f;
            float angle = Vector3.Angle(transform.forward, dir);
            if (angle > stats.broomConeAngle / 2f) continue;

            // CAS 1 : corpse scene/tuto avec CorpseRagdoll
            CorpseRagdoll corpseRagdoll = hit.GetComponentInParent<CorpseRagdoll>();
            if (corpseRagdoll != null)
            {
                corpseRagdoll.Launch(dir * stats.broomCorpseForce);
                hitSomething = true;
                if (!corpseHitSoundPlayed && audioSource != null && stats.broomCorpseSound != null)
                {
                    audioSource.PlayOneShot(stats.broomCorpseSound);
                    corpseHitSoundPlayed = true;
                }
                Debug.Log($"[BROOM] CorpseRagdoll {hit.name} launched");
                continue;
            }

            // CAS 2 : ennemi mort avec DeadBodyPhysics
            DeadBodyPhysics deadBody = hit.GetComponentInParent<DeadBodyPhysics>();
            if (deadBody != null)
            {
                RagdollDeathEffect ragdoll = deadBody.GetComponent<RagdollDeathEffect>();
                if (ragdoll != null && ragdoll.hipsRb != null)
                    ragdoll.hipsRb.AddForce(dir * stats.broomCorpseForce, ForceMode.Impulse);
                hitSomething = true;
                if (!corpseHitSoundPlayed && audioSource != null && stats.broomCorpseSound != null)
                {
                    audioSource.PlayOneShot(stats.broomCorpseSound);
                    corpseHitSoundPlayed = true;
                }
                Debug.Log($"[BROOM] DeadBody {hit.name} launched");
                continue;
            }
        }

    }
    IEnumerator KnockdownTarget(GameObject target, Vector3 knockbackDirection)
    {
        EnemyAI_AStar zombieAI_AStar = target.GetComponent<EnemyAI_AStar>();
        bool shouldDisableAI = zombieAI_AStar != null
            && !zombieAI_AStar.isInPitMode
            && !zombieAI_AStar.isOnRotatingPlatform;

        if (shouldDisableAI)
        {
            Pathfinding.AIPath aiPath = target.GetComponent<Pathfinding.AIPath>();
            if (aiPath != null)
            {
                aiPath.enabled = false;
            }
            zombieAI_AStar.enabled = false;
        }

        yield return new WaitForSeconds(2f);

        EnemyAI_AStar zombieForRotation = target.GetComponent<EnemyAI_AStar>();
        EnemyHealth healthForRotation = target.GetComponent<EnemyHealth>();

        if (zombieForRotation != null && healthForRotation != null && !healthForRotation.IsDead())
        {
            if (healthForRotation.stats != null && !zombieForRotation.isOnRotatingPlatform)
            {
                Vector3 impactDirection = -knockbackDirection;
                zombieForRotation.StartRotationToImpact(impactDirection, healthForRotation.stats.rotationToImpactDuration);
                Debug.Log($"[BROOM] Started rotation to impact for {target.name}");
            }
        }

        if (zombieAI_AStar != null && target != null)
        {
            if (zombieAI_AStar.isInPitMode)
            {
                zombieAI_AStar.enabled = true;
            }
            else if (zombieAI_AStar.isOnRotatingPlatform)
            {
                zombieAI_AStar.enabled = true;
            }
            else
            {
                Pathfinding.AIPath aiPath = target.GetComponent<Pathfinding.AIPath>();
                if (aiPath != null)
                    aiPath.enabled = true;
                zombieAI_AStar.enabled = true;
            }

            zombieAI_AStar.lastPathDestination = Vector3.positiveInfinity;

            if (zombieAI_AStar.isOnIslandPlatform && zombieAI_AStar.currentState != EnemyAI_AStar.State.RotatingToImpact)
            {
                zombieAI_AStar.currentState = EnemyAI_AStar.State.OnIslandPlatform;
                zombieAI_AStar.DetectHumans();
            }
        }
    }

    public void OnGrabStart()
    {
        isGrabbed = true;

        if (isAttacking)
        {
            StopAllCoroutines();
            isAttacking = false;
            if (animator != null)
                animator.SetLayerWeight(1, 0f);
            Debug.Log("BroomAttack: CANCELLED by grab");
        }
    }

    public void OnGrabEnd()
    {
        isGrabbed = false;
        Debug.Log("BroomAttack: Player released, isGrabbed now FALSE");
    }

    public bool IsAttacking() => isAttacking;
}