using UnityEngine;

public class LightFade : MonoBehaviour
{
    public float fadeDuration = 0.15f;
    private Light lightComponent;
    private float startIntensity;
    private float elapsed = 0f;

    void Start()
    {
        lightComponent = GetComponent<Light>();
        if (lightComponent != null)
            startIntensity = lightComponent.intensity;
    }

    void Update()
    {
        if (lightComponent == null) return;

        elapsed += Time.deltaTime;
        float t = elapsed / fadeDuration;
        lightComponent.intensity = Mathf.Lerp(startIntensity, 0f, t);

        if (t >= 1f)
            lightComponent.enabled = false;
    }
}