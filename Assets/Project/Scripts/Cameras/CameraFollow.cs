using UnityEngine;

public enum CameraMode
{
    Normal,
    Sentinel
}

public class CameraFollow : MonoBehaviour
{
    [Header("Target")]
    public Transform target;
    public Transform sentinelTransform;
    public SentinelSettings sentinelSettings; // Reference au SO
    public TargetLockSystem lockSystem;

    [Header("Normal Mode")]
    public float distance = 9f;
    public float sensibiliteSouris = 500f;
    public float hauteurMinSol = 0.2f;
    public float offsetVertical = 3f;

    [Header("Lock-On Mode")]
    public float lockBlendSpeed = 5f;
    public float lockMouseInfluence = 0.3f;

    // Variables privees
    private CameraMode currentMode = CameraMode.Normal;
    private float rotationX = 0f;
    private float rotationY = 0f;
    private float hauteurFixe;

    private Vector3 sentinelModeReferenceDirection;

    void Start()
    {
        hauteurFixe = transform.position.y;
    }
    void Update()
    {
        // Check toggle AVANT que PlayerInputManager reset le flag
        if (PlayerInputManager.Instance.SentinelCameraPressed)
        {
            Debug.Log("SENTINEL CAMERA TOGGLE PRESSED!");
            ToggleSentinelMode();
        }
    }

    void LateUpdate()
    {
        if (target == null) return;

        // Update selon le mode actif (RETIRE le check input d'ici)
        switch (currentMode)
        {
            case CameraMode.Normal:
                if (lockSystem != null && lockSystem.IsLocked)
                {
                    UpdateLockedCamera();
                }
                else
                {
                    UpdateNormalCamera();
                }
                break;

            case CameraMode.Sentinel:
                UpdateSentinelCamera();
                break;
        }
    }

    private void ToggleSentinelMode()
    {
        if (currentMode == CameraMode.Normal)
        {
            // Sauvegarder la direction actuelle de la camera comme reference
            sentinelModeReferenceDirection = transform.forward;
            sentinelModeReferenceDirection.y = 0f;
            sentinelModeReferenceDirection.Normalize();

            currentMode = CameraMode.Sentinel;
            Debug.Log("Mode SENTINEL active");
        }
        else
        {
            currentMode = CameraMode.Normal;
            Debug.Log("Mode NORMAL active");
        }
    }

    private void UpdateNormalCamera()
    {
        Vector2 lookInput = PlayerInputManager.Instance.LookInput;
        float sourisX = lookInput.x * sensibiliteSouris * Time.deltaTime;
        float sourisY = lookInput.y * sensibiliteSouris * Time.deltaTime;

        rotationX += sourisX;
        rotationY -= sourisY;
        rotationY = Mathf.Clamp(rotationY, -80f, 80f);

        Quaternion rotation = Quaternion.Euler(rotationY, rotationX, 0f);
        Vector3 offset = rotation * new Vector3(0f, 0f, -distance);
        Vector3 positionCible = new Vector3(target.position.x, target.position.y + offsetVertical, target.position.z) + offset;

        if (positionCible.y < hauteurMinSol)
            positionCible.y = hauteurMinSol;

        transform.position = positionCible;

        Vector3 pointRegard = new Vector3(target.position.x, hauteurFixe, target.position.z);
        transform.LookAt(pointRegard);
    }

    private void UpdateLockedCamera()
    {
        Transform lockedTarget = lockSystem.CurrentTarget;
        if (lockedTarget == null)
        {
            UpdateNormalCamera();
            return;
        }

        Vector2 lookInput = PlayerInputManager.Instance.LookInput;
        float sourisX = lookInput.x * sensibiliteSouris * lockMouseInfluence * Time.deltaTime;
        float sourisY = lookInput.y * sensibiliteSouris * lockMouseInfluence * Time.deltaTime;

        Vector3 directionToTarget = lockedTarget.position - target.position;
        directionToTarget.y = 0f;
        float targetYaw = Mathf.Atan2(directionToTarget.x, directionToTarget.z) * Mathf.Rad2Deg;

        rotationX = Mathf.LerpAngle(rotationX, targetYaw + sourisX, lockBlendSpeed * Time.deltaTime);
        rotationY -= sourisY;
        rotationY = Mathf.Clamp(rotationY, -80f, 80f);

        Quaternion rotation = Quaternion.Euler(rotationY, rotationX, 0f);
        Vector3 offset = rotation * new Vector3(0f, 0f, -distance);
        Vector3 positionCible = new Vector3(target.position.x, target.position.y + offsetVertical, target.position.z) + offset;

        if (positionCible.y < hauteurMinSol)
            positionCible.y = hauteurMinSol;

        transform.position = positionCible;

        Vector3 lookPoint = Vector3.Lerp(target.position, lockedTarget.position, 0.5f);
        lookPoint.y = hauteurFixe;
        transform.LookAt(lookPoint);
    }

    private void UpdateSentinelCamera()
    {
        if (sentinelTransform == null)
        {
            Debug.LogWarning("Sentinel Transform non assigne ! Retour en mode Normal.");
            currentMode = CameraMode.Normal;
            return;
        }

        if (sentinelSettings == null)
        {
            Debug.LogWarning("SentinelSettings non assigne ! Retour en mode Normal.");
            currentMode = CameraMode.Normal;
            return;
        }

        // Calcul de la distance player-sentinelle
        float distanceToSentinel = Vector3.Distance(target.position, sentinelTransform.position);

        // Ratio de proximite (0 = loin, 1 = tres proche)
        float proximityRatio = Mathf.Clamp01(1f - (distanceToSentinel / sentinelSettings.cameraTransitionRange));

        // Interpolation de la distance camera selon proximite
        float currentDistance = Mathf.Lerp(sentinelSettings.cameraMaxDistance, sentinelSettings.cameraMinDistance, proximityRatio);

        // Interpolation de l'offset lateral selon proximite
        float currentLateralOffset = Mathf.Lerp(sentinelSettings.cameraLateralOffset, sentinelSettings.cameraMinLateralOffset, proximityRatio);

        // Utiliser la direction de reference sauvegardee au lieu de transform.forward
        Vector3 directionToSentinel = sentinelTransform.position - target.position;
        directionToSentinel.y = 0f;
        directionToSentinel.Normalize();

        // Produit vectoriel pour determiner le cote
        float crossProduct = Vector3.Cross(sentinelModeReferenceDirection, directionToSentinel).y;
        float sideMultiplier = crossProduct > 0 ? -1f : 1f;

        // Position de base : derriere le player avec offset lateral
        Vector3 targetBack = -directionToSentinel; // Direction opposee a la sentinelle
        Vector3 lateralDirection = Vector3.Cross(Vector3.up, targetBack).normalized;

        // Appliquer l'offset lateral du bon cote
        Vector3 lateralOffset = lateralDirection * (currentLateralOffset * sideMultiplier);

        // Position finale de la camera
        Vector3 cameraPosition = target.position
            + targetBack * currentDistance
            + lateralOffset
            + Vector3.up * sentinelSettings.cameraHeightOffset;

        // Contrainte de hauteur min
        if (cameraPosition.y < hauteurMinSol)
            cameraPosition.y = hauteurMinSol;

        // Contrainte pour garder la tete dans le cadre
        float playerHeadHeight = target.position.y + 1.8f; // Approximation hauteur tete
        if (cameraPosition.y > playerHeadHeight)
            cameraPosition.y = playerHeadHeight;

        // Application instantanee (pas de lerp)
        transform.position = cameraPosition;

        // LookAt vers la sentinelle
        transform.LookAt(sentinelTransform.position);
    }
}