using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class LevelData
{
    public string levelName;
    public int sceneIndex;
    public string[] collectibleIDs;
}

public class LevelProgressionManager : MonoBehaviour
{
    private static LevelProgressionManager _instance;

    public static LevelProgressionManager Instance
    {
        get
        {
            if (_instance == null)
            {
                GameObject prefab = Resources.Load<GameObject>("LevelProgressionManager");
                if (prefab != null)
                {
                    Instantiate(prefab);
                    Debug.Log("[LevelProgressionManager] Auto-instancie depuis Resources.");
                }
                else
                {
                    Debug.LogError("[LevelProgressionManager] Prefab introuvable dans Resources !");
                }
            }
            return _instance;
        }
    }

    [Header("Configuration niveaux")]
    public List<LevelData> levels = new List<LevelData>();
    public int lastUnlockedLevelIndex { get; private set; } = -1;

    private ModifierType activeModifier = ModifierType.None;

    private HashSet<int> completedLevels = new HashSet<int>();
    private HashSet<int> unlockedLevels = new HashSet<int>();

    private const string PREFS_COMPLETED = "CompletedLevels";
    private const string PREFS_UNLOCKED = "UnlockedLevels";

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);
        Load();
    }

    public void CompleteLevel(int sceneIndex)
    {
        int idx = GetLevelIndex(sceneIndex);
        if (idx == -1)
        {
            Debug.LogWarning("[LevelProgression] Scene introuvable : " + sceneIndex);
            return;
        }

        completedLevels.Add(idx);

        if (idx + 1 < levels.Count)
            unlockedLevels.Add(idx + 1);
        lastUnlockedLevelIndex = idx + 1;

        Save();
        Debug.Log("[LevelProgression] Niveau complete : " + levels[idx].levelName + " | Suivant debloque : " + (idx + 1 < levels.Count ? levels[idx + 1].levelName : "aucun"));
    }

    public bool IsCompleted(int sceneIndex)
    {
        int idx = GetLevelIndex(sceneIndex);
        if (idx == -1) return false;
        return completedLevels.Contains(idx);
    }

    public bool IsUnlocked(int sceneIndex)
    {
        int idx = GetLevelIndex(sceneIndex);
        if (idx == -1) return false;
        return unlockedLevels.Contains(idx);
    }

    public bool HasSave()
    {
        return PlayerPrefs.HasKey(PREFS_UNLOCKED);
    }

    public void ResetProgression()
    {
        completedLevels.Clear();
        unlockedLevels.Clear();
        if (levels.Count > 0) unlockedLevels.Add(0);
        if (levels.Count > 1) unlockedLevels.Add(1);
        Save();
        Debug.Log("[LevelProgression] Progression reinitalisee.");
        if (CollectibleManager.Instance != null)
            CollectibleManager.Instance.ResetAllCollectibles();
        if (CleaningCreditManager.Instance != null)
            CleaningCreditManager.Instance.ResetCredits();
    }

    public int GetCollectedCountForLevel(int sceneIndex)
    {
        int idx = GetLevelIndex(sceneIndex);
        if (idx == -1) return 0;
        LevelData data = levels[idx];
        if (data.collectibleIDs == null || data.collectibleIDs.Length == 0) return 0;

        int count = 0;
        foreach (string id in data.collectibleIDs)
        {
            if (!string.IsNullOrEmpty(id) && CollectibleManager.Instance != null && CollectibleManager.Instance.IsPermanentlyCollected(id))
                count++;
        }
        return count;
    }

    public int GetTotalCollectiblesForLevel(int sceneIndex)
    {
        int idx = GetLevelIndex(sceneIndex);
        if (idx == -1) return 0;
        LevelData data = levels[idx];
        if (data.collectibleIDs == null) return 0;
        return data.collectibleIDs.Length;
    }

    public List<LevelData> GetAllLevels()
    {
        return levels;
    }

    private int GetLevelIndex(int sceneIndex)
    {
        for (int i = 0; i < levels.Count; i++)
        {
            if (levels[i].sceneIndex == sceneIndex)
                return i;
        }
        return -1;
    }

    private void Save()
    {
        PlayerPrefs.SetString(PREFS_COMPLETED, SerializeSet(completedLevels));
        PlayerPrefs.SetString(PREFS_UNLOCKED, SerializeSet(unlockedLevels));
        PlayerPrefs.Save();
    }

    private void Load()
    {
        completedLevels = DeserializeSet(PlayerPrefs.GetString(PREFS_COMPLETED, ""));
        unlockedLevels = DeserializeSet(PlayerPrefs.GetString(PREFS_UNLOCKED, ""));

        if (levels.Count > 0) unlockedLevels.Add(0);
        if (levels.Count > 1) unlockedLevels.Add(1);

        Debug.Log("[LevelProgression] Charge. Completes : " + completedLevels.Count + " | Debloques : " + unlockedLevels.Count);
    }

    private string SerializeSet(HashSet<int> set)
    {
        return string.Join(",", set);
    }

    private HashSet<int> DeserializeSet(string data)
    {
        HashSet<int> result = new HashSet<int>();
        if (string.IsNullOrEmpty(data)) return result;
        foreach (string s in data.Split(','))
        {
            if (int.TryParse(s, out int val))
                result.Add(val);
        }
        return result;
    }
    public void SetActiveModifier(ModifierType modifier)
    {
        activeModifier = modifier;
        Debug.Log($"[LevelProgression] Modifier actif : {modifier}");
    }

    public ModifierType GetActiveModifier()
    {
        return activeModifier;
    }
}