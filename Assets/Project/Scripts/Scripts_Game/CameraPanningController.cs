using UnityEngine;
using Unity.Cinemachine;

public class CameraPanningExtension : CinemachineExtension
{
    [Header("Vertical Panning")]
    [SerializeField] private float verticalOffset = 5f;
    [SerializeField] private float verticalPanSpeed = 10f;
    [SerializeField] private float initialRiseDuration = 2f;

    [Header("View Toggle")]
    [SerializeField] private float lowCameraOffset = -5f;
    [SerializeField] private float viewToggleDuration = 2f;

    [Header("Lateral Panning")]
    [SerializeField] private float lateralOffset = 5f;
    [SerializeField] private float lateralPanDuration = 1f;
    [SerializeField] private float lateralThreshold = 0.7f;

    [Header("Start Zone")]
    [SerializeField] private CountdownManager countdownManager;

    private Vector3 currentPanOffset;
    private Vector3 currentLateralOffset = Vector3.zero;
    private bool isHighPosition = false;
    private bool isLowView = false;
    private Transform playerTransform;

    private void Start()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            playerTransform = player.transform;
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

    protected override void PostPipelineStageCallback(
        CinemachineVirtualCameraBase vcam,
        CinemachineCore.Stage stage,
        ref CameraState state,
        float deltaTime)
    {
        if (stage == CinemachineCore.Stage.Body)
        {
            Vector2 lookInput = PlayerInputManager.Instance.LookInput;
            Vector3 targetOffset;
            float speed;

            if (isHighPosition)
            {
                if (isLowView)
                {
                    targetOffset = Vector3.up * lowCameraOffset;
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


            Vector3 targetLateralOffset = Vector3.zero;

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