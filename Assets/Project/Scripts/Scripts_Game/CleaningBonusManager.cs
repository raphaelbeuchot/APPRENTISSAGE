using UnityEngine;
using System.Collections.Generic;

public class CleaningBonusManager : MonoBehaviour
{
    public static CleaningBonusManager Instance { get; private set; }

    [Header("References")]
    [SerializeField] private EnemyIconsUI enemyIconsUI;

    [Header("Audio")]
    [SerializeField] private AudioClip cleanSound;
    [SerializeField] private AudioClip allClearSound;

    private AudioSource audioSource;
    private List<EnemyHealth> registeredEnemies = new List<EnemyHealth>();
    private int cleanedCount = 0;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.spatialBlend = 0f;
        audioSource.playOnAwake = false;
    }

    private void Start()
    {
        EnemyHealth[] allEnemies = FindObjectsByType<EnemyHealth>(FindObjectsSortMode.None);
        foreach (EnemyHealth enemy in allEnemies)
        {
            if (enemy.GetComponent<BrightEyesController>() == null)
                registeredEnemies.Add(enemy);
        }
        Debug.Log("[CleaningBonus] Ennemis enregistres : " + registeredEnemies.Count);
    }

    private void OnEnable()
    {
        CorpsePitHandler.OnCorpseCleaned += HandleCorpseCleaned;
        if (enemyIconsUI != null)
            enemyIconsUI.OnAllIconsCleaned += HandleAllIconsCleaned;
    }

    private void OnDisable()
    {
        CorpsePitHandler.OnCorpseCleaned -= HandleCorpseCleaned;
        if (enemyIconsUI != null)
            enemyIconsUI.OnAllIconsCleaned -= HandleAllIconsCleaned;
    }

    private void HandleCorpseCleaned(CorpsePitHandler corpse)
    {
        cleanedCount++;

        if (CleaningCreditManager.Instance != null)
            CleaningCreditManager.Instance.AddCredit();

        if (enemyIconsUI != null && corpse.sourceEnemy != null)
            enemyIconsUI.SetIconCleaned(corpse.sourceEnemy);

        bool isLast = cleanedCount >= registeredEnemies.Count;

        if (!isLast)
        {
            if (audioSource != null && cleanSound != null)
                audioSource.PlayOneShot(cleanSound);
            CommentPanel.Show("Cleaned Up !");
        }

        Debug.Log("[CleaningBonus] Nettoye : " + cleanedCount + " / " + registeredEnemies.Count + " | Dernier : " + isLast);
    }

    private void HandleAllIconsCleaned()
    {
        if (audioSource != null && allClearSound != null)
            audioSource.PlayOneShot(allClearSound);
        CommentPanel.Show("Spick and span !");
        Debug.Log("[CleaningBonus] Tous les ennemis nettoyes.");
    }

    public List<EnemyHealth> GetRegisteredEnemies()
    {
        return registeredEnemies;
    }
}