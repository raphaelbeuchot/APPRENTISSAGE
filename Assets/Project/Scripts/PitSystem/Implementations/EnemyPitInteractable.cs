using UnityEngine;

/// <summary>
/// Implémentation de IPitInteractable pour les ennemis
/// </summary>
[RequireComponent(typeof(EnemyHealth))]
public class EnemyPitInteractable : MonoBehaviour, IPitInteractable
{
    [Header("Pit Settings")]
    [Tooltip("Multiplicateur de dégâts de fosse (1.0 = normal)")]
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

        // Auto-detect character center height si on a un collider
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

        // Désactive l'AI si le fill type le demande
        if (pitZone.fillType != null && pitZone.fillType.disableEnemyAI)
        {
            if (enemyAI != null)
            {
                enemyAI.enabled = false;
                Debug.Log($"[EnemyPit] {name} AI disabled in {pitZone.fillType.contentName}");
            }
        }
    }

    public void OnExitPit(PitZone pitZone)
    {
        isInPit = false;

        // Réactive l'AI
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

        // Applique les dégâts via le système existant
        enemyHealth.TakeMeleeDamage(finalDamage);

        string typeStr = damageType == PitDamageType.Fall ? "fall" :
                        damageType == PitDamageType.InstantKill ? "instakill" : "DoT";
        Debug.Log($"[EnemyPit] {name} took {finalDamage:F1} {typeStr} damage");
    }

    public bool CanTakePitDamage()
    {
        // Ne prend pas de dégâts si mort
        if (enemyHealth == null) return false;
        return !enemyHealth.IsDead();
    }

    public Vector3 GetCharacterCenter()
    {
        // Retourne la position du centre du personnage
        return transform.position + Vector3.up * characterCenterHeight;
    }

    public GameObject GetGameObject()
    {
        return gameObject;
    }

    // Getter pour savoir si l'ennemi est dans une fosse
    public bool IsInPit() => isInPit;
    public PitZone GetCurrentPitZone() => currentPitZone;
}