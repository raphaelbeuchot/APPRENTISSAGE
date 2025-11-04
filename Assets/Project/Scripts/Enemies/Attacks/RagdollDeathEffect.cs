using UnityEngine;

public class RagdollDeathEffect : MonoBehaviour, IDeathEffect
{
    [HideInInspector] public float baseRagdollForce = 5f;
    [HideInInspector] public float ragdollTorque = 10f;
    [HideInInspector] public float meleeMultiplier = 1f;
    [HideInInspector] public float sentinelMultiplier = 1.5f;

    public void OnDeath(Vector3 deathPosition, DeathContext context)
    {
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            Debug.LogWarning($"RagdollDeathEffect: No Rigidbody on {gameObject.name}");
            return;
        }

        rb.isKinematic = false;
        rb.useGravity = true;
        rb.constraints = RigidbodyConstraints.None;

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

        rb.AddForce(direction * force, ForceMode.VelocityChange);
        rb.AddTorque(Random.insideUnitSphere * ragdollTorque, ForceMode.VelocityChange);

        Debug.Log($"Ragdoll applied: {context.deathType}, Force: {force}");
    }
}