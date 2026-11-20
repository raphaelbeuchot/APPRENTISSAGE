using UnityEngine;

public class OptionsManager : MonoBehaviour
{
    public static OptionsManager Instance { get; private set; }

    // Banco = Easy, Superbanco = Normal. Ordre sauvegarde en int : ajouter a la fin, ne pas reordonner.
    public enum Difficulty { Banco, Superbanco, Hard }

    private const string KEY_GAUGE = "GaugeVisible";
    private const string KEY_DIFFICULTY = "Difficulty";
    private const string KEY_VOLUME = "MasterVolume";

    // Volume general en crans de 10 % (0 a 10), applique a AudioListener.volume (tous les sons)
    public const int VolumeSteps = 10;
    public int volumeStep { get; private set; } = VolumeSteps;

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

    // Difficulte de la partie en cours : ecrite seulement a Nouvelle partie (SoloPanelUI), puis
    // relue telle quelle par Continuer et l'ecran de choix de niveaux. Plus modifiable dans les Options.
    public void SetDifficulty(Difficulty value)
    {
        difficulty = value;
        PlayerPrefs.SetInt(KEY_DIFFICULTY, (int)value);
        PlayerPrefs.Save();
    }

    public void SetVolumeStep(int step)
    {
        volumeStep = Mathf.Clamp(step, 0, VolumeSteps);
        AudioListener.volume = volumeStep / (float)VolumeSteps;
        PlayerPrefs.SetInt(KEY_VOLUME, volumeStep);
        PlayerPrefs.Save();
    }

    public float GetSentinelDamage(float baseDamage, float maxHealth)
    {
        // Sentinelle one-shot en Hard seulement ; Easy et Normal : deux touches
        return difficulty == Difficulty.Hard ? maxHealth : maxHealth * 0.5f;
    }

    // Vie max des ennemis (EnemyHealth), cumulee avec le modificateur de niveau (ModifierApplier).
    // x2 en Normal et en Hard, x1 en Easy.
    public float GetEnemyHealthMultiplier()
    {
        return difficulty == Difficulty.Banco ? 1f : 2f;
    }

    void Load()
    {
        gaugeVisible = PlayerPrefs.GetInt(KEY_GAUGE, 1) == 1;
        difficulty = (Difficulty)PlayerPrefs.GetInt(KEY_DIFFICULTY, (int)Difficulty.Superbanco);
        volumeStep = Mathf.Clamp(PlayerPrefs.GetInt(KEY_VOLUME, VolumeSteps), 0, VolumeSteps);
        AudioListener.volume = volumeStep / (float)VolumeSteps;
    }
}