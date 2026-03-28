using UnityEngine;
using System.Collections;

public class RingShrink : MonoBehaviour
{
    public void Play(float startDiameter, float endDiameter, float duration)
    {
        StartCoroutine(ShrinkCoroutine(startDiameter, endDiameter, duration));
    }

    IEnumerator ShrinkCoroutine(float startDiameter, float endDiameter, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            float t = elapsed / duration;
            float diameter = Mathf.Lerp(startDiameter, endDiameter, t);
            transform.localScale = Vector3.one * diameter;
            elapsed += Time.deltaTime;
            yield return null;
        }
        Destroy(gameObject);
    }
}