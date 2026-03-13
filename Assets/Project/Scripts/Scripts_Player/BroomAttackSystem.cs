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
                hitSomething = true;

                if (broomImpactEffects != null && broomImpactEffects.Length > 0)
                {
                    int randomIndex = Random.Range(0, broomImpactEffects.Length);
                    Instantiate(broomImpactEffects[randomIndex], hit.bounds.center, Quaternion.identity);
                }

                // CALCULER DIRECTION KNOCKBACK D'ABORD
                Vector3 knockbackDir = (hit.transform.position - transform.position).normalized;
                knockbackDir.y = 0;

                // Knockback (applique a TOUS les zombies, Blinder inclus)
                Rigidbody targetRb = hit.GetComponent<Rigidbody>();
                if (targetRb != null)
                {
                    targetRb.AddForce(knockbackDir * stats.broomKnockbackForce, ForceMode.VelocityChange);

                    // ACTIVER LE FLAG KNOCKBACK
                    enemyHealth.SetKnockbackState(0.8f);
                }

                // Degats
                enemyHealth.TakeMeleeDamage(EnemyHealth.AttackType.Broom);

                // ANNULER WINDUP GRAB SI EN COURS
                GrabAttack grab = enemyHealth.GetComponent<GrabAttack>();
                if (grab != null && grab.isInWindup)
                {
                    grab.CancelWindup();
                    Debug.Log($"[BROOM] Cancelled {enemyHealth.gameObject.name} grab windup");
                }

                // ANNULER ROTATION TO IMPACT SI EN COURS
                EnemyAI_AStar zombieAI = hit.GetComponent<EnemyAI_AStar>();
                if (zombieAI != null && zombieAI.currentState == EnemyAI_AStar.State.RotatingToImpact)
                {
                    zombieAI.CancelRotationToImpact();
                    Debug.Log($"[BROOM] Cancelled {enemyHealth.gameObject.name} rotation to impact");
                }

                // Son d'impact individuel
                if (audioSource != null && stats.broomHitSound != null)
                {
                    audioSource.PlayOneShot(stats.broomHitSound);
                }

                // Knockdown (TOUS les zombies, Blinders inclus)
                EnemyAI_AStar zombieAI_AStar_temp = hit.GetComponent<EnemyAI_AStar>();
                if (zombieAI_AStar_temp != null)
                {
                    zombieAI_AStar_temp.enabled = false;
                }

                // Désactiver aussi BlinderWanderBehavior si présent
                BlinderWanderBehavior wanderBehavior = hit.GetComponent<BlinderWanderBehavior>();
                if (wanderBehavior != null)
                {
                    wanderBehavior.StopWandering();
                }

                MeleeAudioManager.TriggerMeleeHit(hit.transform.position);
                StartCoroutine(KnockdownTarget(hit.gameObject, knockbackDir));
            }

            // SWARMS
            SwarmController_AStar swarmAStar = hit.GetComponent<SwarmController_AStar>();
            if (swarmAStar != null)
            {
                hitSomething = true;
                
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
                    hitSomething = true;
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
        if (!hitSomething)
        {
            if (audioSource != null && stats.broomSound != null)
                audioSource.PlayOneShot(stats.broomSound);
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
                {
                    aiPath.enabled = true;
                }
                zombieAI_AStar.enabled = true;
            }
            zombieAI_AStar.lastPathDestination = Vector3.positiveInfinity;
            // SUPPRIMER : zombieAI_AStar.DetectHumans();
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