using Unity.Cinemachine;
using UnityEngine;

public class SimpleLateralPanning : CinemachineExtension
{
    [Header("Lateral Panning")]
    [SerializeField] private float lateralOffset = 5f;
    [SerializeField] private float lateralPanDuration = 1f;
    [SerializeField] private float lateralThreshold = 0.7f;

    [Header("Vertical Toggle")]
    [SerializeField] private float highCameraOffset = 5f;
    [SerializeField] private float lowCameraOffset = -5f;
    [SerializeField] private float lowCameraOffsetInWater = 2f;
    [SerializeField] private float viewToggleDuration = 2f;

    [Header("Camera Z Clamp")]
    [SerializeField] private float cameraZMarginFromPlayer = 2f;

    private Vector3 currentLateralOffset = Vector3.zero;
    private bool isLowView = false;
    private Vector3 currentVerticalOffset = Vector3.zero;
    private Transform playerTransform;

    private void Start()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            playerTransform = player.transform;
        }

        isLowView = false;
        Debug.Log("[SimpleLateralPanning] START - isLowView: false");
    }

    private void Update()
    {
        if (PlayerInputManager.Instance != null && PlayerInputManager.Instance.ToggleCameraViewPressed)
        {
            isLowView = !isLowView;
            Debug.Log("[SimpleLateralPanning] Toggle view - isLowView: " + isLowView);
        }
    }
    private void LateUpdate()
    {
        Camera mainCamera = Camera.main;
        if (playerTransform != null && mainCamera != null)
        {
            Vector3 camPos = mainCamera.transform.position;
            float maxZ = playerTransform.position.z - cameraZMarginFromPlayer;
            if (camPos.z > maxZ)
            {
                mainCamera.transform.position = new Vector3(camPos.x, camPos.y, maxZ);
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
            if (PlayerInputManager.Instance == null)
                return;

            Vector3 targetVerticalOffset = Vector3.zero;
            float verticalSpeed = 1f / viewToggleDuration;

            if (isLowView)
            {
                bool isInWater = false;
                if (playerTransform != null)
                {
                    PlayerPitInteractable pitInteractable = playerTransform.GetComponent<PlayerPitInteractable>();
                    isInWater = pitInteractable != null && pitInteractable.IsInWater();
                }

                float offsetToUse = isInWater ? lowCameraOffsetInWater : lowCameraOffset;
                targetVerticalOffset = Vector3.up * offsetToUse;
            }
            else
            {
                targetVerticalOffset = Vector3.up * highCameraOffset;
            }

            currentVerticalOffset = Vector3.Lerp(currentVerticalOffset, targetVerticalOffset, verticalSpeed * deltaTime);

            Vector2 lookInput = PlayerInputManager.Instance.LookInput;
            Vector3 targetLateralOffset = currentLateralOffset;

            if (Mathf.Abs(lookInput.y) < 0.5f)
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

            state.PositionCorrection += currentVerticalOffset + currentLateralOffset;
        }
    }
    public void ResetOffsets()
    {
        currentLateralOffset = Vector3.zero;
        currentVerticalOffset = Vector3.zero;
    }
}