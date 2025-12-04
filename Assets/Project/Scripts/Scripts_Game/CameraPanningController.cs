using UnityEngine;
using Unity.Cinemachine;

public class CameraPanningExtension : CinemachineExtension
{
    [Header("Panning Presets")]
    [SerializeField] private float lateralOffset = 2f;
    [SerializeField] private float backwardOffset = 2f;
    [SerializeField] private float returnSpeed = 3f;
    [SerializeField] private float stickThreshold = 0.7f;

    private Vector3 currentPanOffset;

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

            // Detection directions discretes
            if (lookInput.magnitude > stickThreshold)
            {
                // BAS : X monde negatif
                if (lookInput.y < -stickThreshold && Mathf.Abs(lookInput.x) < 0.5f)
                {
                    targetOffset = new Vector3(-backwardOffset, 0, 0);
                }
                // GAUCHE : Lateral gauche selon camera
                else if (lookInput.x < -stickThreshold && Mathf.Abs(lookInput.y) < 0.5f)
                {
                    Vector3 cameraRight = state.RawOrientation * Vector3.right;
                    targetOffset = -cameraRight * lateralOffset;
                }
                // DROITE : Lateral droite selon camera
                else if (lookInput.x > stickThreshold && Mathf.Abs(lookInput.y) < 0.5f)
                {
                    Vector3 cameraRight = state.RawOrientation * Vector3.right;
                    targetOffset = cameraRight * lateralOffset;
                }
                // HAUT : A definir plus tard
            }

            currentPanOffset = Vector3.Lerp(currentPanOffset, targetOffset, returnSpeed * deltaTime);
            state.PositionCorrection += currentPanOffset;
        }
    }
}