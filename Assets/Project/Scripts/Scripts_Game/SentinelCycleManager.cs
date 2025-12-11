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

      

    [Header("References")]
    public Transform playerTransform;
    public Transform sentinelTransform;
    [SerializeField] private Transform startZoneTransform;


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

        // 1. Calculer Ps (projection sentinelle au sol)
        Vector3 sentinelGroundPos = new Vector3(sentinelTransform.position.x, 0f, sentinelTransform.position.z);

        // 2. Calculer D (distance player - Ps)
        Vector3 playerGroundPos = new Vector3(playerTransform.position.x, 0f, playerTransform.position.z);
        float D = Vector3.Distance(playerGroundPos, sentinelGroundPos);

        // 3. Calculer Dmax (distance startZone - Ps)
        Vector3 startZoneGroundPos = new Vector3(startZoneTransform.position.x, 0f, startZoneTransform.position.z);
        float Dmax = Vector3.Distance(startZoneGroundPos, sentinelGroundPos);

        // 4. Calculer pourcentage (0% = loin sentinelle, 100% = collé sentinelle)
        float percentage = (D / Dmax) * 100f;

        Debug.Log($"[BEETHOVEN] D={D:F1}m, Dmax={Dmax:F1}m, Pourcentage={percentage:F1}%");

        // 5. Déterminer les délais selon la tranche
        float delay1, delay2, delay3;

        if (percentage >= 75f)
        {
            // AVANT : delay1 = tranche1_delay;
            // APRES :
            delay1 = sentinelSettings.tranche1_delay;
            delay2 = sentinelSettings.tranche1_delay;
            delay3 = sentinelSettings.tranche1_delay;
            Debug.Log($"[BEETHOVEN] TRANCHE 1 (loin) - Délais identiques: {delay1}s");
        }
        else if (percentage >= 50f)
        {
            delay1 = sentinelSettings.tranche2_delay12;
            delay2 = sentinelSettings.tranche2_delay12;
            delay3 = Random.Range(sentinelSettings.tranche2_delay3Min, sentinelSettings.tranche2_delay3Max);
            Debug.Log($"[BEETHOVEN] TRANCHE 2 - Délai 1&2: {delay1}s, Délai 3 random: {delay3:F2}s");
        }
        else if (percentage >= 25f)
        {
            float randomDelay12 = Random.Range(sentinelSettings.tranche3_delay12Min, sentinelSettings.tranche3_delay12Max);
            delay1 = randomDelay12;
            delay2 = randomDelay12;
            delay3 = Random.Range(sentinelSettings.tranche3_delay3Min, sentinelSettings.tranche3_delay3Max);
            Debug.Log($"[BEETHOVEN] TRANCHE 3 - Délai 1&2 random: {delay1:F2}s, Délai 3 random: {delay3:F2}s");
        }
        else
        {
            delay1 = sentinelSettings.tranche4_delay;
            delay2 = sentinelSettings.tranche4_delay;
            delay3 = sentinelSettings.tranche4_delay;
            Debug.Log($"[BEETHOVEN] TRANCHE 4 (proche) - Délais identiques: {delay1}s");
        }

        // 6. Jouer la séquence Beethoven
        // SON 1
        if (sentinelSettings.alertSound1 != null)
        {
            audioSource.PlayOneShot(sentinelSettings.alertSound1);
            Debug.Log($"[BEETHOVEN] Son 1 - Attente {delay1:F2}s");
            yield return new WaitForSeconds(delay1);
        }

        // SON 2
        if (sentinelSettings.alertSound2 != null)
        {
            audioSource.PlayOneShot(sentinelSettings.alertSound2);
            Debug.Log($"[BEETHOVEN] Son 2 - Attente {delay2:F2}s");
            yield return new WaitForSeconds(delay2);
        }

        // SON 3
        if (sentinelSettings.alertSound3 != null)
        {
            audioSource.PlayOneShot(sentinelSettings.alertSound3);
            Debug.Log($"[BEETHOVEN] Son 3 - Attente {delay3:F2}s");
            yield return new WaitForSeconds(delay3);
        }

        // SON 4 FINAL
        if (sentinelSettings.finalAlertSound != null)
        {
            audioSource.PlayOneShot(sentinelSettings.finalAlertSound);
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