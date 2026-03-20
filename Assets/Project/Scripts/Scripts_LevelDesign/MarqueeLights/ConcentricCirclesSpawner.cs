using UnityEngine;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class ConcentricCirclesSpawner : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject bulbPrefab;
    [SerializeField] private Transform sentinelTransform; // NOUVEAU

    [Header("Circles Configuration")]
    [SerializeField] private float[] radii = { 5f, 7f, 9f, 11f, 13f, 15f };
    [SerializeField] private float bulbSpacing = 0.5f;
    [SerializeField] private float startAngle = 0f;

    [Header("Visual Settings")]
    [SerializeField] private float heightOffset = 0f;
    [SerializeField] private bool useBillboard = false; // NOUVEAU

    private List<GameObject> previewBulbs = new List<GameObject>();
    private Transform previewContainer;
    private Transform runtimeContainer;

    void Start()
    {
        Debug.Log("[DEBUG] ConcentricCirclesSpawner.Start() appelé");

        if (Application.isPlaying)
        {
            Debug.Log("[DEBUG] Application.isPlaying = true, spawn...");
            SpawnCircles();
        }
        else
        {
            Debug.Log("[DEBUG] Application.isPlaying = false, pas de spawn");
        }
    }

    // NOUVEAU : retourne le centre des cercles
    private Vector3 GetCenter()
    {
        if (sentinelTransform != null)
            return sentinelTransform.position;
        return transform.position;
    }

    void SpawnCircles()
    {
        if (bulbPrefab == null)
        {
            Debug.LogError("[ConcentricCirclesSpawner] Bulb prefab manquant !");
            return;
        }

        GameObject containerObj = new GameObject("CirclesRuntimeContainer");
        containerObj.transform.SetParent(transform);
        containerObj.transform.position = GetCenter(); // MODIFIE
        containerObj.transform.rotation = Quaternion.identity;
        runtimeContainer = containerObj.transform;

        for (int circleIndex = 0; circleIndex < radii.Length; circleIndex++)
        {
            SpawnCircle(circleIndex, runtimeContainer, false);
        }

        Debug.Log($"[ConcentricCirclesSpawner] {radii.Length} cercles générés (runtime)");
    }

    public void GeneratePreview()
    {
        ClearPreview();

        if (bulbPrefab == null)
        {
            Debug.LogWarning("[ConcentricCirclesSpawner] Bulb prefab manquant !");
            return;
        }

        GameObject containerObj = new GameObject("CirclesPreviewContainer");
        containerObj.transform.SetParent(transform, false);
        containerObj.transform.position = GetCenter(); // MODIFIE
        containerObj.transform.rotation = Quaternion.identity;
        containerObj.hideFlags = HideFlags.DontSave;
        previewContainer = containerObj.transform;

        for (int circleIndex = 0; circleIndex < radii.Length; circleIndex++)
        {
            SpawnCircle(circleIndex, previewContainer, true);
        }

        Debug.Log($"[ConcentricCirclesSpawner] Preview : {radii.Length} cercles générés");
    }

    public void ClearPreview()
    {
        foreach (var bulb in previewBulbs)
        {
            if (bulb != null)
            {
#if UNITY_EDITOR
                DestroyImmediate(bulb);
#else
                Destroy(bulb);
#endif
            }
        }
        previewBulbs.Clear();

        if (previewContainer != null)
        {
#if UNITY_EDITOR
            DestroyImmediate(previewContainer.gameObject);
#else
            Destroy(previewContainer.gameObject);
#endif
            previewContainer = null;
        }
    }

    void SpawnCircle(int circleIndex, Transform parentContainer, bool isPreview)
    {
        float radius = radii[circleIndex];
        float circumference = 2f * Mathf.PI * radius;
        int bulbCount = Mathf.Max(1, Mathf.RoundToInt(circumference / bulbSpacing));

        GameObject circleContainer = new GameObject($"Circle_{circleIndex}_R{radius:F1}m");
        circleContainer.transform.SetParent(parentContainer, false);
        circleContainer.transform.localPosition = Vector3.zero;

        if (isPreview)
            circleContainer.hideFlags = HideFlags.DontSave;

        for (int i = 0; i < bulbCount; i++)
        {
            float angleRad = (startAngle + (360f * i / bulbCount)) * Mathf.Deg2Rad;
            float x = Mathf.Cos(angleRad) * radius;
            float y = Mathf.Sin(angleRad) * radius;
            Vector3 localPosition = new Vector3(x, heightOffset + y, 0f);

            GameObject bulb = Instantiate(bulbPrefab);
            bulb.transform.SetParent(circleContainer.transform, false);
            bulb.transform.localPosition = localPosition;
            bulb.transform.localRotation = Quaternion.identity;
            bulb.name = $"Bulb_{i:D3}";

            // NOUVEAU : ajout BillboardRenderer si demande
            if (useBillboard)
                bulb.AddComponent<BillboardRenderer>();

            if (isPreview)
            {
                bulb.hideFlags = HideFlags.DontSave;
                previewBulbs.Add(bulb);
            }
        }
    }

    private void OnDisable()
    {
        if (!Application.isPlaying)
        {
            ClearPreview();
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (radii == null || radii.Length == 0)
            return;

        Gizmos.color = Color.cyan;
        Vector3 center = GetCenter(); // MODIFIE

        foreach (float radius in radii)
        {
            DrawCircleGizmo(center, radius);
        }
    }

    void DrawCircleGizmo(Vector3 center, float radius)
    {
        int segments = 64;
        Quaternion rotation = transform.rotation;
        Vector3 localStart = new Vector3(radius, heightOffset, 0);
        Vector3 prevPoint = center + rotation * localStart;

        for (int i = 1; i <= segments; i++)
        {
            float angle = (360f * i / segments) * Mathf.Deg2Rad;
            Vector3 localPoint = new Vector3(Mathf.Cos(angle) * radius, heightOffset, Mathf.Sin(angle) * radius);
            Vector3 newPoint = center + rotation * localPoint;
            Gizmos.DrawLine(prevPoint, newPoint);
            prevPoint = newPoint;
        }
    }
}