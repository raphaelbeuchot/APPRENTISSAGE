using System.Collections;
using UnityEngine;

public class SentinelCentralLight : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Light centralLight;
    [SerializeField] private SentinelCycleManager sentinelCycleManager;

    [Header("GreenLight Settings")]
    [SerializeField] private Color greenLightColor = Color.green;
    [SerializeField] private float greenLightIntensity = 1f;
    [SerializeField] private float greenLightRange = 5f;

    [Header("Alert Settings")]
    [SerializeField] private Color alertColor = Color.yellow;
    [SerializeField] private float alertIntensity = 2f;
    [SerializeField] private float alertRange = 5f;

    [Header("RedLight Settings")]
    [SerializeField] private Color redLightColor = Color.red;
    [SerializeField] private float redLightIntensity = 3f;
    [SerializeField] private float redLightRange = 10f;

    [Header("Release Settings")]
    [SerializeField] private Color releaseColor = Color.white;
    [SerializeField] private float releaseIntensity = 1f;
    [SerializeField] private float releaseRange = 5f;

    [Header("Pattern Settings")]
    [SerializeField] private float pulseDuration = 0.5f;

    private Coroutine currentPatternCoroutine;

    void Start()
    {
        if (centralLight == null)
        {
            centralLight = GetComponent<Light>();
        }

        if (centralLight == null)
        {
            Debug.LogError("[SentinelCentralLight] Aucune Light trouvee!");
        }
        else
        {
            centralLight.enabled = false;
        }
    }

    public void StartGreenLightPattern()
    {
        Debug.Log("[SentinelCentralLight] StartGreenLightPattern CALLED");
        StopCurrentPattern();
        if (centralLight != null)
        {
            centralLight.enabled = true;
            Debug.Log($"[SentinelCentralLight] Light enabled! Color: {greenLightColor}, Intensity: {greenLightIntensity}, Range: {greenLightRange}");
        }
        else
        {
            Debug.LogError("[SentinelCentralLight] centralLight is NULL!");
        }
        ApplySettings(greenLightColor, greenLightIntensity, greenLightRange);
    }

    public void StartAlertPattern(float duration)
    {
        StopCurrentPattern();
        currentPatternCoroutine = StartCoroutine(AlertFallbackCoroutine(duration));
    }

    public void StartRedLightPattern()
    {
        StopCurrentPattern();
        currentPatternCoroutine = StartCoroutine(RedLightPulseCoroutine());
    }

    public void StartReleaseFade(float fadeDuration)
    {
        StopCurrentPattern();
        currentPatternCoroutine = StartCoroutine(ReleaseFadeCoroutine(fadeDuration));
    }

    private void StopCurrentPattern()
    {
        if (currentPatternCoroutine != null)
        {
            StopCoroutine(currentPatternCoroutine);
            currentPatternCoroutine = null;
        }
    }

    private void ApplySettings(Color color, float intensity, float range)
    {
        if (centralLight == null) return;

        centralLight.color = color;
        centralLight.intensity = intensity;
        centralLight.range = range;
    }

    private IEnumerator AlertFallbackCoroutine(float duration)
    {
        if (centralLight == null) yield break;

        centralLight.enabled = true;
        centralLight.color = alertColor;
        centralLight.range = alertRange;

        float elapsed = 0f;

        while (elapsed < duration)
        {
            float t = Mathf.PingPong(Time.time * 3f, 1f);
            centralLight.intensity = Mathf.Lerp(alertIntensity * 0.5f, alertIntensity * 1.5f, t);

            elapsed += Time.deltaTime;
            yield return null;
        }

        ApplySettings(alertColor, alertIntensity, alertRange);
    }

    private IEnumerator RedLightPulseCoroutine()
    {
        if (centralLight == null) yield break;

        centralLight.enabled = true;
        centralLight.color = redLightColor;
        centralLight.range = redLightRange;

        while (true)
        {
            float elapsed = 0f;

            while (elapsed < pulseDuration)
            {
                float t = elapsed / pulseDuration;
                centralLight.intensity = Mathf.Lerp(redLightIntensity * 0.6f, redLightIntensity, t);
                elapsed += Time.deltaTime;
                yield return null;
            }

            elapsed = 0f;

            while (elapsed < pulseDuration)
            {
                float t = elapsed / pulseDuration;
                centralLight.intensity = Mathf.Lerp(redLightIntensity, redLightIntensity * 0.6f, t);
                elapsed += Time.deltaTime;
                yield return null;
            }
        }
    }

    public void TurnOff()
    {
        StopCurrentPattern();
        if (centralLight != null)
        {
            centralLight.enabled = false;
        }
    }
    private IEnumerator ReleaseFadeCoroutine(float fadeDuration)
    {
        if (centralLight == null) yield break;

        centralLight.enabled = true;
        Color startColor = centralLight.color;
        float startIntensity = centralLight.intensity;
        float startRange = centralLight.range;

        float elapsed = 0f;

        while (elapsed < fadeDuration)
        {
            float t = elapsed / fadeDuration;
            centralLight.color = Color.Lerp(startColor, releaseColor, t);
            centralLight.intensity = Mathf.Lerp(startIntensity, releaseIntensity, t);
            centralLight.range = Mathf.Lerp(startRange, releaseRange, t);

            elapsed += Time.deltaTime;
            yield return null;
        }

        ApplySettings(releaseColor, releaseIntensity, releaseRange);
    }
}