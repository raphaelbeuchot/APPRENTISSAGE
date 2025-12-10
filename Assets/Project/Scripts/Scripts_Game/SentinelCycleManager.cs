using System.Collections;
using UnityEngine;

public class SentinelCycleManager : MonoBehaviour
{
    public enum GameState { GreenLight, Alert, RedLight, Release }

    [Header("Sentinel Settings")]
    public SentinelSettings sentinelSettings;

    [Header("Visual Feedback")]
    public Renderer sentinelLightRenderer;
    public Material greenMaterial;
    public Material redMaterial;
    public Material yellowMaterial;

    [Header("Audio")]
    private AudioSource audioSource;

    [Header("Alert System - Beethoven Pattern")]
    [SerializeField] private AudioClip alertSound1;      // Son 1 "pam"
    [SerializeField] private AudioClip alertSound2;      // Son 2 "pam"
    [SerializeField] private AudioClip alertSound3;      // Son 3 "pam"
    [SerializeField] private AudioClip finalAlertSound;  // Son 4 "PAAAAM"

    [Header("Alert Distance Scaling")]
    [SerializeField] private float minAlertDistance = 5f;              // Distance min sentinelle
    [SerializeField] private Transform startZoneTransform;             // Distance max

    [Header("Alert Delay 1 & 2 - Distance Based")]
    [SerializeField] private float minBaseDelay = 0.3f;           // Delai si proche sentinelle
    [SerializeField] private float maxBaseDelay = 1.5f;           // Delai si loin sentinelle
    [SerializeField] private float delayRandomnessMin = -0.1f;    // Petite variation
    [SerializeField] private float delayRandomnessMax = 0.2f;     // Petite variation

    [Header("Alert Delay 3 - Psycho Random")]
    [SerializeField] private float delay3MinRandom = 0.5f;   // Delai 3 min si proche
    [SerializeField] private float delay3MaxRandom = 2.0f;   // Delai 3 max si proche

    [Header("References")]
    public Transform playerTransform;

    [Header("State")]
    public GameState currentState = GameState.GreenLight;
    private float cycleTimer;
    private float targetDuration;
    private bool gameStarted = false;

    private Coroutine alertCoroutine;

    void Start()
    {
        if (sentinelSettings == null)
        {
            Debug.LogError("SentinelCycleManager: SentinelSettings non assigne!");
            return;
        }

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        Time.timeScale = 1f;
    }

    void Update()
    {
        if (!gameStarted || sentinelSettings == null) return;

        cycleTimer += Time.deltaTime;

        // Alert gere par coroutine, on ne fait rien ici
        if (currentState == GameState.Alert)
        {
            return;
        }

        if (currentState == GameState.Release)
        {
            if (cycleTimer >= sentinelSettings.releaseDuration)
            {
                StartNewCycle(GameState.GreenLight);
                return;
            }
            return;
        }

        if (cycleTimer >= targetDuration)
        {
            if (currentState == GameState.GreenLight)
                StartNewCycle(GameState.Alert);
            else if (currentState == GameState.RedLight)
                StartNewCycle(GameState.Release);
        }
    }

    public void StartNewCycle(GameState newState)
    {
        SetState(newState);
        cycleTimer = 0f;

        if (newState == GameState.GreenLight)
        {
            targetDuration = sentinelSettings.GetRandomGreenlightDuration();
            Debug.Log($"[CYCLE] GreenLight - Duree: {targetDuration:F1}s");
        }
        else if (newState == GameState.Alert)
        {
            // Lancer la coroutine Beethoven
            if (alertCoroutine != null)
                StopCoroutine(alertCoroutine);

            alertCoroutine = StartCoroutine(BeethovenAlertCoroutine());
        }
        else if (newState == GameState.RedLight)
        {
            targetDuration = sentinelSettings.GetRandomRedlightDuration();

            if (audioSource != null && sentinelSettings.redlightSound != null)
                audioSource.PlayOneShot(sentinelSettings.redlightSound);

            Debug.Log($"[CYCLE] RedLight - Duree: {targetDuration:F1}s");
        }
        else if (newState == GameState.Release)
        {
            targetDuration = sentinelSettings.releaseDuration;

            if (audioSource != null && sentinelSettings.releaseSound != null)
                audioSource.PlayOneShot(sentinelSettings.releaseSound);

            Debug.Log($"[CYCLE] Release - Duree: {targetDuration:F1}s");
        }
    }

    private IEnumerator BeethovenAlertCoroutine()
    {
        Debug.Log("[BEETHOVEN ALERT] Debut de la sequence");

        // Calcul distance joueur-sentinelle
        float distanceToPlayer = playerTransform != null
            ? Vector3.Distance(transform.position, playerTransform.position)
            : 25f;

        float maxDistance = startZoneTransform != null
            ? Vector3.Distance(transform.position, startZoneTransform.position)
            : 50f;

        // Normaliser distance entre 0 (proche) et 1 (loin)
        float normalizedDistance = Mathf.InverseLerp(minAlertDistance, maxDistance, distanceToPlayer);
        normalizedDistance = Mathf.Clamp01(normalizedDistance);

        // Calculer delai de base selon distance
        // Proche (0) = minBaseDelay, Loin (1) = maxBaseDelay
        float baseDelay = Mathf.Lerp(minBaseDelay, maxBaseDelay, normalizedDistance);

        Debug.Log($"[BEETHOVEN] Distance: {distanceToPlayer:F1}m, Normalized: {normalizedDistance:F2}, Base Delay: {baseDelay:F2}s");

        // DELAIS 1 et 2 : Identiques, bases sur distance + petite variation
        float delay1and2 = baseDelay;

        // DELAI 3 : Mixe entre stable (loin) et tres random (proche)
        float stableDelay3 = baseDelay + Random.Range(delayRandomnessMin, delayRandomnessMax);
        float randomDelay3 = Random.Range(delay3MinRandom, delay3MaxRandom);

        // normalizedDistance = 1 (loin) = stable, 0 (proche) = random
        float delay3 = Mathf.Lerp(randomDelay3, stableDelay3, normalizedDistance);

        Debug.Log($"[BEETHOVEN] Delay1&2: {delay1and2:F2}s, Delay3: {delay3:F2}s (stable={stableDelay3:F2}s, random={randomDelay3:F2}s)");

        // SON 1 - PAM
        if (alertSound1 != null)
        {
            audioSource.PlayOneShot(alertSound1);
            Debug.Log($"[BEETHOVEN] Son 1 - Delai 1: {delay1and2:F2}s");
            yield return new WaitForSeconds(delay1and2);
        }

        // SON 2 - PAM
        if (alertSound2 != null)
        {
            audioSource.PlayOneShot(alertSound2);
            Debug.Log($"[BEETHOVEN] Son 2 - Delai 2: {delay1and2:F2}s (identique au delai 1)");
            yield return new WaitForSeconds(delay1and2);
        }

        // SON 3 - PAM
        if (alertSound3 != null)
        {
            audioSource.PlayOneShot(alertSound3);
            Debug.Log($"[BEETHOVEN] Son 3 - Delai 3 PSYCHO: {delay3:F2}s");
            yield return new WaitForSeconds(delay3);
        }

        // SON 4 LONG - PAAAAM - DEBUT REDLIGHT
        if (finalAlertSound != null)
        {
            audioSource.PlayOneShot(finalAlertSound);
            Debug.Log("[BEETHOVEN] Son final - PAAAAM !");
        }

        // Transition immediate vers RedLight
        StartNewCycle(GameState.RedLight);
    }

    void SetState(GameState newState)
    {
        currentState = newState;

        if (sentinelLightRenderer != null)
        {
            if (newState == GameState.GreenLight)
                sentinelLightRenderer.material = greenMaterial;
            else if (newState == GameState.Alert)
                sentinelLightRenderer.material = yellowMaterial != null ? yellowMaterial : redMaterial;
            else if (newState == GameState.RedLight)
                sentinelLightRenderer.material = redMaterial;
            else if (newState == GameState.Release)
                sentinelLightRenderer.material = greenMaterial;
        }
    }

    public void StartGameCycle()
    {
        if (gameStarted) return;
        gameStarted = true;
        StartNewCycle(GameState.GreenLight);
        Debug.Log("[CYCLE] Demarrage du jeu !");
    }

    public bool IsInRedLight()
    {
        return currentState == GameState.RedLight;
    }

    public bool IsInGreenLight()
    {
        return currentState == GameState.GreenLight;
    }

    public bool IsInAlert()
    {
        return currentState == GameState.Alert;
    }

    public GameState GetCurrentState()
    {
        return currentState;
    }

    public bool IsGameStarted()
    {
        return gameStarted;
    }
}