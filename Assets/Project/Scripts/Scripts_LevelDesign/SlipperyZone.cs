using UnityEngine;

public class SlipperyZone : MonoBehaviour
{
    [SerializeField] private float slipperyAccel = 4f;
    [SerializeField] private float slipperyBrake = 1.5f;
    [SerializeField] private float slipperyRotation = 10f;

    private void OnCollisionEnter(Collision collision)
    {
        PlayerPhysicsMovement player = collision.gameObject.GetComponent<PlayerPhysicsMovement>();
        if (player != null)
            player.SetSlippery(true, slipperyAccel, slipperyBrake, slipperyRotation);
    }

    private void OnCollisionExit(Collision collision)
    {
        PlayerPhysicsMovement player = collision.gameObject.GetComponent<PlayerPhysicsMovement>();
        if (player != null)
            player.SetSlippery(false, 0f, 0f, 0f);
    }
}