using UnityEngine;

public class OptionsManager : MonoBehaviour
{
    public static OptionsManager Instance { get; private set; }

    public enum Difficulty { Banco, Superbanco }

    private const string KEY_GAUGE = "GaugeVisible";
    private const string KEY_DIFFICULTY = "Difficulty";

    public bool gaugeVisible { get; private set; } = true;
    public Difficulty difficulty { get; private set; } = Difficulty.Superbanco;

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

    public void SetGaugeVisible(bool value)
    {
        gaugeVisible = value;
        PlayerPrefs.SetInt(KEY_GAUGE, value ? 1 : 0);
        PlayerPrefs.Save();
    }

    public void SetDifficulty(Difficulty value)
    {
        difficulty = value;
        PlayerPrefs.SetInt(KEY_DIFFICULTY, (int)value);
        PlayerPrefs.Save();
    }

    public float GetSentinelDamage(float baseDamage, float maxHealth)
    {
        switch (difficulty)
        {
            case Difficulty.Superbanco: return maxHealth;
            default: return maxHealth * 0.5f;
        }
    }

    void Load()
    {
        gaugeVisible = PlayerPrefs.GetInt(KEY_GAUGE, 1) == 1;
        difficulty = (Difficulty)PlayerPrefs.GetInt(KEY_DIFFICULTY, (int)Difficulty.Superbanco);
    }
}