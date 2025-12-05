using UnityEngine;
using Unity.Cinemachine;

public class CameraPanningExtension : CinemachineExtension
{
    [Header("Lateral Panning")]
    [SerializeField] private float lateralOffset = 2f;
    [SerializeField] private float panSpeed = 10f;
    [SerializeField] private float returnSpeed = 0f;

    [Header("Vertical Panning")]
    [SerializeField] private float verticalOffset = 5f;
    [SerializeField] private float verticalPanSpeed = 10f;
    [SerializeField] private float verticalReturnSpeed = 0f;

    [Header("Manual Reset")]
    [SerializeField] private float resetSpeed = 5f;

    [Header("Detection")]
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
            bool isManualReset = false;

            // Detection directions discretes
            if (lookInput.magnitude > stickThreshold)
            {
                // BAS : stick en bas - reset position normale
                if (lookInput.y < -stickThreshold && Mathf.Abs(lookInput.x) < 0.5f)
                {
                    targetOffset = Vector3.zero;
                    isManualReset = true;
                }
                // HAUT : stick en haut - camera monte de 5 en Y
                else if (lookInput.y > stickThreshold && Mathf.Abs(lookInput.x) < 0.5f)
                {
                    targetOffset = Vector3.up * verticalOffset;
                }
                // GAUCHE : stick a gauche - camera va a DROITE
                else if (lookInput.x < -stickThreshold && Mathf.Abs(lookInput.y) < 0.5f)
                {
                    Vector3 cameraRight = state.RawOrientation * Vector3.right;
                    targetOffset = cameraRight * lateralOffset;
                }
                // DROITE : stick a droite - camera va a GAUCHE
                else if (lookInput.x > stickThreshold && Mathf.Abs(lookInput.y) < 0.5f)
                {
                    Vector3 cameraRight = state.RawOrientation * Vector3.right;
                    targetOffset = -cameraRight * lateralOffset;
                }
            }

            // Choix de la vitesse
            float speed;
            if (isManualReset)
            {
                speed = resetSpeed;
            }
            else
            {
                bool isVerticalMovement = Mathf.Abs(targetOffset.y) > 0.01f;
                bool isMoving = targetOffset.magnitude > 0.01f;

                if (isVerticalMovement)
                {
                    speed = isMoving ? verticalPanSpeed : verticalReturnSpeed;
                }
                else
                {
                    speed = isMoving ? panSpeed : returnSpeed;
                }
            }

            currentPanOffset = Vector3.Lerp(currentPanOffset, targetOffset, speed * deltaTime);
            state.PositionCorrection += currentPanOffset;
        }
    }
}