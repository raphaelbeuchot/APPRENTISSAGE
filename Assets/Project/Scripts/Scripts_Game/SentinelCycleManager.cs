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
    public GameManager gameManager;
    [SerializeField] private MarqueeLightController marqueeLightController;

    [Header("State")]
    public GameState currentState = GameState.GreenLight;
    private float cycleTimer;
    private float targetDuration;
    private bool gameStarted = false;

    [SerializeField] private Light aerialLight;
    private float aerialLightOriginalIntensity;

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

        // NOUVEAU : Sauvegarder intensite aerial light
        if (aerialLight != null)
        {
            aerialLightOriginalIntensity = aerialLight.intensity;
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
            Debug.Log(string.Format("[CYCLE] GreenLight - Duree: {0:F1}s", targetDuration));

            // NOUVEAU : Allumer aerial light
            if (aerialLight != null)
            {
                aerialLight.enabled = true;
                aerialLight.intensity = aerialLightOriginalIntensity;
            }

            // NOUVEAU : Marquee lights
            if (marqueeLightController != null)
                marqueeLightController.StartGreenLightPattern();

            // NOUVEAU : Allumer aerial light
            if (aerialLight != null)
                aerialLight.enabled = true;
        }
        else if (newState == GameState.Alert)
        {
            // ARRETER le son GreenLight en boucle
            if (audioSource != null && audioSource.loop)
            {
                audioSource.loop = false;
                audioSource.Stop();
            }

            if (alertCoroutine != null)
                StopCoroutine(alertCoroutine);

            // NOUVEAU : Calculer duree Alert pour marquee lights
            float distance = Vector3.Distance(playerTransform.position, sentinelTransform.position);
            float normalizedDistance = Mathf.Clamp01(1f - (distance / 50f));
            float pitch = Mathf.Lerp(sentinelSettings.minPitch, sentinelSettings.maxPitch, normalizedDistance);
            float alertDuration = sentinelSettings.alertSound.length / pitch;

            // NOUVEAU : Marquee lights
            if (marqueeLightController != null)
                marqueeLightController.StartAlertPattern(alertDuration);

            alertCoroutine = StartCoroutine(BeethovenAlertCoroutine());
            Debug.Log("[CYCLE] Alert - duree = duree du son");

            // NOUVEAU : Fade out aerial light pendant Alert
            if (aerialLight != null)
            {
                aerialLight.enabled = true;
                aerialLight.intensity = aerialLightOriginalIntensity;
                StartCoroutine(FadeAerialLightCoroutine(alertDuration));
            }
        }
        else if (newState == GameState.RedLight)
        {
            if (gameManager != null)
                gameManager.ResetAllTracking();

            targetDuration = sentinelSettings.GetRandomRedlightDuration();

            // NOUVEAU : Son en boucle
            if (audioSource != null && sentinelSettings.redlightSound != null)
            {
                audioSource.clip = sentinelSettings.redlightSound;
                audioSource.loop = true;
                audioSource.Play();
            }

            Debug.Log(string.Format("[CYCLE] RedLight - Duree: {0:F1}s", targetDuration));

            // NOUVEAU : Marquee lights
            if (marqueeLightController != null)
                marqueeLightController.StartRedLightPattern();

            // NOUVEAU : Eteindre aerial light
            if (aerialLight != null)
                aerialLight.enabled = false;
        }
        else if (newState == GameState.Release)
        {
            targetDuration = sentinelSettings.releaseDuration;

            // NOUVEAU : Allumer aerial light
            if (aerialLight != null)
            {
                aerialLight.enabled = true;
                aerialLight.intensity = aerialLightOriginalIntensity;
            }

            // ARRETER le son RedLight
            if (audioSource != null)
            {
                audioSource.Stop();
            }

            // DEMARRER le son GreenLight en boucle (Release -> GreenLight -> Alert)
            if (audioSource != null && sentinelSettings.greenlightAmbientSound != null)
            {
                audioSource.clip = sentinelSettings.greenlightAmbientSound;
                audioSource.loop = false;
                audioSource.volume = 1f;
                audioSource.spatialBlend = 0f;  // 2D NON-SPATIALISE
                audioSource.Play();
                Debug.Log("[AUDIO] GreenLight loop started (Release)");
            }

            Debug.Log(string.Format("[CYCLE] Release - Duree: {0:F1}s", targetDuration));

            // NOUVEAU : Marquee lights fade out 2.5s
            if (marqueeLightController != null)
                marqueeLightController.StartReleaseFade(2.5f);

            // NOUVEAU : Allumer aerial light
            if (aerialLight != null)
                aerialLight.enabled = true;
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

        float normalizedDistance = Mathf.Clamp01(1f - (distance / 50f));

        float pitch = Mathf.Lerp(sentinelSettings.minPitch, sentinelSettings.maxPitch, normalizedDistance);

        PlaySoundAtPitch(sentinelSettings.alertSound, pitch);

        Debug.Log(string.Format("[ALERT] Son joue - Distance: {0:F1}m, Pitch: {1:F2}", distance, pitch));

        float soundDuration = sentinelSettings.alertSound.length / pitch;
        yield return new WaitForSeconds(soundDuration);

        Debug.Log("[ALERT] Son termine - transition RedLight");
        StartNewCycle(GameState.RedLight);
    }

    private void PlaySoundAtPitch(AudioClip clip, float pitch)
    {
        if (clip == null || audioSource == null) return;

        GameObject tempGO = new GameObject("TempAudio_Alert");
        tempGO.transform.position = sentinelTransform.position;
        AudioSource tempAS = tempGO.AddComponent<AudioSource>();
        tempAS.clip = clip;
        tempAS.pitch = pitch;
        tempAS.spatialBlend = 0f;
        tempAS.volume = audioSource.volume;
        tempAS.Play();
        Destroy(tempGO, clip.length / pitch + 0.1f);
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