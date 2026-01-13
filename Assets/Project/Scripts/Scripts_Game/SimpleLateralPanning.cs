using Unity.Cinemachine;
using UnityEngine;

public class SimpleLateralPanning : CinemachineExtension
{
    [Header("Lateral Panning")]
    [SerializeField] private float lateralOffset = 5f;
    [SerializeField] private float lateralPanDuration = 1f;
    [SerializeField] private float lateralThreshold = 0.7f;

    private Vector3 currentLateralOffset = Vector3.zero;

    protected override void PostPipelineStageCallback(
        CinemachineVirtualCameraBase vcam,
        CinemachineCore.Stage stage,
        ref CameraState state,
        float deltaTime)
    {
        if (stage == CinemachineCore.Stage.Body)
        {
            Vector2 lookInput = PlayerInputManager.Instance.LookInput;
            Vector3 targetLateralOffset = currentLateralOffset;

            // Pan latéral si pas de mouvement vertical du stick
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

            state.PositionCorrection += currentLateralOffset;
        }
    }
}