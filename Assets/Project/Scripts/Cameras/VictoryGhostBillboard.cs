using UnityEngine;

public class VictoryGhostBillboard : MonoBehaviour
{
    private Camera mainCamera;

    void Start()
    {
        mainCamera = Camera.main;
    }

    void LateUpdate()
    {
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
            return;
        }
        transform.rotation = Quaternion.LookRotation(mainCamera.transform.forward, Vector3.up) * Quaternion.Euler(180f, 0f, 0f);
    }
}