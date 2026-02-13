using UnityEngine;
using System.Collections;

public class CountdownManager : MonoBehaviour
{
    [Header("Audio")]
    public AudioClip countdownStartSound;
    private AudioSource audioSource;
    [HideInInspector]
    public bool countdownFinished = false;
    private bool isCountdownRunning = false;
    private bool isRestart = false;
    public AudioClip ambientTrack;
    private AudioSource ambientAudioSource;


    void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
        // Setup AudioSource pour ambient track
        ambientAudioSource = gameObject.AddComponent<AudioSource>();
        ambientAudioSource.loop = true;
        ambientAudioSource.playOnAwake = false;
    }

    void Start()
    {
        // Si c'est un restart depuis Game Over, lancer automatiquement
        if (PlayerPrefs.GetInt("AutoStartCountdown", 0) == 1)
        {
            isRestart = true;
            PlayerPrefs.DeleteKey("AutoStartCountdown");
            Debug.Log("[CountdownManager] Auto-start apres restart detecte !");
            StartCoroutine(AutoStartCountdownAfterDelay(0.5f));
        }
    }

    IEnumerator AutoStartCountdownAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        StartCountdown();
    }

    public void StartCountdown()
    {
        // PROTECTION : Si deja lance, ignorer
        if (isCountdownRunning || countdownFinished)
        {
            Debug.Log("[CountdownManager] Countdown deja en cours ou termine, ignore.");
            return;
        }
        isCountdownRunning = true;

        Debug.Log($"[CountdownManager] isRestart = {isRestart}"); // NOUVEAU

        if (audioSource != null && countdownStartSound != null)
        {
            audioSource.PlayOneShot(countdownStartSound);

            // Jouer le second son apres la fin du premier
            if (ambientTrack != null)
            {
                StartCoroutine(PlaySoundAfterDelay(countdownStartSound.length));
            }
        }

        GameUIManager uiManager = FindObjectOfType<GameUIManager>();
        if (uiManager != null)
        {
            Debug.Log($"[CountdownManager] Appel ShowCountdown avec skipText = {isRestart}"); // NOUVEAU
            uiManager.ShowCountdown(isRestart);
        }

        Debug.Log("Countdown demarre!");
        StartCoroutine(CountdownCoroutine());
    }

    IEnumerator CountdownCoroutine()
    {
        Debug.Log("3...");
        yield return new WaitForSeconds(0.6f);
        Debug.Log("2...");
        yield return new WaitForSeconds(0.6f);
        Debug.Log("1...");
        yield return new WaitForSeconds(3f);
        Debug.Log("GO!");

        countdownFinished = true;

        
    }
    IEnumerator PlaySoundAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);

        if (ambientAudioSource != null && ambientTrack != null)
        {
            ambientAudioSource.clip = ambientTrack;
            ambientAudioSource.Play();
            Debug.Log("[CountdownManager] Ambient track demarre en boucle");
        }
    }
    public void PauseAmbient()
    {
        if (ambientAudioSource != null && ambientAudioSource.isPlaying)
        {
            ambientAudioSource.Pause();
        }
    }

    public void ResumeAmbient()
    {
        if (ambientAudioSource != null && ambientAudioSource.clip != null)
        {
            ambientAudioSource.UnPause();
        }
    }

    public void StopAmbient()
    {
        if (ambientAudioSource != null)
        {
            ambientAudioSource.Stop();
        }
    }
    void OnDestroy()
    {
        StopAmbient();
    }
}