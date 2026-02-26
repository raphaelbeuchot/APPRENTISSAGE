using Unity.Cinemachine;
using UnityEngine;

public class SimpleLateralPanning : CinemachineExtension
{
    [Header("Vertical Toggle")]
    [SerializeField] private float highCameraOffset = 5f;
    [SerializeField] private float lowCameraOffset = -5f;
    [SerializeField] private float lowCameraOffsetInWater = 2f;
    [SerializeField] private float viewToggleDuration = 2f;

    private bool isLowView = false;
    private Vector3 currentVerticalOffset = Vector3.zero;
    private Transform playerTransform;

    private void Start()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
            playerTransform = player.transform;

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
            state.PositionCorrection += currentVerticalOffset;
        }
    }

    public void ResetOffsets()
    {
        currentVerticalOffset = Vector3.zero;
    }
}