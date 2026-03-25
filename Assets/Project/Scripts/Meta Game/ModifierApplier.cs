using UnityEngine;
public class ModifierApplier : MonoBehaviour
{
    public static ModifierApplier Instance { get; private set; }

    [Header("Debug")]
    [SerializeField] private bool debugOverride = false;
    [SerializeField] private ModifierType debugModifier = ModifierType.None;

    public float playerMaxHealthMultiplier { get; private set; } = 1f;
    public float sprayAmmoMultiplier { get; private set; } = 1f;
    public float broomKnockbackMultiplier { get; private set; } = 1f;
    public float enduranceMultiplier { get; private set; } = 1f;
    public float sentinelMovementThresholdMultiplier { get; private set; } = 1f;
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
        ApplyModifier();
    }

    void ApplyModifier()
    {
        ModifierType modifier;

        if (debugOverride)
        {
            modifier = debugModifier;
            Debug.Log($"[ModifierApplier] Mode debug : forçage modifier {modifier}");
        }
        else
        {
            if (LevelProgressionManager.Instance == null)
            {
                Debug.LogWarning("[ModifierApplier] LevelProgressionManager introuvable, aucun modifier applique.");
                return;
            }
            modifier = LevelProgressionManager.Instance.GetActiveModifier();
            Debug.Log($"[ModifierApplier] Application modifier : {modifier}");
        }

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
                sentinelMovementThresholdMultiplier = 10f;
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
                break;
        }
    }
}