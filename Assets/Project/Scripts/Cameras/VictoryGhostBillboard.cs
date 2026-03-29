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
        Vector3 dirToCamera = transform.position - mainCamera.transform.position;
        dirToCamera.y = 0f;
        transform.rotation = Quaternion.LookRotation(dirToCamera.normalized, Vector3.up) * Quaternion.Euler(180f, 0f, 0f) * Quaternion.Euler(90f, 0f, 0f);
    }
}