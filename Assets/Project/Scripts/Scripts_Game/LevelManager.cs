using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class LevelManager : MonoBehaviour
{
    [Header("References")]
    public GoalDoor goalDoor;
    public GameManager gameManager;
    public PlayerPhysicsMovement player;
    public PlayerHealth playerHealth;

    [Header("UI")]
    public VictoryUI victoryUI;
    public GameOverUI gameOverUI;
    private PlayerVictoryScale playerVictoryScale;



    [Header("Configuration")]
    public float delayBeforeNextLevel = 3f;
    public float delayBeforeRestart = 2f;

    [Header("Level Rules")]
    [Tooltip("Si coche, skip l'ecran Victory UI et passe direct au niveau suivant")]
    public bool skipVictoryUI = false;

    [Tooltip("Si coche, la porte reste fermee jusqu'a ce que tous les ennemis soient tues")]
    public bool requireAllEnemiesKilled = false;

    [Header("Level End (Legacy)")]
    [SerializeField] private bool isTutorialLevel = false;

    private bool levelCompleted = false;
    private bool gameOver = false;
    private bool doorHasBeenActivated = false;

    void Awake()
    {
        if (ModifierApplier.Instance == null)
        {
            GameObject go = new GameObject("ModifierApplier");
            go.AddComponent<ModifierApplier>();
        }
    }

    void Start()
    {
        if (ModifierApplier.Instance == null)
        {
            GameObject go = new GameObject("ModifierApplier");
            go.AddComponent<ModifierApplier>();
        }

        if (goalDoor == null)
            goalDoor = FindObjectOfType<GoalDoor>();

        if (gameManager == null)
            gameManager = FindObjectOfType<GameManager>();

        //if (CleaningBonusManager.Instance != null)
          //  CleaningBonusManager.Instance.OnAllCorpsesCleaned += OnAllCorpsesCleaned;

        if (player == null)
            player = FindObjectOfType<PlayerPhysicsMovement>();
        if (playerVictoryScale == null)
            playerVictoryScale = FindObjectOfType<PlayerVictoryScale>();

        if (playerHealth == null && player != null)
            playerHealth = player.GetComponent<PlayerHealth>();

        if (CleaningCreditManager.Instance != null)
            CleaningCreditManager.Instance.SnapshotLevelStart();

        if (victoryUI == null)
        {
            VictoryUI[] allVictoryUI = Resources.FindObjectsOfTypeAll<VictoryUI>();
            foreach (VictoryUI ui in allVictoryUI)
            {
                if (ui.gameObject.scene.name != null)
                {
                    victoryUI = ui;
                    break;
                }
            }
        }

        if (gameOverUI == null)
        {
            GameOverUI[] allGameOverUI = Resources.FindObjectsOfTypeAll<GameOverUI>();
            foreach (GameOverUI ui in allGameOverUI)
            {
                if (ui.gameObject.scene.name != null)
                {
                    gameOverUI = ui;
                    break;
                }
            }
        }

        if (goalDoor != null)
        {
            goalDoor.OnPlayerReached += OnPlayerReachedGoal;

            if (requireAllEnemiesKilled)
                goalDoor.SetActive(false);
        }
        else
        {
            Debug.LogWarning("LevelManager: Aucune GoalDoor trouvee dans la scene!");
        }

        if (playerHealth != null)
            playerHealth.OnDeath += OnPlayerDeath;
        else
            Debug.LogWarning("LevelManager: PlayerHealth non trouve!");

        Time.timeScale = 1f;
    }

    void Update()
    {
        if (requireAllEnemiesKilled && !doorHasBeenActivated && gameManager != null && goalDoor != null)
        {
            if (gameManager.enemiesKilled >= gameManager.totalEnemies && gameManager.totalEnemies > 0)
            {
                doorHasBeenActivated = true;
                StartCoroutine(ActivateDoorDelayed());
            }
        }
    }

    IEnumerator ActivateDoorDelayed()
    {
        yield return new WaitForSeconds(1f);

        if (goalDoor != null)
        {
            goalDoor.ActivateDoor();
            Debug.Log("[LevelManager] Porte activee apres delai 1s");
        }
    }

    // ============================================
    // VICTOIRE
    // ============================================
    private void OnAllCorpsesCleaned()
    {
        if (doorHasBeenActivated) return;
        doorHasBeenActivated = true;
        StartCoroutine(ActivateDoorDelayed());
    }
    void OnPlayerReachedGoal(GameObject playerObject)
    {
        GetComponent<GoalDoorDebug>()?.TriggerDebug();
        if (levelCompleted || gameOver) return;

        if (SceneManager.GetActiveScene().buildIndex == 3)
        {
            if (player != null) player.enabled = false;
            Time.timeScale = 1f;
            SceneManager.LoadScene(4);
            return;
        }

        levelCompleted = true;
        Debug.Log("=== NIVEAU COMPLETE! ===");

        if (CollectibleManager.Instance != null)
            CollectibleManager.Instance.ConfirmRunCollectibles();

        // NOUVEAU : notifie la progression, exception tuto2
        if (LevelProgressionManager.Instance != null)
        {
            int sceneToComplete = SceneManager.GetActiveScene().buildIndex == 4 ? 3 : SceneManager.GetActiveScene().buildIndex;
            
            LevelProgressionManager.Instance.CompleteLevel(sceneToComplete);
        }

        CountdownManager countdown = FindObjectOfType<CountdownManager>();
        if (countdown != null) countdown.StopAmbient();

        if (player != null)
            player.enabled = false;
        if (playerVictoryScale != null)
            playerVictoryScale.TriggerScale();


        if (skipVictoryUI)
        {
            if (isTutorialLevel)
                LoadLevelSelect();
            else
                LoadWheelOrLevelSelect();
        }
        else
        {
            if (victoryUI != null)
                victoryUI.Show(playerHealth);
            else
            {
                Debug.LogWarning("VictoryUI non trouve! Chargement automatique.");
                Invoke(isTutorialLevel ? nameof(LoadLevelSelect) : nameof(LoadWheelOrLevelSelect), delayBeforeNextLevel);
            }
        }
    }
    public void LoadWheelOrLevelSelect()
    {
        if (CleaningCreditManager.Instance != null && CleaningCreditManager.Instance.HasCredits())
            LoadWheelOfFortune();
        else
            LoadLevelSelect();
    }

    // ============================================
    // DEFAITE
    // ============================================

    void OnPlayerDeath()
    {
        if (gameOver || levelCompleted) return;

        gameOver = true;
        Debug.Log("=== GAME OVER ===");

        if (player != null)
            player.enabled = false;

        if (gameOverUI != null)
            gameOverUI.Show();
        if (CleaningCreditManager.Instance != null)
            CleaningCreditManager.Instance.RollbackToSnapshot();
        else
        {
            Debug.LogWarning("GameOverUI non trouve! Redemarrage automatique.");
            Invoke(nameof(RestartLevel), delayBeforeRestart);
        }
    }

    // ============================================
    // NAVIGATION DE NIVEAU
    // ============================================

    public void LoadLevelSelect()
    {
        Time.timeScale = 1f;
        LoadingScreenManager.TargetSceneIndex = 2;
        SceneManager.LoadScene(1);
    }

    public void LoadWheelOfFortune()
    {
        Time.timeScale = 1f;
        if (SceneFader.Instance != null)
            SceneFader.Instance.FadeToScene(19);
        else
        {
            LoadingScreenManager.TargetSceneIndex = 2;
            SceneManager.LoadScene(1);
        }
    }

    public void RestartLevel()
    {
        if (CleaningCreditManager.Instance != null)
            CleaningCreditManager.Instance.RollbackToSnapshot();

        CountdownManager countdown = FindObjectOfType<CountdownManager>();
        if (countdown != null) countdown.StopAmbient();

        PlayerPrefs.SetInt("AutoStartCountdown", 1); // Toujours set, tuto ou non

        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void QuitGame()
    {
        Debug.Log("Quitter le jeu...");

        Time.timeScale = 1f;

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    public void OnPlayerReachedLevelEnd(GameObject playerObject)
    {
        if (levelCompleted || gameOver) return;

        levelCompleted = true;
        Debug.Log("=== NIVEAU COMPLETE (Level End Trigger) ===");

        CountdownManager countdown = FindObjectOfType<CountdownManager>();
        if (countdown != null) countdown.StopAmbient();

        if (player != null)
            player.enabled = false;

        Invoke(nameof(LoadLevelSelect), 0.5f);
    }

    // ============================================
    // CLEANUP
    // ============================================


    void OnDestroy()
    {
        if (goalDoor != null)
            goalDoor.OnPlayerReached -= OnPlayerReachedGoal;

        if (playerHealth != null)
            playerHealth.OnDeath -= OnPlayerDeath;

        //if (CleaningBonusManager.Instance != null)
          //  CleaningBonusManager.Instance.OnAllCorpsesCleaned -= OnAllCorpsesCleaned;
    }
}