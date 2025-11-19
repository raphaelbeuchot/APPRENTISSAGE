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
        // Bloquer completement pendant l'animation
        if (isClimbingOut) return;

        // Sortie du pit avec E
        if ((isInWater || isInShallowWater) && Input.GetKeyDown(exitKey) && !isClimbingOut)
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
        // Check si c'est un mur de pit
        if (collision.gameObject.layer == LayerMask.NameToLayer("PitWall"))
        {
            isCollidingWithPitWall = true;

            Debug.Log("[PlayerPit] COLLISION WITH PITWALL detected!");

            // Recuperer la normale du mur (direction "sortie")
            if (collision.contactCount > 0)
            {
                pitWallNormal = collision.contacts[0].normal;
                Debug.Log("[PlayerPit] Wall normal: " + pitWallNormal);
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

    private void TryClimbOut()
    {
        if (isClimbingOut)
        {
            return;
        }

        if (!isCollidingWithPitWall)
        {
            Debug.Log("[PlayerPit] Not touching pit wall, cannot climb out");
            return;
        }

        // Verifier qu'il y a un input de mouvement
        if (playerMovement != null)
        {
            Vector3 moveInput = playerMovement.GetMoveInput();

            if (moveInput.magnitude < 0.1f)
            {
                Debug.Log("[PlayerPit] No movement input, cannot climb out");
                return;
            }
        }

        // OK, on sort ! On utilise l'input du player comme direction de sortie
        Vector3 exitDirection = playerMovement.GetMoveInput().normalized;

        Debug.Log("[PlayerPit] Starting climb out animation, exit direction: " + exitDirection);
        StartCoroutine(ClimbOutAnimation(exitDirection));
    }

    private IEnumerator ClimbOutAnimation(Vector3 exitDirection)
    {
        // BLOQUER immediatement
        isClimbingOut = true;

        // ATTENDRE 1 frame pour etre sur que Update() voit le flag
        yield return null;

        if (playerMovement != null)
        {
            playerMovement.enabled = false;
        }

        if (playerMovement != null)
        {
            playerMovement.enabled = false;
        }

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

        ExitPit();

        if (playerMovement != null)
        {
            playerMovement.enabled = true;
        }

        isClimbingOut = false;

        Debug.Log("[PlayerPit] Climb out complete");
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
                // SHALLOW WATER - juste ralentir
                isInShallowWater = true;
                if (playerMovement != null)
                {
                    playerMovement.ApplyWaterSlowdown(pitFill.fillType.swimSpeedMultiplier);
                    Debug.Log("[PlayerPit] Player in shallow water - slowdown applied");
                }
            }
            else
            {
                // DEEP WATER - mode swim
                EnterWater(pitFill);
            }
        }
    }

    public void OnExitPit(PitZone pitZone)
    {
        // Cette fonction est appelee par le PitFillDamageController
        // On ne l'utilise plus pour la sortie manuelle
        // Mais on la garde pour compatibilite
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