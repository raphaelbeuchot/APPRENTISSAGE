using UnityEngine;

public class RisingPlatform : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private RisingPlatformSettings settings;

    [Header("References")]
    [SerializeField] private SentinelCycleManager sentinelCycleManager;

    private Vector3 initialPosition;
    private AudioSource audioSource;
    private bool isRising = false;
    private bool isDescending = false;

    void Start()
    {
        // Sauvegarder position initiale
        initialPosition = transform.position;

        // Setup audio
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.loop = true;
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 1f; // 3D sound

        // Trouver SentinelCycleManager si pas assigne
        if (sentinelCycleManager == null)
        {
            sentinelCycleManager = FindObjectOfType<SentinelCycleManager>();
            if (sentinelCycleManager == null)
            {
                Debug.LogError("[RisingPlatform] SentinelCycleManager introuvable !");
            }
        }
    }

    void Update()
    {
        if (sentinelCycleManager == null || settings == null) return;

        SentinelCycleManager.GameState currentState = sentinelCycleManager.GetCurrentState();

        // Determiner si on monte ou descend
        switch (currentState)
        {
            case SentinelCycleManager.GameState.Alert:
            case SentinelCycleManager.GameState.RedLight:
                if (!isRising)
                {
                    StartRising();
                }
                RisePlatform();
                break;

            case SentinelCycleManager.GameState.Release:
            case SentinelCycleManager.GameState.GreenLight:
                if (!isDescending)
                {
                    StartDescending();
                }
                DescendPlatform();
                break;
        }
    }

    private void StartRising()
    {
        isRising = true;
        isDescending = false;

        // Jouer son montee
        if (settings.riseSound != null)
        {
            audioSource.clip = settings.riseSound;
            audioSource.Play();
        }

        Debug.Log($"[RisingPlatform] {settings.platformName} commence a monter");
    }

    private void StartDescending()
    {
        isDescending = true;
        isRising = false;

        // Jouer son descente si different
        if (settings.descendSound != null && settings.descendSound != settings.riseSound)
        {
            audioSource.clip = settings.descendSound;
            audioSource.Play();
        }

        Debug.Log($"[RisingPlatform] {settings.platformName} commence a descendre");
    }

    private void RisePlatform()
    {
        // Calculer position cible (monte OU descend selon signe)
        Vector3 targetPosition = initialPosition + Vector3.up * settings.maxRiseHeight;

        // Determiner direction de mouvement
        bool isMovingUp = settings.maxRiseHeight > 0;

        // Check si on a atteint la cible
        bool hasReachedTarget = isMovingUp
            ? (transform.position.y >= targetPosition.y)
            : (transform.position.y <= targetPosition.y);

        if (!hasReachedTarget)
        {
            // Se deplacer vers la cible
            Vector3 direction = (targetPosition - transform.position).normalized;
            Vector3 newPosition = transform.position + direction * settings.riseSpeed * Time.deltaTime;

            // Clamper pour ne pas depasser
            if (isMovingUp)
                newPosition.y = Mathf.Min(newPosition.y, targetPosition.y);
            else
                newPosition.y = Mathf.Max(newPosition.y, targetPosition.y);

            transform.position = newPosition;
        }
        else
        {
            // Arreter le son si arrive
            if (audioSource.isPlaying && audioSource.clip == settings.riseSound)
            {
                audioSource.Stop();
            }
        }
    }

    private void DescendPlatform()
    {
        // Determiner direction retour
        bool isMovingUp = transform.position.y < initialPosition.y;

        // Check si revenu a l'origine
        bool hasReachedOrigin = isMovingUp
            ? (transform.position.y >= initialPosition.y)
            : (transform.position.y <= initialPosition.y);

        if (!hasReachedOrigin)
        {
            // Revenir vers position initiale
            Vector3 direction = (initialPosition - transform.position).normalized;
            Vector3 newPosition = transform.position + direction * settings.descendSpeed * Time.deltaTime;

            // Clamper pour ne pas depasser
            if (isMovingUp)
                newPosition.y = Mathf.Min(newPosition.y, initialPosition.y);
            else
                newPosition.y = Mathf.Max(newPosition.y, initialPosition.y);

            transform.position = newPosition;
        }
        else
        {
            // Arreter le son si arrive
            if (audioSource.isPlaying)
            {
                audioSource.Stop();
            }
            isDescending = false;
        }
    }

    // Reset pour debug/replay
    public void ResetToInitialPosition()
    {
        transform.position = initialPosition;
        audioSource.Stop();
        isRising = false;
        isDescending = false;
    }
}