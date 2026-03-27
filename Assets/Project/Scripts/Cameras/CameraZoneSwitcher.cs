using Unity.Cinemachine;
using UnityEngine;

public class CameraZoneSwitcher : MonoBehaviour
{
    [Header("Camera References")]
    [SerializeField] private CinemachineCamera normalCamera;
    [SerializeField] private CinemachineCamera zoneCamera;
    [SerializeField] private CameraPanningExtension cameraPanning;

    

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            if (cameraPanning != null) cameraPanning.SetPrimaryCamera(zoneCamera);
            Debug.Log("Switch vers camera de zone");
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            if (cameraPanning != null) cameraPanning.SetPrimaryCamera(normalCamera);
            Debug.Log("Switch vers camera normale");
        }
    }
}