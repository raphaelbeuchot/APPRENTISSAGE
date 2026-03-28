using UnityEngine;
using System.Collections;

public class ExplosionVisual : MonoBehaviour
{
    private Vector3 spawnPosition;

    public void Play(float startDiameter, float targetRadius, float duration, Material mat, float ringStartDiameter, float ringEndDiameter, float ringShrinkDuration, float ringSpawnHeight, Vector3 position, GameObject ringPrefab)
    {
        spawnPosition = position;
        StartCoroutine(ExpandCoroutine(startDiameter, targetRadius, duration, mat));
        StartCoroutine(ShrinkRingCoroutine(ringStartDiameter, ringEndDiameter, ringShrinkDuration, ringSpawnHeight, ringPrefab));
    }

    IEnumerator ShrinkRingCoroutine(float startDiameter, float endDiameter, float duration, float spawnHeight, GameObject ringPrefab)
    {
        if (ringPrefab == null) yield break;

        Vector3 pos = new Vector3(spawnPosition.x, spawnHeight, spawnPosition.z);
        GameObject ring = Instantiate(ringPrefab, pos, Quaternion.Euler(90f, 0f, 0f));
        ring.transform.localScale = Vector3.one * startDiameter;

        RingShrink shrink = ring.GetComponent<RingShrink>();
        if (shrink != null)
            shrink.Play(startDiameter, endDiameter, duration);

        yield break;
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