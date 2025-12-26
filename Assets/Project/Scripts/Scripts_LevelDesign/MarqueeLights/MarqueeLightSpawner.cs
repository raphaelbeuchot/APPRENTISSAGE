using UnityEngine;
using UnityEngine.Splines;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor; // NOUVEAU
#endif

[ExecuteInEditMode]
public class MarqueeLightSpawner : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private SplineContainer splineContainer;
    [SerializeField] private GameObject bulbPrefab;

    [Header("Spawn Settings")]
    [SerializeField] private int bulbCount = 36;

    private Transform bulbsContainer;
    private List<GameObject> previewBulbs = new List<GameObject>();
    private Transform previewContainer;

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
    // ========== PREVIEW EDITOR METHODS ==========

    public void GeneratePreview()
    {
        ClearPreview();

        if (splineContainer == null || bulbPrefab == null)
        {
            Debug.LogWarning("[MarqueeLightSpawner] Spline ou Prefab manquant !");
            return;
        }

        // Creer container preview
        GameObject containerObj = new GameObject("PreviewBulbsContainer");
        containerObj.transform.SetParent(transform);
        containerObj.transform.localPosition = Vector3.zero;
        containerObj.hideFlags = HideFlags.DontSave; // Ne sera pas sauvegardé
        previewContainer = containerObj.transform;

        // Spawn bulbs preview
        Spline spline = splineContainer.Spline;
        for (int i = 0; i < bulbCount; i++)
        {
            float t = (float)i / bulbCount;
            Vector3 position = splineContainer.EvaluatePosition(spline, t);

            GameObject bulb = Instantiate(bulbPrefab, position, Quaternion.identity);
            bulb.transform.SetParent(previewContainer);
            bulb.name = $"PreviewBulb_{i:D2}";
            bulb.hideFlags = HideFlags.DontSave; // Ne sera pas sauvegardé

            previewBulbs.Add(bulb);
        }

        Debug.Log($"[MarqueeLightSpawner] Preview : {bulbCount} bulbs générées");
    }

    public void ClearPreview()
    {
        // Détruire les bulbs trackées
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

        // Détruire le container
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

    private void OnDisable()
    {
        // Auto-clear en quittant Play mode
        if (!Application.isPlaying)
        {
            ClearPreview();
        }
    }
}