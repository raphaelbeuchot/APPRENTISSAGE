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

    [Header("Audio")]
    [Tooltip("Son joue quand le zombie tombe dans l'eau")]
    public AudioClip waterSplashSound;

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

        // NOUVEAU : Forcer healthbar visible et marquer flag pour Empty pits
        PitFill pitFill = pitZone.GetComponentInChildren<PitFill>();
        if (pitFill != null && pitFill.fillType != null)
        {
            if (pitFill.fillType.category == PitContentType.ContentCategory.Empty)
            {
                isFallingInPit = true;
                if (enemyHealth != null && enemyHealth.healthBarUI != null)
                {
                    enemyHealth.healthBarUI.Show();
                    Debug.Log(string.Format("[EnemyPit] Forced healthbar show for {0} on Empty pit entry", name));
                }
            }

            // Check si c'est water shallow
            float pitDepth = Mathf.Abs(pitZone.GetMaxDepth());
            bool isShallowPit = pitDepth <= 1f;

            if (pitFill.fillType.category == PitContentType.ContentCategory.Water && isShallowPit)
            {
                // Water shallow: ralentissement mais AI active
                ApplyWaterSlowdown();
                isInWaterShallow = true;

                // Son splash non spatialise
                if (waterSplashSound != null)
                {
                    AudioSource.PlayClipAtPoint(waterSplashSound, Camera.main.transform.position);  //  NON-SPATIALISÉ
                    Debug.Log(string.Format("[AUDIO] {0} water splash (shallow)", name));
                }

                Debug.Log(string.Format("[EnemyPit] {0} in shallow water - slowed down", name));
            }
            else
            {
                // Tous les autres cas (deep water, lava, acid): desactive AI
                bool shouldDisableAI = (pitFill.fillType.category == PitContentType.ContentCategory.InstantKill) ||
                                       (pitFill.fillType.category == PitContentType.ContentCategory.Water && !isShallowPit);

                // Son splash pour deep water AVANT de desactiver AI
                if (pitFill.fillType.category == PitContentType.ContentCategory.Water && waterSplashSound != null)
                {
                    AudioSource.PlayClipAtPoint(waterSplashSound, Camera.main.transform.position);  //  NON-SPATIALISÉ
                    Debug.Log(string.Format("[AUDIO] {0} water splash (deep)", name));
                }

                if (shouldDisableAI && enemyAI != null)
                {
                    enemyAI.enabled = false;
                    Debug.Log(string.Format("[EnemyPit] {0} AI disabled in {1}", name, pitFill.fillType.contentName));
                }
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