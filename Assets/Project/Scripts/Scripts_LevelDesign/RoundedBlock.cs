using System.Collections.Generic;
using UnityEngine;

// Bloc de decor parametrique : un rectangle aux coins arrondis (profil), extrude.
// Usage : poser sur un Cube (ou tout GO avec MeshFilter), regler, le mesh se met a jour en direct.
// Le mesh n'est pas sauvegarde : il est reconstruit a partir des reglages a chaque chargement,
// donc le composant marche tel quel dans un prefab.
//
// UV0 : en metres (densite constante quelle que soit la taille), le Tiling du materiau regle la repetition.
//       Faces plates : projection dans le plan de la face. Tour : u = distance le long du contour, v = epaisseur.
// UV2 (canal TEXCOORD2, "UV2" dans Shader Graph) : 0-1 sur chaque face, pour degrades / lisere dans un shader maison.
[ExecuteAlways]
[RequireComponent(typeof(MeshFilter))]
public class RoundedBlock : MonoBehaviour
{
    public enum ProfileFace
    {
        Dessus, // profil vu de dessus (plan XZ), extrude en hauteur
        Avant,  // profil vu de face (plan XY), extrude en profondeur (Z)
        Cote    // profil vu de cote (plan ZY), extrude en largeur (X)
    }

    [Header("Taille (metres)")]
    [Tooltip("X = largeur, Y = hauteur, Z = profondeur. Pivot au centre de la base.")]
    public Vector3 size = new Vector3(2f, 1f, 2f);

    [Header("Profil")]
    [Tooltip("Face qui porte les coins arrondis. Dessus = pave a face arrondie ; Avant/Cote = aretes en quart de rond sur la longueur.")]
    public ProfileFace profileFace = ProfileFace.Dessus;

    [Tooltip("Un seul rayon pour les 4 coins.")]
    public bool linkCorners = true;
    [Min(0f)] public float radius = 0.5f;

    [Tooltip("Rayons par coin (si Coins lies est decoche). Les lettres A-D s'affichent dans la scene quand l'objet est selectionne.")]
    [Min(0f)] public float radiusA = 0.5f;
    [Min(0f)] public float radiusB = 0.5f;
    [Min(0f)] public float radiusC = 0.5f;
    [Min(0f)] public float radiusD = 0.5f;

    [Tooltip("Nombre de segments par quart de rond (coins du profil et arrondis du pourtour).")]
    [Range(1, 32)] public int cornerSegments = 4;

    [Header("Arrondi du pourtour")]
    [Tooltip("Quart de rond sur tout le bord de la face 'haute' du profil : dessus (Dessus), avant +Z (Avant), droite +X (Cote).")]
    [Min(0f)] public float roundTop = 0f;
    [Tooltip("Quart de rond sur tout le bord de la face opposee : dessous (Dessus), arriere -Z (Avant), gauche -X (Cote).")]
    [Min(0f)] public float roundBottom = 0f;

    [Header("Materiaux")]
    [Tooltip("Decoche : un seul materiau pour tout le bloc. Coche : 2 materiaux sur le MeshRenderer, " +
             "element 0 = faces (dessus / dessous du profil), element 1 = tranche (le tour, ex. guirlande lumineuse).")]
    public bool separateMaterials = false;

    [System.NonSerialized] Mesh generatedMesh;

    struct RingPoint
    {
        public Vector2 p; // position dans le plan du profil (u, v)
        public Vector2 n; // normale dans ce plan
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

        if (generatedMesh == null)
        {
            generatedMesh = new Mesh();
            generatedMesh.name = "RoundedBlock";
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

    // Autant de materiaux que de sous-meshes : un materiau en trop serait redessine par-dessus le dernier
    // sous-mesh, un materiau manquant laisserait la tranche invisible (on recopie alors celui des faces).
    void MatchMaterialCount()
    {
        MeshRenderer mr = GetComponent<MeshRenderer>();
        if (mr == null) return;
        Material[] mats = mr.sharedMaterials;
        int wanted = separateMaterials ? 2 : 1;
        if (mats.Length == wanted || mats.Length == 0) return;

        Material[] fixedMats = new Material[wanted];
        for (int i = 0; i < wanted; i++)
            fixedMats[i] = i < mats.Length ? mats[i] : mats[0];
        mr.sharedMaterials = fixedMats;
    }

    // ---------------------------------------------------------------- Construction

    // Demi-tailles dans le repere du profil : hu, hv = plan du profil, hw = extrusion
    void GetExtents(out float hu, out float hv, out float hw)
    {
        Vector3 s = new Vector3(Mathf.Max(0.01f, size.x), Mathf.Max(0.01f, size.y), Mathf.Max(0.01f, size.z));
        switch (profileFace)
        {
            case ProfileFace.Avant: hu = s.x * 0.5f; hv = s.y * 0.5f; hw = s.z * 0.5f; break;
            case ProfileFace.Cote: hu = s.z * 0.5f; hv = s.y * 0.5f; hw = s.x * 0.5f; break;
            default: hu = s.x * 0.5f; hv = s.z * 0.5f; hw = s.y * 0.5f; break;
        }
    }

    // (u, v, w) du profil -> position locale, pivot au centre de la base
    Vector3 ToLocal(float u, float v, float w, float hv, float hw)
    {
        switch (profileFace)
        {
            case ProfileFace.Avant: return new Vector3(u, v + hv, w);
            case ProfileFace.Cote: return new Vector3(w, v + hv, u);
            default: return new Vector3(u, w + hw, v);
        }
    }

    Vector3 DirToLocal(float u, float v, float w)
    {
        switch (profileFace)
        {
            case ProfileFace.Avant: return new Vector3(u, v, w);
            case ProfileFace.Cote: return new Vector3(w, v, u);
            default: return new Vector3(u, w, v);
        }
    }

    float[] GetRadii(float hu, float hv)
    {
        float max = Mathf.Min(hu, hv);
        float[] r = linkCorners
            ? new[] { radius, radius, radius, radius }
            : new[] { radiusA, radiusB, radiusC, radiusD };
        for (int i = 0; i < 4; i++) r[i] = Mathf.Clamp(r[i], 0f, max);
        return r;
    }

    // Contour ferme dans le sens trigo, retreci de 'inset' (un rectangle arrondi retreci reste un rectangle arrondi :
    // demi-tailles - inset, rayons - inset). Depart au milieu du cote gauche (couture des UV du tour),
    // puis coins A (-u,-v), B (+u,-v), C (+u,+v), D (-u,+v). Le dernier point = le premier (pour fermer les UV).
    // Chaque coin a toujours cornerSegments + 1 points, meme vif (rayon 0 : points superposes, normales en eventail) :
    // tous les contours ont ainsi le meme nombre de points et se relient point a point. Les quads entre points
    // superposes sont d'aire nulle, l'arete reste donc nette.
    List<RingPoint> BuildRing(float hu, float hv, float[] radii, float inset)
    {
        float iu = hu - inset;
        float iv = hv - inset;
        Vector2[] corners = { new Vector2(-iu, -iv), new Vector2(iu, -iv), new Vector2(iu, iv), new Vector2(-iu, iv) };
        float[] startAngles = { 180f, 270f, 0f, 90f };

        List<RingPoint> ring = new List<RingPoint>();
        ring.Add(new RingPoint(new Vector2(-iu, 0f), Vector2.left));

        for (int k = 0; k < 4; k++)
        {
            Vector2 sign = new Vector2(Mathf.Sign(corners[k].x), Mathf.Sign(corners[k].y));
            float r = Mathf.Max(0f, radii[k] - inset);
            Vector2 center = corners[k] - sign * r;
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

    // Une tranche horizontale du tour : contour retreci de 'inset', place en w, normale = normale du contour * cos
    // + axe d'extrusion * wNormal (arrondis du pourtour). v = distance parcourue sur le tour depuis le bord du dessous.
    struct Station
    {
        public float inset, w, inPlane, wNormal, v;
    }

    // Du bord du dessous au bord du dessus : quart de rond du bas, flanc droit, quart de rond du haut.
    List<Station> BuildStations(float hw, float bottom, float top)
    {
        List<Station> stations = new List<Station>();
        int seg = cornerSegments;

        // Bas : de la face du dessous (theta = 90) au flanc (theta = 0)
        if (bottom > 0.0001f)
        {
            for (int s = 0; s <= seg; s++)
            {
                float t = (90f - 90f * s / seg) * Mathf.Deg2Rad;
                stations.Add(new Station
                {
                    inset = bottom * (1f - Mathf.Cos(t)), w = -hw + bottom - bottom * Mathf.Sin(t),
                    inPlane = Mathf.Cos(t), wNormal = -Mathf.Sin(t)
                });
            }
        }
        else
            stations.Add(new Station { inset = 0f, w = -hw, inPlane = 1f, wNormal = 0f });

        // Haut : du flanc (theta = 0) a la face du dessus (theta = 90)
        if (top > 0.0001f)
        {
            for (int s = 0; s <= seg; s++)
            {
                float t = (90f * s / seg) * Mathf.Deg2Rad;
                stations.Add(new Station
                {
                    inset = top * (1f - Mathf.Cos(t)), w = hw - top + top * Mathf.Sin(t),
                    inPlane = Mathf.Cos(t), wNormal = Mathf.Sin(t)
                });
            }
        }
        else
            stations.Add(new Station { inset = 0f, w = hw, inPlane = 1f, wNormal = 0f });

        // Distance parcourue sur le profil vertical (UV v du tour, en metres)
        float v = 0f;
        for (int j = 0; j < stations.Count; j++)
        {
            if (j > 0)
            {
                Station a = stations[j - 1], b = stations[j];
                v += new Vector2(b.inset - a.inset, b.w - a.w).magnitude;
            }
            Station st = stations[j];
            st.v = v;
            stations[j] = st;
        }
        return stations;
    }

    // Rayons d'arrondi du pourtour bornes : pas plus que la demi-taille du profil (le contour retreci
    // s'inverserait), et a eux deux pas plus que l'epaisseur (reduits en proportion).
    void GetEdgeRounds(float hu, float hv, float hw, out float bottom, out float top)
    {
        float max = Mathf.Min(hu, hv);
        bottom = Mathf.Clamp(roundBottom, 0f, max);
        top = Mathf.Clamp(roundTop, 0f, max);
        float sum = bottom + top;
        if (sum > hw * 2f)
        {
            float k = hw * 2f / sum;
            bottom *= k;
            top *= k;
        }
    }

    void BuildMesh(Mesh mesh)
    {
        GetExtents(out float hu, out float hv, out float hw);
        float[] radii = GetRadii(hu, hv);
        GetEdgeRounds(hu, hv, hw, out float roundBot, out float roundTopClamped);
        List<Station> stations = BuildStations(hw, roundBot, roundTopClamped);

        List<Vector3> verts = new List<Vector3>();
        List<Vector3> normals = new List<Vector3>();
        List<Vector2> uv0 = new List<Vector2>();
        List<Vector2> uv2 = new List<Vector2>();
        List<int> capTris = new List<int>();
        List<int> sideTris = new List<int>();

        // --- Tour : un contour par station, relies point a point. Normales lisses sur les arrondis.
        // UV u = distance le long du contour non retreci, pour que les colonnes d'UV restent alignees d'une station a l'autre.
        List<RingPoint> baseRing = BuildRing(hu, hv, radii, 0f);
        int ringCount = baseRing.Count;
        float[] dist = new float[ringCount];
        for (int i = 1; i < ringCount; i++)
            dist[i] = dist[i - 1] + Vector2.Distance(baseRing[i - 1].p, baseRing[i].p);
        float perimeter = dist[ringCount - 1];
        float profileLength = stations[stations.Count - 1].v;

        int sideStart = verts.Count;
        foreach (Station st in stations)
        {
            List<RingPoint> ring = st.inset > 0f ? BuildRing(hu, hv, radii, st.inset) : baseRing;
            for (int i = 0; i < ringCount; i++)
            {
                Vector2 n2 = baseRing[i].n * st.inPlane;
                verts.Add(ToLocal(ring[i].p.x, ring[i].p.y, st.w, hv, hw));
                normals.Add(DirToLocal(n2.x, n2.y, st.wNormal).normalized);
                uv0.Add(new Vector2(dist[i], st.v));
                uv2.Add(new Vector2(dist[i] / perimeter, st.v / profileLength));
            }
        }
        for (int j = 0; j < stations.Count - 1; j++)
        {
            for (int i = 0; i < ringCount - 1; i++)
            {
                int a = sideStart + j * ringCount + i;
                int b = a + 1;
                int c = b + ringCount;
                int d = a + ringCount;
                Vector3 n = (normals[a] + normals[b] + normals[c] + normals[d]).normalized;
                AddQuad(verts, sideTris, a, b, c, d, n);
            }
        }

        // --- Faces plates (avant = +w, arriere = -w) : eventail depuis le centre, sur le contour retreci
        // par l'arrondi du pourtour. Le contour est convexe et UV / positions sont lineaires sur la face,
        // donc l'eventail est exact.
        for (int side = 0; side < 2; side++)
        {
            float w = side == 0 ? hw : -hw;
            float mirror = side == 0 ? 1f : -1f; // la face arriere vue de derriere n'est pas en miroir
            Vector3 n = DirToLocal(0f, 0f, side == 0 ? 1f : -1f);
            List<RingPoint> ring = BuildRing(hu, hv, radii, side == 0 ? roundTopClamped : roundBot);
            int capStart = verts.Count;

            AddCapVertex(verts, normals, uv0, uv2, 0f, 0f, w, hu, hv, hw, mirror, n);
            // Le dernier point du contour double le premier : inutile ici. Les points superposes des coins vifs
            // donnent des triangles degeneres sans consequence.
            for (int i = 0; i < ring.Count - 1; i++)
                AddCapVertex(verts, normals, uv0, uv2, ring[i].p.x, ring[i].p.y, w, hu, hv, hw, mirror, n);

            int count = ring.Count - 1;
            for (int i = 0; i < count; i++)
            {
                int b = capStart + 1 + i;
                int c = capStart + 1 + (i + 1) % count;
                AddTri(verts, capTris, capStart, b, c, n);
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
            mesh.SetTriangles(capTris, 0);
            mesh.SetTriangles(sideTris, 1);
        }
        else
        {
            capTris.AddRange(sideTris);
            mesh.subMeshCount = 1;
            mesh.SetTriangles(capTris, 0);
        }
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();
    }

    void AddCapVertex(List<Vector3> verts, List<Vector3> normals, List<Vector2> uv0, List<Vector2> uv2,
        float u, float v, float w, float hu, float hv, float hw, float mirror, Vector3 n)
    {
        verts.Add(ToLocal(u, v, w, hv, hw));
        normals.Add(n);
        float uu = mirror > 0f ? u + hu : hu - u;
        uv0.Add(new Vector2(uu, v + hv));
        uv2.Add(new Vector2(uu / (hu * 2f), (v + hv) / (hv * 2f)));
    }

    // Unity : face visible quand Cross(b - a, c - a) pointe vers l'exterieur. On oriente d'apres la normale voulue,
    // ce qui evite de raisonner sur le sens de chaque repere (u, v, w).
    static void AddTri(List<Vector3> verts, List<int> tris, int a, int b, int c, Vector3 outward)
    {
        Vector3 cross = Vector3.Cross(verts[b] - verts[a], verts[c] - verts[a]);
        if (Vector3.Dot(cross, outward) < 0f) { int t = b; b = c; c = t; }
        tris.Add(a); tris.Add(b); tris.Add(c);
    }

    static void AddQuad(List<Vector3> verts, List<int> tris, int a, int b, int c, int d, Vector3 outward)
    {
        // Orientation decidee une fois pour le quad, pour que ses deux triangles restent coherents
        Vector3 cross = Vector3.Cross(verts[b] - verts[a], verts[c] - verts[a]);
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
    // Lettres A-D sur la face qui porte le profil, pour savoir quel rayon touche quel coin
    void OnDrawGizmosSelected()
    {
        if (linkCorners) return;
        GetExtents(out float hu, out float hv, out float hw);
        string[] names = { "A", "B", "C", "D" };
        Vector2[] corners = { new Vector2(-hu, -hv), new Vector2(hu, -hv), new Vector2(hu, hv), new Vector2(-hu, hv) };
        for (int k = 0; k < 4; k++)
        {
            Vector3 p = transform.TransformPoint(ToLocal(corners[k].x, corners[k].y, hw, hv, hw));
            UnityEditor.Handles.Label(p, names[k], UnityEditor.EditorStyles.boldLabel);
        }
    }
#endif
}
