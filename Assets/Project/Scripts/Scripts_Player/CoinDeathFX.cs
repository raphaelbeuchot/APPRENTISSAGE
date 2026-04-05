using UnityEngine;
using System.Collections;

public class CoinDeathFX : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private float lifetime = 3.5f;
    [SerializeField] private float fadeFraction = 0.3f;

    private Rigidbody rb;
    private SpriteRenderer spriteRenderer;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    public void Launch(Vector3 velocity)
    {
        if (rb != null)
            rb.linearVelocity = velocity;

        StartCoroutine(LifetimeRoutine());
    }

    private IEnumerator LifetimeRoutine()
    {
        float elapsed = 0f;
        float fadeStart = lifetime * (1f - fadeFraction);

        while (elapsed < lifetime)
        {
            elapsed += Time.deltaTime;

            if (elapsed > fadeStart && spriteRenderer != null)
            {
                float t = (elapsed - fadeStart) / (lifetime - fadeStart);
                Color c = spriteRenderer.color;
                c.a = Mathf.Lerp(1f, 0f, t);
                spriteRenderer.color = c;
            }

            yield return null;
        }

        Destroy(gameObject);
    }
}