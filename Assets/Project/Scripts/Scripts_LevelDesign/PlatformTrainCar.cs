using UnityEngine;

public class PlatformTrainCar : MonoBehaviour, IMovingPlatform
{
    [HideInInspector] public float currentProgress = 0f;
    private Vector3 currentVelocity = Vector3.zero;

    public void SetVelocity(Vector3 velocity)
    {
        currentVelocity = velocity;
    }

    public Vector3 GetPlatformVelocity()
    {
        return currentVelocity;
    }

    public Transform GetTransform()
    {
        return transform;
    }
}