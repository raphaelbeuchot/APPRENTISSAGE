using UnityEngine;
public class CleaningCreditManager : MonoBehaviour
{
    public static CleaningCreditManager Instance { get; private set; }
    public static event System.Action OnCreditsChanged;
    private int credits = 0;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void AddCredit()
    {
        credits++;
        Debug.Log("[CleaningCreditManager] Credit ajoute. Total : " + credits);
        OnCreditsChanged?.Invoke();
    }

    public bool SpendCredit()
    {
        if (credits <= 0) return false;
        credits--;
        Debug.Log("[CleaningCreditManager] Credit depense. Total : " + credits);
        OnCreditsChanged?.Invoke();
        return true;
    }

    public int GetCredits() { return credits; }
    public bool HasCredits() { return credits > 0; }

    public void ResetCredits()
    {
        credits = 0;
        Debug.Log("[CleaningCreditManager] Credits remis a zero.");
        OnCreditsChanged?.Invoke();
    }
}