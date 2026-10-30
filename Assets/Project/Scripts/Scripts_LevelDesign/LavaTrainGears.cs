using UnityEngine;
using UnityEngine.Splines;
using System.Collections.Generic;

// ESSAI R&D : une roue crantee (etoile extrudee, mesh genere) a chaque knot de la spline du train,
// a la hauteur de la chaine. Elles tournent a la vitesse reelle du train (s'arretent au frein).
// Reglages dans LavaTrainSettings, rien a remplir dans l'Inspector.
[RequireComponent(typeof(PlatformTrainManager))]
public class LavaTrainGears : MonoBehaviour
{
    private PlatformTrainManager manager;
    private LavaTrainSettings settings;
    private List<Transform> gears = new List<Transform>();
    private List<Vector3> basePositions = new List<Vector3>();
    private Mesh gearMesh;

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

        Material material = settings.gearMaterial != null ? settings.gearMaterial : settings.lavaTrainBasic;
        gearMesh = BuildStarMesh(settings.gearTeeth, settings.gearOuterRadius, settings.gearInnerRadius, settings.gearThickness);

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
            GameObject go = new GameObject("Gear (" + i + ")");
            go.transform.SetParent(transform, false);
            go.transform.position = knots[i] + Vector3.up * settings.gearHeightOffset;
            basePositions.Add(knots[i]);
            go.AddComponent<MeshFilter>().sharedMesh = gearMesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
            gears.Add(go.transform);
        }
    }

    void Update()
    {
        // Vitesse angulaire = vitesse lineaire de la chaine / rayon
        float degreesPerSecond = manager.CurrentSpeed / Mathf.Max(0.01f, settings.gearOuterRadius) * Mathf.Rad2Deg;
        float angle = spinSign * degreesPerSecond * settings.gearSpinFactor * Time.deltaTime;
        Vector3 offset = Vector3.up * settings.gearHeightOffset;
        for (int i = 0; i < gears.Count; i++)
        {
            gears[i].position = basePositions[i] + offset;
            gears[i].Rotate(0f, angle, 0f, Space.World);
        }
    }

    void OnDestroy()
    {
        if (gearMesh != null) Destroy(gearMesh);
    }

    // Etoile a "teeth" branches, extrudee sur "thickness" (centree en Y). Faces separees = aretes nettes.
    private static Mesh BuildStarMesh(int teeth, float outerRadius, float innerRadius, float thickness)
    {
        int ringCount = teeth * 2;
        Vector3[] ring = new Vector3[ringCount];
        for (int i = 0; i < ringCount; i++)
        {
            float angle = Mathf.PI * 2f * i / ringCount;
            float radius = (i % 2 == 0) ? outerRadius : innerRadius;
            ring[i] = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
        }

        Vector3 up = Vector3.up * thickness * 0.5f;
        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();

        // Dessus (vu d'en haut) et dessous
        int topCenter = vertices.Count;
        vertices.Add(up);
        for (int i = 0; i < ringCount; i++) vertices.Add(ring[i] + up);
        for (int i = 0; i < ringCount; i++)
        {
            int next = (i + 1) % ringCount;
            triangles.Add(topCenter); triangles.Add(topCenter + 1 + next); triangles.Add(topCenter + 1 + i);
        }

        int bottomCenter = vertices.Count;
        vertices.Add(-up);
        for (int i = 0; i < ringCount; i++) vertices.Add(ring[i] - up);
        for (int i = 0; i < ringCount; i++)
        {
            int next = (i + 1) % ringCount;
            triangles.Add(bottomCenter); triangles.Add(bottomCenter + 1 + i); triangles.Add(bottomCenter + 1 + next);
        }

        // Cotes
        for (int i = 0; i < ringCount; i++)
        {
            int next = (i + 1) % ringCount;
            int start = vertices.Count;
            vertices.Add(ring[i] + up);
            vertices.Add(ring[next] + up);
            vertices.Add(ring[next] - up);
            vertices.Add(ring[i] - up);
            triangles.Add(start); triangles.Add(start + 1); triangles.Add(start + 2);
            triangles.Add(start); triangles.Add(start + 2); triangles.Add(start + 3);
        }

        Mesh mesh = new Mesh { name = "LavaTrainGear" };
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }
}
