using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Chantier 1 — Tracker des stats de run pour un niveau.
/// Pas d'affichage : les donnees sont lisibles via Instance.*
///
/// Setup : ajouter ce script comme composant sur le meme GO que LevelManager.
///
/// Autres scripts qui appellent RegisterXxx() :
///   - PlayerHealth.TakeSentinelShot    -> RegisterSentinelShot()
///   - PlayerDetectionFeedback.OnDetected -> RegisterDetection()
///   - GameManager.OnEnemyKilled        -> RegisterKill()
///   (les morts et les cleanups sont branches en interne)
/// </summary>
public class LevelStatsTracker : MonoBehaviour
{
    public static LevelStatsTracker Instance { get; private set; }

    // --- Stats de la run en cours ---
    public int DeathCount        { get; private set; }
    public int ShotReceivedCount { get; private set; }
    public int DetectionCount    { get; private set; }
    public int KillCount         { get; private set; }
    public int CleanupCount      { get; private set; }
    public float ElapsedTime     { get; private set; }

    /// <summary>
    /// Nombre total de tentatives pour ce niveau (persiste entre sessions via PlayerPrefs).
    /// Increment au chargement, remis a 0 quand le niveau est valide.
    /// </summary>
    public int AttemptCount { get; private set; }

    private bool timerRunning = false;
    private PlayerHealth trackedHealth;

    private string AttemptKey => "Attempts_" + SceneManager.GetActiveScene().buildIndex;

    // ============================================
    // UNITY LIFECYCLE
    // ============================================

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Chaque chargement de scene = une tentative
        AttemptCount = PlayerPrefs.GetInt(AttemptKey, 0) + 1;
        PlayerPrefs.SetInt(AttemptKey, AttemptCount);
        PlayerPrefs.Save();

        Debug.Log($"[LevelStats] Tentative #{AttemptCount} — scene {SceneManager.GetActiveScene().name}");
    }

    void Start()
    {
        // Branchement mort joueur
        trackedHealth = FindObjectOfType<PlayerHealth>();
        if (trackedHealth != null)
            trackedHealth.OnDeath += HandleDeath;
        else
            Debug.LogWarning("[LevelStats] PlayerHealth introuvable — morts non traquees.");

        // Branchement cleanups (event statique existant)
        CorpsePitHandler.OnCorpseCleaned += HandleCleanup;

        timerRunning = true;
    }

    void Update()
    {
        if (timerRunning)
            ElapsedTime += Time.deltaTime;
    }

    void OnDestroy()
    {
        if (trackedHealth != null)
            trackedHealth.OnDeath -= HandleDeath;

        CorpsePitHandler.OnCorpseCleaned -= HandleCleanup;
    }

    // ============================================
    // HANDLERS INTERNES
    // ============================================

    private void HandleDeath()
    {
        DeathCount++;
        Debug.Log($"[LevelStats] Morts : {DeathCount}");
    }

    private void HandleCleanup(CorpsePitHandler _)
    {
        CleanupCount++;
        Debug.Log($"[LevelStats] Cleanups : {CleanupCount}");
    }

    // ============================================
    // API — appeles par les autres scripts (1 ligne chacun)
    // ============================================

    /// <summary>Appeler depuis PlayerHealth.TakeSentinelShot</summary>
    public void RegisterSentinelShot()
    {
        ShotReceivedCount++;
        Debug.Log($"[LevelStats] Tirs sentinel : {ShotReceivedCount}");
    }

    /// <summary>Appeler depuis PlayerDetectionFeedback.OnDetected</summary>
    public void RegisterDetection()
    {
        DetectionCount++;
        Debug.Log($"[LevelStats] Detections : {DetectionCount}");
    }

    /// <summary>Appeler depuis GameManager.OnEnemyKilled</summary>
    public void RegisterKill()
    {
        KillCount++;
        Debug.Log($"[LevelStats] Kills : {KillCount}");
    }

    // ============================================
    // CONTROLE DU TIMER
    // ============================================

    public void PauseTimer()  => timerRunning = false;
    public void ResumeTimer() => timerRunning = true;

    // ============================================
    // FIN DE NIVEAU
    // ============================================

    /// <summary>
    /// Appeler depuis LevelManager quand le niveau est reussi (pas quand on restart).
    /// Stoppe le timer et remet le compteur d'essais a 0 pour cette scene.
    /// </summary>
    public void OnLevelCompleted()
    {
        PauseTimer();
        PlayerPrefs.SetInt(AttemptKey, 0);
        PlayerPrefs.Save();

        Debug.Log($"[LevelStats] NIVEAU COMPLETE — " +
                  $"Essais:{AttemptCount} | " +
                  $"Morts:{DeathCount} | " +
                  $"Tirs:{ShotReceivedCount} | " +
                  $"Detections:{DetectionCount} | " +
                  $"Kills:{KillCount} | " +
                  $"Cleanups:{CleanupCount} | " +
                  $"Temps:{ElapsedTime:F1}s");
    }
}
