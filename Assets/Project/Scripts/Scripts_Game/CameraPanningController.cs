using UnityEngine;
using Unity.Cinemachine;

public class CameraPanningExtension : CinemachineExtension
{
    [Header("Vertical Panning")]
    [SerializeField] private float verticalOffset = 5f;
    [SerializeField] private float verticalPanSpeed = 10f;
    [SerializeField] private float initialRiseDuration = 2f; // Durée montée initiale

    [Header("Stick Down Settings")]
    [SerializeField] private float lowCameraOffset = -5f; // Offset quand stick vers le bas
    [SerializeField] private float stickDownDuration = 2f;
    [SerializeField] private float stickThreshold = 0.7f;

    private Vector3 currentPanOffset;
    private bool isHighPosition = false;
    private Transform playerTransform;

    private void Start()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            playerTransform = player.transform;
        }

        // Initialiser à position normale (0)
        currentPanOffset = Vector3.zero;
        isHighPosition = false;

        Debug.Log($"[CameraPanning] START - isHighPosition: {isHighPosition}, currentPanOffset: {currentPanOffset}");
    }

    // Méthode appelée par StartZone
    public void OnPlayerExitStartZone()
    {
        Debug.Log("[CameraPanning] Player sorti de startzone - Montée caméra !");
        isHighPosition = true;
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
                targetOffset = Vector3.up * verticalOffset; // +5 par défaut

                // Stick BAS = descendre vers offset bas
                if (lookInput.y < -stickThreshold && Mathf.Abs(lookInput.x) < 0.5f)
                {
                    targetOffset = Vector3.up * lowCameraOffset; // Vers offset bas (ex: -5)
                    speed = 1f / stickDownDuration;
                }
                else
                {
                    // Montée initiale OU retour à position haute
                    speed = 1f / initialRiseDuration;
                }
            }
            else
            {
                // Avant de quitter startzone : position normale (0)
                targetOffset = Vector3.zero;
                speed = verticalPanSpeed;
            }

            currentPanOffset = Vector3.Lerp(currentPanOffset, targetOffset, speed * deltaTime);
            state.PositionCorrection += currentPanOffset;
        }
    }
}