using UnityEngine;
using System.Collections;
using System;
using Pathfinding;

public class BombProjectile : MonoBehaviour
{
    [Header("Launch")]
    [SerializeField] private float launchAngle = 45f;
    [SerializeField] private GameObject broomImpactPrefab;

    [Header("Fuse")]
    [SerializeField] private float fuseTimer = 3f;

    [Header("Explosion")]
    [SerializeField] private float explosionRadius = 4f;
    [SerializeField] private float knockbackForce = 10f;
    [SerializeField] private LayerMask explosionLayers;

    [Header("Explosion Visuel")]
    [SerializeField] private GameObject explosionVisualPrefab;
    [SerializeField] private float ringSpawnHeight = 0.2f;

    [Header("Sons")]
    [SerializeField] private AudioClip launchSound;
    [SerializeField] private AudioClip explosionSound;
    [SerializeField] private AudioClip kickSound;

    [Header("Squash")]
    [SerializeField] private float minImpactForSquash = 2f;

    private AudioSource audioSource;
    private Rigidbody rb;
    private LineRenderer lineRenderer;
    private bool hasLaunched = false;
    private bool hasExploded = false;
    private bool isSquashing = false;
    private Action onExplodedCallback;
    private Transform target;
    private Vector3 originalScale;
    private Transform spawnerTransform;
    private float spawnerDetectionRadius = 0f;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.spatialBlend = 0f;
        originalScale = transform.localScale;
        target = GameObject.FindGameObjectWithTag("Player")?.transform;

        lineRenderer = GetComponent<LineRenderer>();
        if (lineRenderer == null)
            lineRenderer = gameObject.AddComponent<LineRenderer>();
        lineRenderer.positionCount = 2;
        lineRenderer.useWorldSpace = true;
        lineRenderer.startWidth = 0.15f;
        lineRenderer.endWidth = 0.15f;
        lineRenderer.enabled = false;
    }

    void Start()
    {
        StartCoroutine(SpawnPopCoroutine());
    }

    public void SetSpawner(Transform spawner, float radius)
    {
        spawnerTransform = spawner;
        spawnerDetectionRadius = radius;
    }

    void Update()
    {
        if (!hasLaunched || hasExploded || target == null || lineRenderer == null) return;

        float dist = Vector3.Distance(transform.position, target.position);
        if (dist > explosionRadius)
        {
            lineRenderer.enabled = false;
            return;
        }

        lineRenderer.enabled = true;
        lineRenderer.SetPosition(0, transform.position);
        lineRenderer.SetPosition(1, target.position + Vector3.up);
    }

    public void UpdateChargeLine(Vector3 playerPos, float t)
    {
        if (lineRenderer == null) return;
        lineRenderer.enabled = true;
        lineRenderer.SetPosition(0, transform.position);
        lineRenderer.SetPosition(1, Vector3.Lerp(transform.position, playerPos, t));
    }

    public void HideChargeLine()
    {
        if (lineRenderer == null) return;
        lineRenderer.enabled = false;
    }

    public void StartRetractLine(Vector3 currentEndPos, float duration)
    {
        StartCoroutine(RetractCoroutine(currentEndPos, duration));
    }

    IEnumerator RetractCoroutine(Vector3 endPos, float duration)
    {
        if (lineRenderer == null) yield break;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = 1f - (elapsed / duration);
            lineRenderer.SetPosition(0, transform.position);
            lineRenderer.SetPosition(1, Vector3.Lerp(transform.position, endPos, t));
            yield return null;
        }
        lineRenderer.enabled = false;
    }

    public void PlayChargeLoop(AudioClip clip)
    {
        if (audioSource == null) return;
        audioSource.clip = clip;
        audioSource.loop = true;
        audioSource.Play();
    }

    public void StopChargeLoop()
    {
        if (audioSource == null) return;
        audioSource.loop = false;
        audioSource.Stop();
    }

    public void PlayOneShot(AudioClip clip)
    {
        if (audioSource == null) return;
        audioSource.PlayOneShot(clip);
    }

    IEnumerator SpawnPopCoroutine()
    {
        Vector3 originalScaleLocal = transform.localScale;
        float duration = 0.2f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            float factor = t < 0.6f
                ? Mathf.Lerp(0f, 1.2f, t / 0.6f)
                : Mathf.Lerp(1.2f, 1f, (t - 0.6f) / 0.4f);
            transform.localScale = originalScaleLocal * factor;
            yield return null;
        }

        transform.localScale = originalScaleLocal;
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

    public void KickBack(Vector3 direction, float force)
    {
        if (!hasLaunched || hasExploded) return;
        if (audioSource != null && kickSound != null)
            audioSource.PlayOneShot(kickSound);
        if (broomImpactPrefab != null)
            Instantiate(broomImpactPrefab, transform.position, Quaternion.identity);
        rb.linearVelocity = Vector3.zero;
        rb.AddForce(direction * force, ForceMode.VelocityChange);
    }

    void OnCollisionEnter(Collision collision)
    {
        if (!hasLaunched || hasExploded || isSquashing) return;
        if (collision.gameObject.CompareTag("Player")) return;
        if (Mathf.Abs(collision.relativeVelocity.y) < minImpactForSquash) return;
        StartCoroutine(SquashCoroutine());
    }

    private IEnumerator SquashCoroutine()
    {
        isSquashing = true;

        float squashDuration = 0.08f;
        float stretchDuration = 0.06f;
        float restoreDuration = 0.1f;

        Vector3 squashed = new Vector3(originalScale.x * 1.4f, originalScale.y * 0.5f, originalScale.z * 1.4f);
        Vector3 stretched = new Vector3(originalScale.x * 0.75f, originalScale.y * 1.35f, originalScale.z * 0.75f);

        float elapsed = 0f;
        while (elapsed < squashDuration)
        {
            elapsed += Time.deltaTime;
            transform.localScale = Vector3.Lerp(originalScale, squashed, elapsed / squashDuration);
            yield return null;
        }

        elapsed = 0f;
        while (elapsed < stretchDuration)
        {
            elapsed += Time.deltaTime;
            transform.localScale = Vector3.Lerp(squashed, stretched, elapsed / stretchDuration);
            yield return null;
        }

        elapsed = 0f;
        while (elapsed < restoreDuration)
        {
            elapsed += Time.deltaTime;
            transform.localScale = Vector3.Lerp(stretched, originalScale, elapsed / restoreDuration);
            yield return null;
        }

        transform.localScale = originalScale;
        isSquashing = false;
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

        if (lineRenderer != null)
            lineRenderer.enabled = false;

        // Passe 1 : player et decor (layers existants)
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
                hitRb.AddForce(dir * knockbackForce, ForceMode.VelocityChange);
        }

        // Passe 2 : ennemis et destabilisables (sans restriction de layer)
        Collider[] blastHits = Physics.OverlapSphere(transform.position, explosionRadius);

        foreach (Collider hit in blastHits)
        {

            if (hit.CompareTag("Player")) continue;

            Vector3 dir = (hit.transform.position - transform.position).normalized;
            dir.y = 0.3f;
            dir.Normalize();

            EnemyHealth enemyHealth = hit.GetComponentInParent<EnemyHealth>();
            if (enemyHealth != null)
            {
                if (enemyHealth.IsDead()) continue;

                EnemyAI_AStar ai = hit.GetComponentInParent<EnemyAI_AStar>();
                AIPath enemyAiPath = hit.GetComponentInParent<AIPath>();

                if (enemyAiPath != null)
                    enemyAiPath.enabled = false;
                if (ai != null)
                    ai.enabled = false;

                Rigidbody enemyRb = hit.GetComponentInParent<Rigidbody>();
                if (enemyRb != null)
                {
                    enemyRb.linearVelocity = Vector3.zero;
                    enemyRb.AddForce(dir * knockbackForce, ForceMode.VelocityChange);
                }

                enemyHealth.SetKnockbackState(0.8f);

                if (ai != null)
                    ai.StartBlastStun(0.8f);

                continue;
            }

            DestabilizableObject destabilizable = hit.GetComponent<DestabilizableObject>();
            if (destabilizable != null)
            {
                destabilizable.TipOver(transform.position);
                continue;
            }

            PhysicsProp prop = hit.GetComponent<PhysicsProp>();
            if (prop != null)
            {
                prop.ReceiveExplosion(transform.position, knockbackForce, explosionRadius);
                continue;
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
    IEnumerator BlastStunCoroutine(EnemyAI_AStar ai)
    {
        AIPath aiPath = ai.GetAIPath();

        if (aiPath != null)
            aiPath.enabled = false;
        ai.enabled = false;

        yield return new WaitForSeconds(3f);

        if (ai == null) yield break;

        if (aiPath != null)
            aiPath.enabled = true;
        ai.enabled = true;
        ai.currentState = EnemyAI_AStar.State.Idle;
        ai.lastPathDestination = Vector3.positiveInfinity;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, explosionRadius);
    }
}