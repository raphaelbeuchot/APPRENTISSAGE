using UnityEngine;

public class RagdollDeathEffect : MonoBehaviour, IDeathEffect
{
    [HideInInspector] public float baseRagdollForce = 5f;
    [HideInInspector] public float ragdollTorque = 10f;
    [HideInInspector] public float meleeMultiplier = 1f;
    [HideInInspector] public float sentinelMultiplier = 1.5f;

    public void OnDeath(Vector3 deathPosition, DeathContext context)
    {
        Debug.Log($"========== RAGDOLL ONDEATH on {gameObject.name} ==========");

        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            Debug.LogError($"NO RIGIDBODY on {gameObject.name}!");
            return;
        }

        Debug.Log($"[Ragdoll Before] isKinematic={rb.isKinematic}, constraints={rb.constraints}");

        rb.isKinematic = false;
        rb.useGravity = true;
        rb.constraints = RigidbodyConstraints.None;

        Debug.Log($"[Ragdoll After] isKinematic={rb.isKinematic}, constraints={rb.constraints}");

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

        Debug.Log($"Applying force: {force}, direction: {direction}");

        rb.AddForce(direction * force, ForceMode.VelocityChange);
        rb.AddTorque(Random.insideUnitSphere * ragdollTorque, ForceMode.VelocityChange);

        Debug.Log($"Force applied! Velocity: {rb.linearVelocity}");
    }
}