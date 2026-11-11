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

    [Header("Disques du dessus autour des obstacles (en jeu)")]
    [Tooltip("Materiau des disques poses par-dessus les disques proches de chaque obstacle. Vide = rien.")]
    [SerializeField] private Material outlineMaterial;
    [Tooltip("De combien le disque du dessus depasse du disque, tout autour (m). Negatif = plus petit.")]
    [SerializeField] private float outlineExtra = 0.08f;
    [Tooltip("Nombre de disques concernes devant et derriere l'obstacle (en plus de celui sous lui).")]
    [Min(0)] [SerializeField] private int outlineRange = 3;
    [Tooltip("Quand un disque sort de la zone de l'obstacle, son disque du dessus retrecit jusqu'a zero en ce temps (s). " +
             "0 = disparait d'un coup.")]
    [Min(0f)] [SerializeField] private float shrinkDuration = 0.4f;
    [Tooltip("Quand un disque entre dans la zone de l'obstacle, son disque du dessus grandit de zero a pleine taille " +
             "en ce temps (s). 0 = apparait d'un coup.")]
    [Min(0f)] [SerializeField] private float growDuration = 0.2f;

    [Header("Debug")]
    [Tooltip("Largeur utilisee au dernier calcul (m), et l'obstacle d'ou elle vient.")]
    [SerializeField] private float usedWidth = 0f;
    [SerializeField] private RoamingObstacle widthSource;

    [System.NonSerialized] private Mesh generatedMesh;

    // Centres des disques (monde) et rayon, notes a la construction du mesh
    private readonly List<Vector3> discCenters = new List<Vector3>();
    private float discRadius;
    private bool discsClosed;

    // Contour (en jeu seulement)
    private readonly List<RoamingObstacle> obstacles = new List<RoamingObstacle>();
    private readonly List<Transform> outlinePool = new List<Transform>();
    private GameObject outlineRoot;
    private Mesh outlineMesh;
    private bool[] outlined;
    // Taille du disque du dessus par disque (0 = absent, 1 = pleine taille)
    private float[] outlineScale;

    void Start()
    {
        if (!Application.isPlaying || outlineMaterial == null || discCenters.Count == 0) return;

        foreach (RoamingObstacle obstacle in FindObjectsByType<RoamingObstacle>(FindObjectsSortMode.InstanceID))
            if (obstacle.SplineContainer == splineContainer) obstacles.Add(obstacle);
        if (obstacles.Count == 0) return;

        // A la racine de la scene, sans rotation ni echelle : le mesh du contour est en metres, a plat
        outlineRoot = new GameObject(name + "_Outlines");
        outlineMesh = BuildDiscMesh(Mathf.Max(0.01f, discRadius + outlineExtra), Mathf.Max(6, segments));
        // Le reste vient a la demande (disques encore en train de retrecir)
        int poolSize = Mathf.Min(discCenters.Count, obstacles.Count * (2 * outlineRange + 1));
        for (int i = 0; i < poolSize; i++) AddOutlineToPool();
        outlined = new bool[discCenters.Count];
        outlineScale = new float[discCenters.Count];
    }

    private void AddOutlineToPool()
    {
        var go = new GameObject("Outline " + outlinePool.Count);
        go.transform.SetParent(outlineRoot.transform, false);
        go.AddComponent<MeshFilter>().sharedMesh = outlineMesh;
        MeshRenderer r = go.AddComponent<MeshRenderer>();
        r.sharedMaterial = outlineMaterial;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        go.SetActive(false);
        outlinePool.Add(go.transform);
    }

    void LateUpdate()
    {
        if (!Application.isPlaying || outlineRoot == null) return;

        int count = discCenters.Count;
        System.Array.Clear(outlined, 0, outlined.Length);

        // Disques a entourer : le plus proche de chaque obstacle, et ses voisins
        foreach (RoamingObstacle obstacle in obstacles)
        {
            if (obstacle == null) continue;
            int nearest = NearestDisc(obstacle.transform.position);
            for (int k = -outlineRange; k <= outlineRange; k++)
            {
                int index = nearest + k;
                if (discsClosed) index = ((index % count) + count) % count;
                else if (index < 0 || index >= count) continue;
                outlined[index] = true;
            }
        }

        // Dans la zone : grandit jusqu'a pleine taille ; sorti : retrecit jusqu'a zero
        float shrink = shrinkDuration > 0f ? Time.deltaTime / shrinkDuration : 1f;
        float grow = growDuration > 0f ? Time.deltaTime / growDuration : 1f;
        int used = 0;
        for (int i = 0; i < count; i++)
        {
            outlineScale[i] = outlined[i] ? Mathf.Min(1f, outlineScale[i] + grow)
                                          : Mathf.Max(0f, outlineScale[i] - shrink);
            if (outlineScale[i] <= 0f) continue;

            if (used >= outlinePool.Count) AddOutlineToPool();
            Transform t = outlinePool[used++];
            // Juste au-dessus du disque
            t.position = discCenters[i] + Vector3.up * 0.005f;
            float s = outlineScale[i];
            t.localScale = new Vector3(s, 1f, s);
            if (!t.gameObject.activeSelf) t.gameObject.SetActive(true);
        }
        for (int i = used; i < outlinePool.Count; i++)
            if (outlinePool[i].gameObject.activeSelf) outlinePool[i].gameObject.SetActive(false);
    }

    private int NearestDisc(Vector3 position)
    {
        int best = 0;
        float bestSqr = float.MaxValue;
        for (int i = 0; i < discCenters.Count; i++)
        {
            Vector3 d = discCenters[i] - position;
            d.y = 0f;
            float sqr = d.sqrMagnitude;
            if (sqr < bestSqr) { bestSqr = sqr; best = i; }
        }
        return best;
    }

    // Disque a plat (face vers le ciel), en metres, centre sur l'origine
    private static Mesh BuildDiscMesh(float radius, int ring)
    {
        var vertices = new Vector3[ring + 1];
        var normals = new Vector3[ring + 1];
        var uvs = new Vector2[ring + 1];
        normals[0] = Vector3.up;
        for (int s = 0; s < ring; s++)
        {
            float a = (float)s / ring * Mathf.PI * 2f;
            Vector2 p = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius;
            vertices[s + 1] = new Vector3(p.x, 0f, p.y);
            normals[s + 1] = Vector3.up;
            uvs[s + 1] = p;
        }
        var triangles = new int[ring * 3];
        for (int s = 0; s < ring; s++)
        {
            triangles[s * 3] = 0;
            triangles[s * 3 + 1] = 1 + (s + 1) % ring;
            triangles[s * 3 + 2] = 1 + s;
        }
        var mesh = new Mesh { name = "SplineCirclesOutline" };
        mesh.vertices = vertices;
        mesh.normals = normals;
        mesh.uv = uvs;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();
        return mesh;
    }

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
        if (outlineRoot != null) Destroy(outlineRoot);
        if (outlineMesh != null) Destroy(outlineMesh);
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
        discCenters.Clear();
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
        discRadius = radius;
        discsClosed = closed;

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
            discCenters.Add(center);

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
