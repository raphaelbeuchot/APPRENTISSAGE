using UnityEngine;
using System.Collections;
using System;

public class BombProjectile : MonoBehaviour
{
    [Header("Launch")]
    [SerializeField] private float launchAngle = 45f;

    [Header("Fuse")]
    [SerializeField] private float fuseTimer = 3f;

    [Header("Explosion")]
    [SerializeField] private float explosionRadius = 4f;
    [SerializeField] private float knockbackForce = 10f;
    [SerializeField] private LayerMask explosionLayers;

    [Header("Collision")]
    [SerializeField] private LayerMask earlyDetonationLayers;

    [Header("Explosion Visuel")]
    [SerializeField] private float explosionStartDiameter = 0.5f;
    [SerializeField] private float explosionExpandDuration = 0.4f;
    [SerializeField] private Material explosionMaterial;

    private Rigidbody rb;
    private bool hasLaunched = false;
    private bool hasExploded = false;
    private Action onExplodedCallback;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;
    }

    public void Launch(Vector3 targetPos, Action callback)
    {
        onExplodedCallback = callback;
        hasLaunched = true;

        rb.isKinematic = false;

        // Calcul velocity en arc vers targetPos
        Vector3 direction = targetPos - transform.position;
        float horizontal = new Vector3(direction.x, 0f, direction.z).magnitude;
        float vertical = direction.y;
        float angle = launchAngle * Mathf.Deg2Rad;

        // Formule arc de cercle
        float v0 = Mathf.Sqrt((Physics.gravity.magnitude * horizontal * horizontal) /
                   (2f * Mathf.Cos(angle) * Mathf.Cos(angle) *
                   (horizontal * Mathf.Tan(angle) - vertical)));

        if (float.IsNaN(v0) || float.IsInfinity(v0))
        {
            // Fallback si calcul impossible
            v0 = 10f;
        }

        Vector3 horizontalDir = new Vector3(direction.x, 0f, direction.z).normalized;
        Vector3 launchVelocity = horizontalDir * v0 * Mathf.Cos(angle) + Vector3.up * v0 * Mathf.Sin(angle);
        rb.linearVelocity = launchVelocity;

        StartCoroutine(FuseCoroutine());
    }

    void OnCollisionEnter(Collision collision)
    {
        if (!hasLaunched || hasExploded) return;

        // Collision avec player ou ennemi -> detonation immediate
        int layer = collision.gameObject.layer;
        if (((1 << layer) & earlyDetonationLayers) != 0)
        {
            Explode();
        }
    }

    IEnumerator FuseCoroutine()
    {
        yield return new WaitForSeconds(fuseTimer);
        Explode();
    }

    void Explode()
    {
        if (hasExploded) return;
        hasExploded = true;

        Collider[] hits = Physics.OverlapSphere(transform.position, explosionRadius, explosionLayers);

        foreach (Collider hit in hits)
        {
            Vector3 dir = (hit.transform.position - transform.position).normalized;
            dir.y = 0.3f;
            dir.Normalize();

            // Player
            PlayerPhysicsMovement playerMovement = hit.GetComponent<PlayerPhysicsMovement>();
            if (playerMovement != null)
            {
                playerMovement.ApplyKnockback(dir * knockbackForce, 0.3f);
                continue;
            }

            // Ennemis
            Rigidbody hitRb = hit.GetComponent<Rigidbody>();
            if (hitRb != null)
            {
                hitRb.AddForce(dir * knockbackForce, ForceMode.VelocityChange);
            }
        }

        // Visual explosion
        GameObject visualGO = new GameObject("ExplosionVisual");
        visualGO.transform.position = transform.position;
        ExplosionVisual visual = visualGO.AddComponent<ExplosionVisual>();
        visual.Play(explosionStartDiameter, explosionRadius, explosionExpandDuration, explosionMaterial);

        onExplodedCallback?.Invoke();
        Destroy(gameObject);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, explosionRadius);
    }
}