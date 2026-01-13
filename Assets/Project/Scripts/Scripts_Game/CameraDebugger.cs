using UnityEngine;
using Unity.Cinemachine;

public class CameraDebugger : MonoBehaviour
{
    [SerializeField] private CinemachineCamera startZoneCamera;
    [SerializeField] private CinemachineCamera normalCamera;

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.D))
        {
            Debug.Log("=== CAMERA DEBUG ===");
            if (startZoneCamera != null)
                Debug.Log($"StartZone Priority: {startZoneCamera.Priority}");
            if (normalCamera != null)
                Debug.Log($"Normal Priority: {normalCamera.Priority}");

            CinemachineBrain brain = Camera.main.GetComponent<CinemachineBrain>();
            if (brain != null)
            {
                Debug.Log($"Active Vcam: {brain.ActiveVirtualCamera?.Name ?? "NULL"}");
                Debug.Log($"Blend state: {brain.IsBlending}");
            }
        }
    }
}