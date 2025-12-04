using UnityEngine;
using Unity.Cinemachine;

public class CameraZoneSwitcher : MonoBehaviour
{
    [Header("Camera References")]
    [SerializeField] private CinemachineCamera normalCamera;
    [SerializeField] private CinemachineCamera zoneCamera;

    [Header("Priority Settings")]
    [SerializeField] private int activePriority = 20;
    [SerializeField] private int inactivePriority = 10;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            // Active la camera de zone
            zoneCamera.Priority = activePriority;
            normalCamera.Priority = inactivePriority;

            Debug.Log("Switch vers camera de zone");
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            // Reactive la camera normale
            normalCamera.Priority = activePriority;
            zoneCamera.Priority = inactivePriority;

            Debug.Log("Switch vers camera normale");
        }
    }
}