using UnityEngine;
using System.Collections;

public class ExplosionVisual : MonoBehaviour
{
    private Vector3 spawnPosition;

    public void Play(float startRadius, float targetRadius, float duration, Material mat, float ringSpawnHeight, Vector3 position, GameObject ringPrefab)
    {
        spawnPosition = position;
        StartCoroutine(ExpandCoroutine(startRadius, targetRadius, duration, mat));
        if (ringPrefab != null)
        {
            Vector3 pos = new Vector3(spawnPosition.x, ringSpawnHeight, spawnPosition.z);
            Instantiate(ringPrefab, pos, Quaternion.Euler(90f, 0f, 0f));
        }
    }

    IEnumerator ExpandCoroutine(float startRadius, float targetRadius, float duration, Material mat)
    {
        float targetDiameter = targetRadius * 2f;
        float startDiameter = startRadius * 2f;
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