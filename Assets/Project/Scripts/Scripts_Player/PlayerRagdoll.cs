using UnityEngine;

public class PlayerRagdoll : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Animator animator;
    [SerializeField] private Rigidbody mainRigidbody;
    [SerializeField] private Transform hipBone;

    [Header("Settings")]
    [SerializeField] private float deathImpulseForce = 4f;

    private Rigidbody[] boneRigidbodies;
    private Collider[] boneColliders;

    void Awake()
    {
        // GetComponentsInChildren inclut le root, on filtrera mainRigidbody
        boneRigidbodies = GetComponentsInChildren<Rigidbody>();
        boneColliders = GetComponentsInChildren<Collider>();

        SetRagdollActive(false);
    }

    public Rigidbody GetHipRigidbody()
    {
        if (hipBone != null)
        {
            Rigidbody hipRb = hipBone.GetComponent<Rigidbody>();
            if (hipRb != null) return hipRb;
        }
        return mainRigidbody;
    }

    public void Activate(Vector3 deathDirection)
    {
        if (animator != null)
            animator.enabled = false;

        if (mainRigidbody != null)
            mainRigidbody.isKinematic = true;

        Collider mainCollider = GetComponent<Collider>();
        if (mainCollider != null)
            mainCollider.enabled = false;

        SetRagdollActive(true);

        // Mettre les bones sur un layer ignore par la sentinel
        int ragdollLayer = LayerMask.NameToLayer("Default");
        foreach (Rigidbody rb in boneRigidbodies)
        {
            if (rb == mainRigidbody) continue;
            rb.gameObject.layer = ragdollLayer;
        }

        Rigidbody hipRb = GetHipRigidbody();
        if (hipRb != null)
        {
            Vector3 impulse = deathDirection.normalized * deathImpulseForce;
            impulse.y = Mathf.Max(impulse.y, 1.5f);
            hipRb.AddForce(impulse, ForceMode.Impulse);
        }
    }

    private void SetRagdollActive(bool active)
    {
        foreach (Rigidbody rb in boneRigidbodies)
        {
            if (rb == mainRigidbody) continue;
            rb.isKinematic = !active;
        }

        Collider mainCollider = GetComponent<Collider>();
        foreach (Collider col in boneColliders)
        {
            if (col == mainCollider) continue;
            col.enabled = active;
        }
    }
}