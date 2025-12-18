using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;

public class CameraPanningExtension : CinemachineExtension
{
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

    [Header("Start Zone")]
    [SerializeField] private CountdownManager countdownManager;

    [Header("Obstacle Hiding")]
    [SerializeField] private float playerHeightOffset = 0.5f;
    [SerializeField] private LayerMask obstacleHidingLayers;

    private Vector3 currentPanOffset;
    private Vector3 currentLateralOffset = Vector3.zero;
    private bool isHighPosition = false;
    private bool isLowView = false;
    private Transform playerTransform;
    private HashSet<Renderer> hiddenRenderers = new HashSet<Renderer>();
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

        if (countdownManager == null)
        {
            countdownManager = FindObjectOfType<CountdownManager>();
        }

        currentPanOffset = Vector3.zero;
        currentLateralOffset = Vector3.zero;
        isHighPosition = false;
        isLowView = false;

        Debug.Log("[CameraPanning] START - isHighPosition: false, isLowView: false");
    }

    public void OnPlayerExitStartZone()
    {
        Debug.Log("[CameraPanning] Player sorti de startzone - Montee camera !");
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
        if (isHighPosition && isLowView && playerTransform != null && Camera.main != null)
        {
            Vector3 cameraPos = Camera.main.transform.position;
            Vector3 playerPos = playerTransform.position + Vector3.up * playerHeightOffset;
            Vector3 direction = playerPos - cameraPos;
            float distance = direction.magnitude;

            Debug.DrawRay(cameraPos, direction, Color.red, 0.1f);

            HashSet<Renderer> currentlyBlocking = new HashSet<Renderer>();

            RaycastHit[] hits = Physics.RaycastAll(cameraPos, direction.normalized, distance, obstacleHidingLayers);

            foreach (RaycastHit hit in hits)
            {
                if (hit.collider.gameObject != playerTransform.gameObject)
                {
                    Renderer rend = hit.collider.GetComponent<Renderer>();
                    if (rend != null)
                    {
                        currentlyBlocking.Add(rend);

                        if (!hiddenRenderers.Contains(rend))
                        {
                            rend.enabled = false;
                            hiddenRenderers.Add(rend);
                        }
                    }
                }
            }

            List<Renderer> toRestore = new List<Renderer>();
            foreach (Renderer rend in hiddenRenderers)
            {
                if (rend != null && !currentlyBlocking.Contains(rend))
                {
                    rend.enabled = true;
                    toRestore.Add(rend);
                }
            }

            foreach (Renderer rend in toRestore)
            {
                hiddenRenderers.Remove(rend);
            }
        }
        else
        {
            List<Renderer> toRestore = new List<Renderer>();
            foreach (Renderer rend in hiddenRenderers)
            {
                if (rend != null)
                {
                    rend.enabled = true;
                    toRestore.Add(rend);
                }
            }

            foreach (Renderer rend in toRestore)
            {
                hiddenRenderers.Remove(rend);
            }
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
            Vector3 targetOffset = Vector3.zero;  //  INITIALISATION
            float speed = verticalPanSpeed;        //  INITIALISATION

            if (isHighPosition)
            {
                if (isLowView)
                {
                    // NOUVEAU : Détecter si dans l'eau
                    bool isInWater = false;
                    if (playerTransform != null)
                    {
                        PlayerPitInteractable pitInteractable = playerTransform.GetComponent<PlayerPitInteractable>();
                        isInWater = pitInteractable != null && pitInteractable.IsInWater();
                    }

                    // Utiliser offset adapté selon si dans l'eau ou non
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

            // Pan actif seulement si countdown termine OU sorti de startzone
            bool canPan = isHighPosition || (countdownManager != null && countdownManager.countdownFinished);

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
}