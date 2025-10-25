using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Rigidbody))]
public class ZombieAI : EnemyAI
{
    [Header("References Zombie")]
    private ZombieHealth health;
    private ZombieGrabSystem grabSystem;

    private bool isStunnedByShot = false;

    protected override void Start()
    {
        base.Start(); // appelle EnemyAI.Start()

        health = GetComponent<ZombieHealth>();
        grabSystem = GetComponent<ZombieGrabSystem>();

        if (stats == null)
        {
            Debug.LogError("ZombieStats non assigné sur " + gameObject.name);
            return;
        }

        currentSpeed = stats.walkSpeed;
    }

    protected override void Update()
    {
        if (stats == null) return;
        if (health != null && health.IsDead()) { StopMovement(); currentState = State.Dead; return; }
        if (gameManager != null && gameManager.zombieStunBySentinel == true) { StopMovement(); return; }
        if (grabSystem != null && grabSystem.IsGrabbing()) { StopMovement(); return; }

        base.Update(); // conserve Idle, Wander, Chase du parent
    }

    protected override void HandleAttackingState()
    {
        if (targetHuman == null) { currentState = State.Idle; return; }

        float distance = Vector3.Distance(transform.position, targetHuman.position);
        if (distance > stats.grabRange * 1.5f)
        {
            currentState = State.Chasing;
            return;
        }

        // Oriente le zombie vers sa cible
        Vector3 direction = (targetHuman.position - transform.position).normalized;
        direction.y = 0;
        if (direction.magnitude > 0.1f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * stats.rotationSpeed);
        }

        // Tentative d’attaque (Grab)
        if (Time.time - lastAttackTime >= stats.knockbackGracePeriod)
        {
            if (grabSystem != null && grabSystem.CanGrab())
                AttemptAttack();
        }
        else
        {
            StopMovement();
        }
    }

    private void AttemptAttack()
    {
        if (targetHuman == null) return;

        lastAttackTime = Time.time;

        if (grabSystem != null && grabSystem.CanGrab())
        {
            grabSystem.AttemptGrab(targetHuman.gameObject);
        }
        else if (grabSystem == null)
        {
            // fallback : morsure directe
            PlayerHealth humanHealth = targetHuman.GetComponent<PlayerHealth>();
            if (humanHealth != null && !humanHealth.IsDead())
            {
                humanHealth.TakeDamage(stats.biteDamage);
                Debug.Log(gameObject.name + " attacked " + targetHuman.name + "!");
            }
        }
    }

    public void UpdateSpeed(float currentHealth, bool isCrawler)
    {
        if (stats == null) return;

        bool isChasing = (currentState == State.Chasing || currentState == State.Attacking);
        currentSpeed = stats.GetAdjustedSpeed(currentHealth, isChasing, isCrawler);
        Debug.Log(gameObject.name + " speed updated: " + currentSpeed);
    }

    protected override void OnDrawGizmosSelected()
    {
        base.OnDrawGizmosSelected();

        if (targetHuman != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(transform.position, targetHuman.position);
        }

        if (grabSystem != null && !grabSystem.CanGrab())
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawCube(transform.position + Vector3.up * 2.5f, Vector3.one * 0.3f);
        }
    }
}
