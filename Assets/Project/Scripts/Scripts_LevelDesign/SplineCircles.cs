using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;

// Disques a plat poses le long d'une spline, espaces regulierement, avec une suite de materiaux qui se repete
// (disque 1 = materiau 1, disque 2 = materiau 2... puis on recommence).
// Largeur : celle du premier RoamingObstacle qui roule sur cette spline (en travers de sa marche), sinon
// "Largeur par defaut". Multiplicateur pour des disques plus petits ou plus grands.
// Usage : poser sur un GameObject a part (par ex. enfant de la spline), renseigner la spline. Le mesh se met a
// jour en direct. Il n'est pas sauvegarde : reconstruit a partir des reglages a chaque chargement.
// Etape 1 du visuel en jeu des roaming obstacles (pas de lueur, pas de decalage lateral pour l'instant).
//
// UV0 : en metres, centres sur chaque disque.
[ExecuteAlways]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class SplineCircles : MonoBehaviour
{
    [Header("Spline")]
    [SerializeField] private SplineContainer splineContainer;

    [Header("Disques")]
    [Tooltip("Ecart entre deux disques, mesure le long de la spline (m). Spline fermee : ajuste pour faire " +
             "un nombre rond de disques sur le tour.")]
    [Min(0.1f)] [SerializeField] private float spacing = 1.5f;
    [Tooltip("Multiplicateur de la largeur de l'obstacle (1 = meme largeur).")]
    [Min(0.01f)] [SerializeField] private float widthScale = 1f;
    [Tooltip("Largeur (m) si aucun RoamingObstacle ne roule sur cette spline.")]
    [Min(0.01f)] [SerializeField] private float defaultWidth = 1f;
    [Tooltip("Hauteur au-dessus de la spline (m), pour ne pas clignoter avec le sol.")]
    [SerializeField] private float heightAboveSpline = 0.02f;
    [Tooltip("Nombre de segments du contour d'un disque.")]
    [Range(6, 64)] [SerializeField] private int segments = 24;

    [Header("Materiaux")]
    [Tooltip("Suite de materiaux, repetee le long de la spline.")]
    [SerializeField] private Material[] materials = new Material[1];

    [Header("Debug")]
    [Tooltip("Largeur utilisee au dernier calcul (m), et l'obstacle d'ou elle vient.")]
    [SerializeField] private float usedWidth = 0f;
    [SerializeField] private RoamingObstacle widthSource;

    [System.NonSerialized] private Mesh generatedMesh;

    void Reset()
    {
        splineContainer = GetComponentInParent<SplineContainer>();
    }

    void OnEnable()
    {
        Spline.Changed += OnSplineChanged;
        Rebuild();
    }

    void OnDisable()
    {
        Spline.Changed -= OnSplineChanged;
    }

    void OnDestroy()
    {
        if (generatedMesh == null) return;
        if (Application.isPlaying) Destroy(generatedMesh);
        else DestroyImmediate(generatedMesh);
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this != null) Rebuild();
        };
    }
#endif

    private void OnSplineChanged(Spline spline, int knotIndex, SplineModification modification)
    {
        if (splineContainer != null && splineContainer.Spline == spline) Rebuild();
    }

    [ContextMenu("Reconstruire")]
    public void Rebuild()
    {
        MeshFilter mf = GetComponent<MeshFilter>();
        if (mf == null) return;

        // Apres un rechargement du code, la reference est perdue mais le mesh existe encore (DontSave) :
        // on reprend le sien, reconnu a son nom
        string meshName = "SplineCircles_" + GetInstanceID();
        if (generatedMesh == null && mf.sharedMesh != null && mf.sharedMesh.name == meshName)
            generatedMesh = mf.sharedMesh;
        if (generatedMesh == null)
        {
            generatedMesh = new Mesh { name = meshName, hideFlags = HideFlags.DontSave };
        }

        BuildMesh(generatedMesh);
        mf.sharedMesh = generatedMesh;

        MeshRenderer mr = GetComponent<MeshRenderer>();
        if (mr != null)
        {
            mr.sharedMaterials = materials != null && materials.Length > 0 ? materials : new Material[1];
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
    }

    private void BuildMesh(Mesh mesh)
    {
        mesh.Clear();
        int materialCount = Mathf.Max(1, materials != null ? materials.Length : 0);

        if (splineContainer == null || splineContainer.Spline == null || splineContainer.Spline.Count < 2)
        {
            mesh.subMeshCount = materialCount;
            return;
        }

        float length = splineContainer.CalculateLength();
        if (length <= 0.001f)
        {
            mesh.subMeshCount = materialCount;
            return;
        }

        usedWidth = FindObstacleWidth() * widthScale;
        float radius = usedWidth * 0.5f;

        bool closed = splineContainer.Spline.Closed;
        int count = closed ? Mathf.Max(1, Mathf.RoundToInt(length / spacing))
                           : Mathf.FloorToInt(length / spacing) + 1;
        count = Mathf.Min(count, 5000);
        float step = closed ? length / count : spacing;

        int ring = Mathf.Max(6, segments);
        int vertsPerDisc = ring + 1;
        var vertices = new List<Vector3>(count * vertsPerDisc);
        var normals = new List<Vector3>(count * vertsPerDisc);
        var uvs = new List<Vector2>(count * vertsPerDisc);
        var triangles = new List<int>[materialCount];
        for (int m = 0; m < materialCount; m++) triangles[m] = new List<int>();

        // Disques a plat dans le monde (face vers le ciel), convertis dans le repere de ce GameObject
        Vector3 localUp = transform.InverseTransformDirection(Vector3.up).normalized;

        for (int i = 0; i < count; i++)
        {
            float t = Mathf.Clamp01(i * step / length);
            Vector3 center = (Vector3)splineContainer.EvaluatePosition(t) + Vector3.up * heightAboveSpline;

            int vBase = vertices.Count;
            vertices.Add(transform.InverseTransformPoint(center));
            normals.Add(localUp);
            uvs.Add(Vector2.zero);

            for (int s = 0; s < ring; s++)
            {
                // Angle depuis +X vers +Z (sens trigo vu de dessus)
                float a = (float)s / ring * Mathf.PI * 2f;
                Vector2 p = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius;
                vertices.Add(transform.InverseTransformPoint(center + new Vector3(p.x, 0f, p.y)));
                normals.Add(localUp);
                uvs.Add(p);
            }

            // Contour en sens trigo vu de dessus : (centre, suivant, courant) pour faire face au ciel
            List<int> tris = triangles[i % materialCount];
            for (int s = 0; s < ring; s++)
            {
                tris.Add(vBase);
                tris.Add(vBase + 1 + (s + 1) % ring);
                tris.Add(vBase + 1 + s);
            }
        }

        if (vertices.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.SetVertices(vertices);
        mesh.SetNormals(normals);
        mesh.SetUVs(0, uvs);
        mesh.subMeshCount = materialCount;
        for (int m = 0; m < materialCount; m++) mesh.SetTriangles(triangles[m], m);
        mesh.RecalculateBounds();
    }

    // Largeur en travers de la marche (X local) du premier RoamingObstacle qui roule sur cette spline
    private float FindObstacleWidth()
    {
        widthSource = null;
        foreach (RoamingObstacle obstacle in FindObjectsByType<RoamingObstacle>(FindObjectsInactive.Include, FindObjectsSortMode.InstanceID))
        {
            if (obstacle.SplineContainer != splineContainer) continue;
            float w = ObstacleWidth(obstacle);
            if (w <= 0f) continue;
            widthSource = obstacle;
            return w;
        }
        return defaultWidth;
    }

    private static float ObstacleWidth(RoamingObstacle obstacle)
    {
        Transform t = obstacle.transform;
        Vector3 s = t.lossyScale;
        Collider col = obstacle.GetComponent<Collider>();

        if (col is SphereCollider sphere)
            return 2f * sphere.radius * Mathf.Max(Mathf.Abs(s.x), Mathf.Abs(s.z));
        if (col is CapsuleCollider capsule)
        {
            // Couchee le long de X : sa longueur est en travers de la marche
            if (capsule.direction == 0) return capsule.height * Mathf.Abs(s.x);
            float side = capsule.direction == 1 ? Mathf.Max(Mathf.Abs(s.x), Mathf.Abs(s.z)) : Mathf.Abs(s.x);
            return 2f * capsule.radius * side;
        }
        if (col is BoxCollider box)
            return box.size.x * Mathf.Abs(s.x);

        MeshFilter mf = obstacle.GetComponent<MeshFilter>();
        if (mf != null && mf.sharedMesh != null) return mf.sharedMesh.bounds.size.x * Mathf.Abs(s.x);
        return col != null ? col.bounds.size.x : 0f;
    }
}
