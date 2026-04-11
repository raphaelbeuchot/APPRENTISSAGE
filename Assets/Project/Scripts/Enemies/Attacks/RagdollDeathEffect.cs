using UnityEngine;
using System.Collections;

public class RagdollDeathEffect : MonoBehaviour, IDeathEffect
{
    [HideInInspector] public float baseRagdollForce = 5f;
    [HideInInspector] public float ragdollTorque = 10f;
    [HideInInspector] public float meleeMultiplier = 1f;
    [HideInInspector] public float sentinelMultiplier = 1.5f;
    [HideInInspector] public EnemyStats.CorpseData corpseData;

    private Rigidbody rootRb;
    private Animator animator;
    private Rigidbody[] boneRigidbodies;
    private Collider[] boneColliders;
    public Rigidbody hipsRb;

    public void InitializeRagdoll()
    {
        rootRb = GetComponent<Rigidbody>();
        animator = GetComponent<Animator>();
        boneRigidbodies = GetComponentsInChildren<Rigidbody>();
        boneColliders = GetComponentsInChildren<Collider>();

        foreach (Rigidbody bone in boneRigidbodies)
        {
            if (bone == rootRb) continue;
            bone.isKinematic = true;
        }

        foreach (Collider col in boneColliders)
        {
            if (col == GetComponent<Collider>()) continue;
            col.enabled = false;
        }
    }

    public void OnDeath(Vector3 deathPosition, DeathContext context)
    {
        EnemyAI_AStar enemyAI = GetComponent<EnemyAI_AStar>();
        if (enemyAI != null && enemyAI.isOnRotatingPlatform && enemyAI.currentRotatingPlatform != null)
            enemyAI.currentRotatingPlatform.RemoveCorpse(GetComponent<RagdollDeathEffect>());

        if (animator != null)
            animator.enabled = false;

        if (rootRb != null)
        {
            rootRb.isKinematic = true;
            rootRb.linearVelocity = Vector3.zero;
            Collider rootCollider = GetComponent<Collider>();
            if (rootCollider != null)
                rootCollider.enabled = false;
        }


        foreach (Rigidbody bone in boneRigidbodies)
        {
            if (bone == rootRb) continue;
            bone.isKinematic = false;
            bone.mass = corpseData.projectionMass / Mathf.Max(1, boneRigidbodies.Length - 1);
            bone.linearDamping = corpseData.projectionDrag;
            bone.collisionDetectionMode = CollisionDetectionMode.Continuous;
        }

        foreach (Collider col in boneColliders)
        {
            if (col == GetComponent<Collider>()) continue;
            col.enabled = true;
        }

        Transform hipsTransform = transform.Find("TPose/Armature/mixamorig:Hips");
        if (hipsTransform != null)
            hipsRb = hipsTransform.GetComponent<Rigidbody>();

        if (hipsRb == null && boneRigidbodies.Length > 1)
            hipsRb = boneRigidbodies[1];

        if (hipsRb != null)
        {
            float force = baseRagdollForce;
            switch (context.deathType)
            {
                case DeathContext.DeathType.Melee:
                    force *= meleeMultiplier;
                    break;
                case DeathContext.DeathType.Sentinel:
                    force *= sentinelMultiplier;
                    break;
            }

            Vector3 direction = context.impactDirection.normalized + Vector3.up * 0.5f;
            direction.Normalize();

            hipsRb.AddForce(direction * force, ForceMode.VelocityChange);
            hipsRb.AddTorque(Random.insideUnitSphere * ragdollTorque, ForceMode.VelocityChange);
        }
        CorpsePitHandler handler = GetComponent<CorpsePitHandler>();
        if (handler == null)
            handler = gameObject.AddComponent<CorpsePitHandler>();
        handler.sourceEnemy = GetComponent<EnemyHealth>();
        StartCoroutine(WaitForRestCoroutine());
    }

    private IEnumerator WaitForRestCoroutine()
    {
        yield return new WaitForSeconds(0.5f);

        while (hipsRb != null && hipsRb.linearVelocity.magnitude > corpseData.velocityThreshold)
            yield return new WaitForSeconds(0.1f);

        foreach (Rigidbody bone in boneRigidbodies)
        {
            if (bone == rootRb) continue;
            bone.mass = corpseData.restingMass / Mathf.Max(1, boneRigidbodies.Length - 1);
            bone.linearDamping = corpseData.restingDrag;
        }

        DeadBodyPhysics deadBody = GetComponent<DeadBodyPhysics>();
        if (deadBody != null)
            deadBody.Activate(corpseData);
    }
}