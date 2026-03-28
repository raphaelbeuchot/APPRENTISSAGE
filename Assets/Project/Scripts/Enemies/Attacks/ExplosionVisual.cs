using UnityEngine;
using System.Collections;

public class ExplosionVisual : MonoBehaviour
{
    public void Play(float startDiameter, float targetRadius, float duration, Material mat)
    {
        StartCoroutine(ExpandCoroutine(startDiameter, targetRadius, duration, mat));
    }

    IEnumerator ExpandCoroutine(float startDiameter, float targetRadius, float duration, Material mat)
    {
        float targetDiameter = targetRadius * 2f;

        GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Destroy(sphere.GetComponent<Collider>());
        sphere.transform.SetParent(transform);
        sphere.transform.localPosition = Vector3.zero;
        sphere.transform.localScale = Vector3.one * startDiameter;

        Renderer rend = sphere.GetComponent<Renderer>();
        if (mat != null)
            rend.material = Instantiate(mat);

        float elapsed = 0f;
        while (elapsed < duration)
        {
            float t = elapsed / duration;
            float diameter = Mathf.Lerp(startDiameter, targetDiameter, t);
            sphere.transform.localScale = Vector3.one * diameter;
            elapsed += Time.deltaTime;
            yield return null;
        }

        Destroy(gameObject);
    }
}