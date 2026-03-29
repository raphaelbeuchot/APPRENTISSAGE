using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class ExplosionVisual : MonoBehaviour
{
    public enum EffectOrientation { None, FlatXZ, Billboard }

    [System.Serializable]
    public class ExplosionEffect
    {
        public bool enabled = true;
        public GameObject prefab;
        public EffectOrientation orientation = EffectOrientation.None;
        public float spawnHeightOffset = 0f;
    }

    [Header("Effets")]
    [SerializeField] private List<ExplosionEffect> effects = new List<ExplosionEffect>();

    [Header("Legacy Sphere")]
    [SerializeField] private bool useLegacySphere = true;
    [SerializeField] private float legacyStartRadius = 0.25f;
    [SerializeField] private float legacyTargetRadius = 4f;
    [SerializeField] private float legacyDuration = 0.4f;
    [SerializeField] private Material legacyMaterial;

    private Vector3 spawnPosition;

    public void Play(Vector3 position)
    {
        spawnPosition = position;

        foreach (ExplosionEffect effect in effects)
        {
            if (!effect.enabled || effect.prefab == null) continue;

            Vector3 pos = new Vector3(spawnPosition.x, spawnPosition.y + effect.spawnHeightOffset, spawnPosition.z);
            Quaternion rot = Quaternion.identity;

            if (effect.orientation == EffectOrientation.FlatXZ)
                rot = Quaternion.Euler(90f, 0f, 0f);

            GameObject go = Instantiate(effect.prefab, pos, rot);

            if (effect.orientation == EffectOrientation.Billboard)
            {
                if (go.GetComponent<BillboardRenderer>() == null)
                    go.AddComponent<BillboardRenderer>();
            }
        }

        if (useLegacySphere)
            StartCoroutine(ExpandCoroutine());
    }

    IEnumerator ExpandCoroutine()
    {
        float targetDiameter = legacyTargetRadius * 2f;
        float startDiameter = legacyStartRadius * 2f;

        GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Destroy(sphere.GetComponent<Collider>());
        sphere.transform.SetParent(transform);
        sphere.transform.localPosition = Vector3.zero;
        sphere.transform.localScale = Vector3.one * startDiameter;

        Renderer rend = sphere.GetComponent<Renderer>();
        if (legacyMaterial != null)
            rend.material = Instantiate(legacyMaterial);

        float elapsed = 0f;
        while (elapsed < legacyDuration)
        {
            float t = elapsed / legacyDuration;
            float diameter = Mathf.Lerp(startDiameter, targetDiameter, t);
            sphere.transform.localScale = Vector3.one * diameter;
            elapsed += Time.deltaTime;
            yield return null;
        }

        Destroy(gameObject);
    }
}