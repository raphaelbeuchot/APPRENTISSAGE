using UnityEngine;
using System.Collections.Generic;
public class CleaningBonusManager : MonoBehaviour
{
    public static CleaningBonusManager Instance { get; private set; }
    [Header("References")]
    [SerializeField] private EnemyIconsUI enemyIconsUI;
    [Header("Audio")]
    [SerializeField] private AudioClip cleanSound;
    private AudioSource audioSource;
    private int cleaningScore = 0;
    private int totalCorpses = 0;
    private List<CorpsePitHandler> registeredCorpses = new List<CorpsePitHandler>();
    public event System.Action OnAllCorpsesCleaned;
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.spatialBlend = 0f;
        audioSource.playOnAwake = false;
    }
    private void OnEnable()
    {
        CorpsePitHandler.OnCorpseCleaned += HandleCorpseCleaned;
    }
    private void OnDisable()
    {
        CorpsePitHandler.OnCorpseCleaned -= HandleCorpseCleaned;
    }
    public void Initialize()
    {
        CorpsePitHandler[] allCorpses = FindObjectsByType<CorpsePitHandler>(FindObjectsSortMode.None);
        totalCorpses = allCorpses.Length;
        foreach (CorpsePitHandler c in allCorpses)
            registeredCorpses.Add(c);
        Debug.Log("[CleaningBonus] Total corpses initialise : " + totalCorpses);
    }
    public void RegisterCorpse(CorpsePitHandler corpse)
    {
        if (registeredCorpses.Contains(corpse)) return;
        registeredCorpses.Add(corpse);
        totalCorpses++;
        Debug.Log("[CleaningBonus] Corpse enregistre. Total : " + totalCorpses);
    }
    private void HandleCorpseCleaned(CorpsePitHandler corpse)
    {
        if (!registeredCorpses.Contains(corpse))
            RegisterCorpse(corpse);

        cleaningScore++;
        if (audioSource != null && cleanSound != null)
            audioSource.PlayOneShot(cleanSound);
        CommentPanel.Show("Cleaned Up !");
        if (enemyIconsUI != null && corpse.sourceEnemy != null)
            enemyIconsUI.SetIconCleaned(corpse.sourceEnemy);
        
        Debug.Log("[CleaningBonus] Score nettoyage : " + cleaningScore + " / " + totalCorpses);
        if (cleaningScore >= totalCorpses && totalCorpses > 0)
            OnAllCorpsesCleaned?.Invoke();
    }
    public int GetCleaningScore() => cleaningScore;
    public int GetTotalCorpses() => totalCorpses;
}