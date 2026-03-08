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
    public static LevelProgressionManager Instance { get; private set; }

    [Header("Configuration niveaux")]
    public List<LevelData> levels = new List<LevelData>();
    public int lastUnlockedLevelIndex { get; private set; } = -1;


    private HashSet<int> completedLevels = new HashSet<int>();
    private HashSet<int> unlockedLevels = new HashSet<int>();

    private const string PREFS_COMPLETED = "CompletedLevels";
    private const string PREFS_UNLOCKED = "UnlockedLevels";

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        Load();
    }

    // Appele par LevelManager quand le joueur passe la GoalDoor
    public void CompleteLevel(int sceneIndex)
    {
        int idx = GetLevelIndex(sceneIndex);
        if (idx == -1)
        {
            Debug.LogWarning("[LevelProgression] Scene introuvable : " + sceneIndex);
            return;
        }

        completedLevels.Add(idx);

        // Debloque le niveau suivant
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

    // True si au moins une sauvegarde existe
    public bool HasSave()
    {
        return PlayerPrefs.HasKey(PREFS_UNLOCKED);
    }

    // Appele par MainMenu sur New Game
    public void ResetProgression()
    {
        completedLevels.Clear();
        unlockedLevels.Clear();
        if (levels.Count > 0) unlockedLevels.Add(0);
        if (levels.Count > 1) unlockedLevels.Add(1);
        if (levels.Count > 2) unlockedLevels.Add(2);
        Save();
        Debug.Log("[LevelProgression] Progression reinitalisee.");
        if (CollectibleManager.Instance != null)
            CollectibleManager.Instance.ResetAllCollectibles();
    }

    // Nombre de collectibles ramasses definitivement pour un niveau
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

    // --- Helpers prive ---

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

        // Le premier niveau est toujours debloque, ainsi que les 2 tutos
        if (levels.Count > 0) unlockedLevels.Add(0);
        if (levels.Count > 1) unlockedLevels.Add(1);
        if (levels.Count > 2) unlockedLevels.Add(2);

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
}