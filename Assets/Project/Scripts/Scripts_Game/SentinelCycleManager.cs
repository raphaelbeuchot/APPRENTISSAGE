using System.Collections;
using UnityEngine;

public class SentinelCycleManager : MonoBehaviour
{
    public enum GameState { GreenLight, Alert, RedLight, Release }

    [Header("Sentinel Settings")]
    public SentinelSettings sentinelSettings;

    [Header("Dynamic Cycle Difficulty")]
    [Tooltip("Plus le chiffre est grand, moins la duree des cycles peut reduire. 0.3f = duree reduite jusqu'a 70%")]
    [SerializeField, Range(0.1f, 1f)] private float minCycleDurationMultiplier = 0.3f;

    private float initialPlayerSentinelDistance;

    [Header("Audio")]
    private AudioSource audioSource;

    [Header("References")]
    public Transform playerTransform;
    public Transform sentinelTransform;
    [SerializeField] private Transform startZoneTransform;
    public GameManager gameManager;
    [SerializeField] private MarqueeLightController marqueeLightController;
    [SerializeField] private Light[] lightsToDisableInRedLight;
    [SerializeField] private SentinelCentralLight sentinelCentralLight;
    [SerializeField] private RedLightVolumeController redLightVolumeController;
    [SerializeField] private Light playerSpotLight;
    [SerializeField] private Color spotColorCompensated = new Color(0.5f, 0.8f, 1f, 1f); // Cyan pour compenser le rouge

    [Header("State")]
    public GameState currentState = GameState.GreenLight;
    private float cycleTimer;
    private float targetDuration;
    private bool gameStarted = false;

    [Header("Tutorial Mode")]
    public bool isTutorialMode = false;
    [SerializeField] private TutorialPromptUI tutorialPromptUI;
    [SerializeField] private Sprite redLightTutorialSprite;
    private bool hasTriggeredFirstRedLight = false;

    [SerializeField] private Light aerialLight;
    private float aerialLightOriginalIntensity;

    private Coroutine alertCoroutine;
    public float alertDuration;

    void OnEnable()
    {
        Debug.LogError("[CYCLE START] DEBUT OnEnable()");

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

        if (aerialLight != null)
        {
            aerialLightOriginalIntensity = aerialLight.intensity;
        }

        if (playerSpotLight != null)
        {
            playerSpotLight.enabled = false;
        }

        bool isRestart = PlayerPrefs.GetInt("AutoStartCountdown", 0) == 1;
        bool hasSeenTutorial = TutorialRedLightTrigger.hasSeenRedLightTutorial;

        Debug.LogError($"[CYCLE START] isRestart={isRestart}, hasSeenTutorial={hasSeenTutorial}, isTutorialMode={isTutorialMode}");

        // NOUVEAU : Reset la variable statique si ce n'est pas un restart
        if (!isRestart)
        {
            TutorialRedLightTrigger.hasSeenRedLightTutorial = false;
            Debug.Log("[CYCLE] Premier lancement - Reset hasSeenRedLightTutorial");
        }

        if (isRestart && hasSeenTutorial && isTutorialMode)
        {
            Debug.Log("[CYCLE] Restart + tutorial deja vu - Mode normal active");
            isTutorialMode = false;
            hasTriggeredFirstRedLight = true;

            TutorialRedLightTrigger triggerScript = FindObjectOfType<TutorialRedLightTrigger>();
            if (triggerScript != null)
            {
                triggerScript.gameObject.SetActive(false);
                Debug.Log("[CYCLE] GameObject triggerredcycle desactive");
            }
        }

        Time.timeScale = 1f;
    }
    void Update()
    {
        if (!gameStarted || sentinelSettings == null) return;

        cycleTimer += Time.deltaTime;

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
            {
                // Bloquer transition vers Alert en mode tutorial
                if (isTutorialMode && !hasTriggeredFirstRedLight)
                {
                    return;
                }
                StartNewCycle(GameState.Alert);
            }
            else if (currentState == GameState.RedLight)
                StartNewCycle(GameState.Release);
        }
    }
    public void TriggerFirstRedLight()
    {
        if (!isTutorialMode || hasTriggeredFirstRedLight || currentState != GameState.GreenLight)
            return;

        hasTriggeredFirstRedLight = true;

        if (tutorialPromptUI != null && redLightTutorialSprite != null)
        {
            tutorialPromptUI.Show(redLightTutorialSprite);
        }

        StartNewCycle(GameState.Alert);
        Debug.Log("[TUTORIAL] Premier RedLight declenche par trigger !");
    }
    private float GetDynamicGreenLightDuration()
    {
        if (gameManager == null)
        {
            Debug.LogWarning("[CYCLE] GameManager null, utilise duree par defaut");
            return sentinelSettings.GetRandomGreenlightDuration();
        }

        float distance = Vector3.Distance(playerTransform.position, sentinelTransform.position);
        float distanceFactor = Mathf.Clamp01(1f - (distance / initialPlayerSentinelDistance));

        int totalEnemies = gameManager.GetTotalEnemies();
        int enemiesKilled = gameManager.GetEnemiesKilled();
        float enemyFactor = 0f;

        if (totalEnemies > 0)
        {
            float enemyRatio = (float)(totalEnemies - enemiesKilled) / totalEnemies;
            enemyFactor = 1f - enemyRatio;
        }

        float combinedFactor = Mathf.Max(distanceFactor, enemyFactor);
        float durationMultiplier = 1f - (combinedFactor * (1f - minCycleDurationMultiplier));

        float baseDuration = sentinelSettings.GetRandomGreenlightDuration();
        float finalDuration = baseDuration * durationMultiplier;

        Debug.Log($"[CYCLE] GreenLight dynamique - Distance: {distanceFactor:F2}, Ennemis: {enemyFactor:F2}, Max: {combinedFactor:F2} - Duree: {finalDuration:F1}s (base: {baseDuration:F1}s, x{durationMultiplier:F2})");

        return finalDuration;
    }
    /*private void OnTriggerEnter(Collider other)
    {
        if (!isTutorialMode || hasTriggeredFirstRedLight) return;

        if (other.CompareTag("Player"))
        {
            hasTriggeredFirstRedLight = true;

            // Afficher message tuto
            if (tutorialPromptUI != null && redLightTutorialSprite != null)
            {
                tutorialPromptUI.Show(redLightTutorialSprite);
            }

            // Forcer transition immediate vers Alert
            StartNewCycle(GameState.Alert);
            Debug.Log("[TUTORIAL] Premier RedLight declenche par trigger !");
        }
    }*/
    private float GetDynamicRedLightDuration()
    {
        if (gameManager == null)
        {
            Debug.LogWarning("[CYCLE] GameManager null, utilise duree par defaut");
            return sentinelSettings.GetRandomRedlightDuration();
        }

        float distance = Vector3.Distance(playerTransform.position, sentinelTransform.position);
        float distanceFactor = Mathf.Clamp01(1f - (distance / initialPlayerSentinelDistance));

        int totalEnemies = gameManager.GetTotalEnemies();
        int enemiesKilled = gameManager.GetEnemiesKilled();
        float enemyFactor = 0f;

        if (totalEnemies > 0)
        {
            float enemyRatio = (float)(totalEnemies - enemiesKilled) / totalEnemies;
            enemyFactor = 1f - enemyRatio;
        }

        float combinedFactor = Mathf.Max(distanceFactor, enemyFactor);
        float durationMultiplier = 1f - (combinedFactor * (1f - minCycleDurationMultiplier));

        float baseDuration = sentinelSettings.GetRandomRedlightDuration();
        float finalDuration = baseDuration * durationMultiplier;

        Debug.Log($"[CYCLE] RedLight dynamique - Distance: {distanceFactor:F2}, Ennemis: {enemyFactor:F2}, Max: {combinedFactor:F2} - Duree: {finalDuration:F1}s (base: {baseDuration:F1}s, x{durationMultiplier:F2})");

        return finalDuration;
    }

    public void StartNewCycle(GameState newState)
    {
        currentState = newState;
        cycleTimer = 0f;

        if (newState == GameState.GreenLight)
        {
            targetDuration = GetDynamicGreenLightDuration();
            Debug.Log(string.Format("[CYCLE] GreenLight - Duree: {0:F1}s", targetDuration));

            if (marqueeLightController != null)
                marqueeLightController.StartGreenLightPattern();

            if (sentinelCentralLight != null)
                sentinelCentralLight.TurnOff();

            // Eteindre spot player
            if (playerSpotLight != null)
                playerSpotLight.enabled = false;


        }
    
        else if (newState == GameState.Alert)
        {
            if (audioSource != null && audioSource.loop)
            {
                audioSource.loop = false;
                audioSource.Stop();
            }

            if (alertCoroutine != null)
                StopCoroutine(alertCoroutine);

            float distance = Vector3.Distance(playerTransform.position, sentinelTransform.position);
            float distanceFactor = Mathf.Clamp01(1f - (distance / initialPlayerSentinelDistance));

            float enemyFactor = 0f;
            if (gameManager != null)
            {
                int totalEnemies = gameManager.GetTotalEnemies();
                int enemiesKilled = gameManager.GetEnemiesKilled();
                if (totalEnemies > 0)
                {
                    float enemyRatio = (float)(totalEnemies - enemiesKilled) / totalEnemies;
                    enemyFactor = 1f - enemyRatio;
                }

                
            }

            float combinedFactor = Mathf.Max(distanceFactor, enemyFactor);
            float pitch = Mathf.Lerp(1.0f, sentinelSettings.maxPitch, combinedFactor);
            float alertDuration = sentinelSettings.alertSound.length / pitch;

            alertCoroutine = StartCoroutine(BeethovenAlertCoroutine());
            Debug.Log("[CYCLE] Alert - duree = duree du son");
        }
        else if (newState == GameState.RedLight)
        {
            if (gameManager != null)
                gameManager.ResetAllTracking();

            targetDuration = GetDynamicRedLightDuration();

            if (audioSource != null && sentinelSettings.redlightSound != null)
            {
                audioSource.clip = sentinelSettings.redlightSound;
                audioSource.loop = true;
                audioSource.Play();
            }

            Debug.Log(string.Format("[CYCLE] RedLight - Duree: {0:F1}s", targetDuration));

            // Fade in volume RedLight
            if (redLightVolumeController != null)
                redLightVolumeController.FadeIn();

            // Dans la section RedLight, juste après avoir allumé le spot :
            if (playerSpotLight != null)
            {
                playerSpotLight.enabled = true;
                playerSpotLight.color = spotColorCompensated;
                Debug.Log($"[SPOT] ALLUME en RedLight - intensity: {playerSpotLight.intensity}, enabled: {playerSpotLight.enabled}");
            }

            if (marqueeLightController != null)
                marqueeLightController.StartRedLightPattern();

            if (sentinelCentralLight != null)
                sentinelCentralLight.StartRedLightPattern();

            if (lightsToDisableInRedLight != null)
            {
                foreach (Light light in lightsToDisableInRedLight)
                {
                    if (light != null)
                        light.enabled = false;
                }
            }
        }
        else if (newState == GameState.Release)
        {
            targetDuration = sentinelSettings.releaseDuration;

            if (audioSource != null)
            {
                audioSource.Stop();
            }

            if (audioSource != null && sentinelSettings.greenlightAmbientSound != null)
            {
                audioSource.clip = sentinelSettings.greenlightAmbientSound;
                audioSource.loop = false;
                audioSource.volume = 1f;
                audioSource.spatialBlend = 0f;
                audioSource.Play();
                Debug.Log("[AUDIO] GreenLight loop started (Release)");
            }

            Debug.Log(string.Format("[CYCLE] Release - Duree: {0:F1}s", targetDuration));

            // Fade out volume RedLight
            if (redLightVolumeController != null)
                redLightVolumeController.FadeOut();

            // Eteindre spot player
            if (playerSpotLight != null)
                playerSpotLight.enabled = false;

            if (marqueeLightController != null)
                marqueeLightController.StartReleaseFade(1f);

            if (sentinelCentralLight != null)
                sentinelCentralLight.TurnOff();

            if (lightsToDisableInRedLight != null)
            {
                foreach (Light light in lightsToDisableInRedLight)
                {
                    if (light != null)
                        light.enabled = true;
                }
            }
        }
    }

    private IEnumerator FadeAerialLightCoroutine(float duration)
    {
        if (aerialLight == null) yield break;

        float elapsed = 0f;
        float startIntensity = aerialLight.intensity;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            aerialLight.intensity = Mathf.Lerp(startIntensity, 0f, t);
            yield return null;
        }

        aerialLight.intensity = 0f;
    }

    private IEnumerator BeethovenAlertCoroutine()
    {
        Debug.Log("[ALERT] Debut - son unique avec pitch variable");

        if (sentinelSettings.alertSound == null)
        {
            Debug.LogWarning("[ALERT] alertSound est NULL!");
            StartNewCycle(GameState.RedLight);
            yield break;
        }

        float distance = Vector3.Distance(playerTransform.position, sentinelTransform.position);
        float distanceFactor = Mathf.Clamp01(1f - (distance / initialPlayerSentinelDistance));

        float enemyFactor = 0f;
        if (gameManager != null)
        {
            int totalEnemies = gameManager.GetTotalEnemies();
            int enemiesKilled = gameManager.GetEnemiesKilled();
            if (totalEnemies > 0)
            {
                float enemyRatio = (float)(totalEnemies - enemiesKilled) / totalEnemies;
                enemyFactor = 1f - enemyRatio;
            }
        }

        float combinedFactor = Mathf.Max(distanceFactor, enemyFactor);
        float pitch = Mathf.Lerp(1.0f, sentinelSettings.maxPitch, combinedFactor);

        Debug.Log($"[ALERT] Distance: {distanceFactor:F2}, Ennemis: {enemyFactor:F2}, Max: {combinedFactor:F2}, Pitch: {pitch:F2}");

        PlaySoundAtPitch(sentinelSettings.alertSound, pitch);

        float soundDuration = sentinelSettings.alertSound.length / pitch;
        alertDuration = soundDuration; // NOUVEAU - stocke pour BrightEyes

        if (marqueeLightController != null)
        {
            marqueeLightController.StartAlertPattern(soundDuration);
        }

        yield return new WaitForSeconds(soundDuration);

        Debug.Log("[ALERT] Son termine - transition RedLight");
        StartNewCycle(GameState.RedLight);
    }

    private AudioSource PlaySoundAtPitch(AudioClip clip, float pitch)
    {
        if (clip == null || audioSource == null) return null;

        GameObject tempGO = new GameObject("TempAudio_Alert");
        tempGO.transform.position = sentinelTransform.position;
        AudioSource tempAS = tempGO.AddComponent<AudioSource>();
        tempAS.clip = clip;
        tempAS.pitch = pitch;
        tempAS.spatialBlend = 0f;
        tempAS.volume = audioSource.volume;
        tempAS.Play();
        Destroy(tempGO, clip.length / pitch + 0.1f);

        return tempAS;
    }

    public void StartGameCycle()
    {
        if (gameStarted) return;
        gameStarted = true;

        if (playerTransform != null && sentinelTransform != null)
        {
            initialPlayerSentinelDistance = Vector3.Distance(playerTransform.position, sentinelTransform.position);
            Debug.Log($"[CYCLE] Distance initiale player-sentinelle: {initialPlayerSentinelDistance:F1}m");
        }
        else
        {
            initialPlayerSentinelDistance = 50f;
            Debug.LogWarning("[CYCLE] References manquantes, utilise distance fallback 50m");
        }

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