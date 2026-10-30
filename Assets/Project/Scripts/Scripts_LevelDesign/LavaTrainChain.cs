using UnityEngine;
using UnityEngine.Splines;

// Chaine dessinee le long de la spline du train (LineRenderer cree au Start).
// Creux sous le wagon du joueur (chainSagDepth, 0 = pas de creux).
// Les maillons defilent a la vitesse du train (decalage de texture sur une copie du materiau, l'asset n'est pas touche).
// Tous les reglages viennent de LavaTrainSettings, rien a remplir dans l'Inspector.
[RequireComponent(typeof(PlatformTrainManager))]
public class LavaTrainChain : MonoBehaviour
{
    private PlatformTrainManager manager;
    private LavaTrainSettings settings;
    private LineRenderer line;
    private Material chainMaterialInstance;
    private float scrollOffset = 0f;

    private Vector3[] basePoints;
    private float[] pointProgress;
    private Vector3[] points;
    private float splineLength;
    private bool closed;

    private float currentSagDepth = 0f;
    private float sagProgress = 0f;
    private bool sagApplied = false;

    void Start()
    {
        manager = GetComponent<PlatformTrainManager>();
        SplineContainer splineContainer = manager.SplineContainer;
        settings = manager.Settings;

        if (splineContainer == null || settings == null)
        {
            Debug.LogWarning("[LavaTrainChain] Spline ou LavaTrainSettings manquant sur le PlatformTrainManager, pas de chaine.");
            enabled = false;
            return;
        }

        if (settings.chainMaterial == null)
        {
            Debug.LogWarning("[LavaTrainChain] Pas de chainMaterial dans " + settings.name + ", pas de chaine.");
            enabled = false;
            return;
        }

        splineLength = splineContainer.CalculateLength();
        int pointCount = Mathf.Max(2, Mathf.CeilToInt(splineLength / Mathf.Max(0.01f, settings.chainPointSpacing)));
        closed = splineContainer.Spline.Closed;

        // Spline fermee : le dernier point rejoint le premier via loop, on ne le duplique pas
        int segments = closed ? pointCount : pointCount - 1;
        basePoints = new Vector3[pointCount];
        pointProgress = new float[pointCount];
        points = new Vector3[pointCount];
        Vector3 offset = Vector3.up * settings.chainHeightOffset;
        for (int i = 0; i < pointCount; i++)
        {
            pointProgress[i] = (float)i / segments;
            basePoints[i] = (Vector3)splineContainer.EvaluatePosition(pointProgress[i]) + offset;
            points[i] = basePoints[i];
        }

        line = gameObject.AddComponent<LineRenderer>();
        chainMaterialInstance = new Material(settings.chainMaterial);
        chainMaterialInstance.name = settings.chainMaterial.name + " (LavaTrainChain)";
        line.sharedMaterial = chainMaterialInstance;
        line.widthMultiplier = settings.chainWidth;
        line.textureMode = LineTextureMode.Tile;
        line.textureScale = new Vector2(settings.chainTextureScale, 1f);
        line.alignment = LineAlignment.View;
        line.useWorldSpace = true;
        line.loop = closed;
        line.positionCount = pointCount;
        line.SetPositions(points);
    }

    // Defilement au meme rythme que les wagons (FixedUpdate du manager), sinon les maillons glissent
    // en continu sous un creux qui avance par a-coups.
    // En mode Tile la texture se repete chainTextureScale fois par metre : avancer de d metres = decaler de d * chainTextureScale
    void FixedUpdate()
    {
        float travelled = manager.CurrentSpeed * settings.chainScrollFactor * Time.fixedDeltaTime;
        scrollOffset = Mathf.Repeat(scrollOffset - travelled * settings.chainTextureScale, 1f);
    }

    // LateUpdate : les wagons ont deja bouge (FixedUpdate du manager)
    void LateUpdate()
    {
        chainMaterialInstance.mainTextureOffset = new Vector2(scrollOffset, 0f);

        PlatformTrainCar playerCar = manager.PlayerCar;
        if (playerCar != null)
            sagProgress = playerCar.currentProgress;

        float targetDepth = playerCar != null ? settings.chainSagDepth : 0f;
        currentSagDepth = Mathf.MoveTowards(currentSagDepth, targetDepth, settings.chainSagSpeed * Time.deltaTime);

        // Rien a faire tant que la chaine est au repos et deja a plat
        if (currentSagDepth <= 0f && !sagApplied) return;

        float halfWidth = Mathf.Max(0.01f, settings.chainSagHalfWidth);
        for (int i = 0; i < points.Length; i++)
        {
            float delta = Mathf.Abs(pointProgress[i] - sagProgress);
            if (closed) delta = Mathf.Min(delta, 1f - delta);
            float distance = delta * splineLength;

            Vector3 p = basePoints[i];
            if (distance < halfWidth)
            {
                // Creux arrondi : maximum sous le wagon, nul a halfWidth
                float weight = 0.5f * (1f + Mathf.Cos(Mathf.PI * distance / halfWidth));
                p.y -= currentSagDepth * weight;
            }
            points[i] = p;
        }
        line.SetPositions(points);
        sagApplied = currentSagDepth > 0f;
    }

    void OnDestroy()
    {
        if (chainMaterialInstance != null) Destroy(chainMaterialInstance);
    }
}
