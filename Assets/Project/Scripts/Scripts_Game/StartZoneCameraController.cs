using UnityEngine;
using Unity.Cinemachine;
using UnityEngine.Splines;

public class StartZoneCameraController : MonoBehaviour
{
    [SerializeField] private CinemachineCamera vcam;
    [SerializeField] private Transform player;
    [SerializeField] private SplineContainer splineContainer;

    [Header("Settings")]
    [SerializeField] private float searchStepSize = 0.01f;
    [SerializeField] private float smoothSpeed = 5f; // NOUVEAU

    private CinemachineSplineDolly splineDolly;
    private float initialDistance;
    private float currentT = 0f; // NOUVEAU - position actuelle lissée

    void Start()
    {
        if (vcam != null)
        {
            splineDolly = vcam.GetComponent<CinemachineSplineDolly>();
            currentT = splineDolly != null ? splineDolly.CameraPosition : 0f;
        }

        if (player != null && vcam != null)
        {
            initialDistance = Vector3.Distance(vcam.transform.position, player.position);
            Debug.Log($"[StartZoneCamera] Distance initiale : {initialDistance:F2}m");
        }
    }

    void Update()
    {
        if (splineDolly == null || player == null || splineContainer == null) return;

        float bestT = 0f;
        float bestDiff = float.MaxValue;

        for (float t = 0f; t <= 1f; t += searchStepSize)
        {
            Vector3 posOnSpline = splineContainer.EvaluatePosition(t);
            float dist = Vector3.Distance(posOnSpline, player.position);
            float diff = Mathf.Abs(dist - initialDistance);

            if (diff < bestDiff)
            {
                bestDiff = diff;
                bestT = t;
            }
        }

        // SMOOTH le changement
        currentT = Mathf.Lerp(currentT, bestT, smoothSpeed * Time.deltaTime);
        splineDolly.CameraPosition = currentT;
    }
}