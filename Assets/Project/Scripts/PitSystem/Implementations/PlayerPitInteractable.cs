using UnityEngine;
using System.Collections;

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

    [Header("Exit Settings")]
    public KeyCode exitKey = KeyCode.E;
    [Tooltip("Duree de l'animation de sortie (en secondes)")]
    public float climbOutDuration = 1f;
    [Tooltip("Distance d'avancement lors de la sortie (en metres)")]
    public float climbOutDistance = 0.5f;
    [Tooltip("Hauteur maximale pour pouvoir sortir (en metres sous Y=0)")]
    public float maxClimbHeight = 1f;
    [Tooltip("Temps d'immunite apres sortie (pour eviter re-collision)")]
    public float exitImmunityDuration = 0.5f;

    // References
    private PlayerHealth playerHealth;
    private PlayerPhysicsMovement playerMovement;
    private CapsuleCollider capsuleCollider;
    private Rigidbody rb;

    // State
    private bool isInPit = false;
    private bool isInWater = false;
    private bool isFloating = false;
    private bool isInShallowWater = false;
    private bool isClimbingOut = false;
    private bool hasExitImmunity = false;
    private PitZone currentPitZone;
    private PitFill currentPitFill;
    private float waterSurfaceY;
    private float targetFloatY;

    // Wall contact detection
    private bool isCollidingWithPitWall = false;
    private Vector3 pitWallNormal;

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
        // Bloquer TOUT pendant l'animation
        if (isClimbingOut) return;

        // Sortie du pit avec E - UNIQUEMENT GetKeyDown
        if (isInPit && Input.GetKeyDown(exitKey))
        {
            TryClimbOut();
        }
    }

    void FixedUpdate()
    {
        if (!isInWater || currentPitFill == null || isClimbingOut) return;

        // Calcule la surface de l'eau
        waterSurfaceY = currentPitFill.GetFillSurfaceHeight();
        targetFloatY = waterSurfaceY - swimDepth;

        float playerY = transform.position.y;

        // Player atteint la profondeur de nage ? Active flottaison
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

    void OnCollisionStay(Collision collision)
    {
        if (collision.gameObject.layer == LayerMask.NameToLayer("PitWall"))
        {
            isCollidingWithPitWall = true;

            if (collision.contactCount > 0)
            {
                Vector3 avg = Vector3.zero;
                for (int i = 0; i < collision.contactCount; i++)
                    avg += collision.contacts[i].normal;

                avg.Normalize();
                pitWallNormal = avg;
            }
        }
    }

    void OnCollisionExit(Collision collision)
    {
        if (collision.gameObject.layer == LayerMask.NameToLayer("PitWall"))
        {
            isCollidingWithPitWall = false;
        }
    }

    /// <summary>
    /// Vérifie si le player peut sortir du pit (pieds à moins de 1m du sol Y=0)
    /// </summary>
    /// <summary>
    /// Vérifie si le player peut sortir du pit
    /// </summary>
    private bool CanClimbOut()
    {
        if (capsuleCollider == null) return false;

        float feetY = transform.position.y - (capsuleCollider.height / 2f);

        // Si le player est dans l'eau (shallow ou deep), check classique
        if (isInWater || isInShallowWater)
        {
            float groundY = currentPitZone != null ? currentPitZone.transform.position.y : 0f;
            float distanceFromGround = groundY - feetY;
            bool canClimbWater = distanceFromGround <= maxClimbHeight;

            Debug.Log($"[PlayerPit] Water check: feetY={feetY:F2}, groundY={groundY:F2}, distance={distanceFromGround:F2}, canClimb={canClimbWater}");
            return canClimbWater;
        }

        // Si le player n'est PAS dans l'eau = empty pit ou hors pit
        // Autoriser climb out si Y entre -maxClimbHeight et 0
        bool canClimbEmpty = feetY >= -maxClimbHeight && feetY <= 0f;

        Debug.Log($"[PlayerPit] Empty/default check: feetY={feetY:F2}, canClimb={canClimbEmpty}");
        return canClimbEmpty;
    }

    private void TryClimbOut()
    {
        Debug.Log("[PlayerPit] TryClimbOut CALLED");

        if (isClimbingOut)
        {
            Debug.Log("[PlayerPit] Already climbing out!");
            return;
        }

        // CHECK 1 : Peut-on sortir ? (hauteur)
        if (!CanClimbOut())
        {
            Debug.Log("[PlayerPit] Too deep to climb out!");
            return;
        }

        // CHECK 2 : Touche-t-on un mur ?
        if (!isCollidingWithPitWall)
        {
            Debug.Log("[PlayerPit] Not touching pit wall, cannot climb out");
            return;
        }

        // CHECK 3 : Y a-t-il un input de mouvement ?
        if (playerMovement != null)
        {
            Vector3 moveInput = playerMovement.GetMoveInput();

            if (moveInput.magnitude < 0.1f)
            {
                Debug.Log("[PlayerPit] No movement input, cannot climb out");
                return;
            }
        }

        // Récupère la direction de sortie et applique la correction pour l'axe X
        Vector3 exitDirection = transform.TransformDirection(playerMovement.GetMoveInput()).normalized;

        // Correction AXE X inversé
        exitDirection.x *= -1f;

        // CHECK 4 : Pousse-t-on vers le mur ?
        float dotProduct = Vector3.Dot(exitDirection, -pitWallNormal);
        Debug.Log("[PlayerPit] Dot product: " + dotProduct);

        if (dotProduct < 0.7f)
        {
            Debug.Log("[PlayerPit] Not pushing towards wall correctly (dot=" + dotProduct + ")");
            return;
        }

        Debug.Log("[PlayerPit] Starting climb out animation, exit direction: " + exitDirection);
        StartCoroutine(ClimbOutAnimation(exitDirection));
    }

    private IEnumerator ClimbOutAnimation(Vector3 exitDirection)
    {
        Debug.Log("[PlayerPit] === COROUTINE STARTED === exitDirection: " + exitDirection);

        // FORCER exitDirection sur le plan XZ et normaliser MANUELLEMENT
        exitDirection.y = 0f;
        float magnitude = Mathf.Sqrt(exitDirection.x * exitDirection.x + exitDirection.z * exitDirection.z);
        if (magnitude > 0.001f)
        {
            exitDirection.x /= magnitude;
            exitDirection.z /= magnitude;
        }
        Debug.Log("[PlayerPit] Manually normalized exitDirection: " + exitDirection);

        // BLOQUER immediatement
        isClimbingOut = true;

        // ATTENDRE 1 frame
        yield return null;

        // Désactivation movement et collider
        if (playerMovement != null) playerMovement.enabled = false;
        if (capsuleCollider != null) capsuleCollider.enabled = false;

        Vector3 startPos = transform.position;
        float groundY = currentPitZone != null ? currentPitZone.transform.position.y : 0f;

        Vector3 endPos = new Vector3(
            startPos.x + exitDirection.x * climbOutDistance,
            groundY,
            startPos.z + exitDirection.z * climbOutDistance
        );

        Debug.Log("[PlayerPit] Climb out: from " + startPos + " to " + endPos);

        float elapsed = 0f;
        while (elapsed < climbOutDuration)
        {
            float t = elapsed / climbOutDuration;
            transform.position = Vector3.Lerp(startPos, endPos, t);
            elapsed += Time.deltaTime;
            yield return null;
        }

        transform.position = endPos;

        // Nettoyage et réactivation
        ExitPit();

        if (capsuleCollider != null) capsuleCollider.enabled = true;
        if (playerMovement != null) playerMovement.enabled = true;

        isClimbingOut = false;

        // Activer immunité temporaire
        StartCoroutine(ExitImmunityCoroutine());

        Debug.Log("[PlayerPit] Climb out complete");
    }

    /// <summary>
    /// Donne une immunité temporaire après sortie pour éviter re-collision immédiate
    /// </summary>
    private IEnumerator ExitImmunityCoroutine()
    {
        hasExitImmunity = true;
        Debug.Log("[PlayerPit] Exit immunity activated");

        yield return new WaitForSeconds(exitImmunityDuration);

        hasExitImmunity = false;
        Debug.Log("[PlayerPit] Exit immunity ended");
    }

    private void ExitPit()
    {
        if (isInShallowWater)
        {
            isInShallowWater = false;
        }

        if (isInWater)
        {
            ExitWater();
        }

        // Retirer le slow
        if (playerMovement != null)
        {
            playerMovement.RemoveWaterSlowdown();
        }

        isInPit = false;
        currentPitZone = null;
        currentPitFill = null;

        Debug.Log("[PlayerPit] Player exited pit");
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

    public void OnEnterPit(PitZone pitZone)
    {
        // Si on a l'immunité, ignorer
        if (hasExitImmunity)
        {
            Debug.Log("[PlayerPit] Exit immunity active, ignoring pit entry");
            return;
        }

        isInPit = true;
        currentPitZone = pitZone;

        Debug.Log("[PlayerPit] Player entered pit: " + pitZone.name);

        // Récupérer le PitFill
        PitFill pitFill = pitZone.GetComponent<PitFill>();
        if (pitFill == null)
            pitFill = pitZone.GetComponentInChildren<PitFill>();

        if (pitFill != null && pitFill.fillType != null &&
            pitFill.fillType.category == PitContentType.ContentCategory.Water)
        {
            currentPitFill = pitFill;

            float fillHeight = pitFill.GetFillHeightMeters();
            bool isShallow = fillHeight <= 1.0f;

            if (isShallow)
            {
                // SHALLOW WATER - juste ralentir
                isInShallowWater = true;
                isInWater = false;
                isFloating = false;
                if (playerMovement != null)
                    playerMovement.ApplyWaterSlowdown(pitFill.fillType.swimSpeedMultiplier);

                Debug.Log("[PlayerPit] Player in shallow water - slowdown applied");
            }
            else
            {
                // DEEP WATER - mode swim
                isInShallowWater = false;
                isInWater = true;

                waterSurfaceY = pitFill.GetFillSurfaceHeight();
                targetFloatY = waterSurfaceY - swimDepth;

                if (transform.position.y <= targetFloatY)
                {
                    StartFloating();
                    transform.position = new Vector3(transform.position.x, targetFloatY, transform.position.z);
                }

                Debug.Log("[PlayerPit] Player in deep water - swim mode activated");
            }
        }
        else
        {
            // EMPTY PIT ou autre type
            Debug.Log("[PlayerPit] Player in empty/other pit");
        }

        Debug.Log("[PitFill DEBUG] Called OnEnterPit for Player");
    }

    public void OnExitPit(PitZone pitZone)
    {
        // Compatibilité avec PitFillDamageController
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

        if (playerMovement != null)
        {
            playerMovement.RemoveWaterSlowdown();
        }

        if (rb != null)
        {
            rb.useGravity = true;
        }

        currentPitFill = null;

        Debug.Log("[PlayerPit] Player exited water");
    }

    public bool IsInPit() { return isInPit; }
    public bool IsInWater() { return isInWater; }
    public PitZone GetCurrentPitZone() { return currentPitZone; }
}