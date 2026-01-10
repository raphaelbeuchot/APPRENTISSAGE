using UnityEngine;

public interface IMovingPlatform
{
    Vector3 GetPlatformVelocity();
    Transform GetTransform();
}