using UnityEngine;

/// <summary>
/// Implementation de IPitInteractable pour le joueur
/// </summary>
[RequireComponent(typeof(PlayerHealth))]
public class PlayerPitInteractable : MonoBehaviour, IPitInteractable
{
    [Header("Pit Settings")]
    [Tooltip("Multiplicateur de degats de fosse (1.0 = normal)")]
    public float pitDamageMultiplier = 1f;

    [Header("Character Dimensions")]
    [Tooltip("Hauteur du centre du personnage (pour calcul submersion)")]
    public float characterCenterHeight = 1f;

    [Header("Water Settings")]
    [Tooltip("Multiplicateur de vitesse dans l'eau (ex: 0.6 = 60% de vitesse)")]
    public float waterSpeedMultiplier = 0.6f;

    // References
    private PlayerHealth playerHealth;
    private PlayerPhysicsMovement playerMovement;
    private CapsuleCollider capsuleCollider;

    // State
    private bool isInPit = false;
    private bool isInWater = false;
    private PitZone currentPitZone;
    private float waterSlowdownMultiplier = 1f;

    void Awake()
    {
        playerHealth = GetComponent<PlayerHealth>();
        playerMovement = GetComponent<PlayerPhysicsMovement>();
        capsuleCollider = GetComponent<CapsuleCollider>();

        // Auto-detect character center height
        if (capsuleCollider != null)
        {
            characterCenterHeight = capsuleCollider.height / 2f;
        }
    }

    public void OnEnterPit(PitZone pitZone)
    {
        isInPit = true;
        currentPitZone = pitZone;

        Debug.Log("[PlayerPit] Player entered pit: " + pitZone.name);

        // Check si c'est de l'eau (Liquid category)
        if (pitZone.fillType != null &&
            pitZone.fillType.category == PitContentType.ContentCategory.Liquid)
        {
            EnterWater(pitZone.fillType);
        }
    }

    public void OnExitPit(PitZone pitZone)
    {
        isInPit = false;

        if (isInWater)
        {
            ExitWater();
        }

        currentPitZone = null;
        Debug.Log("[PlayerPit] Player exited pit");
    }

    public void TakePitDamage(float damage, PitDamageType damageType)
    {
        if (!CanTakePitDamage()) return;

        float finalDamage = damage * pitDamageMultiplier;

        // Applique les degats via le systeme existant
        playerHealth.TakeDamage(finalDamage);

        string typeStr = damageType == PitDamageType.Fall ? "fall" :
                        damageType == PitDamageType.InstantKill ? "instakill" : "DoT";
        Debug.Log("[PlayerPit] Player took " + finalDamage + " " + typeStr + " damage");
    }

    public bool CanTakePitDamage()
    {
        if (playerHealth == null) return false;
        return !playerHealth.IsDead();
    }

    public Vector3 GetCharacterCenter()
    {
        return transform.position + Vector3.up * characterCenterHeight;
    }

    public GameObject GetGameObject()
    {
        return gameObject;
    }

    /// <summary>
    /// Applique les effets de l'eau (slow)
    /// Utilise le meme systeme que les swarms
    /// </summary>
    private void EnterWater(PitContentType waterType)
    {
        if (isInWater) return;

        isInWater = true;
        waterSlowdownMultiplier = waterType.movementSpeedMultiplier;

        if (playerMovement != null)
        {
            // On applique un slowdown supplementaire qui se cumule avec les swarms
            // Le CalculateSpeed() va multiplier baseSpeed par ce facteur
            // Note: Si deja ralenti par swarms, ca se cumule
            playerMovement.ApplySwarmSlowdown(waterSlowdownMultiplier);

            Debug.Log("[PlayerPit] Player in water - slowdown: " + waterSlowdownMultiplier);
        }
    }

    /// <summary>
    /// Retire les effets de l'eau
    /// </summary>
    private void ExitWater()
    {
        if (!isInWater) return;

        isInWater = false;

        if (playerMovement != null)
        {
            // Restaure la vitesse normale
            playerMovement.RemoveSwarmSlowdown();

            Debug.Log("[PlayerPit] Player exited water - speed restored");
        }
    }

    // Getters publics
    public bool IsInPit() { return isInPit; }
    public bool IsInWater() { return isInWater; }
    public PitZone GetCurrentPitZone() { return currentPitZone; }
}