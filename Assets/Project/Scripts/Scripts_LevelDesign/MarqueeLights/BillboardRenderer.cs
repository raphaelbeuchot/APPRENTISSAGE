using UnityEngine;

public class BillboardRenderer : MonoBehaviour
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
        transform.rotation = mainCamera.transform.rotation;
    }
}