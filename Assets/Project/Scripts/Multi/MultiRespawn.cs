using System.Collections;
using UnityEngine;

// Multi : a la mort du joueur, le remet debout au point de respawn (devant le pupitre, comme le restart solo).
// A poser sur chaque joueur. Pas de penalite : revenir au debut est deja la sanction.
// Les pieces / la barre de credits de DeathSequence sont a laisser vides dans l'Inspector en multi.
public class MultiRespawn : MonoBehaviour
{
    [Tooltip("Point de respawn (position + orientation). Un vide place devant le pupitre, tourne vers lui.")]
    [SerializeField] private Transform respawnPoint;
    [SerializeField] private float respawnDelay = 1f;

    private PlayerHealth health;
    private PlayerRagdoll ragdoll;
    private DeathSequence deathSequence;
    private PlayerPhysicsMovement movement;
    private Rigidbody body;
    private SentinelCycleManager cycle;

    // Immunite : uniquement si le respawn a lieu pendant le Red Light du kill, pas sur les cycles suivants.
    private bool wasDead;
    private bool diedInRedLight;

    private void Awake()
    {
        health = GetComponent<PlayerHealth>();
        ragdoll = GetComponentInChildren<PlayerRagdoll>();
        deathSequence = GetComponent<DeathSequence>();
        movement = GetComponent<PlayerPhysicsMovement>();
        body = GetComponent<Rigidbody>();
        cycle = FindFirstObjectByType<SentinelCycleManager>();
    }

    private void OnEnable()
    {
        if (health != null)
            health.OnDeath += HandleDeath;
        SentinelCycleManager.OnCycleChanged += HandleCycleChanged;
    }

    private void OnDisable()
    {
        if (health != null)
            health.OnDeath -= HandleDeath;
        SentinelCycleManager.OnCycleChanged -= HandleCycleChanged;
        if (movement != null)
            movement.respawnImmune = false;
    }

    // Releve le moment exact du kill (OnDeath n'arrive qu'apres le ragdoll)
    private void Update()
    {
        bool dead = health != null && health.IsDead();
        if (dead && !wasDead)
            diedInRedLight = cycle != null && cycle.IsInRedLight();
        wasDead = dead;
    }

    // Tout changement d'etat du cycle : le Red Light du kill est termine
    private void HandleCycleChanged(SentinelCycleManager.GameState state)
    {
        diedInRedLight = false;
        if (movement != null)
            movement.respawnImmune = false;
    }

    private void HandleDeath()
    {
        StartCoroutine(RespawnRoutine());
    }

    private IEnumerator RespawnRoutine()
    {
        yield return new WaitForSeconds(respawnDelay);

        if (respawnPoint == null)
        {
            Debug.LogWarning($"[MultiRespawn] {name} : pas de respawnPoint, respawn annule");
            yield break;
        }

        if (ragdoll != null) ragdoll.Deactivate();

        if (body != null)
        {
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.position = respawnPoint.position;
            body.rotation = respawnPoint.rotation;
        }
        transform.SetPositionAndRotation(respawnPoint.position, respawnPoint.rotation);

        // La sentinelle detecte le mouvement par difference de position entre deux scans :
        // sans ca, la teleportation depuis le lieu de mort passe pour un deplacement et declenche un tir.
        foreach (SentinelDetector detector in FindObjectsByType<SentinelDetector>(FindObjectsSortMode.None))
        {
            detector.trackedTargets.Remove(gameObject);
            detector.alreadyShot.Remove(gameObject);
        }

        if (deathSequence != null) deathSequence.Restore();
        if (health != null) health.Revive();

        if (movement != null)
        {
            // Un solo recharge la scene (etat neuf) ; ici on reutilise l'objet, donc on remet a zero
            // ce que la mort n'a pas annule : la glace (pas de OnCollisionExit quand le collider est coupe) et l'accroupi.
            movement.SetSlippery(false, 0f, 0f, 0f);
            movement.ExitCrouch();
            movement.ResetAllInputs();
            movement.enabled = true;
            movement.canMove = true;
            movement.respawnImmune = diedInRedLight && cycle != null && cycle.IsInRedLight();
        }
        diedInRedLight = false;

        Debug.Log($"[MultiRespawn] {name} respawn");
    }
}
