using System.Collections.Generic;
using UnityEngine;

// Mur courbe parametrique : une section (epaisseur x hauteur, coins arrondis) qu'on fait tourner autour de l'axe Y
// du GameObject, sur un angle donne (quart de tour, demi-tour, anneau complet...).
// Usage : poser sur un Cube (ou tout GO avec MeshFilter), regler, le mesh se met a jour en direct.
// Pivot = centre du cercle, au sol : des murs poses au meme endroit avec des rayons differents sont concentriques,
// et deux quarts de tour se raccordent en tournant l'un de 90 degres. L'arc part de +X et tourne vers +Z.
// Comme RoundedBlock, le mesh n'est pas sauvegarde : il est reconstruit au chargement (compatible prefab).
//
// UV0 : en metres. Pourtour : u = longueur d'arc (a la distance du centre de chaque point), v = distance parcourue
//       sur le contour de la section depuis le milieu du dessous. Bouts : projection dans le plan de la section.
// UV2 (canal TEXCOORD2, "UV2" dans Shader Graph) : 0-1 sur le pourtour (u = angle, v = contour) et sur chaque bout.
[ExecuteAlways]
[RequireComponent(typeof(MeshFilter))]
public class CurvedWall : MonoBehaviour
{
    [Header("Arc")]
    [Tooltip("Angle couvert, en degres. 360 = anneau ferme (pas de bouts).")]
    [Range(1f, 360f)] public float angle = 90f;
    [Tooltip("Distance entre le centre du cercle (pivot) et la face interieure du mur.")]
    [Min(0f)] public float innerRadius = 3f;
    [Tooltip("Nombre de facettes par quart de tour.")]
    [Range(1, 64)] public int segmentsPer90 = 12;

    [Header("Section (metres)")]
    [Min(0.01f)] public float thickness = 0.3f;
    [Min(0.01f)] public float height = 2f;

    [Header("Arrondis de la section")]
    [Tooltip("Un seul rayon pour les 4 coins de la section.")]
    public bool linkCorners = true;
    [Min(0f)] public float radius = 0f;
    [Tooltip("Rayons par coin (si Coins lies est decoche) : A = bas interieur, B = bas exterieur, C = haut exterieur, " +
             "D = haut interieur. Les lettres s'affichent sur le bout de depart quand l'objet est selectionne.")]
    [Min(0f)] public float radiusA = 0f;
    [Min(0f)] public float radiusB = 0f;
    [Min(0f)] public float radiusC = 0f;
    [Min(0f)] public float radiusD = 0f;
    [Tooltip("Nombre de segments par quart de rond des coins de la section.")]
    [Range(1, 32)] public int cornerSegments = 4;

    public enum EndShape { Biseau, Arrondi }
    public enum EndSides { LesDeux, Depart, Arrivee }

    [Header("Aretes des bouts")]
    [Tooltip("Biseau / arrondi de l'arete haute du bout (D-C), entre la face du bout et le dessus du mur. " +
             "Taille en metres, mesuree au milieu de l'epaisseur. 0 = coupe franche.")]
    [Min(0f)] public float endTop = 0f;
    [Tooltip("Biseau / arrondi de l'arete basse du bout (A-B), entre la face du bout et le dessous du mur. 0 = coupe franche.")]
    [Min(0f)] public float endBottom = 0f;
    [Tooltip("Forme des deux aretes : pan coupe a 45 degres ou quart de rond (segments = ceux des coins de la section).")]
    public EndShape endShape = EndShape.Biseau;
    [Tooltip("Bouts concernes. Le depart est sur l'axe +X (lettres A-D).")]
    public EndSides endSides = EndSides.LesDeux;

    [Header("Materiaux")]
    [Tooltip("Decoche : un seul materiau. Coche : un materiau par partie sur le MeshRenderer, " +
             "element 0 = faces interieure et exterieure, 1 = dessus, dessous, arrondis et bouts.")]
    public bool separateMaterials = false;

    [System.NonSerialized] Mesh generatedMesh;

    struct RingPoint
    {
        public Vector2 p; // position dans la section (u = radial vers l'exterieur, v = hauteur), centree
        public Vector2 n; // normale dans la section
        public RingPoint(Vector2 p, Vector2 n) { this.p = p; this.n = n; }
    }

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

        // Apres un rechargement du code, la reference est perdue mais le mesh existe encore (DontSave : jamais nettoye
        // par Unity) : on reprend le sien, reconnu a son nom, au lieu d en creer un de plus a chaque rechargement.
        string meshName = "CurvedWall_" + GetInstanceID();
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

    // Autant de materiaux que de sous-meshes (meme logique que RoundedBlock).
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

    // ---------------------------------------------------------------- Section

    void GetSection(out float ht, out float hh, out float[] radii)
    {
        ht = Mathf.Max(0.01f, thickness) * 0.5f;
        hh = Mathf.Max(0.01f, height) * 0.5f;
        float max = Mathf.Min(ht, hh);
        radii = linkCorners
            ? new[] { radius, radius, radius, radius }
            : new[] { radiusA, radiusB, radiusC, radiusD };
        for (int k = 0; k < 4; k++) radii[k] = Mathf.Clamp(radii[k], 0f, max);
    }

    // Coins A (bas interieur), B (bas exterieur), C (haut exterieur), D (haut interieur), dans la section centree.
    static Vector2 Corner(int k, float ht, float hh)
    {
        switch (k)
        {
            case 0: return new Vector2(-ht, -hh);
            case 1: return new Vector2(ht, -hh);
            case 2: return new Vector2(ht, hh);
            default: return new Vector2(-ht, hh);
        }
    }

    // Contour ferme de la section, depart au milieu du dessous (couture des UV cachee au sol), puis coins B, C, D, A.
    // Le dernier point = le premier (pour fermer les UV). Chaque coin a toujours cornerSegments + 1 points, meme vif
    // (rayon 0 : points superposes, normales en eventail), comme dans RoundedBlock : les faces restent nettes.
    // Avec N = cornerSegments : face exterieure = segment N+1 -> N+2, face interieure = segment 3N+3 -> 3N+4.
    List<RingPoint> BuildSection(float ht, float hh, float[] radii)
    {
        Vector2[] signs = { new Vector2(-1, -1), new Vector2(1, -1), new Vector2(1, 1), new Vector2(-1, 1) };
        float[] startAngles = { 180f, 270f, 0f, 90f };
        int[] order = { 1, 2, 3, 0 };

        List<RingPoint> ring = new List<RingPoint>();
        ring.Add(new RingPoint(new Vector2(0f, -hh), Vector2.down));
        foreach (int k in order)
        {
            float r = radii[k];
            Vector2 center = Corner(k, ht, hh) - signs[k] * r;
            for (int s = 0; s <= cornerSegments; s++)
            {
                float a = (startAngles[k] + 90f * s / cornerSegments) * Mathf.Deg2Rad;
                Vector2 n = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                ring.Add(new RingPoint(center + n * r, n));
            }
        }
        ring.Add(ring[0]);
        return ring;
    }

    // ---------------------------------------------------------------- Stations le long de l'arc

    // Une copie de la section a l'angle theta. Pres des bouts, le haut de la section est abaisse de lowTop et le bas
    // remonte de lowBot (aretes des bouts) ; (cos, sin) = bascule de la normale du dessus / dessous vers l'exterieur
    // du bout (outSign : -1 au depart, +1 a l'arrivee, 0 ailleurs).
    struct ArcStation
    {
        public float theta, lowTop, lowBot, topCos, topSin, botCos, botSin, outSign;
    }

    // Points du haut de la section : coins C et D et le dessus (indices N+2 a 3N+3, cf. BuildSection).
    bool IsTopPoint(int i)
    {
        int n = cornerSegments;
        return i >= n + 2 && i <= 3 * n + 3;
    }

    static Vector2 ShiftedPoint(Vector2 p, bool top, ArcStation st)
    {
        return new Vector2(p.x, top ? p.y - st.lowTop : p.y + st.lowBot);
    }

    // Une arete de bout de taille h, a la distance t du bout : abaissement et bascule de la normale.
    // pastKink : juste apres la fin du biseau (la normale redevient verticale, arete nette).
    static void EvalEdge(float h, EndShape shape, float t, bool pastKink, out float low, out float c, out float s)
    {
        if (h <= 0f || t > h + 1e-5f || (pastKink && t >= h - 1e-5f))
        {
            low = 0f; c = 1f; s = 0f;
            return;
        }
        if (shape == EndShape.Biseau)
        {
            low = h - t;
            c = s = 0.70710678f;
        }
        else
        {
            // Quart de rond de rayon h : au bout (t = 0) la normale est horizontale, a t = h elle est verticale
            s = Mathf.Clamp01(1f - t / h);
            c = Mathf.Sqrt(1f - s * s);
            low = h * (1f - c);
        }
    }

    // Distances au bout ou poser une station pour une arete de taille h
    void AddEdgeSamples(List<float> ts, float h)
    {
        if (h <= 0f) return;
        if (endShape == EndShape.Biseau)
        {
            ts.Add(0f);
            ts.Add(h);
            return;
        }
        for (int s = 0; s <= cornerSegments; s++)
        {
            float phi = (90f - 90f * s / cornerSegments) * Mathf.Deg2Rad;
            ts.Add(h * (1f - Mathf.Sin(phi)));
        }
    }

    // Stations d'une zone de bout, de t = 0 (le bout) a t = taille de la zone. Sur la fin d'un biseau, deux stations
    // au meme endroit (normale inclinee puis verticale) pour garder l'arete nette.
    List<ArcStation> BuildEndZone(float hTop, float hBot)
    {
        List<float> ts = new List<float>();
        AddEdgeSamples(ts, hTop);
        AddEdgeSamples(ts, hBot);
        ts.Sort();

        List<ArcStation> zone = new List<ArcStation>();
        float last = -1f;
        foreach (float t in ts)
        {
            if (t - last < 1e-4f) continue;
            last = t;
            bool kink = endShape == EndShape.Biseau &&
                        ((hTop > 0f && Mathf.Abs(t - hTop) < 1e-4f) || (hBot > 0f && Mathf.Abs(t - hBot) < 1e-4f));
            zone.Add(EvalStation(t, hTop, hBot, false));
            if (kink) zone.Add(EvalStation(t, hTop, hBot, true));
        }
        return zone;
    }

    ArcStation EvalStation(float t, float hTop, float hBot, bool pastKink)
    {
        ArcStation st = new ArcStation { theta = t }; // theta = distance au bout, convertie en angle par l'appelant
        EvalEdge(hTop, endShape, t, pastKink, out st.lowTop, out st.topCos, out st.topSin);
        EvalEdge(hBot, endShape, t, pastKink, out st.lowBot, out st.botCos, out st.botSin);
        return st;
    }

    static ArcStation FlatStation(float theta)
    {
        return new ArcStation { theta = theta, topCos = 1f, botCos = 1f };
    }

    // Zone du bout de depart, corps du mur a pas reguliers, zone du bout d'arrivee. Les distances aux bouts sont
    // mesurees sur le rayon du milieu de la section (meme angle pour toute l'epaisseur).
    List<ArcStation> BuildArcStations(float arcRad, bool closed, float rc, float[] radii, float hh)
    {
        bool startEnd = !closed && endSides != EndSides.Arrivee;
        bool endEnd = !closed && endSides != EndSides.Depart;

        // Tailles bornees : le haut abaisse et le bas remonte ne doivent pas se croiser sur les flancs,
        // et les zones des bouts ne doivent pas occuper plus de 90 % de l'arc.
        float hTop = Mathf.Max(0f, endTop), hBot = Mathf.Max(0f, endBottom);
        float maxSum = Mathf.Max(0f, Mathf.Min(hh * 2f - radii[1] - radii[2], hh * 2f - radii[0] - radii[3]) - 0.01f);
        if (hTop + hBot > maxSum)
        {
            float k = maxSum / (hTop + hBot);
            hTop *= k;
            hBot *= k;
        }
        int ends = (startEnd ? 1 : 0) + (endEnd ? 1 : 0);
        float zoneLength = Mathf.Max(hTop, hBot);
        float maxZone = ends > 0 ? arcRad * rc * 0.9f / ends : 0f;
        if (zoneLength > maxZone && zoneLength > 0f)
        {
            float k = maxZone / zoneLength;
            hTop *= k;
            hBot *= k;
            zoneLength = maxZone;
        }
        bool hasEdges = zoneLength > 0.0001f;
        startEnd &= hasEdges;
        endEnd &= hasEdges;

        List<ArcStation> zone = hasEdges ? BuildEndZone(hTop, hBot) : null;
        float zoneAngle = zoneLength / rc;
        float bodyStart = startEnd ? zoneAngle : 0f;
        float bodyEnd = endEnd ? arcRad - zoneAngle : arcRad;

        List<ArcStation> stations = new List<ArcStation>();
        if (startEnd)
        {
            foreach (ArcStation z in zone)
            {
                ArcStation st = z;
                st.theta = z.theta / rc;
                st.outSign = -1f;
                stations.Add(st);
            }
        }
        else
            stations.Add(FlatStation(0f));

        int steps = Mathf.Max(1, Mathf.CeilToInt(segmentsPer90 * (bodyEnd - bodyStart) * Mathf.Rad2Deg / 90f));
        for (int j = 1; j < steps; j++)
            stations.Add(FlatStation(Mathf.Lerp(bodyStart, bodyEnd, (float)j / steps)));

        if (endEnd)
        {
            for (int z = zone.Count - 1; z >= 0; z--)
            {
                ArcStation st = zone[z];
                st.theta = arcRad - zone[z].theta / rc;
                st.outSign = 1f;
                stations.Add(st);
            }
        }
        else
            stations.Add(FlatStation(arcRad));

        return stations;
    }

    // ---------------------------------------------------------------- Construction

    void BuildMesh(Mesh mesh)
    {
        GetSection(out float ht, out float hh, out float[] radii);
        float rc = Mathf.Max(0f, innerRadius) + ht; // rayon du milieu de la section
        List<RingPoint> ring = BuildSection(ht, hh, radii);
        int ringCount = ring.Count;
        int n = cornerSegments;
        int outerFace = n + 1, innerFace = 3 * n + 3;

        float[] dist = new float[ringCount];
        for (int i = 1; i < ringCount; i++)
            dist[i] = dist[i - 1] + Vector2.Distance(ring[i - 1].p, ring[i].p);
        float perimeter = Mathf.Max(0.0001f, dist[ringCount - 1]);

        float arc = Mathf.Clamp(angle, 1f, 360f);
        bool closed = arc >= 359.999f;
        float arcRad = arc * Mathf.Deg2Rad;
        List<ArcStation> stations = BuildArcStations(arcRad, closed, rc, radii, hh);
        int stationCount = stations.Count;

        List<Vector3> verts = new List<Vector3>();
        List<Vector3> normals = new List<Vector3>();
        List<Vector2> uv0 = new List<Vector2>();
        List<Vector2> uv2 = new List<Vector2>();
        List<int> faceTris = new List<int>();
        List<int> edgeTris = new List<int>();

        // --- Pourtour : une copie de la section par station, reliees point a point
        foreach (ArcStation st in stations)
        {
            Vector3 radial = new Vector3(Mathf.Cos(st.theta), 0f, Mathf.Sin(st.theta));
            Vector3 tangent = new Vector3(-Mathf.Sin(st.theta), 0f, Mathf.Cos(st.theta));
            for (int i = 0; i < ringCount; i++)
            {
                bool top = IsTopPoint(i);
                Vector2 p = ShiftedPoint(ring[i].p, top, st);
                Vector2 n2 = ring[i].n;
                float r = rc + p.x;
                verts.Add(radial * r + Vector3.up * (p.y + hh));

                // Pres des bouts, la composante verticale de la normale (dessus pour le haut, dessous pour le bas)
                // bascule vers l'exterieur du bout, selon la pente du biseau / de l'arrondi.
                Vector3 normal = radial * n2.x + Vector3.up * n2.y;
                if (top ? n2.y > 0f : n2.y < 0f)
                {
                    float c = top ? st.topCos : st.botCos;
                    float s = top ? st.topSin : st.botSin;
                    normal = radial * n2.x + Vector3.up * (n2.y * c) + tangent * (st.outSign * Mathf.Abs(n2.y) * s);
                }
                normals.Add(normal.normalized);
                uv0.Add(new Vector2(st.theta * r, dist[i]));
                uv2.Add(new Vector2(st.theta / arcRad, dist[i] / perimeter));
            }
        }
        for (int j = 0; j < stationCount - 1; j++)
        {
            for (int i = 0; i < ringCount - 1; i++)
            {
                int a = j * ringCount + i;
                int b = a + 1;
                int c = b + ringCount;
                int d = a + ringCount;
                Vector3 outward = (normals[a] + normals[b] + normals[c] + normals[d]).normalized;
                AddQuad(verts, i == outerFace || i == innerFace ? faceTris : edgeTris, a, b, c, d, outward);
            }
        }

        // --- Bouts (si l'arc n'est pas ferme) : eventail depuis le centre de la section (convexe),
        // section de la premiere / derniere station (abaissee par les aretes des bouts).
        if (!closed)
        {
            int count = ringCount - 1; // le dernier point double le premier
            for (int side = 0; side < 2; side++)
            {
                ArcStation st = side == 0 ? stations[0] : stations[stationCount - 1];
                float theta = st.theta;
                Vector3 radial = new Vector3(Mathf.Cos(theta), 0f, Mathf.Sin(theta));
                Vector3 tangent = new Vector3(-Mathf.Sin(theta), 0f, Mathf.Cos(theta));
                Vector3 capNormal = side == 0 ? -tangent : tangent;
                // Vu de face, le radial va vers la droite au bout de depart et vers la gauche au bout d'arrivee
                float mirror = side == 0 ? 1f : -1f;
                int capStart = verts.Count;

                Vector2 center = new Vector2(0f, (st.lowBot - st.lowTop) * 0.5f);
                AddCapVertex(verts, normals, uv0, uv2, center, radial, rc, ht, hh, mirror, capNormal);
                for (int i = 0; i < count; i++)
                    AddCapVertex(verts, normals, uv0, uv2, ShiftedPoint(ring[i].p, IsTopPoint(i), st),
                        radial, rc, ht, hh, mirror, capNormal);
                // Les points superposes des coins vifs donnent des triangles degeneres sans consequence.
                for (int i = 0; i < count; i++)
                    AddTri(verts, edgeTris, capStart, capStart + 1 + i, capStart + 1 + (i + 1) % count, capNormal);
            }
        }

        mesh.Clear();
        mesh.SetVertices(verts);
        mesh.SetNormals(normals);
        mesh.SetUVs(0, uv0);
        mesh.SetUVs(2, uv2);
        if (separateMaterials)
        {
            mesh.subMeshCount = 2;
            mesh.SetTriangles(faceTris, 0);
            mesh.SetTriangles(edgeTris, 1);
        }
        else
        {
            faceTris.AddRange(edgeTris);
            mesh.subMeshCount = 1;
            mesh.SetTriangles(faceTris, 0);
        }
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();
    }

    static void AddCapVertex(List<Vector3> verts, List<Vector3> normals, List<Vector2> uv0, List<Vector2> uv2,
        Vector2 p, Vector3 radial, float rc, float ht, float hh, float mirror, Vector3 n)
    {
        verts.Add(radial * (rc + p.x) + Vector3.up * (p.y + hh));
        normals.Add(n);
        float uu = mirror > 0f ? p.x + ht : ht - p.x;
        uv0.Add(new Vector2(uu, p.y + hh));
        uv2.Add(new Vector2(uu / (ht * 2f), (p.y + hh) / (hh * 2f)));
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
        // Orientation decidee une fois pour le quad, pour que ses deux triangles restent coherents.
        // Le premier triangle peut etre degenere (points superposes des coins vifs) : on prend alors l'autre.
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
    // Lettres A-D sur les coins de la section (si non lies), sur le bout de depart (axe +X).
    void OnDrawGizmosSelected()
    {
        if (linkCorners) return;
        GetSection(out float ht, out float hh, out float[] radii);
        float rc = Mathf.Max(0f, innerRadius) + ht;
        string[] names = { "A", "B", "C", "D" };
        for (int k = 0; k < 4; k++)
        {
            Vector2 c = Corner(k, ht, hh);
            Vector3 world = transform.TransformPoint(new Vector3(rc + c.x, c.y + hh, 0f));
            UnityEditor.Handles.Label(world, names[k], UnityEditor.EditorStyles.boldLabel);
        }
    }
#endif
}

#if UNITY_EDITOR
// Inspecteur : boutons d'angle rapides, rayons par coin seulement si non lies.
[UnityEditor.CustomEditor(typeof(CurvedWall))]
[UnityEditor.CanEditMultipleObjects]
public class CurvedWallEditor : UnityEditor.Editor
{
    static readonly float[] QuickAngles = { 90f, 180f, 270f, 360f };

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        CurvedWall wall = (CurvedWall)target;

        UnityEditor.SerializedProperty angleProp = serializedObject.FindProperty("angle");
        GUILayout.BeginHorizontal();
        GUILayout.Label("Angle rapide", GUILayout.Width(UnityEditor.EditorGUIUtility.labelWidth - 4f));
        foreach (float a in QuickAngles)
            if (GUILayout.Button(a + "°"))
                angleProp.floatValue = a;
        GUILayout.EndHorizontal();

        List<string> hidden = new List<string>();
        if (wall.linkCorners)
            hidden.AddRange(new[] { "radiusA", "radiusB", "radiusC", "radiusD" });
        else
            hidden.Add("radius");

        if (wall.angle >= 359.999f)
            hidden.AddRange(new[] { "endTop", "endBottom", "endShape", "endSides" });
        else if (wall.endTop <= 0f && wall.endBottom <= 0f)
            hidden.AddRange(new[] { "endShape", "endSides" });

        DrawPropertiesExcluding(serializedObject, hidden.ToArray());
        serializedObject.ApplyModifiedProperties();
    }
}
#endif
