using UnityEngine;

public enum ImpactSource
{
    Broom,
    Collision
}

public class PhysicsProp : MonoBehaviour
{
    [Header("Explosion")]
    [SerializeField] private float explosionForceMultiplier = 1f;
    [SerializeField] private float explosionUpwardModifier = 0.5f;

    [Header("Impact Multipliers")]
    [SerializeField] private float broomMultiplier = 1f;
    [SerializeField] private float broomTorque = 3f;
    [SerializeField] private float collisionMultiplier = 0.6f;

    [Header("Impact")]
    [SerializeField] private float upwardBias = 0.25f;
    [SerializeField] private float minImpactForce = 3f;

    [Header("Physics")]
    [SerializeField] private float linearDamping = 2f;
    [SerializeField] private float angularDamping = 2f;
    [SerializeField] private float gravityMultiplier = 3f;


    private Rigidbody rb;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (rb == null)
            rb = gameObject.AddComponent<Rigidbody>();

        rb.isKinematic = false;
        rb.linearDamping = linearDamping;
        rb.angularDamping = angularDamping;
        rb.Sleep();
    }

    void Update()
    {
        if (!rb.IsSleeping())
            rb.AddForce(Physics.gravity * (gravityMultiplier - 1f) * rb.mass);
    }

    public void ReceiveExplosion(Vector3 center, float force, float radius)
    {
        rb.WakeUp();
        rb.AddExplosionForce(force * explosionForceMultiplier, center, radius, explosionUpwardModifier, ForceMode.Impulse);
    }

    public void ReceiveImpact(Vector3 origin, float force, ImpactSource source)
    {
        float multiplier = source switch
        {
            ImpactSource.Broom => broomMultiplier,
            ImpactSource.Collision => collisionMultiplier,
            _ => 1f
        };

        rb.WakeUp();
        Vector3 dir = (transform.position - origin).normalized;
        dir.y = upwardBias;
        dir.Normalize();
        rb.AddForce(dir * force * multiplier, ForceMode.Impulse);
        if (source == ImpactSource.Broom)
        {
            Vector3 torqueAxis = Vector3.Cross(Vector3.up, dir);
            rb.AddTorque(torqueAxis * broomTorque, ForceMode.Impulse);
        }
    }

    void OnCollisionEnter(Collision collision)
    {
        Rigidbody other = collision.rigidbody;
        if (other == null) return;

        float impactForce = other.mass * collision.relativeVelocity.magnitude;
        if (impactForce < minImpactForce) return;

        rb.WakeUp();
        Vector3 dir = (transform.position - collision.contacts[0].point).normalized;
        dir.y = upwardBias;
        dir.Normalize();
        rb.AddForce(dir * impactForce * collisionMultiplier, ForceMode.Impulse);
    }
}