using UnityEngine;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class ConcentricCirclesSpawner : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject bulbPrefab;

    [Header("Circles Configuration")]
    [SerializeField] private float[] radii = { 5f, 7f, 9f, 11f, 13f, 15f };
    [SerializeField] private float bulbSpacing = 0.5f; // Distance physique entre bulbs
    [SerializeField] private float startAngle = 0f; // Angle de départ (0 = 3h, 90 = 12h)

    [Header("Visual Settings")]
    [SerializeField] private float heightOffset = 0f; // Hauteur Y des cercles

    // Preview data
    private List<GameObject> previewBulbs = new List<GameObject>();
    private Transform previewContainer;

    // Runtime data
    private Transform runtimeContainer;

    void Start()
    {
        Debug.Log("[DEBUG] ConcentricCirclesSpawner.Start() appelé"); // AJOUTE

        if (Application.isPlaying)
        {
            Debug.Log("[DEBUG] Application.isPlaying = true, spawn..."); // AJOUTE
            SpawnCircles();
        }
        else
        {
            Debug.Log("[DEBUG] Application.isPlaying = false, pas de spawn"); // AJOUTE
        }
    }

    // ========== RUNTIME SPAWN ==========

    void SpawnCircles()
    {
        if (bulbPrefab == null)
        {
            Debug.LogError("[ConcentricCirclesSpawner] Bulb prefab manquant !");
            return;
        }
        // Creer container runtime
        GameObject containerObj = new GameObject("CirclesRuntimeContainer");
        containerObj.transform.SetParent(transform);
        containerObj.transform.localRotation = Quaternion.identity;
        containerObj.transform.localPosition = Vector3.zero;
        runtimeContainer = containerObj.transform;

        // Générer chaque cercle
        for (int circleIndex = 0; circleIndex < radii.Length; circleIndex++)
        {
            SpawnCircle(circleIndex, runtimeContainer, false);
        }

        Debug.Log($"[ConcentricCirclesSpawner] {radii.Length} cercles générés (runtime)");
    }

    // ========== PREVIEW EDITOR ==========

    public void GeneratePreview()
    {
        ClearPreview();

        if (bulbPrefab == null)
        {
            Debug.LogWarning("[ConcentricCirclesSpawner] Bulb prefab manquant !");
            return;
        }

        // Creer container preview
        GameObject containerObj = new GameObject("CirclesPreviewContainer");
        containerObj.transform.SetParent(transform, false);
        containerObj.transform.localRotation = Quaternion.identity; // Force héritage rotation parent
        containerObj.transform.localPosition = Vector3.zero;
        containerObj.hideFlags = HideFlags.DontSave;
        previewContainer = containerObj.transform;

        // Générer chaque cercle
        for (int circleIndex = 0; circleIndex < radii.Length; circleIndex++)
        {
            SpawnCircle(circleIndex, previewContainer, true);
        }

        Debug.Log($"[ConcentricCirclesSpawner] Preview : {radii.Length} cercles générés");
    }

    public void ClearPreview()
    {
        // Détruire bulbs trackées
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

        // Détruire container
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

    // ========== CORE LOGIC ==========

    void SpawnCircle(int circleIndex, Transform parentContainer, bool isPreview)
    {
        float radius = radii[circleIndex];

        // Calculer nombre de bulbs pour espacement constant
        float circumference = 2f * Mathf.PI * radius;
        int bulbCount = Mathf.Max(1, Mathf.RoundToInt(circumference / bulbSpacing));

        // Container pour ce cercle
        GameObject circleContainer = new GameObject($"Circle_{circleIndex}_R{radius:F1}m");
        circleContainer.transform.SetParent(parentContainer, false);
        circleContainer.transform.localPosition = Vector3.zero;

        if (isPreview)
            circleContainer.hideFlags = HideFlags.DontSave;

        // Générer les bulbs
        for (int i = 0; i < bulbCount; i++)
        {
            // Angle en radians
            float angleRad = (startAngle + (360f * i / bulbCount)) * Mathf.Deg2Rad;

            // Position LOCALE en coordonnees polaires
            float x = Mathf.Cos(angleRad) * radius;
            float z = Mathf.Sin(angleRad) * radius;
            Vector3 localPosition = new Vector3(x, heightOffset, z);

            // Spawn bulb (sans position/rotation initiale)
            GameObject bulb = Instantiate(bulbPrefab);
            bulb.transform.SetParent(circleContainer.transform, false); // false = garde local space
            bulb.transform.localPosition = localPosition; // Position LOCALE
            bulb.transform.localRotation = Quaternion.LookRotation(-localPosition, Vector3.up); // Rotation LOCALE vers centre
            bulb.name = $"Bulb_{i:D3}";

            if (isPreview)
            {
                bulb.hideFlags = HideFlags.DontSave;
                previewBulbs.Add(bulb);
            }
        }
    }

    // ========== CLEANUP ==========

    private void OnDisable()
    {
        if (!Application.isPlaying)
        {
            ClearPreview();
        }
    }

    // ========== DEBUG GIZMOS ==========

    private void OnDrawGizmosSelected()
    {
        if (radii == null || radii.Length == 0)
            return;

        Gizmos.color = Color.cyan;

        // Centre en world space
        Vector3 center = transform.position;

        foreach (float radius in radii)
        {
            DrawCircleGizmo(center, radius);
        }
    }

    void DrawCircleGizmo(Vector3 center, float radius)
    {
        int segments = 64;
        Quaternion rotation = transform.rotation; // Rotation du GameObject

        // Premier point en local, transformé en world
        Vector3 localStart = new Vector3(radius, heightOffset, 0);
        Vector3 prevPoint = center + rotation * localStart;

        for (int i = 1; i <= segments; i++)
        {
            float angle = (360f * i / segments) * Mathf.Deg2Rad;
            // Position locale dans le plan XZ
            Vector3 localPoint = new Vector3(Mathf.Cos(angle) * radius, heightOffset, Mathf.Sin(angle) * radius);
            // Transformer en world space avec la rotation
            Vector3 newPoint = center + rotation * localPoint;
            Gizmos.DrawLine(prevPoint, newPoint);
            prevPoint = newPoint;
        }
    }
}