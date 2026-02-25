using System.Collections;
using UnityEngine;

public class CanyonTile : MonoBehaviour
{
    [Header("Shake")]
    public float shakeDuration = 1f;
    public float shakeIntensity = 0.05f;

    [Header("Fall")]
    public float dropDepth = 5f;
    public float dropDuration = 0.5f;

    [Header("Rise")]
    public float riseDelay = 0f;
    public float riseDuration = 0.3f;

    private Vector3 originalPosition;
    private bool hasFallen = false;
    private bool isAnimating = false;

    private void Start()
    {
        originalPosition = transform.position;
    }

    private void OnCollisionEnter(Collision other)
    {
        if (other.gameObject.layer != LayerMask.NameToLayer("Human") &&
            other.gameObject.layer != LayerMask.NameToLayer("Zombie")) return; if (hasFallen || isAnimating) return;
        StartCoroutine(ShakeAndFall());
    }

    private IEnumerator ShakeAndFall()
    {
        isAnimating = true;

        float elapsed = 0f;
        while (elapsed < shakeDuration)
        {
            elapsed += 0.05f;
            float offsetX = Random.Range(-shakeIntensity, shakeIntensity);
            float offsetZ = Random.Range(-shakeIntensity, shakeIntensity);
            transform.position = originalPosition + new Vector3(offsetX, 0f, offsetZ);
            yield return new WaitForSeconds(0.05f);
        }

        transform.position = originalPosition;

        yield return StartCoroutine(Fall());
    }

    private IEnumerator Fall()
    {
        hasFallen = true;
        Vector3 startPos = originalPosition;
        Vector3 endPos = originalPosition + Vector3.down * dropDepth;

        float elapsed = 0f;
        while (elapsed < dropDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / dropDuration;
            t = t * t;
            transform.position = Vector3.Lerp(startPos, endPos, t);
            yield return null;
        }

        transform.position = endPos;
        isAnimating = false;
    }

    public void TriggerRise()
    {
        StartCoroutine(Rise());
    }

    private IEnumerator Rise()
    {
        yield return new WaitForSeconds(riseDelay);

        hasFallen = false;

        Vector3 startPos = transform.position;
        float elapsed = 0f;
        while (elapsed < riseDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / riseDuration;
            t = 1f - (1f - t) * (1f - t);
            transform.position = Vector3.Lerp(startPos, originalPosition, t);
            yield return null;
        }

        transform.position = originalPosition;
    }
}