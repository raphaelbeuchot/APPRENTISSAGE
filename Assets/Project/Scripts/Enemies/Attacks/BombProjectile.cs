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

    [Header("Explosion Visuel")]
    [SerializeField] private GameObject explosionVisualPrefab;
    // [SerializeField] private float explosionStartRadius = 0.25f;
    // [SerializeField] private float explosionExpandDuration = 0.4f;
    // [SerializeField] private Material explosionMaterial;
    // [SerializeField] private float ringStartRadius = 5f;
    // [SerializeField] private float ringEndRadius = 0.25f;
    // [SerializeField] private float ringShrinkDuration = 0.5f;
    [SerializeField] private float ringSpawnHeight = 0.2f;
    // [SerializeField] private GameObject ringPrefab;

    [Header("Sons")]
    [SerializeField] private AudioClip launchSound;
    [SerializeField] private AudioClip explosionSound;
    private AudioSource audioSource;

    private Rigidbody rb;
    private bool hasLaunched = false;
    private bool hasExploded = false;
    private Action onExplodedCallback;
    private Transform target;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.spatialBlend = 0f;
    }

    void Start()
    {
        StartCoroutine(SpawnPopCoroutine());
    }

    IEnumerator SpawnPopCoroutine()
    {
        Vector3 originalScale = transform.localScale;
        float duration = 0.2f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            float factor = t < 0.6f
                ? Mathf.Lerp(0f, 1.2f, t / 0.6f)
                : Mathf.Lerp(1.2f, 1f, (t - 0.6f) / 0.4f);
            transform.localScale = originalScale * factor;
            yield return null;
        }

        transform.localScale = originalScale;
    }

    public void Launch(Transform target, Action callback)
    {
        this.target = target;
        onExplodedCallback = callback;
        hasLaunched = true;

        rb.isKinematic = false;

        Vector3 targetPos = target.position;
        Vector3 direction = targetPos - transform.position;
        float horizontal = new Vector3(direction.x, 0f, direction.z).magnitude;
        float vertical = direction.y;
        float angle = launchAngle * Mathf.Deg2Rad;

        float v0 = Mathf.Sqrt((Physics.gravity.magnitude * horizontal * horizontal) /
                   (2f * Mathf.Cos(angle) * Mathf.Cos(angle) *
                   (horizontal * Mathf.Tan(angle) - vertical)));

        if (float.IsNaN(v0) || float.IsInfinity(v0))
            v0 = 10f;

        Vector3 horizontalDir = new Vector3(direction.x, 0f, direction.z).normalized;
        Vector3 launchVelocity = horizontalDir * v0 * Mathf.Cos(angle) + Vector3.up * v0 * Mathf.Sin(angle);
        rb.linearVelocity = launchVelocity;

        if (audioSource != null && launchSound != null)
            audioSource.PlayOneShot(launchSound);

        StartCoroutine(FuseCoroutine());
    }

    // void OnCollisionEnter(Collision collision)
    // {
    //     if (!hasLaunched || hasExploded) return;
    //     int layer = collision.gameObject.layer;
    //     if (((1 << layer) & earlyDetonationLayers) != 0)
    //     {
    //         Explode();
    //     }
    // }

    public void KickBack(Vector3 direction, float force)
    {
        if (!hasLaunched || hasExploded) return;
        rb.linearVelocity = Vector3.zero;
        rb.AddForce(direction * force, ForceMode.VelocityChange);
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

            PlayerPhysicsMovement playerMovement = hit.GetComponent<PlayerPhysicsMovement>();
            if (playerMovement != null)
            {
                playerMovement.ApplyKnockback(dir * knockbackForce, 0.3f);
                continue;
            }

            Rigidbody hitRb = hit.GetComponent<Rigidbody>();
            if (hitRb != null)
            {
                hitRb.AddForce(dir * knockbackForce, ForceMode.VelocityChange);
            }
        }

        if (explosionVisualPrefab != null)
        {
            Vector3 groundPos = transform.position;
            RaycastHit groundHit;
            if (Physics.Raycast(transform.position, Vector3.down, out groundHit, 10f, LayerMask.GetMask("Ground")))
                groundPos = groundHit.point;

            GameObject visualGO = Instantiate(explosionVisualPrefab, groundPos, Quaternion.identity);
            ExplosionVisual visual = visualGO.GetComponent<ExplosionVisual>();
            if (visual != null)
                visual.Play(transform.position);
        }

        // GameObject visualGO = new GameObject("ExplosionVisual");
        // visualGO.transform.position = groundPos;
        // ExplosionVisual visual = visualGO.AddComponent<ExplosionVisual>();
        // visual.Play(explosionStartRadius, explosionRadius, explosionExpandDuration, explosionMaterial, ringStartRadius, ringEndRadius, ringShrinkDuration, ringSpawnHeight, transform.position, ringPrefab);

        onExplodedCallback?.Invoke();

        if (explosionSound != null)
        {
            GameObject tempAudio = new GameObject("ExplosionAudio");
            AudioSource tempSource = tempAudio.AddComponent<AudioSource>();
            tempSource.spatialBlend = 0f;
            tempSource.PlayOneShot(explosionSound);
            Destroy(tempAudio, explosionSound.length + 0.1f);
        }

        Destroy(gameObject);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, explosionRadius);
    }
}