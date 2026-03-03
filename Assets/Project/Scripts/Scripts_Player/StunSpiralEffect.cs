using UnityEngine;

public class StunSpiralEffect : MonoBehaviour
{
    [SerializeField] private GameObject spiralPrefab;
    [SerializeField] private int spiralCount = 3;
    [SerializeField] private float minDistance = 0.25f;
    [SerializeField] private float maxDistance = 0.5f;

    private Camera mainCamera;
    private Transform[] spirals;
    private float[] angles;
    private float[] distances;

    void Start()
    {
        mainCamera = Camera.main;
        SpawnSpirals();
    }

    void SpawnSpirals()
    {
        spirals = new Transform[spiralCount];
        angles = new float[spiralCount];
        distances = new float[spiralCount];

        float step = 200f / spiralCount;

        for (int i = 0; i < spiralCount; i++)
        {
            float baseAngle = -100f + step * i + step * 0.5f;
            angles[i] = baseAngle + Random.Range(-step * 0.2f, step * 0.2f);
            distances[i] = Random.Range(minDistance, maxDistance);

            GameObject spiral = Instantiate(spiralPrefab, transform.position, Quaternion.identity, transform);
            spirals[i] = spiral.transform;
        }
    }

    void LateUpdate()
    {
        if (mainCamera == null) return;

        for (int i = 0; i < spirals.Length; i++)
        {
            if (spirals[i] == null) continue;

            float rad = angles[i] * Mathf.Deg2Rad;
            Vector3 camRight = mainCamera.transform.right;
            Vector3 camUp = mainCamera.transform.up;
            Vector3 offset = (camRight * Mathf.Sin(rad) + camUp * Mathf.Cos(rad)) * distances[i];
            spirals[i].position = transform.position + offset;

            spirals[i].rotation = mainCamera.transform.rotation;
        }
    }
}