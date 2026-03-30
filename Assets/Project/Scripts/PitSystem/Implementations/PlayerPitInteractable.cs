using UnityEngine;
using System.Collections;

[RequireComponent(typeof(PlayerHealth))]
public class PlayerPitInteractable : MonoBehaviour, IPitInteractable
{
    [Header("Pit Settings")]
    public float pitDamageMultiplier = 1f;

    private float lastClimbOutTime = -999f;
    private float climbOutCooldown = 1.5f; // Cooldown entre sorties

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
    private float lastWallContactTime = -999f;
    private float wallContactPersistence = 0.2f; // Contact persiste 0.2s apres exit

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

        // === NOUVEAU : Utiliser PlayerInputManager ===
        if (isInPit && PlayerInputManager.Instance.InteractPressed)
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
            lastWallContactTime = Time.time; // Timestamp contact

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
            // Ne plus set false immediatement, la persistence gere
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

        // NOUVEAU : Check cooldown
        if (Time.time - lastClimbOutTime < climbOutCooldown)
        {
            return;
        }

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

        // CHECK 2 : Touche-t-on un mur ? (avec persistence)
        bool hasWallContact = isCollidingWithPitWall || (Time.time - lastWallContactTime < wallContactPersistence);
        if (!hasWallContact)
        {
            Debug.Log("[PlayerPit] Not touching pit wall, cannot climb out");
            return;
        }

        // Si pas de contact actif mais persistence active, invalider le flag apres le check
        if (!isCollidingWithPitWall && Time.time - lastWallContactTime >= wallContactPersistence)
        {
            isCollidingWithPitWall = false;
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

        // NOUVEAU : Direction de sortie = normale du mur (sortir perpendiculairement)
        Vector3 exitDirection = -pitWallNormal;
        exitDirection.y = 0;
        exitDirection.Normalize();

        // === NOUVEAU CHECK 4 : Bloquer si mur climbable ===
        Vector3 checkPosition = new Vector3(transform.position.x, 0.1f, transform.position.z);
        RaycastHit hit;
        float rayDistance = 0.75f;

        // Debug visuel du raycast (rouge = rayon, vert = hit si trouvé)
        Debug.DrawRay(checkPosition, exitDirection * rayDistance, Color.red, 2f);

        if (Physics.Raycast(checkPosition, exitDirection, out hit, rayDistance, LayerMask.GetMask("Obstacle")))
        {
            Debug.Log($"[PlayerPit] Raycast HIT something: {hit.collider.name} at distance {hit.distance}");
            Debug.DrawLine(checkPosition, hit.point, Color.green, 2f);

            ClimbableObject climbable = hit.collider.GetComponent<ClimbableObject>();
            if (climbable != null)
            {
                Debug.Log("[PlayerPit] Cannot exit here - climbable wall blocking. Use climb system instead.");
                return; // BLOQUE la sortie
            }
            else
            {
                Debug.Log($"[PlayerPit] Hit obstacle but no ClimbableObject component");
            }
        }
        else
        {
            Debug.Log("[PlayerPit] Raycast found NO obstacle in exit direction");
        }

        Debug.Log($"[PlayerPit] Starting climb out, wall normal: {pitWallNormal}, exit direction: {exitDirection}");
        StartCoroutine(ClimbOutAnimation(exitDirection));
    }

    private IEnumerator ClimbOutAnimation(Vector3 exitDirection)
    {
        Debug.Log($"[PlayerPit] === COROUTINE STARTED === exitDirection: {exitDirection}");

        isClimbingOut = true;

        // === BLOCAGE COMPLET INPUTS ===
        if (playerMovement != null)
        {
            playerMovement.enabled = false;
            playerMovement.canMove = false;
            playerMovement.ResetAllInputs(); // Vide la mémoire des inputs
        }

        // === FREEZE ROTATION ===
        RigidbodyConstraints oldConstraints = rb.constraints;
        rb.constraints = RigidbodyConstraints.FreezeRotation;

        // Normaliser la direction
        exitDirection.y = 0;
        if (exitDirection.sqrMagnitude > 0.01f)
        {
            exitDirection.Normalize();
        }
        Debug.Log($"[PlayerPit] Normalized exitDirection: {exitDirection}");

        Vector3 startPos = transform.position;

        // --- PHASE 1 : TRACTION VERTICALE (monte à Y=0) ---
        float tractionDuration = 0.6f;
        Vector3 topPos = new Vector3(startPos.x, 0f, startPos.z);

        Debug.Log($"[PlayerPit] Phase 1 - Traction: from {startPos} to {topPos}");

        float elapsedTime = 0f;
        while (elapsedTime < tractionDuration)
        {
            elapsedTime += Time.fixedDeltaTime; // FIXE AU LIEU DE deltaTime
            float t = elapsedTime / tractionDuration;

            Vector3 targetPos = Vector3.Lerp(startPos, topPos, t);
            rb.MovePosition(targetPos); // RIGIDBODY AU LIEU DE transform.position

            // FORCE VELOCITY ZERO
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;

            yield return new WaitForFixedUpdate(); // FIXED AU LIEU DE null
        }

        // Force position exacte après traction
        rb.MovePosition(topPos);
        Debug.Log($"[PlayerPit] Phase 1 complete, position: {transform.position}");

        // --- PHASE 2 : PAS EN AVANT (avance horizontalement) ---
        float stepDuration = 0.4f;
        float stepDistance = 0.5f;
        Vector3 finalPos = topPos + exitDirection * stepDistance;

        Debug.Log($"[PlayerPit] Phase 2 - Step: from {topPos} to {finalPos}");

        elapsedTime = 0f;
        while (elapsedTime < stepDuration)
        {
            elapsedTime += Time.fixedDeltaTime; // FIXE
            float t = elapsedTime / stepDuration;

            Vector3 targetPos = Vector3.Lerp(topPos, finalPos, t);
            rb.MovePosition(targetPos); // RIGIDBODY

            // FORCE VELOCITY ZERO
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;

            yield return new WaitForFixedUpdate(); // FIXED
        }

        // Force position finale exacte
        rb.MovePosition(finalPos);
        Debug.Log($"[PlayerPit] Phase 2 complete, final position: {transform.position}");

        // === RESTAURATION ===
        rb.constraints = oldConstraints;

        // Sortie du pit
        ExitPit();

        // Réactiver le contrôle
        if (playerMovement != null)
        {
            playerMovement.enabled = true;
            playerMovement.canMove = true;
        }

        // Immunité temporaire
        StartCoroutine(ExitImmunityCoroutine());

        isClimbingOut = false;
        lastClimbOutTime = Time.time;

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

        // NOUVEAU : Check si on est encore dans un pit trigger
        // Si oui, re-process l'entree manuellement
        if (currentPitZone != null)
        {
            Debug.Log("[PlayerPit] Still in pit zone after immunity, re-processing entry");
            OnEnterPit(currentPitZone);
        }
    }

    private void ExitPit()
    {
        // NOUVEAU : Nettoyer le dictionnaire du PitFillDamageController
        if (currentPitZone != null)
        {
            PitFill pitFill = currentPitZone.GetComponent<PitFill>();
            if (pitFill != null)
            {
                PitFillDamageController damageController = pitFill.GetComponent<PitFillDamageController>();
                if (damageController != null)
                {
                    damageController.ForceRemoveEntity(gameObject);
                }
            }
        }

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

        // Reset wall contact state
        isCollidingWithPitWall = false;
        lastWallContactTime = -999f;

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
        // Si on a l'immunite, ignorer LES EFFETS mais SET quand meme isInPit
        if (hasExitImmunity)
        {
            Debug.Log("[PlayerPit] Exit immunity active, delaying pit effects");
            isInPit = true;
            currentPitZone = pitZone;
            return;
        }

        isInPit = true;
        currentPitZone = pitZone;

        // ANNULER CROUCH SI ACTIF
        if (playerMovement != null && playerMovement.IsCrouching())
        {
            playerMovement.ExitCrouch();
            Debug.Log("[PlayerPit] Crouch cancelled by pit entry");
        }

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

    public bool IsClimbingOut()
    {
        return isClimbingOut;
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

    public bool IsInAnyWater() { return isInWater || isInShallowWater; }
}