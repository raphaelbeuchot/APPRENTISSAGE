using UnityEngine;
using System.Collections;

public class RingShrink : MonoBehaviour
{
    [SerializeField] private float startScale = 5f;
    [SerializeField] private float endScale = 0.25f;
    [SerializeField] private float duration = 0.5f;

    void Start()
    {
        StartCoroutine(ShrinkCoroutine());
    }

    IEnumerator ShrinkCoroutine()
    {
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        float elapsed = 0f;

        transform.localScale = Vector3.one * startScale;

        while (elapsed < duration)
        {
            float t = elapsed / duration;
            float scale = Mathf.Lerp(startScale, endScale, t);
            transform.localScale = Vector3.one * scale;
            if (sr != null)
            {
                Color c = sr.color;
                c.a = Mathf.Lerp(1f, 0f, t);
                sr.color = c;
            }
            elapsed += Time.deltaTime;
            yield return null;
        }

        Destroy(gameObject);
    }
}