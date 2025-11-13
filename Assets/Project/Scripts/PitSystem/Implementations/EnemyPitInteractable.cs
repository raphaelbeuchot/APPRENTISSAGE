using UnityEngine;

[RequireComponent(typeof(EnemyHealth))]
public class EnemyPitInteractable : MonoBehaviour, IPitInteractable
{
    [Header("Pit Settings")]
    [Tooltip("Multiplicateur de degats de fosse (1.0 = normal)")]
    public float pitDamageMultiplier = 1f;

    [Header("Character Dimensions")]
    [Tooltip("Hauteur du centre du personnage (pour calcul submersion)")]
    public float characterCenterHeight = 1f;

    // References
    private EnemyHealth enemyHealth;
    private EnemyAI enemyAI;
    private CapsuleCollider capsuleCollider;

    // State
    private bool isInPit = false;
    private PitZone currentPitZone;

    void Awake()
    {
        enemyHealth = GetComponent<EnemyHealth>();
        enemyAI = GetComponent<EnemyAI>();
        capsuleCollider = GetComponent<CapsuleCollider>();

        if (capsuleCollider != null)
        {
            characterCenterHeight = capsuleCollider.height / 2f;
        }
    }

    public void OnEnterPit(PitZone pitZone)
    {
        isInPit = true;
        currentPitZone = pitZone;
        Debug.Log($"[EnemyPit] {name} entered pit: {pitZone.name}");

        // Desactive l'AI si le fill type le demande
        PitFill pitFill = pitZone.GetComponent<PitFill>();
        if (pitFill != null && pitFill.fillType != null && pitFill.fillType.disableEnemyAI)
        {
            if (enemyAI != null)
            {
                enemyAI.enabled = false;
                Debug.Log($"[EnemyPit] {name} AI disabled in {pitFill.fillType.contentName}");
            }
        }
    }

    public void OnExitPit(PitZone pitZone)
    {
        isInPit = false;

        // Reactive l'AI
        if (enemyAI != null && !enemyHealth.IsDead())
        {
            enemyAI.enabled = true;
            Debug.Log($"[EnemyPit] {name} AI re-enabled");
        }

        currentPitZone = null;
        Debug.Log($"[EnemyPit] {name} exited pit");
    }

    public void TakePitDamage(float damage, PitDamageType damageType)
    {
        if (!CanTakePitDamage()) return;

        float finalDamage = damage * pitDamageMultiplier;

        // Applique les degats via le systeme existant
        enemyHealth.TakeMeleeDamage(finalDamage);

        string typeStr = damageType == PitDamageType.Fall ? "fall" :
                        damageType == PitDamageType.InstantKill ? "instakill" : "DoT";
        Debug.Log($"[EnemyPit] {name} took {finalDamage:F1} {typeStr} damage");
    }

    public bool CanTakePitDamage()
    {
        if (enemyHealth == null) return false;
        return !enemyHealth.IsDead();
    }

    public Vector3 GetCharacterCenter()
    {
        return transform.position + Vector3.up * characterCenterHeight;
    }

    public GameObject GetGameObject()
    {
        return gameObject;
    }

    public bool IsInPit() => isInPit;
    public PitZone GetCurrentPitZone() => currentPitZone;
}