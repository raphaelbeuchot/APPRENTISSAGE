using UnityEngine;

public class CleaningCreditManager : MonoBehaviour
{
    public static CleaningCreditManager Instance { get; private set; }

    private const string CREDITS_KEY = "CleaningCredits";

    public static event System.Action OnCreditsChanged;

    private int creditsAtLevelStart = 0;


    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void OnEnable()
    {
        CorpsePitHandler.OnCorpseCleaned += OnCorpseCleaned;
    }

    void OnDisable()
    {
        CorpsePitHandler.OnCorpseCleaned -= OnCorpseCleaned;
    }

    private void OnCorpseCleaned(CorpsePitHandler corpse)
    {
        AddCredit();
    }

    public void AddCredit()
    {
        int credits = GetCredits();
        credits++;
        PlayerPrefs.SetInt(CREDITS_KEY, credits);
        PlayerPrefs.Save();
        Debug.Log("[CleaningCreditManager] Credit ajoute. Total : " + credits);
        OnCreditsChanged?.Invoke();
    }

    public bool SpendCredit()
    {
        int credits = GetCredits();
        if (credits <= 0) return false;
        credits--;
        PlayerPrefs.SetInt(CREDITS_KEY, credits);
        PlayerPrefs.Save();
        Debug.Log("[CleaningCreditManager] Credit depense. Total : " + credits);
        OnCreditsChanged?.Invoke();
        return true;
    }

    public int GetCredits()
    {
        return PlayerPrefs.GetInt(CREDITS_KEY, 0);
    }

    public bool HasCredits()
    {
        return GetCredits() > 0;
    }

    public void SnapshotLevelStart()
    {
        creditsAtLevelStart = GetCredits();
        Debug.Log("[CleaningCreditManager] Snapshot debut niveau : " + creditsAtLevelStart);
    }

    public void RollbackToSnapshot()
    {
        PlayerPrefs.SetInt(CREDITS_KEY, creditsAtLevelStart);
        PlayerPrefs.Save();
        Debug.Log("[CleaningCreditManager] Rollback credits : " + creditsAtLevelStart);
        OnCreditsChanged?.Invoke();
    }

    public void ResetCredits()
    {
        PlayerPrefs.SetInt(CREDITS_KEY, 0);
        PlayerPrefs.Save();
        Debug.Log("[CleaningCreditManager] Credits remis a zero.");
        OnCreditsChanged?.Invoke();
    }
}