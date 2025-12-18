using UnityEngine;
using System.Collections;

public class CountdownManager : MonoBehaviour
{
    public MetalShutter shutter;
    public GameManager gameManager;

    [Header("Audio")]
    public AudioClip countdownStartSound;
    private AudioSource audioSource;

    [HideInInspector]
    public bool countdownFinished = false;

    private bool isCountdownRunning = false;  // NOUVEAU : Protection anti-double

    void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
    }

    void Start()
    {
        // Si c'est un restart depuis Game Over, lancer automatiquement
        if (PlayerPrefs.GetInt("AutoStartCountdown", 0) == 1)
        {
            PlayerPrefs.DeleteKey("AutoStartCountdown");
            Debug.Log("[CountdownManager] Auto-start après restart détecté !");
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
        // PROTECTION : Si déjà lancé, ignorer
        if (isCountdownRunning || countdownFinished)
        {
            Debug.Log("[CountdownManager] Countdown déjà en cours ou terminé, ignoré.");
            return;
        }

        isCountdownRunning = true;  //  Verrouiller

        if (audioSource != null && countdownStartSound != null)
        {
            audioSource.PlayOneShot(countdownStartSound);
        }

        GameUIManager uiManager = FindObjectOfType<GameUIManager>();
        if (uiManager != null)
        {
            uiManager.ShowCountdown();
        }

        Debug.Log("Countdown demarre!");
        StartCoroutine(CountdownCoroutine());
    }

    IEnumerator CountdownCoroutine()
    {
        Debug.Log("3...");
        yield return new WaitForSeconds(1f);
        Debug.Log("2...");
        yield return new WaitForSeconds(1f);
        Debug.Log("1...");
        yield return new WaitForSeconds(1f);
        Debug.Log("GO!");

        if (shutter != null)
        {
            shutter.Open();
        }

        countdownFinished = true;

        if (gameManager != null)
        {
            gameManager.StartGameCycle();
        }
    }
}