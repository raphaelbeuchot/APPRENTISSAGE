using UnityEngine;

public class ModifierApplier : MonoBehaviour
{
    public static ModifierApplier Instance { get; private set; }

    // Multiplicateurs exposés aux scripts du niveau
    public float playerMaxHealthMultiplier { get; private set; } = 1f;
    public float sprayAmmoMultiplier { get; private set; } = 1f;
    public float broomKnockbackMultiplier { get; private set; } = 1f;
    public float enduranceMultiplier { get; private set; } = 1f;
    public float sentinelExposureDelayMultiplier { get; private set; } = 1f;
    public float enemyMaxHealthMultiplier { get; private set; } = 1f;
    public bool noEnemy { get; private set; } = false;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void Start()
    {
        ApplyModifier();
    }

    void ApplyModifier()
    {
        if (LevelProgressionManager.Instance == null) return;

        ModifierType modifier = LevelProgressionManager.Instance.GetActiveModifier();
        Debug.Log($"[ModifierApplier] Application modifier : {modifier}");

        switch (modifier)
        {
            case ModifierType.BonusViePlayer:
                playerMaxHealthMultiplier = 1.5f;
                break;
            case ModifierType.BonusSpray:
                sprayAmmoMultiplier = 1.5f;
                break;
            case ModifierType.BonusKnockbackBroom:
                broomKnockbackMultiplier = 1.5f;
                break;
            case ModifierType.BonusEndurance:
                enduranceMultiplier = 1.5f;
                break;
            case ModifierType.BonusFurtivite:
                sentinelExposureDelayMultiplier = 2f;
                break;
            case ModifierType.MalusViePlayer:
                playerMaxHealthMultiplier = 0.5f;
                break;
            case ModifierType.MalusSpray:
                sprayAmmoMultiplier = 0.5f;
                break;
            case ModifierType.MalusEndurance:
                enduranceMultiplier = 0.5f;
                break;
            case ModifierType.MalusVieEnnemis:
                enemyMaxHealthMultiplier = 1.5f;
                break;
            case ModifierType.Neutre:
            case ModifierType.None:
            default:
                // Tout reste a 1f
                break;
        }
    }
}