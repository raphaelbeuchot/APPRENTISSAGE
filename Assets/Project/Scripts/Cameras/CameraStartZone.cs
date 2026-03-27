using UnityEngine;
using Unity.Cinemachine;

public class CameraStartZoneSwitcher : MonoBehaviour
{
    [Header("Camera References")]
    [SerializeField] private CinemachineCamera startZoneCamera;
    [SerializeField] private CinemachineCamera normalCamera;

    [Header("Priority Settings")]
    [SerializeField] private int activePriority = 20;
    [SerializeField] private int inactivePriority = 10;

    private void Awake()
    {
        // Au démarrage du jeu : StartZone active
        if (startZoneCamera != null)
            startZoneCamera.Priority = activePriority;

        if (normalCamera != null)
            normalCamera.Priority = inactivePriority;
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            // Player sort de la StartZone = switch vers normal
            normalCamera.Priority = activePriority;
            startZoneCamera.Priority = inactivePriority;
            Debug.Log("Switch vers camera normale");
        }
    }
}