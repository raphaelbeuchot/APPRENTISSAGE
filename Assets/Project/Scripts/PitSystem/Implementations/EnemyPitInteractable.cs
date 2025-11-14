using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(EnemyHealth))]
public class EnemyPitInteractable : MonoBehaviour, IPitInteractable
{
    [Header("Pit Settings")]
    [Tooltip("Multiplicateur de dégâts de fosse (1.0 = normal)")]
    public float pitDamageMultiplier = 1f;

    [Header("Character Dimensions")]
    [Tooltip("Hauteur du centre du personnage (pour calcul submersion)")]
    public float characterCenterHeight = 1f;

    [Header("Water Slowdown")]
    [Tooltip("Multiplicateur de vitesse dans l'eau shallow")]
    public float waterSlowdownMultiplier = 0.5f;

    // References
    private EnemyHealth enemyHealth;
    private EnemyAI enemyAI;
    private CapsuleCollider capsuleCollider;

    // State
    private bool isInPit = false;
    private bool isInWaterShallow = false;
    private PitZone currentPitZone;
    private float originalNavSpeed = 0f;

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

        // Désactive l'AI si le fill type le demande
        PitFill pitFill = pitZone.GetComponent<PitFill>();
        if (pitFill != null && pitFill.fillType != null)
        {
            // Check si c'est water shallow
            float pitDepth = Mathf.Abs(pitZone.GetMaxDepth());
            bool isShallowPit = pitDepth <= 1f;

            if (pitFill.fillType.category == PitContentType.ContentCategory.Water && isShallowPit)
            {
                // Water shallow: ralentissement mais AI active
                ApplyWaterSlowdown();
                isInWaterShallow = true;
                Debug.Log($"[EnemyPit] {name} in shallow water - slowed down");
            }
            else
            {
                // Tous les autres cas (deep water, lava, acid): désactive AI
                bool shouldDisableAI = (pitFill.fillType.category == PitContentType.ContentCategory.InstantKill) ||
                                       (pitFill.fillType.category == PitContentType.ContentCategory.Water && !isShallowPit);

                if (shouldDisableAI && enemyAI != null)
                {
                    enemyAI.enabled = false;
                    Debug.Log($"[EnemyPit] {name} AI disabled in {pitFill.fillType.contentName}");
                }
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

        // Retire le ralentissement
        if (isInWaterShallow)
        {
            RemoveWaterSlowdown();
            isInWaterShallow = false;
        }

        currentPitZone = null;
        Debug.Log($"[EnemyPit] {name} exited pit");
    }

    public void TakePitDamage(float damage, PitDamageType damageType)
    {
        if (!CanTakePitDamage()) return;

        float finalDamage;

        // Si c'est un dégât progressif (mort dans liquide), on calcule en pourcentage
        if (damageType == PitDamageType.InstantKill && damage <= 100f)
        {
            // C'est un pourcentage (ex: 20% = 20)
            float maxHealth = enemyHealth.GetMaxHealth();
            finalDamage = (damage / 100f) * maxHealth * pitDamageMultiplier;
        }
        else
        {
            // Dégâts absolus normaux
            finalDamage = damage * pitDamageMultiplier;
        }

        // Applique les dégâts via le système existant
        enemyHealth.TakeMeleeDamage(finalDamage);

        string typeStr = damageType == PitDamageType.Fall ? "fall" :
                        damageType == PitDamageType.InstantKill ? "liquid" : "DoT";
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

    private void ApplyWaterSlowdown()
    {
        if (enemyAI == null) return;

        NavMeshAgent agent = enemyAI.GetNavMeshAgent();
        if (agent == null) return;

        // Sauvegarde la vitesse originale
        originalNavSpeed = agent.speed;

        // Applique le ralentissement
        agent.speed = originalNavSpeed * waterSlowdownMultiplier;

        Debug.Log($"[EnemyPit] {name} speed reduced to {agent.speed:F2}");
    }

    private void RemoveWaterSlowdown()
    {
        if (enemyAI == null) return;

        NavMeshAgent agent = enemyAI.GetNavMeshAgent();
        if (agent == null) return;

        // Restaure la vitesse originale
        agent.speed = originalNavSpeed;

        Debug.Log($"[EnemyPit] {name} speed restored to {originalNavSpeed:F2}");
    }

    public bool IsInPit() => isInPit;
    public PitZone GetCurrentPitZone() => currentPitZone;
}