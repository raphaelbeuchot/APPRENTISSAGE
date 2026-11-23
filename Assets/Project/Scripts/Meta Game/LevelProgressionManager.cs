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

    // --- Bilan de la partie en cours (remis a zero a New Game) ---
    // Survivor : chaque niveau reussi au premier essai (LevelStatsTracker.AttemptCount, mort, Restart
    // et quitter/Continuer rechargent le niveau). Ghost : l'essai reussi de chaque niveau sans detection.
    // Non classee : une aide du DevPanel a servi. Terminee : dernier niveau de la campagne reussi.
    private const string PREFS_RUN_SURVIVOR = "RunSurvivor";
    private const string PREFS_RUN_GHOST = "RunGhost";
    private const string PREFS_RUN_UNRANKED = "RunUnranked";
    private const string PREFS_RUN_FINISHED = "RunFinished";
    // --- Marques de fin de jeu, par difficulte (permanentes : ni New Game ni le catalogue ne les effacent) ---
    private const string PREFS_ENDING_MARKS = "EndingMarks_"; // + (int)OptionsManager.Difficulty

    [System.Flags]
    public enum EndingMark { None = 0, Finished = 1, Ghost = 2, Survivor = 4, GhostSurvivor = 8 }

    public bool RunSurvivor { get; private set; }
    public bool RunGhost { get; private set; }
    public bool RunUnranked { get; private set; }
    public bool RunFinished { get; private set; }

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

#if DEMO_BUILD
            // Build demo : seulement les niveaux coches "In Demo"
            if (!entry.inDemo) continue;
#endif

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

        // Bilan de partie : seulement pendant la vraie partie (pas en test editeur, pas en rejouant apres la fin)
        if (saveEnabled && !RunFinished)
        {
            UpdateRunRecord();
            if (idx == levels.Count - 1) FinishRun();
        }

        completedLevels.Add(idx);

        if (idx + 1 < levels.Count)
            unlockedLevels.Add(idx + 1);
        lastUnlockedLevelIndex = idx + 1;

        Save();
        Debug.Log("[LevelProgression] Niveau complete : " + levels[idx].levelName + " | Suivant debloque : " + (idx + 1 < levels.Count ? levels[idx + 1].levelName : "aucun"));
    }

    // Lit les stats de l'essai qui vient de reussir (LevelStatsTracker, sur le LevelManager de chaque niveau)
    private void UpdateRunRecord()
    {
        if (DevFlags.UsedThisSession) RunUnranked = true;

        LevelStatsTracker stats = LevelStatsTracker.Instance;
        if (stats == null)
        {
            Debug.LogWarning("[LevelProgression] Pas de LevelStatsTracker : Ghost et Survivor perdus pour cette partie.");
            RunSurvivor = false;
            RunGhost = false;
            return;
        }

        if (stats.AttemptCount > 1) RunSurvivor = false;
        if (stats.DetectionCount > 0) RunGhost = false;
        Debug.Log("[LevelProgression] Bilan de partie : Survivor " + RunSurvivor + " | Ghost " + RunGhost + (RunUnranked ? " | NON CLASSEE (DevPanel)" : ""));
    }

    // Dernier niveau reussi : la partie est close, ses marques s'ajoutent a celles de sa difficulte
    private void FinishRun()
    {
        RunFinished = true;

        if (RunUnranked)
        {
            Debug.Log("[LevelProgression] Jeu termine, partie non classee (DevPanel) : aucune marque.");
            return;
        }
        if (OptionsManager.Instance == null)
        {
            Debug.LogWarning("[LevelProgression] Jeu termine mais OptionsManager absent : difficulte inconnue, aucune marque.");
            return;
        }
        FinishRunFor(OptionsManager.Instance.difficulty);
    }

    private void FinishRunFor(OptionsManager.Difficulty difficulty)
    {
        EndingMark marks = EndingMark.Finished;
        if (RunGhost) marks |= EndingMark.Ghost;
        if (RunSurvivor) marks |= EndingMark.Survivor;
        if (RunGhost && RunSurvivor) marks |= EndingMark.GhostSurvivor;

        string key = PREFS_ENDING_MARKS + (int)difficulty;
        PlayerPrefs.SetInt(key, PlayerPrefs.GetInt(key, 0) | (int)marks);
        PlayerPrefs.Save();
        Debug.Log("[LevelProgression] Jeu termine en " + difficulty + " : " + marks);
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    // DevPanel : fin de jeu simulee avec le bilan de la partie en cours (DevPanel ignore, sinon toujours
    // non classee), puis marques affichees. Le bilan reste ouvert : on peut recommencer.
    public void DevSimulateGameEnd()
    {
        bool unranked = RunUnranked;
        UpdateRunRecord();
        RunUnranked = unranked;

        bool hadOptions = OptionsManager.Instance != null;
        if (!hadOptions) Debug.Log("[LevelProgression] TEST : OptionsManager absent (niveau lance hors MainMenu), marques en " + OptionsManager.Difficulty.Superbanco);
        FinishRunFor(hadOptions ? OptionsManager.Instance.difficulty : OptionsManager.Difficulty.Superbanco);
        DevLogEndingMarks();
    }

    // DevPanel : debloque tous les niveaux de la campagne jusqu'a la scene donnee comprise (test d'un niveau
    // lance hors ordre, ex. Trou normand). En memoire seulement : lance hors MainMenu, rien n'est sauvegarde ;
    // depuis MainMenu, ca partirait avec la prochaine sauvegarde.
    public void DevUnlockUpTo(int sceneIndex)
    {
        int idx = GetLevelIndex(sceneIndex);
        if (idx == -1)
        {
            Debug.LogWarning("[LevelProgression] TEST : scene " + sceneIndex + " hors catalogue, rien de debloque.");
            return;
        }
        for (int i = 0; i <= idx; i++) unlockedLevels.Add(i);
        if (lastUnlockedLevelIndex < idx) lastUnlockedLevelIndex = idx;
        Debug.Log("[LevelProgression] TEST : niveaux debloques jusqu'a " + levels[idx].levelName + " compris.");
    }

    public void DevLogEndingMarks()
    {
        foreach (OptionsManager.Difficulty d in System.Enum.GetValues(typeof(OptionsManager.Difficulty)))
            Debug.Log("[LevelProgression] Marques " + d + " : " + GetEndingMarks(d));
    }

    public void DevClearEndingMarks()
    {
        foreach (OptionsManager.Difficulty d in System.Enum.GetValues(typeof(OptionsManager.Difficulty)))
            PlayerPrefs.DeleteKey(PREFS_ENDING_MARKS + (int)d);
        PlayerPrefs.Save();
        Debug.Log("[LevelProgression] TEST : marques de fin de jeu effacees.");
    }
#endif

    /// <summary>Marques de fin de jeu obtenues pour une difficulte, toutes parties confondues.</summary>
    public EndingMark GetEndingMarks(OptionsManager.Difficulty difficulty)
    {
        return (EndingMark)PlayerPrefs.GetInt(PREFS_ENDING_MARKS + (int)difficulty, 0);
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
        // Nouvelle partie : bilan remis a neuf (les marques de fin de jeu restent)
        RunSurvivor = true;
        RunGhost = true;
        RunUnranked = false;
        RunFinished = false;
        DevFlags.UsedThisSession = false;
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
        PlayerPrefs.SetInt(PREFS_RUN_SURVIVOR, RunSurvivor ? 1 : 0);
        PlayerPrefs.SetInt(PREFS_RUN_GHOST, RunGhost ? 1 : 0);
        PlayerPrefs.SetInt(PREFS_RUN_UNRANKED, RunUnranked ? 1 : 0);
        PlayerPrefs.SetInt(PREFS_RUN_FINISHED, RunFinished ? 1 : 0);
        PlayerPrefs.Save();
    }

    private void Load()
    {
        completedLevels = DeserializeSet(PlayerPrefs.GetString(PREFS_COMPLETED, ""));
        unlockedLevels = DeserializeSet(PlayerPrefs.GetString(PREFS_UNLOCKED, ""));
        lastUnlockedLevelIndex = PlayerPrefs.GetInt("LastUnlockedLevel", -1);
        // Partie commencee avant ce bilan : pas de preuve, donc ni Ghost ni Survivor (0 par defaut)
        RunSurvivor = PlayerPrefs.GetInt(PREFS_RUN_SURVIVOR, 0) == 1;
        RunGhost = PlayerPrefs.GetInt(PREFS_RUN_GHOST, 0) == 1;
        RunUnranked = PlayerPrefs.GetInt(PREFS_RUN_UNRANKED, 0) == 1;
        RunFinished = PlayerPrefs.GetInt(PREFS_RUN_FINISHED, 0) == 1;

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