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
    [SerializeField] private float smoothSpeed = 5f;
    [SerializeField] private float activationDistance = 1f;

    private CinemachineSplineDolly splineDolly;
    private float initialDistance; // Sera calculee a l'activation
    private float currentT = 0f;
    private Vector3 playerStartPosition;
    private bool isActive = false;

    void Start()
    {
        if (vcam != null)
        {
            splineDolly = vcam.GetComponent<CinemachineSplineDolly>();
            currentT = splineDolly != null ? splineDolly.CameraPosition : 0f;
        }

        if (player != null)
        {
            playerStartPosition = player.position;
        }
    }

    void Update()
    {
        if (splineDolly == null || player == null || splineContainer == null) return;

        // Verifier si le joueur a marche assez loin pour activer le script
        if (!isActive)
        {
            float distanceMoved = Vector3.Distance(player.position, playerStartPosition);
            if (distanceMoved >= activationDistance)
            {
                isActive = true;

                // CALCUL de la distance de reference au moment de l'activation
                initialDistance = Vector3.Distance(vcam.transform.position, player.position);

                Debug.Log($"[StartZoneCamera] ACTIVE apres {distanceMoved:F2}m - Distance reference: {initialDistance:F2}m");
            }
            else
            {
                return;
            }
        }

        // Script actif : fonctionnement normal
        float bestT = CalculateBestT();
        currentT = Mathf.Lerp(currentT, bestT, smoothSpeed * Time.deltaTime);
        splineDolly.CameraPosition = currentT;
    }

    private float CalculateBestT()
    {
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

        return bestT;
    }
}