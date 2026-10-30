using UnityEngine;
using UnityEngine.Splines;
using System.Collections.Generic;

// Une roue crantee (gearPrefab, en general un GearShape) a chaque knot de la spline du train,
// a la hauteur de la chaine. Elles tournent a la vitesse reelle du train (s'arretent au frein).
// Reglages dans LavaTrainSettings, rien a remplir dans l'Inspector.
[RequireComponent(typeof(PlatformTrainManager))]
public class LavaTrainGears : MonoBehaviour
{
    private PlatformTrainManager manager;
    private LavaTrainSettings settings;
    private List<Transform> gears = new List<Transform>();
    private List<Vector3> basePositions = new List<Vector3>();

    // Rayon servant a caler la rotation sur la chaine : celui du GearShape du prefab
    private float gearRadius = 0.5f;

    // -1 ou 1 : sens de rotation, deduit du sens de la boucle vue de dessus
    private float spinSign = 1f;

    void Start()
    {
        manager = GetComponent<PlatformTrainManager>();
        SplineContainer splineContainer = manager.SplineContainer;
        settings = manager.Settings;

        if (splineContainer == null || settings == null)
        {
            Debug.LogWarning("[LavaTrainGears] Spline ou LavaTrainSettings manquant sur le PlatformTrainManager, pas de roues.");
            enabled = false;
            return;
        }

        if (settings.gearPrefab == null)
        {
            Debug.LogWarning("[LavaTrainGears] Pas de gearPrefab dans " + settings.name + ", pas de roues.");
            enabled = false;
            return;
        }

        GearShape shape = settings.gearPrefab.GetComponent<GearShape>();
        if (shape != null) gearRadius = shape.outerRadius;

        List<Vector3> knots = new List<Vector3>();
        foreach (BezierKnot knot in splineContainer.Spline.Knots)
            knots.Add(splineContainer.transform.TransformPoint(knot.Position) + Vector3.up * settings.chainHeightOffset);

        // Aire signee en XZ : positive = boucle dans le sens anti-horaire vue de dessus
        float area = 0f;
        for (int i = 0; i < knots.Count; i++)
        {
            Vector3 a = knots[i];
            Vector3 b = knots[(i + 1) % knots.Count];
            area += a.x * b.z - b.x * a.z;
        }
        // Rotation Unity positive autour de Y = sens horaire vu de dessus
        spinSign = area > 0f ? -1f : 1f;

        for (int i = 0; i < knots.Count; i++)
        {
            GameObject go = Instantiate(settings.gearPrefab, knots[i] + Vector3.up * settings.gearHeightOffset,
                settings.gearPrefab.transform.rotation, transform);
            go.name = settings.gearPrefab.name + " (" + i + ")";
            gears.Add(go.transform);
            basePositions.Add(knots[i]);
        }
    }

    void Update()
    {
        // Vitesse angulaire = vitesse lineaire de la chaine / rayon
        float degreesPerSecond = manager.CurrentSpeed / Mathf.Max(0.01f, gearRadius) * Mathf.Rad2Deg;
        float angle = spinSign * degreesPerSecond * settings.gearSpinFactor * Time.deltaTime;
        Vector3 offset = Vector3.up * settings.gearHeightOffset;
        for (int i = 0; i < gears.Count; i++)
        {
            gears[i].position = basePositions[i] + offset;
            gears[i].Rotate(0f, angle, 0f, Space.World);
        }
    }
}
