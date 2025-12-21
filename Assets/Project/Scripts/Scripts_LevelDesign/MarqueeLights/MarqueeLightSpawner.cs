using UnityEngine;
using UnityEngine.Splines;

public class MarqueeLightSpawner : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private SplineContainer splineContainer;
    [SerializeField] private GameObject bulbPrefab;

    [Header("Spawn Settings")]
    [SerializeField] private int bulbCount = 36;

    private Transform bulbsContainer;

    void Start()
    {
        SpawnBulbs();
    }

    void SpawnBulbs()
    {
        if (splineContainer == null || bulbPrefab == null)
        {
            Debug.LogError("[MarqueeLightSpawner] Spline ou Prefab manquant !");
            return;
        }

        // Creer container pour organiser hierarchy
        GameObject containerObj = new GameObject("BulbsContainer");
        containerObj.transform.SetParent(transform);
        containerObj.transform.localPosition = Vector3.zero;
        bulbsContainer = containerObj.transform;

        // Spawn bulbs le long de la spline
        Spline spline = splineContainer.Spline;

        for (int i = 0; i < bulbCount; i++)
        {
            // Position normalisee sur spline (0.0 a 1.0)
            float t = (float)i / bulbCount;

            // Evaluer position sur spline
            Vector3 position = splineContainer.EvaluatePosition(spline, t);

            // Spawn bulb
            GameObject bulb = Instantiate(bulbPrefab, position, Quaternion.identity);
            bulb.transform.SetParent(bulbsContainer);
            bulb.name = "Bulb_" + i.ToString("D2");
        }

        Debug.Log($"[MarqueeLightSpawner] {bulbCount} bulbs spawned !");
    }
}