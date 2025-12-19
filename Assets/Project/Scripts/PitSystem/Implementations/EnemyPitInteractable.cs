using UnityEngine;
using Pathfinding; // AJOUT A*

[RequireComponent(typeof(EnemyHealth))]
public class EnemyPitInteractable : MonoBehaviour, IPitInteractable
{
    [Header("Pit Settings")]
    [Tooltip("Multiplicateur de degats de fosse (1.0 = normal)")]
    public float pitDamageMultiplier = 1f;

    [Header("Character Dimensions")]
    [Tooltip("Hauteur du centre du personnage (pour calcul submersion)")]
    public float characterCenterHeight = 1f;

    public bool isFallingInPit = false;
    public bool shouldIgnoreHealthbarDistance = false;

    [Header("Water Slowdown")]
    [Tooltip("Multiplicateur de vitesse dans l'eau shallow")]
    public float waterSlowdownMultiplier = 0.5f;

    
    // References
    private EnemyHealth enemyHealth;
    private EnemyAI_AStar enemyAI; // MODIFIE : _AStar
    private CapsuleCollider capsuleCollider;

    // State
    private bool isInPit = false;
    private bool isInWaterShallow = false;
    public bool isInShallowWater = false;
    private PitZone currentPitZone;
    private float originalNavSpeed = 0f;

    void Awake()
    {
        enemyHealth = GetComponent<EnemyHealth>();
        enemyAI = GetComponent<EnemyAI_AStar>(); // MODIFIE : _AStar
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
        Debug.Log(string.Format("[EnemyPit] {0} entered pit: {1}", name, pitZone.name));

        PitFill pitFill = pitZone.GetComponentInChildren<PitFill>();
        if (pitFill == null || pitFill.fillType == null)
        {
            Debug.LogWarning(string.Format("[EnemyPit] {0} - No PitFill found!", name));
            return;
        }

        float pitDepth = Mathf.Abs(pitZone.GetMaxDepth());
        bool isShallowPit = pitDepth <= 1f;

        // EMPTY PIT
        if (pitFill.fillType.category == PitContentType.ContentCategory.Empty)
        {
            isFallingInPit = true;
            if (enemyHealth != null && enemyHealth.healthBarUI != null)
            {
                enemyHealth.healthBarUI.Show();
                Debug.Log(string.Format("[EnemyPit] Forced healthbar show for {0} on Empty pit entry", name));
            }
        }
        // WATER PIT - SHALLOW
        else if (pitFill.fillType.category == PitContentType.ContentCategory.Water && isShallowPit)
        {
            ApplyWaterSlowdown();
            isInWaterShallow = true;

            // Son splash non spatialise
            if (enemyHealth != null && enemyHealth.stats.waterSplashSound != null)
            {
                AudioSource.PlayClipAtPoint(enemyHealth.stats.waterSplashSound, Camera.main.transform.position);
                Debug.Log(string.Format("[AUDIO] {0} water splash (shallow)", name));
            }

            Debug.Log(string.Format("[EnemyPit] {0} in shallow water - slowed down", name));
        }
        // WATER PIT - DEEP
        else if (pitFill.fillType.category == PitContentType.ContentCategory.Water && !isShallowPit)
        {
            // Son splash AVANT de desactiver AI
            if (enemyHealth != null && enemyHealth.stats.waterSplashSound != null)
            {
                AudioSource.PlayClipAtPoint(enemyHealth.stats.waterSplashSound, Camera.main.transform.position);
                Debug.Log(string.Format("[AUDIO] {0} water splash (deep)", name));
            }

            // Desactiver AI pour deep water
            if (enemyAI != null)
            {
                enemyAI.enabled = false;
                Debug.Log(string.Format("[EnemyPit] {0} AI disabled in deep water", name));
            }
        }
        // INSTANT KILL (Lava, Acid, etc.)
        else if (pitFill.fillType.category == PitContentType.ContentCategory.InstantKill)
        {
            // Desactiver AI
            if (enemyAI != null)
            {
                enemyAI.enabled = false;
                Debug.Log(string.Format("[EnemyPit] {0} AI disabled in {1}", name, pitFill.fillType.contentName));
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

        // FORCER l'affichage de la healthbar pour les degats de pit
        if (enemyHealth != null && enemyHealth.healthBarUI != null)
        {
            Debug.Log($"[EnemyPit] Forcing healthbar show for {name}");
            enemyHealth.healthBarUI.Show();
        }
        else
        {
            Debug.LogWarning($"[EnemyPit] Cannot show healthbar - enemyHealth: {enemyHealth != null}, healthBarUI: {enemyHealth?.healthBarUI != null}");
        }

        float finalDamage;

        // Si c'est un degat progressif (mort dans liquide), on calcule en pourcentage
        if (damageType == PitDamageType.InstantKill && damage <= 100f)
        {
            // C'est un pourcentage (ex: 20% = 20)
            float maxHealth = enemyHealth.GetMaxHealth();
            finalDamage = (damage / 100f) * maxHealth * pitDamageMultiplier;

            // MARQUER COMME MORT PAR PIT
            enemyHealth.deathByPit = true;
        }
        else
        {
            // Degats absolus normaux (fall damage, spikes)
            finalDamage = damage * pitDamageMultiplier;
        }

        // Applique les degats via le systeme existant
        enemyHealth.TakeMeleeDamage(finalDamage);

        string typeStr = damageType == PitDamageType.Fall ? "fall" :
                        damageType == PitDamageType.InstantKill ? "liquid" : "DoT";
        Debug.Log(string.Format("[EnemyPit] {0} took {1:F1} {2} damage", name, finalDamage, typeStr));
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

        AIPath aiPath = enemyAI.GetAIPath(); // MODIFIE : AIPath au lieu de NavMeshAgent
        if (aiPath == null) return;

        // Sauvegarde la vitesse originale
        originalNavSpeed = aiPath.maxSpeed; // MODIFIE : maxSpeed au lieu de speed

        // Applique le ralentissement
        aiPath.maxSpeed = originalNavSpeed * waterSlowdownMultiplier; // MODIFIE

        Debug.Log($"[EnemyPit] {name} speed reduced to {aiPath.maxSpeed:F2}");
    }

    private void RemoveWaterSlowdown()
    {
        if (enemyAI == null) return;

        AIPath aiPath = enemyAI.GetAIPath(); // MODIFIE : AIPath au lieu de NavMeshAgent
        if (aiPath == null) return;

        // Restaure la vitesse originale
        aiPath.maxSpeed = originalNavSpeed; // MODIFIE

        Debug.Log($"[EnemyPit] {name} speed restored to {originalNavSpeed:F2}");
    }

    public bool IsInPit() => isInPit;
    public PitZone GetCurrentPitZone() => currentPitZone;
}