using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;

public class CameraPanningExtension : CinemachineExtension
{
    [Header("Camera active sans pupitre")]

    [SerializeField] private bool startWithFreeCameraEnabled = false;

    [Header("Vertical Panning")]
    [SerializeField] private float verticalOffset = 5f;
    [SerializeField] private float verticalPanSpeed = 10f;
    [SerializeField] private float initialRiseDuration = 2f;

    [Header("View Toggle")]
    [SerializeField] private float lowCameraOffset = -5f;
    [SerializeField] private float viewToggleDuration = 2f;
    [SerializeField] private float lowCameraOffsetInWater = 2f;

    [Header("Lateral Panning")]
    [SerializeField] private float lateralOffset = 5f;
    [SerializeField] private float lateralPanDuration = 1f;
    [SerializeField] private float lateralThreshold = 0.7f;

   [Header("Obstacle Hiding")]
    [SerializeField] private float playerHeightOffset = 0.5f;
    [SerializeField] private LayerMask obstacleHidingLayers;
    [SerializeField] private Material transparentMaterial;
    [SerializeField] private float detectionRadius = 1f;

    private Vector3 currentPanOffset;
    private Vector3 currentLateralOffset = Vector3.zero;
    private bool isHighPosition = false;
    private bool isLowView = false;
    private Transform playerTransform;
    // NOUVEAU
    private Dictionary<Renderer, Material[]> originalMaterials = new Dictionary<Renderer, Material[]>();
    private HashSet<Renderer> occludedRenderers = new HashSet<Renderer>();
    private Camera mainCamera;
    private void Start()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            playerTransform = player.transform;
        }
        mainCamera = Camera.main;
        if (mainCamera == null)
        {
            mainCamera = FindObjectOfType<Camera>();
        }

        // Reset tous les offsets au demarrage
        currentPanOffset = Vector3.zero;
        currentLateralOffset = Vector3.zero;

        // NOUVEAU : Activer la camera libre si demandé
        if (startWithFreeCameraEnabled)
        {
            isHighPosition = true;
            isLowView = false;
            Debug.Log("[CameraPanning] Camera libre activée dès le départ (niveau tuto)");
        }
        else
        {
            isHighPosition = false;
            isLowView = false;
        }

        Debug.Log($"[CameraPanning] START - isHighPosition: {isHighPosition}, isLowView: {isLowView}");
    }

    public void EnableFreeCameraMode()
    {
        Debug.Log("[CameraPanning] Mode camera libre active !");
        isHighPosition = true;
    }

    private void Update()
    {
        

        if (isHighPosition && PlayerInputManager.Instance.ToggleCameraViewPressed)
        {
            isLowView = !isLowView;
            Debug.Log($"[CameraPanning] Toggle view - isLowView: {isLowView}");
        }
    }

    private void LateUpdate()
    {
        if (isHighPosition && isLowView && playerTransform != null && mainCamera != null)
        {


            Vector3 cameraPos = mainCamera.transform.position;
            Vector3 playerCenter = playerTransform.position + Vector3.up * playerHeightOffset;
            Vector3 direction = playerCenter - cameraPos;
            float distance = direction.magnitude;

            Debug.DrawRay(cameraPos, direction, Color.red, 0.1f);

            HashSet<Renderer> currentlyBlocking = new HashSet<Renderer>();

            // Un seul SphereCast avec petit rayon vers le centre du player
            float smallRadius = 0.1f;  // Petite marge autour du player
            RaycastHit[] hits = Physics.SphereCastAll(cameraPos, smallRadius, direction.normalized, distance, obstacleHidingLayers);

            foreach (RaycastHit hit in hits)
            {
                if (hit.collider.gameObject != playerTransform.gameObject)
                {
                    // Verifier que l'obstacle est vraiment entre camera et player
                    float distToObstacle = hit.distance;

                    if (distToObstacle < distance)
                    {
                        Renderer rend = hit.collider.GetComponent<Renderer>();
                        if (rend != null)
                        {
                            currentlyBlocking.Add(rend);

                            if (!occludedRenderers.Contains(rend))
                            {
                                SaveAndSwapToTransparent(rend);
                                occludedRenderers.Add(rend);
                            }
                        }
                    }
                }
            }

            // Restaurer les renderers qui n'occludent plus
            List<Renderer> toRestore = new List<Renderer>();
            foreach (Renderer rend in occludedRenderers)
            {
                if (rend != null && !currentlyBlocking.Contains(rend))
                {
                    toRestore.Add(rend);
                }
            }

            foreach (Renderer rend in toRestore)
            {
                RestoreMaterials(rend);
                occludedRenderers.Remove(rend);
            }
        }
        else
        {
            // Restaurer tous les matériaux si pas en vue basse
            RestoreAllMaterials();
        }
    }

    protected override void PostPipelineStageCallback(
    CinemachineVirtualCameraBase vcam,
    CinemachineCore.Stage stage,
    ref CameraState state,
    float deltaTime)
    {
        if (stage == CinemachineCore.Stage.Body)
        {
            Vector2 lookInput = PlayerInputManager.Instance.LookInput;
            Vector3 targetOffset = Vector3.zero;
            float speed = verticalPanSpeed;

            if (isHighPosition)
            {
                if (isLowView)
                {
                    bool isInWater = false;
                    if (playerTransform != null)
                    {
                        PlayerPitInteractable pitInteractable = playerTransform.GetComponent<PlayerPitInteractable>();
                        isInWater = pitInteractable != null && pitInteractable.IsInWater();
                    }

                    float offsetToUse = isInWater ? lowCameraOffsetInWater : lowCameraOffset;
                    targetOffset = Vector3.up * offsetToUse;
                    speed = 1f / viewToggleDuration;
                }
                else
                {
                    targetOffset = Vector3.up * verticalOffset;
                    speed = 1f / initialRiseDuration;
                }
            }
            else
            {
                targetOffset = Vector3.zero;
                speed = verticalPanSpeed;
            }

            Vector3 targetLateralOffset = currentLateralOffset;

            bool canPan = isHighPosition;

            if (canPan && Mathf.Abs(lookInput.y) < 0.5f)
            {
                Vector3 cameraRight = state.RawOrientation * Vector3.right;
                cameraRight.y = 0f;
                cameraRight.Normalize();

                float horizontalInput = lookInput.x;
                float deadzone = 0.15f;

                if (Mathf.Abs(horizontalInput) < deadzone)
                {
                    horizontalInput = 0f;
                }
                else
                {
                    float sign = Mathf.Sign(horizontalInput);
                    float absValue = Mathf.Abs(horizontalInput);

                    if (absValue >= lateralThreshold)
                    {
                        horizontalInput = sign * 1f;
                    }
                    else
                    {
                        horizontalInput = sign * Mathf.InverseLerp(deadzone, lateralThreshold, absValue);
                    }
                }

                if (Mathf.Abs(horizontalInput) > 0.01f)
                {
                    targetLateralOffset = -cameraRight * lateralOffset * horizontalInput;
                }
            }

            float lateralSpeed = 1f / lateralPanDuration;
            currentLateralOffset = Vector3.Lerp(currentLateralOffset, targetLateralOffset, lateralSpeed * deltaTime);

            currentPanOffset = Vector3.Lerp(currentPanOffset, targetOffset, speed * deltaTime);
            state.PositionCorrection += currentPanOffset + currentLateralOffset;
        }
    }

    private void SaveAndSwapToTransparent(Renderer renderer)
    {
        if (!originalMaterials.ContainsKey(renderer))
        {
            originalMaterials[renderer] = renderer.sharedMaterials;
        }

        Material[] transparents = new Material[renderer.sharedMaterials.Length];
        for (int i = 0; i < transparents.Length; i++)
        {
            transparents[i] = transparentMaterial;
        }
        renderer.sharedMaterials = transparents;
    }

    

    private void RestoreMaterials(Renderer renderer)
    {
        // NULL CHECK
        if (renderer == null)
        {
            originalMaterials.Remove(renderer);
            return;
        }

        if (originalMaterials.TryGetValue(renderer, out Material[] original))
        {
            renderer.sharedMaterials = original;
            originalMaterials.Remove(renderer);
        }
    }

    private void RestoreAllMaterials()
    {
        // Copier la liste pour éviter modification pendant iteration
        List<Renderer> toProcess = new List<Renderer>(occludedRenderers);

        foreach (Renderer rend in toProcess)
        {
            if (rend != null)
            {
                RestoreMaterials(rend);
            }
        }

        occludedRenderers.Clear();

        // Cleanup des entrées null dans le dictionnaire
        List<Renderer> nullKeys = new List<Renderer>();
        foreach (var kvp in originalMaterials)
        {
            if (kvp.Key == null)
            {
                nullKeys.Add(kvp.Key);
            }
        }

        foreach (var key in nullKeys)
        {
            originalMaterials.Remove(key);
        }
    }

    private void OnDisable()
    {
        RestoreAllMaterials();
    }
}