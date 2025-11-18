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

    [Tooltip("Distance du bord pour sortir de l'eau (en metres)")]
    public float waterExitDistance = 0.5f;

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
    private PitFill currentPitFill;
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
        if (!isInWater || currentPitFill == null) return;

        // Calcule la surface de l'eau
        waterSurfaceY = currentPitFill.GetFillSurfaceHeight();

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

    void Update()
    {
        // Check sortie de l'eau avec Space
        if (isInWater && isFloating && Input.GetKeyDown(KeyCode.Space))
        {
            TryExitWater();
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

        // Applique le slow
        if (playerMovement != null && currentPitFill != null && currentPitFill.fillType != null)
        {
            playerMovement.ApplyWaterSlowdown(currentPitFill.fillType.swimSpeedMultiplier);
            Debug.Log("[PlayerPit] Started floating + slow applied");
        }
    }

    private void EnterWater(PitFill pitFill)
    {
        if (isInWater) return;

        isInWater = true;
        currentPitFill = pitFill;

        Debug.Log("[PlayerPit] Player in water - waiting for swim depth");
    }

    private void MaintainFloating()
    {
        if (rb == null) return;

        // Teleporte le player a la position cible
        Vector3 pos = transform.position;
        pos.y = targetFloatY;
        transform.position = pos;

        // Stop toute velocite verticale
        Vector3 vel = rb.linearVelocity;
        vel.y = 0f;
        rb.linearVelocity = vel;
    }

    private void TryExitWater()
    {
        if (currentPitZone == null) return;

        // Check si le player est a moins de waterExitDistance du bord du pit
        if (IsNearPitEdge(waterExitDistance))
        {
            ExitWater();
            Debug.Log("[PlayerPit] Player exited water manually (Space at edge)");
        }
        else
        {
            Debug.Log("[PlayerPit] Too far from edge to exit water (need < " + waterExitDistance + "m)");
        }
    }

    private bool IsNearPitEdge(float distance)
    {
        if (currentPitZone == null || currentPitZone.gridData == null) return false;

        // Position player dans la grille
        Vector2Int playerCell = currentPitZone.gridData.WorldToCell(transform.position);

        // Check les 4 directions pour trouver un bord
        Vector2Int[] neighbors = new Vector2Int[]
        {
            playerCell + new Vector2Int(0, 1),  // Nord
            playerCell + new Vector2Int(0, -1), // Sud
            playerCell + new Vector2Int(1, 0),  // Est
            playerCell + new Vector2Int(-1, 0)  // Ouest
        };

        var ownedCells = currentPitZone.gridData.GetCellsForZone(currentPitZone.zoneID);

        foreach (var neighbor in neighbors)
        {
            // Si le voisin n'est pas dans le pit, c'est un bord
            if (!ownedCells.ContainsKey(neighbor))
            {
                // Calcule distance au bord de cette cellule
                Vector3 edgePos = currentPitZone.gridData.CellToWorld(playerCell);
                float cellSize = currentPitZone.gridData.gridCellSize;

                // Position du bord au centre de la cellule voisine
                Vector3 neighborPos = currentPitZone.gridData.CellToWorld(neighbor);
                Vector3 edgeCenter = (edgePos + neighborPos) * 0.5f;

                float distToEdge = Vector3.Distance(new Vector3(transform.position.x, 0, transform.position.z),
                                                     new Vector3(edgeCenter.x, 0, edgeCenter.z));

                if (distToEdge <= distance)
                {
                    return true;
                }
            }
        }

        return false;
    }

    public void OnEnterPit(PitZone pitZone)
    {
        isInPit = true;
        currentPitZone = pitZone;

        Debug.Log("[PlayerPit] Player entered pit: " + pitZone.name);

        // Check si c'est de l'eau - chercher dans les enfants aussi
        PitFill pitFill = pitZone.GetComponent<PitFill>();
        if (pitFill == null)
        {
            pitFill = pitZone.GetComponentInChildren<PitFill>();
        }

        if (pitFill != null && pitFill.fillType != null &&
            pitFill.fillType.category == PitContentType.ContentCategory.Water)
        {
            EnterWater(pitFill);
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
        currentPitFill = null;
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