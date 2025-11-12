using UnityEngine;

[RequireComponent(typeof(PlayerHealth))]
public class PlayerPitInteractable : MonoBehaviour, IPitInteractable
{
    [Header("Pit Settings")]
    public float pitDamageMultiplier = 1f;

    [Header("Character Dimensions")]
    public float characterCenterHeight = 1f;

    [Header("Water Settings")]
    [Tooltip("Profondeur de nage sous la surface (en metres)")]
    public float swimDepth = 0.7f;

    [Tooltip("Tolerance pour declencher la flottaison (en metres)")]
    public float floatTriggerThreshold = 0.1f;

    // References
    private PlayerHealth playerHealth;
    private PlayerPhysicsMovement playerMovement;
    private CapsuleCollider capsuleCollider;
    private Rigidbody rb;

    // State
    private bool isInPit = false;
    private bool isInWater = false;
    private bool isFloating = false;
    private PitZone currentPitZone;
    private float waterSurfaceY;
    private float targetFloatY;

    void Awake()
    {
        playerHealth = GetComponent<PlayerHealth>();
        playerMovement = GetComponent<PlayerPhysicsMovement>();
        capsuleCollider = GetComponent<CapsuleCollider>();
        rb = GetComponent<Rigidbody>();

        if (capsuleCollider != null)
        {
            characterCenterHeight = capsuleCollider.height / 2f;
        }
    }

    void FixedUpdate()
    {
        if (!isInWater || currentPitZone == null) return;

        // Calcule la surface de l'eau
        float maxDepth = currentPitZone.GetMaxDepth();
        float fillHeightAbsolute = Mathf.Abs(maxDepth * currentPitZone.fillHeightPercent);
        waterSurfaceY = maxDepth + fillHeightAbsolute;

        // Position cible de flottaison
        targetFloatY = waterSurfaceY - swimDepth;

        float playerY = transform.position.y;

        // Player atteint la profondeur de nage ? Active flottaison + slow
        if (!isFloating && playerY <= targetFloatY)
        {
            StartFloating();
        }

        // Si en flottaison, maintient la position
        if (isFloating)
        {
            MaintainFloating();
        }
    }

    private void StartFloating()
    {
        isFloating = true;

        // Stop la velocite verticale
        if (rb != null)
        {
            Vector3 vel = rb.linearVelocity;
            vel.y = 0f;
            rb.linearVelocity = vel;

            rb.useGravity = false;
        }

        // APPLIQUE LE SLOW MAINTENANT
        if (playerMovement != null && currentPitZone != null && currentPitZone.fillType != null)
        {
            playerMovement.ApplyWaterSlowdown(currentPitZone.fillType.movementSpeedMultiplier);
            Debug.Log("[PlayerPit] Started floating + slow applied");
        }
    }

    private void EnterWater(PitContentType waterType)
    {
        if (isInWater) return;

        isInWater = true;

        // NE PAS appliquer le slow ici, on attend la flottaison
        Debug.Log("[PlayerPit] Player in water - waiting for swim depth");
    }

    private void MaintainFloating()
    {
        if (rb == null) return;

        // TELEPORTE le player a la position cible
        Vector3 pos = transform.position;
        pos.y = targetFloatY;
        transform.position = pos;

        // STOP toute velocite verticale
        Vector3 vel = rb.linearVelocity;
        vel.y = 0f;
        rb.linearVelocity = vel;
    }

    public void OnEnterPit(PitZone pitZone)
    {
        isInPit = true;
        currentPitZone = pitZone;

        Debug.Log("[PlayerPit] Player entered pit: " + pitZone.name);

        // Check si c'est de l'eau
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

    

    private void ExitWater()
    {
        if (!isInWater) return;

        isInWater = false;
        isFloating = false;

        // Restaure tout
        if (playerMovement != null)
        {
            playerMovement.RemoveWaterSlowdown();
        }

        if (rb != null)
        {
            rb.useGravity = true;
        }

        Debug.Log("[PlayerPit] Player exited water");
    }

    public bool IsInPit() { return isInPit; }
    public bool IsInWater() { return isInWater; }
    public PitZone GetCurrentPitZone() { return currentPitZone; }
}