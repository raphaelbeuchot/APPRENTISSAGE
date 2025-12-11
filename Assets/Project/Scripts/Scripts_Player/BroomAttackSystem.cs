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

    private bool isAttacking = false;
    private bool isInWindup = false;
    private float lastAttackTime = 0f;
    private bool isGrabbed = false;

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

        // CHECK STAMINA
        if (movement != null && movement.GetCurrentStamina() < stats.broomStaminaCost)
        {
            Debug.Log("Cannot broom attack: not enough stamina!");
            return false;
        }

        if (Time.time - lastAttackTime < stats.broomCooldown)
        {
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
        isInWindup = true;

        //Sortir du crouch si actif

        if (movement != null)
        {
            movement.ExitCrouch();
        }

        try
        {
            // WINDUP PHASE
            Debug.Log("BROOM WINDUP START");
            yield return new WaitForSeconds(stats.broomWindupTime);

            // Check si annule par grab
            if (isGrabbed)
            {
                Debug.Log("BROOM ATTACK CANCELLED BY GRAB");
                yield break;
            }

            isInWindup = false;

            // CONSOMMER LA STAMINA
            if (movement != null)
            {
                float currentStamina = movement.GetCurrentStamina();
                movement.UpdateStamina(currentStamina - stats.broomStaminaCost);
            }

            lastAttackTime = Time.time;

            // EXECUTE ATTACK
            Debug.Log("BROOM ATTACK!");
            DetectAndHitTargets();

            // Son
            if (audioSource != null && stats.broomSound != null)
            {
                audioSource.PlayOneShot(stats.broomSound);
            }

            // RECOVERY PHASE
            float recoveryTime = stats.broomAttackDuration - stats.broomWindupTime;
            yield return new WaitForSeconds(recoveryTime);
        }
        finally
        {
            isAttacking = false;
            isInWindup = false;
        }
    }

    void DetectAndHitTargets()
    {
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

            // ========== LINE-OF-SIGHT CHECK ==========
            Vector3 rayOrigin = transform.position + Vector3.up * 0.5f; // Depuis ton torse
            Vector3 targetPoint = hit.bounds.center; // Vers le centre du collider
            Vector3 directionToTarget3D = (targetPoint - rayOrigin).normalized;
            float distance = Vector3.Distance(rayOrigin, targetPoint);

            RaycastHit hitInfo;
            if (Physics.Raycast(rayOrigin,
                                directionToTarget3D,
                                out hitInfo,
                                distance,
                                LayerMask.GetMask("Obstacle")))
            {
                Debug.Log($"[BROOM] {hit.name} est derrière un obstacle, ignoré");
                continue;
            }
            // ========== FIN LINE-OF-SIGHT CHECK ==========

            // ZOMBIES
            EnemyHealth enemyHealth = hit.GetComponent<EnemyHealth>();
            if (enemyHealth != null && !enemyHealth.IsDead())
            {
                // DESACTIVER ZOMBIE AVANT KNOCKBACK
                EnemyAI_AStar zombieAI_AStar_temp = hit.GetComponent<EnemyAI_AStar>();
                if (zombieAI_AStar_temp != null)
                {
                    zombieAI_AStar_temp.enabled = false;
                }

                // Knockback (applique a TOUS les zombies, Blinder inclus)
                Rigidbody targetRb = hit.GetComponent<Rigidbody>();
                if (targetRb != null)
                {
                    Vector3 knockbackDir = (hit.transform.position - transform.position).normalized;
                    knockbackDir.y = 0;
                    targetRb.AddForce(knockbackDir * stats.broomKnockbackForce, ForceMode.VelocityChange);
                }

                // Degats
                enemyHealth.TakeMeleeDamage(EnemyHealth.AttackType.Broom);
                // Son d'impact individuel
                if (audioSource != null && stats.broomHitSound != null)
                {
                    audioSource.PlayOneShot(stats.broomHitSound);
                }


                // Knockdown (sauf Blinders)
                ChargeAttack chargeAttack = hit.GetComponent<ChargeAttack>();
                if (chargeAttack == null)
                {
                    MeleeAudioManager.TriggerMeleeHit(hit.transform.position);
                    StartCoroutine(KnockdownTarget(hit.gameObject));
                }

            }

            // SWARMS
            SwarmController_AStar swarmAStar = hit.GetComponent<SwarmController_AStar>();
            if (swarmAStar != null)
            {
                // AVANT : swarmAStar.TakeDamage(stats.broomDamage);
                // APRÈS :
                swarmAStar.TakeDamage(swarmAStar.stats.broomDamageTaken);
                Debug.Log(gameObject.name + " BROOM hit swarm (A*) for " + swarmAStar.stats.broomDamageTaken + " damage!");
            }
            else
            {
                SwarmController swarm = hit.GetComponent<SwarmController>();
                if (swarm != null)
                {
                    swarm.TakeDamage(swarm.stats.broomDamageTaken);
                    Debug.Log(gameObject.name + " BROOM hit swarm for " + swarm.stats.broomDamageTaken + " damage!");
                }
            }

            // BRIGHT EYES
            BrightEyesController brightEyes = hit.GetComponent<BrightEyesController>();
            if (brightEyes != null && brightEyes.IsAlive() && !brightEyes.IsFlameExtinguished())
            {
                brightEyes.ExtinguishFlame();
                Debug.Log(gameObject.name + " BROOM extinguished " + hit.gameObject.name + "'s flame!");
            }
        }

        // NOTIFICATION GLOBALE : Tous les Blinders dans leur audioDetectionRange chargent
        ChargeAttack[] allBlinders = FindObjectsOfType<ChargeAttack>();

        foreach (ChargeAttack blinder in allBlinders)
        {
            if (blinder.stats != null)
            {
                float distanceToPlayer = Vector3.Distance(blinder.transform.position, transform.position);

                if (distanceToPlayer <= blinder.stats.audioDetectionRange)
                {
                    blinder.OnDirectHit(transform.position);
                }
            }
        }
    }
    IEnumerator KnockdownTarget(GameObject target)
    {
        // Support A* et NavMesh
        EnemyAI_AStar zombieAI_AStar = target.GetComponent<EnemyAI_AStar>();
        EnemyAI zombieAI = target.GetComponent<EnemyAI>();

        // Desactiver pathfinding A* (sauf si en PitMode)
        if (zombieAI_AStar != null && !zombieAI_AStar.isInPitMode)
        {
            Pathfinding.AIPath aiPath = target.GetComponent<Pathfinding.AIPath>();
            if (aiPath != null)
            {
                aiPath.enabled = false;
            }
            zombieAI_AStar.enabled = false;
        }
        // Desactiver NavMesh
        else if (zombieAI != null)
        {
            UnityEngine.AI.NavMeshAgent agent = target.GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (agent != null && agent.isOnNavMesh)
            {
                agent.enabled = false;
            }
            zombieAI.enabled = false;
        }

        yield return new WaitForSeconds(2f);

        if (zombieAI_AStar != null && target != null)
        {
            if (zombieAI_AStar.isInPitMode)
            {
                // En PitMode : juste réactiver AI (AIPath reste disabled)
                zombieAI_AStar.enabled = true;
            }
            else
            {
                // Hors pit : réactiver AI + AIPath
                Pathfinding.AIPath aiPath = target.GetComponent<Pathfinding.AIPath>();
                if (aiPath != null)
                {
                    aiPath.enabled = true;
                }
                zombieAI_AStar.enabled = true;
            }
        }
        /*
        // Reactiver NavMesh
        else if (zombieAI != null && target != null)
        {
            UnityEngine.AI.NavMeshAgent agent = target.GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (agent != null)
            {
                agent.enabled = true;
            }
            zombieAI.enabled = true;
        }*/
    }

    public void OnGrabStart()
    {
        isGrabbed = true;

        if (isInWindup)
        {
            StopAllCoroutines();
            isAttacking = false;
            isInWindup = false;
            Debug.Log("BroomAttack: CANCELLED by grab during windup");
        }
    }

    public void OnGrabEnd()
    {
        isGrabbed = false;
        Debug.Log("BroomAttack: Player released, isGrabbed now FALSE");
    }

    public bool IsAttacking() => isAttacking;
}