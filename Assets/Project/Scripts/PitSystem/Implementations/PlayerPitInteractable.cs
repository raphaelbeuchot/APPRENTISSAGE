using UnityEngine;
using UnityEngine.Splines;
using Unity.Mathematics;
using System.Collections;

[RequireComponent(typeof(PlayerHealth))]
public class PlayerPitInteractable : MonoBehaviour, IPitInteractable
{
    [Header("Pit Settings")]
    public float pitDamageMultiplier = 1f;

    private float lastClimbOutTime = -999f;
    private float climbOutCooldown = 1.5f;

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
    private Animator animator;

    // State - ancien systeme
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

    // State - spline systeme
    private SplinePitZone currentSplinePitZone;
    private bool isInSplinePit = false;
    private bool isInSplineFill = false;

    // Wall contact detection (ancien systeme)
    private bool isCollidingWithPitWall = false;
    private Vector3 pitWallNormal;
    private float lastWallContactTime = -999f;
    private float wallContactPersistence = 0.2f;

    void Awake()
    {
        playerHealth = GetComponent<PlayerHealth>();
        playerMovement = GetComponent<PlayerPhysicsMovement>();
        capsuleCollider = GetComponent<CapsuleCollider>();
        rb = GetComponent<Rigidbody>();
        animator = GetComponentInChildren<Animator>();

        if (capsuleCollider != null)
            characterCenterHeight = capsuleCollider.height / 2f;
    }

    void Update()
    {
        if (isClimbingOut) return;

        if (isInPit && PlayerInputManager.Instance.InteractPressed)
            TryClimbOut();
    }

    void FixedUpdate()
    {
        if (!isInWater || isClimbingOut) return;
        if (!isInSplineFill && currentPitFill == null) return;

        if (isInSplineFill)
            waterSurfaceY = GetSplineFillSurfaceHeight();
        else
            waterSurfaceY = currentPitFill.GetFillSurfaceHeight();

        targetFloatY = waterSurfaceY - swimDepth;

        float playerY = transform.position.y;

        if (!isFloating && playerY <= targetFloatY)
            StartFloating();

        if (isFloating)
            MaintainFloating();
    }

    void OnCollisionStay(Collision collision)
    {
        if (collision.gameObject.layer == LayerMask.NameToLayer("PitWall"))
        {
            isCollidingWithPitWall = true;
            lastWallContactTime = Time.time;

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
        // Persistence geree par lastWallContactTime
    }

    private bool CanClimbOut(out float groundY)
    {
        if (isInSplinePit && currentSplinePitZone != null)
            groundY = currentSplinePitZone.transform.position.y;
        else
            groundY = currentPitZone != null ? currentPitZone.transform.position.y : 0f;

        if (capsuleCollider == null) return false;

        float feetY = transform.position.y - (capsuleCollider.height / 2f);

        if (isInWater || isInShallowWater)
        {
            float distanceFromGround = groundY - feetY;
            return distanceFromGround <= maxClimbHeight;
        }

        return feetY >= -maxClimbHeight && feetY <= 0f;
    }

    private void TryClimbOut()
    {
        if (Time.time - lastClimbOutTime < climbOutCooldown) return;
        if (isClimbingOut) return;

        if (!CanClimbOut(out float groundY)) return;

        // Spline : calcul normal depuis la spline
        if (isInSplinePit && currentSplinePitZone != null)
        {
            SplineContainer sc = currentSplinePitZone.GetSplineContainer();
            if (sc != null)
            {
                float3 localPos = (float3)sc.transform.InverseTransformPoint(transform.position);
                SplineUtility.GetNearestPoint(sc.Spline, localPos, out float3 nearestLocal, out float t);
                Vector3 nearestWorld = sc.transform.TransformPoint((Vector3)nearestLocal);
                Vector3 toPlayer = transform.position - nearestWorld;
                toPlayer.y = 0f;
                if (toPlayer.sqrMagnitude > 0.001f)
                {
                    pitWallNormal = toPlayer.normalized;
                    isCollidingWithPitWall = true;
                    lastWallContactTime = Time.time;
                }
            }
        }

        // Check contact mur (avec persistence)
        bool hasWallContact = isCollidingWithPitWall ||
                              (Time.time - lastWallContactTime < wallContactPersistence);
        if (!hasWallContact) return;

        // Check input mouvement
        if (playerMovement != null)
        {
            Vector3 moveInput = playerMovement.GetMoveInput();
            if (moveInput.magnitude < 0.1f) return;
        }

        Vector3 exitDirection = -pitWallNormal;
        exitDirection.y = 0f;
        exitDirection.Normalize();

        // Raycast : bloquer si mur climbable
        Vector3 checkPosition = new Vector3(transform.position.x, 0.1f, transform.position.z);
        float rayDistance = 0.75f;
        Debug.DrawRay(checkPosition, exitDirection * rayDistance, Color.red, 2f);

        RaycastHit hit;
        if (Physics.Raycast(checkPosition, exitDirection, out hit, rayDistance, LayerMask.GetMask("Obstacle")))
        {
            ClimbableObject climbable = hit.collider.GetComponent<ClimbableObject>();
            if (climbable != null) return;
        }

        StartCoroutine(ClimbOutAnimation(exitDirection, groundY));
    }

    private IEnumerator ClimbOutAnimation(Vector3 exitDirection, float groundY)
    {
        isClimbingOut = true;

        if (playerMovement != null)
        {
            playerMovement.enabled = false;
            playerMovement.canMove = false;
            playerMovement.ResetAllInputs();
        }

        RigidbodyConstraints oldConstraints = rb.constraints;
        rb.constraints = RigidbodyConstraints.FreezeRotation;

        exitDirection.y = 0f;
        if (exitDirection.sqrMagnitude > 0.01f)
            exitDirection.Normalize();

        if (exitDirection != Vector3.zero)
            transform.rotation = Quaternion.LookRotation(exitDirection);

        Vector3 rayOrigin = transform.position + exitDirection * 0.5f + Vector3.up * 2f;
        RaycastHit groundHit;
        float detectedGroundY = transform.position.y;
        if (Physics.Raycast(rayOrigin, Vector3.down, out groundHit, 5f, LayerMask.GetMask("Ground")))
        {
            detectedGroundY = groundHit.point.y;
            Debug.Log($"[ClimbOut] Ground hit: {groundHit.collider.name} at Y={detectedGroundY:F2}");
        }
        else
        {
            Debug.LogWarning("[ClimbOut] Ground raycast MISSED");
        }

        float climbHeight = detectedGroundY - transform.position.y;
        Vector3 startPos = transform.position;
        Vector3 topPos = startPos + Vector3.up * climbHeight;

        Debug.Log($"[ClimbOut] playerY={transform.position.y:F2} | detectedGroundY={detectedGroundY:F2} | climbHeight={climbHeight:F2}");
        Debug.Log($"[ClimbOut] pitWallNormal={pitWallNormal} | exitDirection={exitDirection}");
        Debug.Log($"[ClimbOut] startPos={startPos} | topPos={topPos}");

        // Phase 1 : montee verticale
        if (animator != null)
            animator.SetTrigger("ExitPit");

        float tractionDuration = 0.6f;
        float elapsedTime = 0f;
        while (elapsedTime < tractionDuration)
        {
            elapsedTime += Time.fixedDeltaTime;
            float t = elapsedTime / tractionDuration;
            rb.MovePosition(Vector3.Lerp(startPos, topPos, t));
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            yield return new WaitForFixedUpdate();
        }
        rb.MovePosition(topPos);

        yield return new WaitUntil(() => animator.GetCurrentAnimatorStateInfo(0).IsName("ExitPit"));
        yield return new WaitUntil(() => animator.GetCurrentAnimatorStateInfo(0).normalizedTime >= 1f);

        // Phase 2 : pas horizontal
        float stepDuration = 0.4f;
        float stepDistance = 0.5f;
        Vector3 finalPos = topPos + exitDirection * stepDistance;

        elapsedTime = 0f;
        while (elapsedTime < stepDuration)
        {
            elapsedTime += Time.fixedDeltaTime;
            float t = elapsedTime / stepDuration;
            rb.MovePosition(Vector3.Lerp(topPos, finalPos, t));
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            yield return new WaitForFixedUpdate();
        }
        rb.MovePosition(finalPos);

        if (animator != null)
            animator.SetTrigger("ExitDone");

        rb.constraints = oldConstraints;
        ExitPit();

        if (playerMovement != null)
        {
            playerMovement.enabled = true;
            playerMovement.canMove = true;
        }

        StartCoroutine(ExitImmunityCoroutine());
        isClimbingOut = false;
        lastClimbOutTime = Time.time;
    }

    private IEnumerator ExitImmunityCoroutine()
    {
        hasExitImmunity = true;
        yield return new WaitForSeconds(exitImmunityDuration);
        hasExitImmunity = false;

        if (currentPitZone != null)
            OnEnterPit(currentPitZone);
    }

    private void ExitPit()
    {
        // Ancien systeme : nettoyer PitFillDamageController
        if (currentPitZone != null)
        {
            PitFill pitFill = currentPitZone.GetComponent<PitFill>();
            if (pitFill != null)
            {
                PitFillDamageController damageController = pitFill.GetComponent<PitFillDamageController>();
                if (damageController != null)
                    damageController.ForceRemoveEntity(gameObject);
            }
        }

        // Spline systeme : nettoyer fill state
        if (isInSplineFill)
        {
            isInSplineFill = false;
            if (isInShallowWater) isInShallowWater = false;
            if (isInWater) ExitWater();
        }

        if (isInShallowWater)
            isInShallowWater = false;

        if (isInWater)
            ExitWater();

        if (playerMovement != null)
            playerMovement.RemoveWaterSlowdown();

        isCollidingWithPitWall = false;
        lastWallContactTime = -999f;

        isInPit = false;
        isInSplinePit = false;
        currentPitZone = null;
        currentSplinePitZone = null;
        currentPitFill = null;
    }

    // =====================================================
    // SPLINE PIT API
    // =====================================================

    public void OnEnterSplinePit(SplinePitZone zone)
    {
        if (hasExitImmunity)
        {
            isInPit = true;
            isInSplinePit = true;
            currentSplinePitZone = zone;
            return;
        }

        isInPit = true;
        isInSplinePit = true;
        currentSplinePitZone = zone;

        if (playerMovement != null && playerMovement.IsCrouching())
            playerMovement.ExitCrouch();

        Debug.Log("[PlayerPit] Entered spline pit: " + zone.name);
    }

    public void OnExitSplinePit()
    {
        if (isClimbingOut) return;
        ExitPit();
        Debug.Log("[PlayerPit] Exited spline pit");
    }

    public void OnEnterSplineFill(SplinePitZone zone)
    {
        if (isInSplineFill) return;

        isInSplineFill = true;
        currentSplinePitZone = zone;

        bool isShallow = zone.fillLevel <= 1.0f;

        if (isShallow)
        {
            isInShallowWater = true;
            isInWater = false;
            isFloating = false;
            if (playerMovement != null && zone.fillContentType != null)
                playerMovement.ApplyWaterSlowdown(zone.fillContentType.swimSpeedMultiplier);
            Debug.Log("[PlayerPit] Spline shallow water - slowdown applied");
        }
        else
        {
            isInShallowWater = false;
            isInWater = true;

            waterSurfaceY = GetSplineFillSurfaceHeight();
            targetFloatY = waterSurfaceY - swimDepth;

            if (transform.position.y <= targetFloatY)
            {
                StartFloating();
                transform.position = new Vector3(transform.position.x, targetFloatY, transform.position.z);
            }

            Debug.Log("[PlayerPit] Spline deep water - swim mode activated");
        }
    }

    public void OnExitSplineFill()
    {
        if (!isInSplineFill) return;

        isInSplineFill = false;

        if (isInShallowWater)
            isInShallowWater = false;

        if (isInWater)
            ExitWater();

        if (playerMovement != null)
            playerMovement.RemoveWaterSlowdown();

        Debug.Log("[PlayerPit] Exited spline fill");
    }

    private float GetSplineFillSurfaceHeight()
    {
        if (currentSplinePitZone == null) return 0f;
        return currentSplinePitZone.transform.position.y
               - currentSplinePitZone.GetDepth()
               + currentSplinePitZone.fillLevel;
    }

    // =====================================================
    // ANCIEN SYSTEME
    // =====================================================

    public void OnEnterPit(PitZone pitZone)
    {
        if (hasExitImmunity)
        {
            isInPit = true;
            currentPitZone = pitZone;
            return;
        }

        isInPit = true;
        currentPitZone = pitZone;

        if (playerMovement != null && playerMovement.IsCrouching())
        {
            playerMovement.ExitCrouch();
            Debug.Log("[PlayerPit] Crouch cancelled by pit entry");
        }

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
                isInShallowWater = true;
                isInWater = false;
                isFloating = false;
                if (playerMovement != null)
                    playerMovement.ApplyWaterSlowdown(pitFill.fillType.swimSpeedMultiplier);
            }
            else
            {
                isInShallowWater = false;
                isInWater = true;

                waterSurfaceY = pitFill.GetFillSurfaceHeight();
                targetFloatY = waterSurfaceY - swimDepth;

                if (transform.position.y <= targetFloatY)
                {
                    StartFloating();
                    transform.position = new Vector3(transform.position.x, targetFloatY, transform.position.z);
                }
            }
        }
    }

    public void OnExitPit(PitZone pitZone)
    {
        // Compatibilite IPitInteractable
    }

    private void StartFloating()
    {
        isFloating = true;

        if (rb != null)
        {
            Vector3 vel = rb.linearVelocity;
            vel.y = 0f;
            rb.linearVelocity = vel;
            rb.useGravity = false;
        }

        if (playerMovement != null)
        {
            PitContentType contentType = null;
            if (isInSplineFill && currentSplinePitZone != null)
                contentType = currentSplinePitZone.fillContentType;
            else if (currentPitFill != null)
                contentType = currentPitFill.fillType;

            if (contentType != null)
                playerMovement.ApplyWaterSlowdown(contentType.swimSpeedMultiplier);
        }
    }

    private void MaintainFloating()
    {
        if (rb == null) return;

        Vector3 pos = transform.position;
        pos.y = targetFloatY;
        transform.position = pos;

        Vector3 vel = rb.linearVelocity;
        vel.y = 0f;
        rb.linearVelocity = vel;
    }

    private void ExitWater()
    {
        if (!isInWater) return;

        isInWater = false;
        isFloating = false;

        if (playerMovement != null)
            playerMovement.RemoveWaterSlowdown();

        if (rb != null)
            rb.useGravity = true;

        currentPitFill = null;
    }

    public void TakePitDamage(float damage, PitDamageType damageType)
    {
        if (!CanTakePitDamage()) return;
        float finalDamage = damage * pitDamageMultiplier;
        playerHealth.TakeDamage(finalDamage);
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

    public bool IsClimbingOut() { return isClimbingOut; }
    public bool IsInPit() { return isInPit; }
    public bool IsInWater() { return isInWater; }
    public bool IsInAnyWater() { return isInWater || isInShallowWater; }
    public PitZone GetCurrentPitZone() { return currentPitZone; }
}