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

    [Tooltip("Distance du bord pour sortir de l'eau (en metres)")]
    public float exitDistance = 1.5f;

    [Header("Exit Settings")]
    public KeyCode exitKey = KeyCode.E;

    // References
    private PlayerHealth playerHealth;
    private PlayerPhysicsMovement playerMovement;
    private CapsuleCollider capsuleCollider;
    private Rigidbody rb;

    // State
    private bool isInPit = false;
    private bool isInShallowWater = false;
    private bool isInDeepWater = false;
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

    void Update()
    {
        // Sortie de shallow water avec E
        if (isInShallowWater && Input.GetKeyDown(exitKey))
        {
            TryExitShallowWater();
        }

        // Sortie de deep water avec E
        if (isInDeepWater && Input.GetKeyDown(exitKey))
        {
            TryExitDeepWater();
        }
    }

    void FixedUpdate()
    {
        if (!isInDeepWater || currentPitFill == null) return;

        // Calcule la surface de l'eau
        waterSurfaceY = currentPitFill.GetFillSurfaceHeight();
        targetFloatY = waterSurfaceY - swimDepth;

        float playerY = transform.position.y;

        // Player atteint la profondeur de nage ? Active flottaison
        if (!isFloating && playerY <= targetFloatY)
        {
            StartFloating();
        }

        // Maintient la flottaison
        if (isFloating)
        {
            MaintainFloating();
        }
    }

    public void OnEnterPit(PitZone pitZone)
    {
        isInPit = true;
        currentPitZone = pitZone;

        Debug.Log("[PlayerPit] Player entered pit: " + pitZone.name);

        // Check si c'est de l'eau
        PitFill pitFill = pitZone.GetComponent<PitFill>();
        if (pitFill == null)
        {
            pitFill = pitZone.GetComponentInChildren<PitFill>();
        }

        if (pitFill != null && pitFill.fillType != null &&
            pitFill.fillType.category == PitContentType.ContentCategory.Water)
        {
            float fillHeight = pitFill.GetFillHeightMeters();
            bool isShallow = fillHeight <= 1.0f;

            if (isShallow)
            {
                EnterShallowWater(pitFill);
            }
            else
            {
                EnterDeepWater(pitFill);
            }
        }
    }

    public void OnExitPit(PitZone pitZone)
    {
        isInPit = false;

        if (isInShallowWater)
        {
            ExitShallowWater();
        }

        if (isInDeepWater)
        {
            ExitDeepWater();
        }

        currentPitZone = null;
        currentPitFill = null;
        Debug.Log("[PlayerPit] Player exited pit");
    }

    private void EnterShallowWater(PitFill pitFill)
    {
        isInShallowWater = true;
        currentPitFill = pitFill;

        if (playerMovement != null && pitFill.fillType != null)
        {
            playerMovement.ApplyWaterSlowdown(pitFill.fillType.swimSpeedMultiplier);
            Debug.Log("[PlayerPit] Entered shallow water - slowdown applied");
        }
    }

    private void EnterDeepWater(PitFill pitFill)
    {
        isInDeepWater = true;
        currentPitFill = pitFill;

        Debug.Log("[PlayerPit] Entered deep water - waiting for swim depth");
    }

    private void StartFloating()
    {
        isFloating = true;

        // Stop velocite verticale
        if (rb != null)
        {
            Vector3 vel = rb.linearVelocity;
            vel.y = 0f;
            rb.linearVelocity = vel;
            rb.useGravity = false;
        }

        // Applique slowdown
        if (playerMovement != null && currentPitFill != null && currentPitFill.fillType != null)
        {
            playerMovement.ApplyWaterSlowdown(currentPitFill.fillType.swimSpeedMultiplier);
            Debug.Log("[PlayerPit] Started floating - slowdown applied");
        }
    }

    private void MaintainFloating()
    {
        if (rb == null || currentPitFill == null) return;

        // Recalculer la surface et target a chaque frame
        waterSurfaceY = currentPitFill.GetFillSurfaceHeight();
        targetFloatY = waterSurfaceY - swimDepth;

        // Maintient position de flottaison
        Vector3 pos = transform.position;
        pos.y = targetFloatY;
        transform.position = pos;

        // Stop velocite verticale
        Vector3 vel = rb.linearVelocity;
        vel.y = 0f;
        rb.linearVelocity = vel;
    }

    private void TryExitShallowWater()
    {
        Debug.Log("[PlayerPit] TryExitShallowWater called");

        if (currentPitZone == null)
        {
            Debug.LogWarning("[PlayerPit] currentPitZone is NULL!");
            return;
        }

        bool isNear = IsNearPitEdge(exitDistance);
        Debug.Log("[PlayerPit] IsNearPitEdge: " + isNear + ", exitDistance: " + exitDistance);

        if (isNear)
        {
            Vector3 exitPos = GetNearestEdgeExitPosition();
            ExitShallowWater();

            Debug.Log("[PlayerPit] Teleporting to: " + exitPos);
            transform.position = exitPos;

            Debug.Log("[PlayerPit] Exited shallow water successfully");
        }
        else
        {
            Debug.Log("[PlayerPit] Too far from edge (need < " + exitDistance + "m)");
        }
    }

    private void TryExitDeepWater()
    {
        Debug.Log("[PlayerPit] TryExitDeepWater called");

        if (currentPitZone == null)
        {
            Debug.LogWarning("[PlayerPit] currentPitZone is NULL!");
            return;
        }

        bool isNear = IsNearPitEdge(exitDistance);
        Debug.Log("[PlayerPit] IsNearPitEdge: " + isNear + ", exitDistance: " + exitDistance);

        if (isNear)
        {
            Vector3 exitPos = GetNearestEdgeExitPosition();

            // ORDRE IMPORTANT: Sortir de l'eau AVANT de téléporter
            PitZone zoneToExit = currentPitZone; // Garder référence
            ExitDeepWater();

            // Puis appeler OnExitPit pour nettoyer complètement
            OnExitPit(zoneToExit);

            Debug.Log("[PlayerPit] Teleporting to: " + exitPos);
            transform.position = exitPos;

            Debug.Log("[PlayerPit] Exited deep water successfully");
        }
        else
        {
            Debug.Log("[PlayerPit] Too far from edge (need < " + exitDistance + "m)");
        }
    }
    private void ExitShallowWater()
    {
        if (!isInShallowWater) return;

        isInShallowWater = false;

        if (playerMovement != null)
        {
            playerMovement.RemoveWaterSlowdown();
        }

        Debug.Log("[PlayerPit] Exited shallow water");
    }

    private void ExitDeepWater()
    {
        if (!isInDeepWater) return;

        isInDeepWater = false;
        isFloating = false;

        if (playerMovement != null)
        {
            playerMovement.RemoveWaterSlowdown();
        }

        if (rb != null)
        {
            rb.useGravity = true;
        }

        Debug.Log("[PlayerPit] Exited deep water");
    }

    private bool IsNearPitEdge(float distance)
    {
        if (currentPitZone == null || currentPitZone.gridData == null) return false;

        Vector2Int playerCell = currentPitZone.gridData.WorldToCell(transform.position);
        Vector2Int[] neighbors = new Vector2Int[]
        {
            playerCell + new Vector2Int(0, 1),
            playerCell + new Vector2Int(0, -1),
            playerCell + new Vector2Int(1, 0),
            playerCell + new Vector2Int(-1, 0)
        };

        var ownedCells = currentPitZone.gridData.GetCellsForZone(currentPitZone.zoneID);

        foreach (var neighbor in neighbors)
        {
            if (!ownedCells.ContainsKey(neighbor))
            {
                Vector3 edgePos = currentPitZone.gridData.CellToWorld(playerCell);
                Vector3 neighborPos = currentPitZone.gridData.CellToWorld(neighbor);
                Vector3 edgeCenter = (edgePos + neighborPos) * 0.5f;

                float distToEdge = Vector3.Distance(
                    new Vector3(transform.position.x, 0, transform.position.z),
                    new Vector3(edgeCenter.x, 0, edgeCenter.z));

                if (distToEdge <= distance)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private Vector3 GetNearestEdgeExitPosition()
    {
        if (currentPitZone == null || currentPitZone.gridData == null)
            return transform.position;

        Vector2Int playerCell = currentPitZone.gridData.WorldToCell(transform.position);
        Vector2Int[] neighbors = new Vector2Int[]
        {
            playerCell + new Vector2Int(0, 1),
            playerCell + new Vector2Int(0, -1),
            playerCell + new Vector2Int(1, 0),
            playerCell + new Vector2Int(-1, 0)
        };

        var ownedCells = currentPitZone.gridData.GetCellsForZone(currentPitZone.zoneID);

        Vector3 nearestEdge = transform.position;
        float nearestDist = float.MaxValue;

        foreach (var neighbor in neighbors)
        {
            if (!ownedCells.ContainsKey(neighbor))
            {
                Vector3 neighborWorldPos = currentPitZone.gridData.CellToWorld(neighbor);

                // Position de sortie = sol HORS du pit (pas dedans!)
                float groundY = currentPitZone.transform.position.y;

                Vector3 exitPos = new Vector3(
                    neighborWorldPos.x,
                    groundY + characterCenterHeight + 0.1f,
                    neighborWorldPos.z);

                float dist = Vector3.Distance(
                    new Vector3(transform.position.x, 0, transform.position.z),
                    new Vector3(exitPos.x, 0, exitPos.z));

                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    nearestEdge = exitPos;
                }
            }
        }

        Debug.Log("[PlayerPit] Exit position: " + nearestEdge + ", Ground Y: " + currentPitZone.transform.position.y);
        return nearestEdge;
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

    public bool IsInPit() { return isInPit; }
    public bool IsInShallowWater() { return isInShallowWater; }
    public bool IsInDeepWater() { return isInDeepWater; }
    public PitZone GetCurrentPitZone() { return currentPitZone; }
}