using System.Collections.Generic;
using UnityEngine;

// Mur droit parametrique avec une ouverture : fenetre, porte (ouverture qui mange un bord) ou niche (creusee sans traverser).
// Usage : poser sur un Cube (ou tout GO avec MeshFilter), regler, le mesh se met a jour en direct.
// Le mesh n'est pas sauvegarde : il est reconstruit a partir des reglages a chaque chargement (comme RoundedBlock).
// Repere : mur dans le plan XY (largeur X, hauteur Y), epaisseur en Z, pivot au milieu de la base. Face avant = +Z.
//
// UV0 : en metres. Faces : projection dans le plan du mur. Tranches : u = distance le long du contour, v = profondeur.
// UV2 (canal TEXCOORD2) : 0-1 sur chaque face.
//
// Coins A-D (mur et ouverture) : A bas gauche, B bas droite, C haut droite, D haut gauche, vus de face.
[ExecuteAlways]
[RequireComponent(typeof(MeshFilter))]
public class PerforatedWall : MonoBehaviour
{
    public enum OpeningEdge { Aucun, Bas, Haut, Gauche, Droite }

    [Header("Mur (metres)")]
    [Min(0.02f)] public float width = 3f;
    [Min(0.02f)] public float height = 2.5f;
    [Min(0.01f)] public float thickness = 0.2f;

    [Header("Coins du mur")]
    [Tooltip("Un seul rayon pour les 4 coins du mur.")]
    public bool linkCorners = true;
    [Min(0f)] public float radius = 0f;
    [Tooltip("Rayons par coin (si Coins lies est decoche). Les lettres A-D s'affichent dans la scene quand l'objet est selectionne.")]
    [Min(0f)] public float radiusA = 0f;
    [Min(0f)] public float radiusB = 0f;
    [Min(0f)] public float radiusC = 0f;
    [Min(0f)] public float radiusD = 0f;
    [Tooltip("Nombre de segments par quart de rond (coins du mur et de l'ouverture).")]
    [Range(1, 16)] public int cornerSegments = 4;

    [Header("Ouverture")]
    public bool opening = true;
    [Tooltip("Bord mange par l'ouverture. Bas = porte (posee au sol). Aucun = fenetre ou niche entierement dans le mur.")]
    public OpeningEdge edge = OpeningEdge.Aucun;
    [Tooltip("Position horizontale du centre de l'ouverture (0 = milieu du mur).")]
    public float openingX = 0f;
    [Tooltip("Hauteur du bas de l'ouverture depuis le sol.")]
    [Min(0f)] public float openingY = 0.9f;
    [Min(0.01f)] public float openingWidth = 1f;
    [Min(0.01f)] public float openingHeight = 1.2f;
    [Tooltip("Un seul rayon pour les coins de l'ouverture. Les coins poses sur un bord mange restent vifs.")]
    public bool linkOpeningCorners = true;
    [Min(0f)] public float openingRadius = 0.1f;
    [Tooltip("Rayons par coin de l'ouverture (si decoche). Rayon = demi-largeur en haut d'une ouverture assez haute : arche.")]
    [Min(0f)] public float openingRadiusA = 0.1f;
    [Min(0f)] public float openingRadiusB = 0.1f;
    [Min(0f)] public float openingRadiusC = 0.1f;
    [Min(0f)] public float openingRadiusD = 0.1f;
    [Tooltip("Coche : creuse sans traverser (niche). Decoche : perce de part en part.")]
    public bool niche = false;
    [Tooltip("Profondeur de la niche (bornee : il reste au moins 1 cm de mur derriere).")]
    [Min(0.01f)] public float nicheDepth = 0.1f;
    [Tooltip("Coche : la niche est creusee dans la face arriere (-Z) au lieu de la face avant (+Z).")]
    public bool nicheOnBack = false;

    [Header("Materiaux")]
    [Tooltip("Decoche : un seul materiau. Coche : un materiau par partie sur le MeshRenderer, " +
             "element 0 = faces, 1 = tranche exterieure, 2 = interieur de l'ouverture (tranche et fond de niche).")]
    public bool separateMaterials = false;

    [System.NonSerialized] Mesh generatedMesh;
    // Message affiche dans l'inspecteur quand l'ouverture ne peut pas etre percee (le mur reste plein)
    [System.NonSerialized] public string warning;

    // Point d'un contour : position, normale lisse sur les arrondis (zero = coin vif, normale du segment),
    // et nature du segment qui part de ce point (tranche de l'ouverture ou tranche exterieure).
    struct CPoint
    {
        public Vector2 p;
        public Vector2 n;
        public bool openingSeg;
    }

    // Rectangle aux coins arrondis : bornes et rayons A-D
    struct RRect
    {
        public float minX, maxX, minY, maxY;
        public float[] r;
    }

    static readonly Vector2[] Signs = { new Vector2(-1, -1), new Vector2(1, -1), new Vector2(1, 1), new Vector2(-1, 1) };
    static readonly float[] StartAngles = { 180f, 270f, 0f, 90f };

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
    // Remplace le BoxCollider du cube par un MeshCollider qui epouse la forme.
    void Reset()
    {
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this == null) return;
            BoxCollider box = GetComponent<BoxCollider>();
            if (box != null) UnityEditor.Undo.DestroyObjectImmediate(box);
            if (GetComponent<MeshCollider>() == null) UnityEditor.Undo.AddComponent<MeshCollider>(gameObject);
            Rebuild();
        };
    }

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

        if (generatedMesh == null)
        {
            generatedMesh = new Mesh();
            generatedMesh.name = "PerforatedWall";
            generatedMesh.hideFlags = HideFlags.DontSave;
        }
        bool hasOpening = BuildMesh(generatedMesh);
        mf.sharedMesh = generatedMesh;
        MatchMaterialCount(hasOpening);

        MeshCollider mc = GetComponent<MeshCollider>();
        if (mc != null)
        {
            mc.sharedMesh = null; // force la mise a jour du collider
            mc.sharedMesh = generatedMesh;
        }
    }

    // Autant de materiaux que de sous-meshes (meme regle que RoundedBlock)
    void MatchMaterialCount(bool hasOpening)
    {
        MeshRenderer mr = GetComponent<MeshRenderer>();
        if (mr == null) return;
        Material[] mats = mr.sharedMaterials;
        int wanted = separateMaterials ? (hasOpening ? 3 : 2) : 1;
        if (mats.Length == wanted || mats.Length == 0) return;

        Material[] fixedMats = new Material[wanted];
        for (int i = 0; i < wanted; i++)
            fixedMats[i] = i < mats.Length ? mats[i] : mats[mats.Length - 1];
        mr.sharedMaterials = fixedMats;
    }

    // ---------------------------------------------------------------- Dimensions

    RRect GetWallRect()
    {
        float w = Mathf.Max(0.02f, width), h = Mathf.Max(0.02f, height);
        RRect rect = new RRect { minX = -w * 0.5f, maxX = w * 0.5f, minY = 0f, maxY = h };
        float[] r = linkCorners
            ? new[] { radius, radius, radius, radius }
            : new[] { radiusA, radiusB, radiusC, radiusD };
        // 0.999 : deux arrondis voisins ne se touchent jamais (pas de points superposes)
        float max = Mathf.Min(w, h) * 0.5f * 0.999f;
        for (int k = 0; k < 4; k++) r[k] = Mathf.Clamp(r[k], 0f, max);
        rect.r = r;
        return rect;
    }

    float Thickness => Mathf.Max(0.01f, thickness);

    // Cote mange : 0 = bas (A-B), 1 = droite (B-C), 2 = haut (C-D), 3 = gauche (D-A), -1 = aucun
    int EdgeSide
    {
        get
        {
            switch (edge)
            {
                case OpeningEdge.Bas: return 0;
                case OpeningEdge.Droite: return 1;
                case OpeningEdge.Haut: return 2;
                case OpeningEdge.Gauche: return 3;
                default: return -1;
            }
        }
    }

    // Rectangle de l'ouverture, borne dans le mur (1 cm de marge, ou colle au bord mange).
    RRect GetOpeningRect(RRect wall, int side)
    {
        float W = wall.maxX - wall.minX, H = wall.maxY - wall.minY;
        float w = Mathf.Clamp(openingWidth, 0.01f, W - 0.02f);
        float h = Mathf.Clamp(openingHeight, 0.01f, H - 0.02f);
        float x0 = openingX - w * 0.5f;
        float y0 = openingY;

        if (side == 1) x0 = wall.maxX - w;
        else if (side == 3) x0 = wall.minX;
        else x0 = Mathf.Clamp(x0, wall.minX + 0.01f, wall.maxX - 0.01f - w);

        if (side == 0) y0 = wall.minY;
        else if (side == 2) y0 = wall.maxY - h;
        else y0 = Mathf.Clamp(y0, wall.minY + 0.01f, wall.maxY - 0.01f - h);

        RRect rect = new RRect { minX = x0, maxX = x0 + w, minY = y0, maxY = y0 + h };
        float[] r = linkOpeningCorners
            ? new[] { openingRadius, openingRadius, openingRadius, openingRadius }
            : new[] { openingRadiusA, openingRadiusB, openingRadiusC, openingRadiusD };
        // Les coins poses sur le bord mange sont vifs : leurs voisins peuvent alors aller jusqu'a toute la longueur du cote
        float max;
        if (side == 0 || side == 2) max = Mathf.Min(w * 0.5f, h);
        else if (side == 1 || side == 3) max = Mathf.Min(w, h * 0.5f);
        else max = Mathf.Min(w, h) * 0.5f;
        max *= 0.999f;
        for (int k = 0; k < 4; k++) r[k] = Mathf.Clamp(r[k], 0f, max);
        if (side >= 0)
        {
            r[side] = 0f;
            r[(side + 1) % 4] = 0f;
        }
        rect.r = r;
        return rect;
    }

    // L'ouverture doit rester dans le mur, arrondis du mur compris. Les deux coins poses sur le bord mange
    // doivent tomber sur sa partie droite (pas dans un arrondi du mur).
    bool OpeningFits(RRect wall, RRect hole, int side)
    {
        List<CPoint> ring = BuildRing(hole);
        foreach (CPoint cp in ring)
        {
            bool onEdge = side >= 0 && IsOnSide(cp.p, wall, side);
            if (!InsideWall(cp.p, wall, onEdge ? -0.0001f : 0.005f)) return false;
        }
        return true;
    }

    static bool IsOnSide(Vector2 p, RRect r, int side)
    {
        const float e = 0.0001f;
        switch (side)
        {
            case 0: return Mathf.Abs(p.y - r.minY) < e;
            case 1: return Mathf.Abs(p.x - r.maxX) < e;
            case 2: return Mathf.Abs(p.y - r.maxY) < e;
            default: return Mathf.Abs(p.x - r.minX) < e;
        }
    }

    static bool InsideWall(Vector2 p, RRect wall, float margin)
    {
        if (p.x < wall.minX + margin || p.x > wall.maxX - margin || p.y < wall.minY + margin || p.y > wall.maxY - margin)
            return false;
        for (int k = 0; k < 4; k++)
        {
            float R = wall.r[k];
            if (R <= 0f) continue;
            Vector2 c = CornerPoint(wall, k) - Signs[k] * R;
            Vector2 d = p - c;
            // Dans le quart de coin (au-dela du centre de l'arrondi, vers le coin) : il faut rester dans le cercle
            if (d.x * Signs[k].x > 0f && d.y * Signs[k].y > 0f && d.magnitude > R - margin)
                return false;
        }
        return true;
    }

    // ---------------------------------------------------------------- Contours

    static Vector2 CornerPoint(RRect r, int k)
    {
        return new Vector2(Signs[k].x < 0f ? r.minX : r.maxX, Signs[k].y < 0f ? r.minY : r.maxY);
    }

    // Un coin : quart de rond (cornerSegments + 1 points) ou un seul point s'il est vif.
    // reverse = parcouru dans le sens horaire ; normalSign = -1 pour un trou (normales vers le vide de l'ouverture).
    void AppendCorner(List<CPoint> list, RRect r, int k, bool reverse, float normalSign, bool openingSeg)
    {
        float R = r.r[k];
        Vector2 corner = CornerPoint(r, k);
        if (R < 0.00001f)
        {
            list.Add(new CPoint { p = corner, n = Vector2.zero, openingSeg = openingSeg });
            return;
        }
        Vector2 c = corner - Signs[k] * R;
        int seg = Mathf.Max(1, cornerSegments);
        for (int s = 0; s <= seg; s++)
        {
            int ss = reverse ? seg - s : s;
            float a = (StartAngles[k] + 90f * ss / seg) * Mathf.Deg2Rad;
            Vector2 d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            list.Add(new CPoint { p = c + d * R, n = d * normalSign, openingSeg = openingSeg });
        }
    }

    // Contour plein, sens trigo, depart coin A. Normales vers l'exterieur.
    List<CPoint> BuildRing(RRect r)
    {
        List<CPoint> list = new List<CPoint>();
        for (int k = 0; k < 4; k++) AppendCorner(list, r, k, false, 1f, false);
        return Dedupe(list);
    }

    // Contour d'un trou, sens horaire (A, D, C, B). Normales vers le vide du trou.
    List<CPoint> BuildHoleRing(RRect r)
    {
        List<CPoint> list = new List<CPoint>();
        int[] order = { 0, 3, 2, 1 };
        foreach (int k in order) AppendCorner(list, r, k, true, -1f, true);
        return Dedupe(list);
    }

    // Contour du mur entaille par une ouverture qui mange le cote 'side' : en longeant ce cote (sens trigo),
    // on remonte le long de l'ouverture (sens horaire autour d'elle) avant de reprendre le bord du mur.
    List<CPoint> BuildNotchedRing(RRect wall, RRect hole, int side)
    {
        List<CPoint> list = new List<CPoint>();
        int[] notch = { side, (side + 3) % 4, (side + 2) % 4, (side + 1) % 4 };
        for (int k = 0; k < 4; k++)
        {
            AppendCorner(list, wall, k, false, 1f, false);
            if (k != side) continue;
            // Le dernier coin de l'entaille est sur le bord du mur : le segment qui en part est de la tranche exterieure
            for (int i = 0; i < 4; i++)
                AppendCorner(list, hole, notch[i], true, -1f, i < 3);
        }
        return Dedupe(list);
    }

    // Fusionne les points superposes (ouverture collee a un arrondi du mur, par exemple) : le coin devient vif.
    static List<CPoint> Dedupe(List<CPoint> list)
    {
        List<CPoint> result = new List<CPoint>();
        foreach (CPoint cp in list)
        {
            if (result.Count > 0 && (result[result.Count - 1].p - cp.p).sqrMagnitude < 1e-10f)
            {
                CPoint last = result[result.Count - 1];
                last.openingSeg = cp.openingSeg;
                if (last.n != cp.n) last.n = Vector2.zero;
                result[result.Count - 1] = last;
            }
            else result.Add(cp);
        }
        if (result.Count > 1 && (result[result.Count - 1].p - result[0].p).sqrMagnitude < 1e-10f)
        {
            CPoint first = result[0];
            if (first.n != result[result.Count - 1].n) first.n = Vector2.zero;
            result[0] = first;
            result.RemoveAt(result.Count - 1);
        }
        return result;
    }

    static List<Vector2> Positions(List<CPoint> ring)
    {
        List<Vector2> list = new List<Vector2>(ring.Count);
        foreach (CPoint cp in ring) list.Add(cp.p);
        return list;
    }

    // ---------------------------------------------------------------- Construction

    // Faces avant et arriere, tranches, et pour une niche son fond. Renvoie vrai si l'ouverture est percee.
    bool BuildMesh(Mesh mesh)
    {
        RRect wall = GetWallRect();
        float hz = Thickness * 0.5f;
        float zSign = niche && nicheOnBack ? -1f : 1f; // niche a l'arriere : tout le mur est retourne en Z
        int side = EdgeSide;

        warning = null;
        bool hasOpening = opening;
        RRect hole = default;
        if (hasOpening)
        {
            hole = GetOpeningRect(wall, side);
            if (!OpeningFits(wall, hole, side))
            {
                hasOpening = false;
                warning = side >= 0
                    ? "L'ouverture tombe dans un arrondi du mur (ou en deborde) : elle n'est pas percee. Deplacez-la ou reduisez les rayons des coins du mur."
                    : "L'ouverture deborde du mur ou d'un de ses arrondis : elle n'est pas percee. Deplacez-la, reduisez-la ou choisissez un bord a manger.";
            }
        }

        float zPlate = hz - Mathf.Clamp(nicheDepth, 0.005f, Thickness - 0.01f);
        bool isNiche = hasOpening && niche;

        List<CPoint> fullRing = BuildRing(wall);
        List<CPoint> frontRing = fullRing;   // contour exterieur de la face avant
        List<CPoint> backRing = fullRing;    // contour exterieur de la face arriere
        List<CPoint> holeRing = null;        // trou interieur (ouverture qui ne touche aucun bord)
        if (hasOpening)
        {
            if (side >= 0)
            {
                List<CPoint> notched = BuildNotchedRing(wall, hole, side);
                frontRing = notched;
                if (!isNiche) backRing = notched;
            }
            else holeRing = BuildHoleRing(hole);
        }

        MeshData data = new MeshData { zSign = zSign, wall = wall, thickness = Thickness };

        // Faces avant (+Z) et arriere (-Z)
        data.AddCap(frontRing, holeRing, hz, 1f, data.capTris);
        data.AddCap(backRing, isNiche ? null : holeRing, -hz, -1f, data.capTris);

        // Tranche exterieure (avec l'entaille d'une porte : ses segments vont dans l'interieur de l'ouverture)
        if (!hasOpening || side < 0 || !isNiche)
            data.AddBand(backRing, -hz, hz, false);
        else
        {
            // Niche qui mange un bord : contour plein derriere le fond de niche, contour entaille devant
            data.AddBand(fullRing, -hz, zPlate, false);
            data.AddBand(frontRing, zPlate, hz, false);
        }

        // Tranche du trou interieur
        if (holeRing != null)
            data.AddBand(holeRing, isNiche ? zPlate : -hz, hz, true);

        // Fond de niche, tourne vers l'avant
        if (isNiche)
            data.AddCap(BuildRing(hole), null, zPlate, 1f, data.openTris);

        data.Apply(mesh, separateMaterials, hasOpening);
        return hasOpening;
    }

    // Accumule sommets et triangles (positions en Z multipliees par zSign pour une niche a l'arriere).
    class MeshData
    {
        public float zSign;
        public RRect wall;
        public float thickness;
        public List<Vector3> verts = new List<Vector3>();
        public List<Vector3> normals = new List<Vector3>();
        public List<Vector2> uv0 = new List<Vector2>();
        public List<Vector2> uv2 = new List<Vector2>();
        public List<int> capTris = new List<int>();
        public List<int> outerTris = new List<int>();
        public List<int> openTris = new List<int>();

        void AddVertex(Vector2 p, float z, Vector3 n, Vector2 uvA, Vector2 uvB)
        {
            verts.Add(new Vector3(p.x, p.y, z * zSign));
            normals.Add(new Vector3(n.x, n.y, n.z * zSign));
            uv0.Add(uvA);
            uv2.Add(uvB);
        }

        // Face plate en z : contour (sens trigo) eventuellement perce d'un trou (sens horaire), triangulee par oreilles.
        public void AddCap(List<CPoint> outer, List<CPoint> hole, float z, float nz, List<int> tris)
        {
            List<Vector2> poly = Positions(outer);
            if (hole != null) poly = MergeHole(poly, Positions(hole));
            List<int> local = new List<int>();
            Triangulate(poly, local);

            Vector3 n = new Vector3(0f, 0f, nz);
            // u croit vers la droite de celui qui regarde la face : vers -X pour une face tournee vers +Z
            bool facesPlusZ = nz * zSign > 0f;
            float W = wall.maxX - wall.minX, H = wall.maxY - wall.minY;
            int start = verts.Count;
            foreach (Vector2 p in poly)
            {
                float u = facesPlusZ ? wall.maxX - p.x : p.x - wall.minX;
                float v = p.y - wall.minY;
                AddVertex(p, z, n, new Vector2(u, v), new Vector2(u / W, v / H));
            }
            Vector3 outward = new Vector3(0f, 0f, nz * zSign);
            for (int i = 0; i < local.Count; i += 3)
                AddTri(verts, tris, start + local[i], start + local[i + 1], start + local[i + 2], outward);
        }

        // Tranche : le contour extrude de z0 a z1. Normales lisses sur les arrondis, nettes aux coins vifs.
        public void AddBand(List<CPoint> ring, float z0, float z1, bool allOpening)
        {
            int n = ring.Count;
            float perimeter = 0f;
            for (int i = 0; i < n; i++) perimeter += Vector2.Distance(ring[i].p, ring[(i + 1) % n].p);
            perimeter = Mathf.Max(0.0001f, perimeter);

            float dist = 0f;
            for (int i = 0; i < n; i++)
            {
                CPoint a = ring[i], b = ring[(i + 1) % n];
                Vector2 d = b.p - a.p;
                float len = d.magnitude;
                if (len < 0.000001f) continue;
                Vector2 segN = new Vector2(d.y, -d.x) / len;
                Vector2 na = a.n.sqrMagnitude > 0f ? a.n : segN;
                Vector2 nb = b.n.sqrMagnitude > 0f ? b.n : segN;
                float v0 = z0 + thickness * 0.5f, v1 = z1 + thickness * 0.5f;

                int start = verts.Count;
                AddVertex(a.p, z0, na, new Vector2(dist, v0), new Vector2(dist / perimeter, v0 / thickness));
                AddVertex(b.p, z0, nb, new Vector2(dist + len, v0), new Vector2((dist + len) / perimeter, v0 / thickness));
                AddVertex(b.p, z1, nb, new Vector2(dist + len, v1), new Vector2((dist + len) / perimeter, v1 / thickness));
                AddVertex(a.p, z1, na, new Vector2(dist, v1), new Vector2(dist / perimeter, v1 / thickness));
                AddQuad(verts, allOpening || a.openingSeg ? openTris : outerTris, start, start + 1, start + 2, start + 3,
                    new Vector3(segN.x, segN.y, 0f));
                dist += len;
            }
        }

        public void Apply(Mesh mesh, bool separate, bool hasOpening)
        {
            mesh.Clear();
            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uv0);
            mesh.SetUVs(2, uv2);
            if (separate)
            {
                mesh.subMeshCount = hasOpening ? 3 : 2;
                mesh.SetTriangles(capTris, 0);
                mesh.SetTriangles(outerTris, 1);
                if (hasOpening) mesh.SetTriangles(openTris, 2);
            }
            else
            {
                List<int> all = new List<int>(capTris);
                all.AddRange(outerTris);
                all.AddRange(openTris);
                mesh.subMeshCount = 1;
                mesh.SetTriangles(all, 0);
            }
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
        }
    }

    // ---------------------------------------------------------------- Triangulation

    static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

    // Relie le trou (sens horaire) au contour (sens trigo) par un pont : un seul polygone, triangulable par oreilles.
    // Pont = du point le plus a droite du trou vers le point du contour le plus proche qui le voit sans rien croiser.
    static List<Vector2> MergeHole(List<Vector2> outer, List<Vector2> hole)
    {
        int hi = 0;
        for (int i = 1; i < hole.Count; i++)
            if (hole[i].x > hole[hi].x) hi = i;
        Vector2 h = hole[hi];

        List<int> order = new List<int>();
        for (int j = 0; j < outer.Count; j++) order.Add(j);
        order.Sort((a, b) => (outer[a] - h).sqrMagnitude.CompareTo((outer[b] - h).sqrMagnitude));

        int oj = order[0];
        foreach (int j in order)
        {
            if (!SegmentClear(h, outer[j], outer) || !SegmentClear(h, outer[j], hole)) continue;
            if (PointInPolygon((h + outer[j]) * 0.5f, hole)) continue;
            oj = j;
            break;
        }

        List<Vector2> merged = new List<Vector2>(outer.Count + hole.Count + 2);
        for (int j = 0; j <= oj; j++) merged.Add(outer[j]);
        for (int i = 0; i <= hole.Count; i++) merged.Add(hole[(hi + i) % hole.Count]);
        for (int j = oj; j < outer.Count; j++) merged.Add(outer[j]);
        return merged;
    }

    static bool SegmentClear(Vector2 a, Vector2 b, List<Vector2> loop)
    {
        for (int i = 0; i < loop.Count; i++)
        {
            Vector2 p = loop[i], q = loop[(i + 1) % loop.Count];
            if (Same(p, a) || Same(p, b) || Same(q, a) || Same(q, b)) continue;
            float d1 = Cross(q - p, a - p), d2 = Cross(q - p, b - p);
            float d3 = Cross(b - a, p - a), d4 = Cross(b - a, q - a);
            if (((d1 > 0f && d2 < 0f) || (d1 < 0f && d2 > 0f)) && ((d3 > 0f && d4 < 0f) || (d3 < 0f && d4 > 0f)))
                return false;
        }
        return true;
    }

    static bool Same(Vector2 a, Vector2 b) => (a - b).sqrMagnitude < 1e-12f;

    static bool PointInPolygon(Vector2 p, List<Vector2> poly)
    {
        bool inside = false;
        for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
        {
            if ((poly[i].y > p.y) != (poly[j].y > p.y) &&
                p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x)
                inside = !inside;
        }
        return inside;
    }

    // Triangulation par oreilles d'un polygone simple en sens trigo (les points doubles du pont sont toleres).
    static void Triangulate(List<Vector2> poly, List<int> result)
    {
        List<int> idx = new List<int>(poly.Count);
        for (int i = 0; i < poly.Count; i++) idx.Add(i);

        int guard = poly.Count * poly.Count + 10;
        while (idx.Count > 3 && guard-- > 0)
        {
            bool clipped = false;
            for (int k = 0; k < idx.Count; k++)
            {
                int ia = idx[(k + idx.Count - 1) % idx.Count], ib = idx[k], ic = idx[(k + 1) % idx.Count];
                Vector2 a = poly[ia], b = poly[ib], c = poly[ic];
                float cross = Cross(b - a, c - b);
                if (Mathf.Abs(cross) < 1e-10f)
                {
                    // Point aligne (ou pointe d'aire nulle) : on le retire sans triangle
                    idx.RemoveAt(k);
                    clipped = true;
                    break;
                }
                if (cross < 0f) continue; // angle rentrant

                bool blocked = false;
                foreach (int j in idx)
                {
                    if (j == ia || j == ib || j == ic) continue;
                    Vector2 p = poly[j];
                    if (Same(p, a) || Same(p, b) || Same(p, c)) continue;
                    if (Cross(b - a, p - a) >= -1e-9f && Cross(c - b, p - b) >= -1e-9f && Cross(a - c, p - c) >= -1e-9f)
                    {
                        blocked = true;
                        break;
                    }
                }
                if (blocked) continue;

                result.Add(ia); result.Add(ib); result.Add(ic);
                idx.RemoveAt(k);
                clipped = true;
                break;
            }
            if (!clipped) break; // polygone degenere : on garde ce qui est triangule
        }
        if (idx.Count == 3 && Mathf.Abs(Cross(poly[idx[1]] - poly[idx[0]], poly[idx[2]] - poly[idx[1]])) >= 1e-10f)
        {
            result.Add(idx[0]); result.Add(idx[1]); result.Add(idx[2]);
        }
    }

    // Unity : face visible quand Cross(b - a, c - a) pointe vers l'exterieur. On oriente d'apres la normale voulue.
    static void AddTri(List<Vector3> verts, List<int> tris, int a, int b, int c, Vector3 outward)
    {
        Vector3 cross = Vector3.Cross(verts[b] - verts[a], verts[c] - verts[a]);
        if (Vector3.Dot(cross, outward) < 0f) { int t = b; b = c; c = t; }
        tris.Add(a); tris.Add(b); tris.Add(c);
    }

    static void AddQuad(List<Vector3> verts, List<int> tris, int a, int b, int c, int d, Vector3 outward)
    {
        Vector3 cross = Vector3.Cross(verts[b] - verts[a], verts[c] - verts[a]);
        if (cross.sqrMagnitude < 1e-12f)
            cross = Vector3.Cross(verts[c] - verts[a], verts[d] - verts[a]);
        if (Vector3.Dot(cross, outward) < 0f)
        {
            tris.Add(a); tris.Add(c); tris.Add(b);
            tris.Add(a); tris.Add(d); tris.Add(c);
        }
        else
        {
            tris.Add(a); tris.Add(b); tris.Add(c);
            tris.Add(a); tris.Add(c); tris.Add(d);
        }
    }

#if UNITY_EDITOR
    // Lettres A-D sur les coins du mur et de l'ouverture (si leurs rayons ne sont pas lies), sur la face avant.
    void OnDrawGizmosSelected()
    {
        RRect wall = GetWallRect();
        float z = Thickness * 0.5f * (niche && nicheOnBack ? -1f : 1f);
        string[] names = { "A", "B", "C", "D" };

        if (!linkCorners)
            for (int k = 0; k < 4; k++)
                Label(CornerPoint(wall, k), z, names[k]);

        if (opening && !linkOpeningCorners)
        {
            RRect hole = GetOpeningRect(wall, EdgeSide);
            Vector2 center = new Vector2((hole.minX + hole.maxX) * 0.5f, (hole.minY + hole.maxY) * 0.5f);
            for (int k = 0; k < 4; k++)
            {
                // Un peu vers l'interieur de l'ouverture, pour ne pas chevaucher les lettres du mur
                Vector2 p = Vector2.Lerp(CornerPoint(hole, k), center, 0.15f);
                Label(p, z, names[k]);
            }
        }
    }

    void Label(Vector2 p, float z, string text)
    {
        Vector3 world = transform.TransformPoint(new Vector3(p.x, p.y, z));
        UnityEditor.Handles.Label(world, text, UnityEditor.EditorStyles.boldLabel);
    }
#endif
}

#if UNITY_EDITOR
// Inspecteur : n'affiche que les reglages utiles, et previent quand l'ouverture ne peut pas etre percee.
[UnityEditor.CustomEditor(typeof(PerforatedWall))]
[UnityEditor.CanEditMultipleObjects]
public class PerforatedWallEditor : UnityEditor.Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        PerforatedWall wall = (PerforatedWall)target;
        List<string> hidden = new List<string>();

        if (wall.linkCorners)
            hidden.AddRange(new[] { "radiusA", "radiusB", "radiusC", "radiusD" });
        else
            hidden.Add("radius");

        if (!wall.opening)
            hidden.AddRange(new[] { "edge", "openingX", "openingY", "openingWidth", "openingHeight", "linkOpeningCorners",
                                    "openingRadius", "openingRadiusA", "openingRadiusB", "openingRadiusC", "openingRadiusD",
                                    "niche", "nicheDepth", "nicheOnBack" });
        else
        {
            if (wall.edge == PerforatedWall.OpeningEdge.Gauche || wall.edge == PerforatedWall.OpeningEdge.Droite)
                hidden.Add("openingX");
            if (wall.edge == PerforatedWall.OpeningEdge.Bas || wall.edge == PerforatedWall.OpeningEdge.Haut)
                hidden.Add("openingY");
            if (wall.linkOpeningCorners)
                hidden.AddRange(new[] { "openingRadiusA", "openingRadiusB", "openingRadiusC", "openingRadiusD" });
            else
                hidden.Add("openingRadius");
            if (!wall.niche)
                hidden.AddRange(new[] { "nicheDepth", "nicheOnBack" });
        }

        DrawPropertiesExcluding(serializedObject, hidden.ToArray());
        serializedObject.ApplyModifiedProperties();

        if (!string.IsNullOrEmpty(wall.warning))
            UnityEditor.EditorGUILayout.HelpBox(wall.warning, UnityEditor.MessageType.Warning);
    }
}
#endif
