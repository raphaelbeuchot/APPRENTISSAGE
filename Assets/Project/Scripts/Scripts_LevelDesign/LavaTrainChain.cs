using UnityEngine;
using UnityEngine.Splines;

// Chaine immobile dessinee le long de la spline du train (LineRenderer cree au Start).
// Tous les reglages viennent de LavaTrainSettings, rien a remplir dans l'Inspector.
[RequireComponent(typeof(PlatformTrainManager))]
public class LavaTrainChain : MonoBehaviour
{
    void Start()
    {
        PlatformTrainManager manager = GetComponent<PlatformTrainManager>();
        SplineContainer splineContainer = manager.SplineContainer;
        LavaTrainSettings settings = manager.Settings;

        if (splineContainer == null || settings == null)
        {
            Debug.LogWarning("[LavaTrainChain] Spline ou LavaTrainSettings manquant sur le PlatformTrainManager, pas de chaine.");
            return;
        }

        if (settings.chainMaterial == null)
        {
            Debug.LogWarning("[LavaTrainChain] Pas de chainMaterial dans " + settings.name + ", pas de chaine.");
            return;
        }

        Spline spline = splineContainer.Spline;
        float length = splineContainer.CalculateLength();
        int pointCount = Mathf.Max(2, Mathf.CeilToInt(length / Mathf.Max(0.01f, settings.chainPointSpacing)));
        bool closed = spline.Closed;

        // Spline fermee : le dernier point rejoint le premier via loop, on ne le duplique pas
        int segments = closed ? pointCount : pointCount - 1;
        Vector3[] points = new Vector3[pointCount];
        Vector3 offset = Vector3.up * settings.chainHeightOffset;
        for (int i = 0; i < pointCount; i++)
        {
            float t = (float)i / segments;
            points[i] = (Vector3)splineContainer.EvaluatePosition(t) + offset;
        }

        LineRenderer line = gameObject.AddComponent<LineRenderer>();
        line.sharedMaterial = settings.chainMaterial;
        line.widthMultiplier = settings.chainWidth;
        line.textureMode = LineTextureMode.Tile;
        line.textureScale = new Vector2(settings.chainTextureScale, 1f);
        line.alignment = LineAlignment.View;
        line.useWorldSpace = true;
        line.loop = closed;
        line.positionCount = pointCount;
        line.SetPositions(points);
    }
}
