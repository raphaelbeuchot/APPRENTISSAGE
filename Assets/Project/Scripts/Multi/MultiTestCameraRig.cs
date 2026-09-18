using Unity.Cinemachine;
using UnityEngine;

public class MultiTestCameraRig : MonoBehaviour
{
    public static MultiTestCameraRig Instance { get; private set; }

    [SerializeField] private CinemachineCamera cameraPlayer0;
    [SerializeField] private CinemachineCamera cameraPlayer1;

    private void Awake()
    {
        Instance = this;
    }

    public void AssignFollow(int playerIndex, Transform target)
    {
        CinemachineCamera cam = playerIndex == 0 ? cameraPlayer0 : cameraPlayer1;
        if (cam == null)
        {
            Debug.LogWarning($"[MultiTest] Pas de CinemachineCamera assignee pour playerIndex {playerIndex}");
            return;
        }

        cam.Follow = target;
        Debug.Log($"[MultiTest] Follow assigne : player {playerIndex} -> {cam.name}");
    }
}
