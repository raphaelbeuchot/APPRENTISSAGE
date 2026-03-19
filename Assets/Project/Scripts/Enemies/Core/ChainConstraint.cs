using UnityEngine;

public class ChainConstraint : MonoBehaviour
{
    [Header("Chain Settings")]
    public Transform anchor;
    public float chainLength = 4f;

    void FixedUpdate()
    {
        if (anchor == null) return;

        Vector3 toZombie = transform.position - anchor.position;
        float dist = toZombie.magnitude;

        if (dist > chainLength)
        {
            Vector3 clamped = anchor.position + toZombie.normalized * chainLength;
            transform.position = new Vector3(clamped.x, transform.position.y, clamped.z);
        }
    }

    public float GetCurrentDistance()
    {
        if (anchor == null) return 0f;
        return Vector3.Distance(transform.position, anchor.position);
    }

    public float GetChainLength()
    {
        return chainLength;
    }
}