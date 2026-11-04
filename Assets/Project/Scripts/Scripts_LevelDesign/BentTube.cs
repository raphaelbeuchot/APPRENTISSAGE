using System.Collections.Generic;
using UnityEngine;

// Tube plein coude : une section (rectangle aux coins arrondis, comme RoundedBlock) balayee le long d'un chemin
// droit - arc - droit. Le tube part du pivot vers +Z ; le coude tourne dans le plan vertical local (YZ), vers le
// haut (+Y) si l'angle est positif, vers le bas s'il est negatif. Pour un coude "a plat", tourner le GO.
// Cercle : section carree, rayon = moitie du cote ; demi-cercle : deux coins au rayon maxi.
// Usage : poser sur un GameObject vide, regler, le mesh se met a jour en direct.
// Le mesh n'est pas sauvegarde : il est reconstruit a partir des reglages a chaque chargement (marche tel quel dans un prefab).
// Pivot au debut du tube, au centre de la section. Un MeshCollider present sur l'objet recoit le mesh.
//
// UV0 : en metres. Pourtour : u = distance le long du contour de la section, v = distance le long du tube (axe).
//       Bouts : projection dans le plan de la section.
[ExecuteAlways]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class BentTube : MonoBehaviour
{
    [Header("Section (metres)")]
    [Tooltip("Largeur de la section (X local au depart du tube).")]
    [Min(0.01f)] public float width = 0.5f;
    [Tooltip("Hauteur de la section (Y local au depart du tube, dans le plan du coude).")]
    [Min(0.01f)] public float height = 0.5f;

    [Tooltip("Un seul rayon pour les 4 coins de la section.")]
    public bool linkCorners = true;
    [Min(0f)] public float radius = 0.25f;
    [Tooltip("Rayons par coin (si Coins lies est decoche), vus depuis le debut du tube en regardant vers +Z : " +
             "A bas gauche, B bas droite, C haut droite, D haut gauche.")]
    [Min(0f)] public float radiusA = 0.25f;
    [Min(0f)] public float radiusB = 0.25f;
    [Min(0f)] public float radiusC = 0.25f;
    [Min(0f)] public float radiusD = 0.25f;
    [Tooltip("Nombre de segments par quart de rond des coins de la section.")]
    [Range(1, 32)] public int cornerSegments = 4;

    [Header("Chemin")]
    [Tooltip("Longueur totale du tube, mesuree sur son axe (coude compris).")]
    [Min(0.01f)] public float length = 3f;
    [Tooltip("Angle du coude, en degres. Positif = vers le haut (+Y), negatif = vers le bas. 0 = tube droit.")]
    [Range(-180f, 180f)] public float bendAngle = 90f;
    [Tooltip("Rayon de courbure du coude, mesure sur l'axe du tube. Au moins la demi-hauteur de la section " +
             "(sinon l'interieur du coude se croiserait).")]
    [Min(0.01f)] public float bendRadius = 0.5f;
    [Tooltip("Position du coude sur la partie droite : 0 = coude au debut, 1 = coude a la fin.")]
    [Range(0f, 1f)] public float bendPosition = 0.5f;
    [Tooltip("Nombre de segments du coude par quart de tour.")]
    [Range(1, 64)] public int bendSegmentsPer90 = 12;

    [Header("Materiaux")]
    [Tooltip("Decoche : un seul materiau. Coche : element 0 = pourtour, 1 = bouts.")]
    public bool separateMaterials = false;

    [System.NonSerialized] Mesh generatedMesh;

    void OnEnable()
    {
        Rebuild();
    }

    void OnDestroy()
    {
        if (generatedMesh == null) return;
        if (Application.isPlaying) Destroy(generatedMesh);
        else DestroyImmediate(generatedMesh);
    }

#if UNITY_EDITOR
    // Toucher au MeshCollider pendant OnValidate provoque des avertissements : on decale d'une frame d'editeur.
    void OnValidate()
    {
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this != null) Rebuild();
        };
    }
#endif

    public void Rebuild()
    {
        MeshFilter mf = GetComponent<MeshFilter>();
        if (mf == null) return;

        // Apres un rechargement du code, la reference est perdue mais le mesh existe encore (DontSave : jamais nettoye
        // par Unity) : on reprend le sien, reconnu a son nom, au lieu d en creer un de plus a chaque rechargement.
        string meshName = "BentTube_" + GetInstanceID();
        if (generatedMesh == null && mf.sharedMesh != null && mf.sharedMesh.name == meshName)
            generatedMesh = mf.sharedMesh;
        if (generatedMesh == null)
        {
            generatedMesh = new Mesh();
            generatedMesh.name = meshName;
            generatedMesh.hideFlags = HideFlags.DontSave;
        }
        BuildMesh(generatedMesh);
        mf.sharedMesh = generatedMesh;
        MatchMaterialCount();

        MeshCollider mc = GetComponent<MeshCollider>();
        if (mc != null)
        {
            mc.sharedMesh = null; // force la mise a jour du collider
            mc.sharedMesh = generatedMesh;
        }
    }

    // Autant de materiaux que de sous-meshes (un en trop serait redessine par-dessus, un manquant laisserait un trou)
    void MatchMaterialCount()
    {
        MeshRenderer mr = GetComponent<MeshRenderer>();
        if (mr == null) return;
        Material[] mats = mr.sharedMaterials;
        int wanted = separateMaterials ? 2 : 1;
        if (mats.Length == wanted || mats.Length == 0) return;

        Material[] fixedMats = new Material[wanted];
        for (int i = 0; i < wanted; i++)
            fixedMats[i] = i < mats.Length ? mats[i] : mats[mats.Length - 1];
        mr.sharedMaterials = fixedMats;
    }

    // ---------------------------------------------------------------- Chemin

    // Rayon reellement utilise : au moins la demi-hauteur de la section
    public float EffectiveRadius => Mathf.Max(bendRadius, Mathf.Max(0.01f, height) * 0.5f);

    // Longueur du coude sur l'axe
    public float ArcLength => EffectiveRadius * Mathf.Abs(bendAngle) * Mathf.Deg2Rad;

    // Le coude seul depasse la longueur : les parties droites sont nulles et le tube est plus long que regle
    public bool BendTooLong => ArcLength > length + 1e-4f;

    // Une position le long du tube, avec son repere : t = sens du tube, n = haut de la section, x = largeur (fixe)
    struct Station
    {
        public Vector3 p, t, n;
        public float s; // distance le long de l'axe
    }

    List<Station> BuildStations()
    {
        var stations = new List<Station>();
        float arc = ArcLength;
        float straight = Mathf.Max(0f, length - arc);
        float a = straight * bendPosition;
        float b = straight - a;

        Vector3 t0 = Vector3.forward, n0 = Vector3.up;
        stations.Add(new Station { p = Vector3.zero, t = t0, n = n0, s = 0f });

        Vector3 pa = t0 * a;
        if (a > 1e-5f) stations.Add(new Station { p = pa, t = t0, n = n0, s = a });

        Vector3 pEnd = pa, tEnd = t0, nEnd = n0;
        if (Mathf.Abs(bendAngle) > 0.01f)
        {
            float sign = Mathf.Sign(bendAngle);
            float total = Mathf.Abs(bendAngle) * Mathf.Deg2Rad;
            float r = EffectiveRadius;
            Vector3 d = n0 * sign; // cote vers lequel le tube tourne (centre du cercle)
            int segs = Mathf.Max(1, Mathf.CeilToInt(Mathf.Abs(bendAngle) / 90f * bendSegmentsPer90));
            for (int i = 1; i <= segs; i++)
            {
                float alpha = total * i / segs;
                Vector3 t = t0 * Mathf.Cos(alpha) + d * Mathf.Sin(alpha);
                Vector3 toCenter = d * Mathf.Cos(alpha) - t0 * Mathf.Sin(alpha);
                Vector3 p = pa + d * r - toCenter * r;
                stations.Add(new Station { p = p, t = t, n = toCenter * sign, s = a + r * alpha });
            }
            Station last = stations[stations.Count - 1];
            pEnd = last.p; tEnd = last.t; nEnd = last.n;
        }

        if (b > 1e-5f) stations.Add(new Station { p = pEnd + tEnd * b, t = tEnd, n = nEnd, s = a + arc + b });
        return stations;
    }

    // ---------------------------------------------------------------- Section

    struct RingPoint
    {
        public Vector2 p; // position dans la section (x = largeur, y = hauteur)
        public Vector2 n; // normale dans ce plan, vers l'exterieur
        public RingPoint(Vector2 p, Vector2 n) { this.p = p; this.n = n; }
    }

    // Contour ferme dans le sens trigo (meme principe que RoundedBlock.BuildRing) : depart au milieu du cote gauche,
    // puis coins A, B, C, D. Chaque coin a cornerSegments + 1 points, meme vif (points superposes, normales en
    // eventail : quads d'aire nulle, l'arete reste nette). Le dernier point = le premier (pour fermer les UV).
    List<RingPoint> BuildRing()
    {
        float hu = Mathf.Max(0.01f, width) * 0.5f;
        float hv = Mathf.Max(0.01f, height) * 0.5f;
        float max = Mathf.Min(hu, hv);
        float[] radii = linkCorners
            ? new[] { radius, radius, radius, radius }
            : new[] { radiusA, radiusB, radiusC, radiusD };

        Vector2[] corners = { new Vector2(-hu, -hv), new Vector2(hu, -hv), new Vector2(hu, hv), new Vector2(-hu, hv) };
        Vector2[] signs = { new Vector2(-1, -1), new Vector2(1, -1), new Vector2(1, 1), new Vector2(-1, 1) };
        float[] startAngles = { 180f, 270f, 0f, 90f };
        int seg = Mathf.Max(1, cornerSegments);

        var ring = new List<RingPoint> { new RingPoint(new Vector2(-hu, 0f), Vector2.left) };
        for (int k = 0; k < 4; k++)
        {
            float r = Mathf.Clamp(radii[k], 0f, max);
            Vector2 center = corners[k] - signs[k] * r;
            for (int s = 0; s <= seg; s++)
            {
                float a = (startAngles[k] + 90f * s / seg) * Mathf.Deg2Rad;
                Vector2 n = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                ring.Add(new RingPoint(center + n * r, n));
            }
        }
        ring.Add(ring[0]);
        return ring;
    }

    // ---------------------------------------------------------------- Mesh

    readonly List<Vector3> verts = new List<Vector3>();
    readonly List<Vector3> normals = new List<Vector3>();
    readonly List<Vector2> uvs = new List<Vector2>();
    readonly List<int> sideTris = new List<int>();
    readonly List<int> capTris = new List<int>();

    void BuildMesh(Mesh mesh)
    {
        verts.Clear(); normals.Clear(); uvs.Clear(); sideTris.Clear(); capTris.Clear();

        List<Station> stations = BuildStations();
        List<RingPoint> ring = BuildRing();
        Vector3 x = Vector3.right; // le coude tourne autour de X : la largeur reste en X

        // Distance le long du contour (u des UV du pourtour)
        var perim = new float[ring.Count];
        for (int j = 1; j < ring.Count; j++) perim[j] = perim[j - 1] + Vector2.Distance(ring[j].p, ring[j - 1].p);

        // --- Pourtour : une couronne de sommets par station, reliees par des quads
        int rc = ring.Count;
        foreach (Station st in stations)
            for (int j = 0; j < rc; j++)
            {
                verts.Add(st.p + x * ring[j].p.x + st.n * ring[j].p.y);
                normals.Add((x * ring[j].n.x + st.n * ring[j].n.y).normalized);
                uvs.Add(new Vector2(perim[j], st.s));
            }
        for (int i = 0; i < stations.Count - 1; i++)
            for (int j = 0; j < rc - 1; j++)
            {
                int a = i * rc + j, b = a + 1, c = a + rc, d = c + 1;
                sideTris.Add(a); sideTris.Add(b); sideTris.Add(c);
                sideTris.Add(b); sideTris.Add(d); sideTris.Add(c);
            }

        // --- Bouts : eventail depuis le centre (section convexe)
        AddCap(stations[0], ring, x, false);
        AddCap(stations[stations.Count - 1], ring, x, true);

        mesh.Clear();
        mesh.SetVertices(verts);
        mesh.SetNormals(normals);
        mesh.SetUVs(0, uvs);
        if (separateMaterials)
        {
            mesh.subMeshCount = 2;
            mesh.SetTriangles(sideTris, 0);
            mesh.SetTriangles(capTris, 1);
        }
        else
        {
            sideTris.AddRange(capTris);
            mesh.subMeshCount = 1;
            mesh.SetTriangles(sideTris, 0);
        }
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();
    }

    void AddCap(Station st, List<RingPoint> ring, Vector3 x, bool end)
    {
        Vector3 normal = end ? st.t : -st.t;
        int center = verts.Count;
        verts.Add(st.p);
        normals.Add(normal);
        uvs.Add(Vector2.zero);
        for (int j = 0; j < ring.Count - 1; j++)
        {
            verts.Add(st.p + x * ring[j].p.x + st.n * ring[j].p.y);
            normals.Add(normal);
            uvs.Add(ring[j].p);
        }
        int n = ring.Count - 1;
        for (int j = 0; j < n; j++)
        {
            int k = center + 1 + j, k1 = center + 1 + (j + 1) % n;
            capTris.Add(center);
            capTris.Add(end ? k : k1);
            capTris.Add(end ? k1 : k);
        }
    }
}

#if UNITY_EDITOR
// Inspecteur : rayons par coin seulement si les coins ne sont pas lies, alerte si le coude ne tient pas dans la longueur.
[UnityEditor.CustomEditor(typeof(BentTube))]
[UnityEditor.CanEditMultipleObjects]
public class BentTubeEditor : UnityEditor.Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        BentTube tube = (BentTube)target;

        string[] hidden = tube.linkCorners
            ? new[] { "radiusA", "radiusB", "radiusC", "radiusD" }
            : new[] { "radius" };
        DrawPropertiesExcluding(serializedObject, hidden);
        serializedObject.ApplyModifiedProperties();

        if (tube.bendRadius < tube.EffectiveRadius - 1e-4f)
            UnityEditor.EditorGUILayout.HelpBox("Rayon du coude ramene a la demi-hauteur de la section (" +
                                                tube.EffectiveRadius.ToString("0.###") + " m).", UnityEditor.MessageType.Info);
        if (tube.BendTooLong)
            UnityEditor.EditorGUILayout.HelpBox("Le coude seul fait " + tube.ArcLength.ToString("0.###") +
                                                " m : plus que la longueur. Pas de partie droite, tube plus long que regle.",
                                                UnityEditor.MessageType.Warning);
    }
}
#endif
