using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MarqueeLightController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform bulbsContainer;

    [Header("Alert Settings")]
    [SerializeField] private int alertFlashCount = 5;

    [Header("Audio Reactive")]
    [SerializeField] private int audioSampleSize = 128;
    [SerializeField, Range(0f, 1f)] private float minIntensityRatio = 0.1f;
    [SerializeField, Range(0f, 1f)] private float maxIntensityRatio = 1f;
    [SerializeField, Range(0f, 0.5f)] private float propagationDelay = 0.1f;
    [SerializeField, Range(1f, 10f)] private float audioMultiplier = 2f; // NOUVEAU : sensibilite generale

    private List<List<MarqueeLightBulb>> circles = new List<List<MarqueeLightBulb>>();
    private Coroutine currentPatternCoroutine;
    private float[] audioSamples;

    void Start()
    {
        StartCoroutine(DelayedStart());
    }

    IEnumerator DelayedStart()
    {
        yield return new WaitForSeconds(0.5f);
        CollectBulbsByCircle();
    }

    void CollectBulbsByCircle()
    {
        bulbsContainer = transform.Find("CirclesRuntimeContainer");

        if (bulbsContainer == null)
        {
            Debug.LogError("[MarqueeLightController] CirclesRuntimeContainer introuvable !");
            return;
        }

        circles.Clear();

        for (int i = 0; i < bulbsContainer.childCount; i++)
        {
            Transform circleTransform = bulbsContainer.GetChild(i);
            MarqueeLightBulb[] circleBulbs = circleTransform.GetComponentsInChildren<MarqueeLightBulb>();

            if (circleBulbs.Length > 0)
            {
                List<MarqueeLightBulb> circleList = new List<MarqueeLightBulb>(circleBulbs);
                circles.Add(circleList);
                Debug.Log($"[MarqueeLightController] Cercle {i} : {circleBulbs.Length} bulbs");
            }
        }

        Debug.Log($"[MarqueeLightController] {circles.Count} cercles collectes");

        // NOUVEAU : Allumer en mode GreenLight par defaut des le depart
        foreach (var circle in circles)
        {
            foreach (var bulb in circle)
            {
                bulb.SetGreenLightMode();
            }
        }
        Debug.Log("[MarqueeLightController] Marquee lights allumees en GreenLight au demarrage");
    }

    // ========== PUBLIC METHODS ==========

    public void StartGreenLightPattern()
    {
        StopCurrentPattern();
        currentPatternCoroutine = StartCoroutine(GreenLightCoroutine());
    }

    public void StartAlertPattern(float alertDuration)
    {
        StopCurrentPattern();
        currentPatternCoroutine = StartCoroutine(AlertCoroutine(alertDuration));
    }

    public void StartAlertAudioReactivePattern(AudioSource alertAudioSource, float totalDuration)
    {
        StopCurrentPattern();
        currentPatternCoroutine = StartCoroutine(AlertAudioReactiveCoroutine(alertAudioSource, totalDuration));
    }

    IEnumerator AlertAudioReactiveCoroutine(AudioSource alertAudioSource, float totalDuration)
    {
        Debug.Log($"[MarqueeLights] Alert AUDIO REACTIVE pattern - duration: {totalDuration}s");

        if (alertAudioSource == null)
        {
            Debug.LogWarning("[MarqueeLights] AudioSource null, fallback vers pattern normal");
            yield return AlertCoroutine(totalDuration);
            yield break;
        }

        // Initialiser le tableau d'echantillons audio
        if (audioSamples == null || audioSamples.Length != audioSampleSize)
        {
            audioSamples = new float[audioSampleSize];
        }

        float elapsed = 0f;

        while (elapsed < totalDuration)
        {
            elapsed += Time.deltaTime;

            // Verifier que l'AudioSource existe toujours
            if (alertAudioSource == null || !alertAudioSource.isPlaying)
            {
                Debug.Log("[MarqueeLights] Audio stopped, finishing pattern");
                break;
            }

            // Recuperer les donnees audio globales
            alertAudioSource.GetOutputData(audioSamples, 0);

            // Calculer l'amplitude moyenne
            float sum = 0f;
            for (int i = 0; i < audioSamples.Length; i++)
            {
                sum += Mathf.Abs(audioSamples[i]);
            }
            float averageAmplitude = sum / audioSamples.Length;

            // Mapper l'amplitude vers intensite (0-1 -> minRatio-maxRatio)
            float normalizedAmplitude = Mathf.Clamp01(averageAmplitude * audioMultiplier);
            float targetIntensityRatio = Mathf.Lerp(minIntensityRatio, maxIntensityRatio, normalizedAmplitude);

            // Appliquer aux bulbs avec propagation exterieur -> interieur (premiere seconde uniquement)
            if (elapsed < 1f)
            {
                // PREMIERE SECONDE : effet de propagation
                for (int circleIndex = 0; circleIndex < circles.Count; circleIndex++)
                {
                    // Calculer le delai pour ce cercle (exterieur -> interieur)
                    float circleDelay = (circles.Count - 1 - circleIndex) * propagationDelay;
                    float delayedElapsed = elapsed - circleDelay;

                    // Si on n'a pas encore atteint ce cercle, intensite minimale
                    float circleIntensity;
                    if (delayedElapsed < 0f)
                    {
                        circleIntensity = minIntensityRatio;
                    }
                    else
                    {
                        // Transition progressive pour ce cercle
                        circleIntensity = Mathf.Lerp(minIntensityRatio, targetIntensityRatio, Mathf.Clamp01(delayedElapsed * 10f));
                    }

                    foreach (var bulb in circles[circleIndex])
                    {
                        bulb.SetAlertMode();
                        float alertIntensity = bulb.GetAlertIntensity();
                        float alertEmission = bulb.GetAlertEmissionIntensity();
                        bulb.SetLightIntensity(alertIntensity * circleIntensity);
                        bulb.SetEmissionIntensity(alertEmission * circleIntensity);
                    }
                }
            }
            else
            {
                // APRES PREMIERE SECONDE : tous les cercles ensemble
                foreach (var circle in circles)
                {
                    foreach (var bulb in circle)
                    {
                        bulb.SetAlertMode();
                        float alertIntensity = bulb.GetAlertIntensity();
                        float alertEmission = bulb.GetAlertEmissionIntensity();
                        bulb.SetLightIntensity(alertIntensity * targetIntensityRatio);
                        bulb.SetEmissionIntensity(alertEmission * targetIntensityRatio);
                    }
                }
            }

            yield return null;
        }

        // Fin du pattern : eteindre
        TurnOffAllBulbs();
        Debug.Log("[MarqueeLights] Alert audio reactive pattern finished");
    }

    public void StartRedLightPattern()
    {
        StopCurrentPattern();
        currentPatternCoroutine = StartCoroutine(RedLightCoroutine());
    }

    public void StartReleaseFade(float duration)
    {
        StopCurrentPattern();
        currentPatternCoroutine = StartCoroutine(ReleaseFadeCoroutine(duration));
    }

    public void StopAll()
    {
        StopCurrentPattern();
        TurnOffAllBulbs();
    }

    // ========== PATTERN COROUTINES ==========

    IEnumerator GreenLightCoroutine()
    {
        Debug.Log("[MarqueeLights] GreenLight pattern started");

        // Allumer toutes en mode GreenLight
        foreach (var circle in circles)
        {
            foreach (var bulb in circle)
            {
                bulb.SetGreenLightMode();
            }
        }

        // Rester allume jusqu'a changement
        while (true)
        {
            yield return null;
        }
    }

    IEnumerator AlertCoroutine(float totalDuration)
    {
        Debug.Log($"[MarqueeLights] Alert pattern - duration: {totalDuration}s, {alertFlashCount} flashes");

        float flashInterval = totalDuration / alertFlashCount;

        for (int i = 0; i < alertFlashCount; i++)
        {
            // Allumer en mode Alert
            foreach (var circle in circles)
            {
                foreach (var bulb in circle)
                {
                    bulb.SetAlertMode();
                }
            }
            yield return new WaitForSeconds(flashInterval * 0.5f);

            // Eteindre
            TurnOffAllBulbs();
            yield return new WaitForSeconds(flashInterval * 0.5f);
        }

        Debug.Log("[MarqueeLights] Alert pattern finished");
    }

    IEnumerator RedLightCoroutine()
    {
        Debug.Log("[MarqueeLights] RedLight pattern started");

        if (circles.Count == 0)
        {
            Debug.LogWarning("[MarqueeLights] No circles to animate");
            yield break;
        }

        // Allumer toutes en mode RedLight
        foreach (var circle in circles)
        {
            foreach (var bulb in circle)
            {
                bulb.SetRedLightMode();
            }
        }

        // Rester allume jusqu'a Release
        while (true)
        {
            yield return null;
        }
    }

    IEnumerator ReleaseFadeCoroutine(float duration)
    {
        Debug.Log($"[MarqueeLights] Release fade started - {duration}s");

        // Changer les materiaux tout de suite
        foreach (var circle in circles)
        {
            foreach (var bulb in circle)
            {
                bulb.SetGreenLightMode();
            }
        }

        // Fade progressif des Point Lights ET emissions RedLight -> GreenLight
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float linear = elapsed / duration;
            float t = 1f - Mathf.Pow(1f - linear, 3f); // Exposant 3 = EaseOut doux
            foreach (var circle in circles)
            {
                foreach (var bulb in circle)
                {
                    // Fade Point Light
                    float intensity = Mathf.Lerp(bulb.GetRedLightIntensity(), bulb.GetGreenLightIntensity(), t);
                    Color color = Color.Lerp(bulb.GetRedLightColor(), bulb.GetGreenLightColor(), t);
                    bulb.SetLightIntensity(intensity);
                    bulb.SetLightColor(color);

                    // Fade Emission
                    float emissionIntensity = Mathf.Lerp(bulb.GetRedEmissionIntensity(), bulb.GetGreenEmissionIntensity(), t);
                    bulb.SetEmissionIntensity(emissionIntensity);
                }
            }

            yield return null;
        }

        // S'assurer qu'on finit exactement sur GreenLight
        foreach (var circle in circles)
        {
            foreach (var bulb in circle)
            {
                bulb.SetLightIntensity(bulb.GetGreenLightIntensity());
                bulb.SetLightColor(bulb.GetGreenLightColor());
                bulb.SetEmissionIntensity(bulb.GetGreenEmissionIntensity());
            }
        }

        // Rester en GreenLight
        while (true)
        {
            yield return null;
        }
    }

    // ========== HELPERS ==========

    void StopCurrentPattern()
    {
        if (currentPatternCoroutine != null)
        {
            StopCoroutine(currentPatternCoroutine);
            currentPatternCoroutine = null;
        }
    }

    void TurnOffAllBulbs()
    {
        foreach (var circle in circles)
        {
            foreach (var bulb in circle)
            {
                bulb.TurnOff();
            }
        }
    }
}