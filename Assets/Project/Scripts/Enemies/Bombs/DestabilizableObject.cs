using UnityEngine;
using System.Collections;

public class DestabilizableObject : MonoBehaviour
{
    [Header("Tip Over")]
    [SerializeField] private float tipForce = 8f;
    [SerializeField] private float tipTorque = 12f;
    [SerializeField] private bool canBeReactivated = false;
    [SerializeField] private float reactivationDelay = 10f;

    private Rigidbody rb;
    private bool isDisabled = false;
    private Vector3 originalPosition;
    private Quaternion originalRotation;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (rb != null)
            rb.isKinematic = true;

        originalPosition = transform.position;
        originalRotation = transform.rotation;
    }

    public bool IsDisabled() => isDisabled;

    public void TipOver(Vector3 blastOrigin)
    {
        if (isDisabled) return;
        isDisabled = true;

        if (rb == null) return;

        rb.isKinematic = false;

        Vector3 dir = (transform.position - blastOrigin).normalized;
        dir.y = 0.3f;
        dir.Normalize();

        rb.AddForce(dir * tipForce, ForceMode.VelocityChange);

        Vector3 torqueAxis = Vector3.Cross(Vector3.up, dir);
        rb.AddTorque(torqueAxis * tipTorque, ForceMode.VelocityChange);

        if (canBeReactivated)
            StartCoroutine(ReactivateCoroutine());
    }

    IEnumerator ReactivateCoroutine()
    {
        yield return new WaitForSeconds(reactivationDelay);

        transform.position = originalPosition;
        transform.rotation = originalRotation;

        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
        }

        isDisabled = false;
    }
}