using UnityEngine;

public class CameraFOVController : MonoBehaviour
{
    [Header("Camera Reference")]
    [SerializeField] private Camera mainCamera;

    [Header("FOV Settings")]
    [SerializeField] private float baseFOV = 60f;
    [SerializeField] private float maxFOVIncrease = 20f;
    [SerializeField] private float fovChangeSpeed = 5f;
    [SerializeField] private float stickThreshold = 0.5f;

    private float targetFOV;

    private void Start()
    {
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }

        baseFOV = mainCamera.fieldOfView;
        targetFOV = baseFOV;
    }

    private void Update()
    {
        Vector2 lookInput = PlayerInputManager.Instance.LookInput;

        // Stick BAS : Augmenter FOV
        if (lookInput.y < -stickThreshold)
        {
            targetFOV = baseFOV + (maxFOVIncrease * Mathf.Abs(lookInput.y));
        }
        else
        {
            targetFOV = baseFOV;
        }

        // Lerp smooth vers target FOV
        mainCamera.fieldOfView = Mathf.Lerp(mainCamera.fieldOfView, targetFOV, fovChangeSpeed * Time.deltaTime);
    }
}