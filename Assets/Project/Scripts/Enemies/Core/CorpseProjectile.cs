using UnityEngine;

public class CorpseProjectile : MonoBehaviour
{
    [System.Serializable]
    public class CorpseData
    {
        public float projectionMass = 5f;
        public float projectionDrag = 0.5f;
        public float restingMass = 1f;
        public float restingDrag = 3f;
        public float velocityThreshold = 0.5f;
    }

    [Header("Physique")]
    [SerializeField] public CorpseData corpseData;

    private TutoFreezeTile tile;
    private Rigidbody rb;
    private bool hasHitPlayer = false;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    public void Init(TutoFreezeTile sourceTile, Vector3 force)
    {
        tile = sourceTile;

        if (rb != null)
        {
            rb.mass = corpseData.projectionMass;
            rb.linearDamping = corpseData.projectionDrag;
        }

        CorpseRagdoll ragdoll = GetComponent<CorpseRagdoll>();
        if (ragdoll != null)
            ragdoll.Launch(force);
        else if (rb != null)
            rb.AddForce(force, ForceMode.Impulse);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (hasHitPlayer) return;

        if (collision.collider.CompareTag("Player"))
        {
            hasHitPlayer = true;

            if (tile != null)
                tile.OnCorpseHitPlayer();
        }
    }
}