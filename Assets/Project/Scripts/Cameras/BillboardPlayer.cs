using UnityEngine;
public class BillboardPlayer : MonoBehaviour
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

        Vector3 toCamera = mainCamera.transform.position - transform.position;
        toCamera.y = 0f;

        if (toCamera.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.LookRotation(toCamera);
    }
}