using UnityEngine;

public class PulseNoise : MonoBehaviour
{
    [SerializeField] private float speed = 1f;
    [SerializeField] private float amplitude = 0.1f;
    [SerializeField] private Vector3 axisVariation = new Vector3(1f, 0.5f, 0.8f);

    [Header("Movement Deform")]
    [SerializeField] private float movementDeformAmplitude = 0.3f;
    [SerializeField] private float movementDeformSpeed = 5f;

    private Vector3 baseScale;
    private Rigidbody rb;

    void OnEnable()
    {
        baseScale = transform.localScale;
        rb = GetComponent<Rigidbody>();
    }

    void Update()
    {
        float t = Time.time * speed;

        float x = Mathf.PerlinNoise(t, 0f) * 2f - 1f;
        float y = Mathf.PerlinNoise(0f, t) * 2f - 1f;
        float z = Mathf.PerlinNoise(t * 0.7f, t * 0.3f) * 2f - 1f;

        Vector3 noise = new Vector3(
            x * axisVariation.x,
            y * axisVariation.y,
            z * axisVariation.z
        ) * amplitude;

        Vector3 movementDeform = Vector3.zero;
        if (rb != null)
        {
            Vector3 localVelocity = transform.InverseTransformDirection(rb.linearVelocity);
            float velocityMagnitude = rb.linearVelocity.magnitude;
            Vector3 deformDir = localVelocity.normalized * movementDeformAmplitude;
            movementDeform = Vector3.Lerp(movementDeform, deformDir, movementDeformSpeed * Time.deltaTime);
        }

        transform.localScale = baseScale + noise + movementDeform;
    }
}