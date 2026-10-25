using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

[System.Serializable]
public class LevelData
{
    public string levelName;
    public int sceneIndex;
    public string[] collectibleIDs;
}

// Awake avant les autres scripts : l'instance de MainMenu doit exister avant qu'un script
// appelle Instance, sinon une copie s'auto-instancie depuis Resources (sauvegarde coupee)
[DefaultExecutionOrder(-100)]
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
                    // Auto-instancie = niveau lance hors MainMenu (en editeur) : progression en memoire seulement
                    if (_instance != null) _instance.saveEnabled = false;
                    Debug.Log("[LevelProgressionManager] Auto-instancie depuis Resources (niveau lance hors MainMenu) : progression non sauvegardee.");
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
    [Tooltip("Ordre de la campagne solo (source unique)")]
    [SerializeField] private LevelCatalog catalog;

    // Construite depuis le catalogue au Awake (plus reglable dans l'Inspector)
    [System.NonSerialized] public List<LevelData> levels = new List<LevelData>();
    public int lastUnlockedLevelIndex { get; private set; } = -1;

    private ModifierType activeModifier = ModifierType.None;

    private HashSet<int> completedLevels = new HashSet<int>();
    private HashSet<int> unlockedLevels = new HashSet<int>();

    // Faux quand le manager s'est auto-instancie (niveau lance directement en editeur) :
    // la progression de la vraie partie (MainMenu > Continue) n'est pas ecrasee par les tests
    private bool saveEnabled = true;

    private const string PREFS_COMPLETED = "CompletedLevels";
    private const string PREFS_UNLOCKED = "UnlockedLevels";
    // Empreinte de l'ordre du catalogue au moment de la sauvegarde : la sauvegarde stocke des positions
    // dans la liste, donc si l'ordre change, la progression est remise a zero (sinon decalage silencieux)
    private const string PREFS_CATALOG_SIGNATURE = "CatalogSignature";

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);
        BuildLevelsFromCatalog();
        Load();
    }

    private void BuildLevelsFromCatalog()
    {
        levels.Clear();
        if (catalog == null)
        {
            Debug.LogError("[LevelProgression] LevelCatalog non assigne !");
            return;
        }

        for (int i = 0; i < catalog.levels.Count; i++)
        {
            LevelCatalogEntry entry = catalog.levels[i];
            if (entry == null || string.IsNullOrEmpty(entry.scenePath))
            {
                Debug.LogError("[LevelProgression] Catalogue, ligne " + i + " : aucune scene, ligne ignoree.");
                continue;
            }

            int buildIndex = SceneUtility.GetBuildIndexByScenePath(entry.scenePath);
            if (buildIndex < 0)
            {
                Debug.LogError("[LevelProgression] Catalogue, ligne " + i + " : scene absente des Build Settings (" + entry.scenePath + "), ligne ignoree.");
                continue;
            }

            levels.Add(new LevelData
            {
                levelName = string.IsNullOrEmpty(entry.levelName) ? System.IO.Path.GetFileNameWithoutExtension(entry.scenePath) : entry.levelName,
                sceneIndex = buildIndex,
                collectibleIDs = entry.collectibleIDs
            });
        }
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
        Save(force: true);
        Debug.Log("[LevelProgression] Progression reinitalisee.");
        if (CollectibleManager.Instance != null)
            CollectibleManager.Instance.ResetAllCollectibles();
        if (CleaningCreditManager.Instance != null)
            CleaningCreditManager.Instance.ResetCredits();
        foreach (var level in levels)
            LevelStatsTracker.ResetAttempts(level.sceneIndex);
        PlayerPrefs.Save();
        lastUnlockedLevelIndex = -1;
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

    public int GetNextLevelSceneIndex(int currentSceneIndex)
    {
        int idx = GetLevelIndex(currentSceneIndex);
        if (idx == -1 || idx + 1 >= levels.Count) return -1;
        return levels[idx + 1].sceneIndex;
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

    // force : la reinitialisation explicite est toujours ecrite, meme hors MainMenu
    private void Save(bool force = false)
    {
        if (!saveEnabled && !force)
        {
            Debug.Log("[LevelProgression] Progression non sauvegardee (niveau lance hors MainMenu).");
            return;
        }

        PlayerPrefs.SetString(PREFS_COMPLETED, SerializeSet(completedLevels));
        PlayerPrefs.SetString(PREFS_UNLOCKED, SerializeSet(unlockedLevels));
        PlayerPrefs.SetString(PREFS_CATALOG_SIGNATURE, ComputeCatalogSignature());
        PlayerPrefs.SetInt("LastUnlockedLevel", lastUnlockedLevelIndex);
        PlayerPrefs.Save();
    }

    private void Load()
    {
        completedLevels = DeserializeSet(PlayerPrefs.GetString(PREFS_COMPLETED, ""));
        unlockedLevels = DeserializeSet(PlayerPrefs.GetString(PREFS_UNLOCKED, ""));
        lastUnlockedLevelIndex = PlayerPrefs.GetInt("LastUnlockedLevel", -1);

        // Ordre du catalogue change depuis la derniere sauvegarde -> progression remise a zero.
        // Pas d'empreinte (sauvegarde anterieure a ce controle) : on adopte l'ordre actuel sans rien effacer.
        string savedSignature = PlayerPrefs.GetString(PREFS_CATALOG_SIGNATURE, "");
        string currentSignature = ComputeCatalogSignature();
        if (HasSave() && savedSignature != "" && savedSignature != currentSignature)
        {
            completedLevels.Clear();
            unlockedLevels.Clear();
            lastUnlockedLevelIndex = -1;
            Debug.LogWarning("[LevelProgression] L'ordre du catalogue a change depuis la derniere sauvegarde : progression remise a zero.");
            if (levels.Count > 0) unlockedLevels.Add(0);
            if (levels.Count > 1) unlockedLevels.Add(1);
            Save();
        }
        else if (HasSave() && savedSignature == "" && saveEnabled)
        {
            PlayerPrefs.SetString(PREFS_CATALOG_SIGNATURE, currentSignature);
            PlayerPrefs.Save();
        }

        if (levels.Count > 0) unlockedLevels.Add(0);
        if (levels.Count > 1) unlockedLevels.Add(1);

        Debug.Log("[LevelProgression] Charge. Completes : " + completedLevels.Count + " | Debloques : " + unlockedLevels.Count);
    }

    // Hash FNV-1a des chemins de scene du catalogue, dans l'ordre (stable d'une session a l'autre)
    private string ComputeCatalogSignature()
    {
        if (catalog == null) return "";

        uint hash = 2166136261;
        foreach (LevelCatalogEntry entry in catalog.levels)
        {
            string path = (entry != null ? entry.scenePath : "") + "|";
            foreach (char c in path)
            {
                hash ^= c;
                hash *= 16777619;
            }
        }
        return hash.ToString("x8");
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