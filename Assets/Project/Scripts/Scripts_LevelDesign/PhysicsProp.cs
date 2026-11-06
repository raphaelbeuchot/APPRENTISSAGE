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
    [Tooltip("Coup de balai pousse au point touche (le prop bascule selon l'endroit frappe) au lieu du centre + Broom Torque fixe")]
    [SerializeField] private bool pushAtHitPoint = false;
    [SerializeField] private float collisionMultiplier = 0.6f;

    [Header("Impact")]
    [SerializeField] private float upwardBias = 0.25f;
    [SerializeField] private float minImpactForce = 3f;

    [Header("Physics")]
    [SerializeField] private float linearDamping = 2f;
    [SerializeField] private float angularDamping = 2f;
    [SerializeField] private float gravityMultiplier = 3f;

    [Header("Audio")]
    [Tooltip("Son quand le balai frappe ce prop. Vide = pas de son (et le balai joue son son de coup rate)")]
    [SerializeField] private AudioClip broomHitSound;
    [Range(0f, 1f)]
    [SerializeField] private float broomHitVolume = 1f;
    [Tooltip("0 = 2D (meme volume partout, comme les sons du balai), 1 = 3D (depuis le prop)")]
    [Range(0f, 1f)]
    [SerializeField] private float spatialBlend = 0f;

    public bool HasBroomHitSound => broomHitSound != null;

    private Rigidbody rb;
    private AudioSource audioSource;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (rb == null)
            rb = gameObject.AddComponent<Rigidbody>();

        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

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

    // hitPoint : point du collider touche (utilise seulement si pushAtHitPoint)
    public void ReceiveImpact(Vector3 origin, float force, ImpactSource source, Vector3? hitPoint = null)
    {
        float multiplier = source switch
        {
            ImpactSource.Broom => broomMultiplier,
            ImpactSource.Collision => collisionMultiplier,
            _ => 1f
        };

        if (source == ImpactSource.Broom)
            PlayBroomHitSound();

        rb.WakeUp();
        Vector3 dir = (transform.position - origin).normalized;
        dir.y = upwardBias;
        dir.Normalize();

        if (pushAtHitPoint && hitPoint.HasValue)
        {
            // La rotation vient du point d'application, pas de broomTorque
            rb.AddForceAtPosition(dir * force * multiplier, hitPoint.Value, ForceMode.Impulse);
            return;
        }

        rb.AddForce(dir * force * multiplier, ForceMode.Impulse);
        if (source == ImpactSource.Broom)
        {
            Vector3 torqueAxis = Vector3.Cross(Vector3.up, dir);
            rb.AddTorque(torqueAxis * broomTorque, ForceMode.Impulse);
        }
    }

    private void PlayBroomHitSound()
    {
        if (broomHitSound == null) return;

        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
        }
        audioSource.spatialBlend = spatialBlend;
        audioSource.PlayOneShot(broomHitSound, broomHitVolume);
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