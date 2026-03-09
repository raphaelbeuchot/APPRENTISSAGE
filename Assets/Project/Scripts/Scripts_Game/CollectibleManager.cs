using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;



public class CollectibleManager : MonoBehaviour
{
    public static CollectibleManager Instance { get; private set; }

    // Collectibles definitvement acquis (persiste entre les niveaux)
    private HashSet<string> permanentlyCollected = new HashSet<string>();

    // Collectibles ramasses dans le run actuel (reset a chaque rechargement)
    private HashSet<string> collectedThisRun = new HashSet<string>();

    private const string PREFS_KEY = "PermanentCollectibles";
    private AudioSource audioSource;


    

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        LoadPermanent();
        SceneManager.sceneLoaded += OnSceneLoaded;
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.spatialBlend = 0f;
    }

    // Cree l'instance automatiquement si elle n'existe pas encore
    public static CollectibleManager GetOrCreate()
    {
        if (Instance == null)
        {
            GameObject go = new GameObject("CollectibleManager");
            go.AddComponent<CollectibleManager>();
        }
        return Instance;
    }
    public void PlayCollectSound(AudioClip clip)
    {
        Debug.Log("[Collectible] PlayCollectSound appelé, clip : " + (clip != null ? clip.name : "NULL") + ", audioSource : " + (audioSource != null ? "OK" : "NULL"));
        if (clip != null && audioSource != null)
            audioSource.PlayOneShot(clip);
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        collectedThisRun.Clear();
        Debug.Log("[CollectibleManager] Run reset pour scene : " + scene.name);
    }

    // Appele par Collectible.cs au ramassage
    public void CollectThisRun(string id)
    {
        collectedThisRun.Add(id);
        Debug.Log("[CollectibleManager] Ramasse ce run : " + id);
    }

    // Appele par LevelManager quand le joueur finit le niveau
    public void ConfirmRunCollectibles()
    {
        foreach (string id in collectedThisRun)
        {
            permanentlyCollected.Add(id);
            Debug.Log("[CollectibleManager] Confirme definitivement : " + id);
        }
        SavePermanent();
        collectedThisRun.Clear();
    }

    public bool IsPermanentlyCollected(string id)
    {
        return permanentlyCollected.Contains(id);
    }

    public bool IsCollectedThisRun(string id)
    {
        return collectedThisRun.Contains(id);
    }

    // Nombre total ramasses definitivement (utile pour UI plus tard)
    public int GetPermanentCount()
    {
        return permanentlyCollected.Count;
    }

    // --- Sauvegarde PlayerPrefs ---
    // On serialise le HashSet en une seule string separee par des virgules

    void SavePermanent()
    {
        string joined = string.Join(",", permanentlyCollected);
        PlayerPrefs.SetString(PREFS_KEY, joined);
        PlayerPrefs.Save();
        Debug.Log("[CollectibleManager] Sauvegarde : " + joined);
    }

    void LoadPermanent()
    {
        permanentlyCollected.Clear();
        string saved = PlayerPrefs.GetString(PREFS_KEY, "");
        if (string.IsNullOrEmpty(saved)) return;

        string[] ids = saved.Split(',');
        foreach (string id in ids)
        {
            if (!string.IsNullOrEmpty(id))
                permanentlyCollected.Add(id);
        }
        Debug.Log("[CollectibleManager] Charge depuis PlayerPrefs : " + saved);
    }

    public void ResetAllCollectibles()
    {
        permanentlyCollected.Clear();
        PlayerPrefs.DeleteKey(PREFS_KEY);
        PlayerPrefs.Save();
        Debug.Log("[CollectibleManager] Tous les collectibles reinitialises.");
    }
}