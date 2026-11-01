using System.Collections.Generic;
using UnityEngine;

// Rampe parametrique : un profil (vu de cote : abscisse le long du chemin, hauteur) balaye sur la largeur, le long
// d'un chemin droit ou en virage. La rampe part vers +Z et monte.
// Usage : poser sur un Cube (ou tout GO avec MeshFilter), regler, le mesh se met a jour en direct.
// Hauteur = celle de la boite englobante (pour placer le decor autour). Le denivele de la surface de marche (le dessus)
// en est deduit : identique sauf pour une dalle coupee a la verticale au depart (elle descend sous le sol), ou dont
// l'epaisseur depasse le denivele. Longueur et angle sont ceux de la surface de marche, sur la ligne mediane.
// Virage : la rampe tourne autour d'un centre (rayon interieur au bord interieur). Longueur = arc de la ligne mediane,
// elle decoule du virage et du rayon ; l'angle de pente en decoule aussi. Le bord interieur est plus raide que l'exterieur.
// Pivot = centre de la boite englobante (comme un Cube) : on raccorde au decor par snap de sommets (V).
// Comme RoundedBlock, le mesh n'est pas sauvegarde : il est reconstruit au chargement (compatible prefab).
//
// Coupes des bouts de la dalle (en virage, les faces verticales sont dans l'axe du rayon) :
//   Verticale   : face d'aplomb. Depart : la dalle descend sous le sol. Arrivee : elle bute contre le chant de la plateforme.
//   Horizontale : face de niveau, la dalle finit en pointe. Depart : elle est posee a plat sur le sol.
//                 Arrivee : la face est au niveau du dessus de la plateforme, la dalle se prolonge dessous (dans le bloc).
//
// UV0 : en metres. Faces du pourtour : u = position sur la largeur, v = distance parcourue sur le profil (ligne mediane).
//       Flancs : u = distance le long du flanc, v = hauteur.
// UV2 (canal TEXCOORD2, "UV2" dans Shader Graph) : 0-1 sur chaque face.
[ExecuteAlways]
[RequireComponent(typeof(MeshFilter))]
public class Ramp : MonoBehaviour
{
    public enum SizeMode { Longueur, Angle }
    public enum Body { Pleine, Dalle }
    public enum Cut { Verticale, Horizontale }

    [Header("Pente")]
    [Tooltip("Hauteur de la boite englobante de la rampe.")]
    [Min(0.01f)] public float height = 1f;
    [Tooltip("Denivele de la surface de marche, entre le pied et le haut du dessus. Calcule depuis la hauteur : " +
             "ne pas modifier.")]
    public float rise = 1f;
    [Tooltip("Longueur : on regle la longueur, l'angle est calcule. Angle : l'inverse. Ignore en virage " +
             "(longueur et angle decoulent du virage et du rayon).")]
    public SizeMode sizeBy = SizeMode.Longueur;
    [Tooltip("Longueur horizontale du dessus (en virage : arc de la ligne mediane). Recalculee si Size By = Angle " +
             "ou en virage.")]
    [Min(0.01f)] public float length = 4f;
    [Tooltip("Angle de la pente, en degres (en virage : sur la ligne mediane). Recalcule si Size By = Longueur " +
             "ou en virage.")]
    [Range(1f, 80f)] public float angle = 14f;
    [Min(0.01f)] public float width = 2f;

    [Header("Virage")]
    [Tooltip("Angle du virage en degres, couvert par la surface de marche. 0 = rampe droite. " +
             "Positif = tourne a droite en montant, negatif = a gauche.")]
    [Range(-720f, 720f)] public float turn = 0f;
    [Tooltip("Distance entre le centre du virage et le bord interieur de la rampe.")]
    [Min(0f)] public float innerRadius = 2f;
    [Tooltip("Nombre de facettes par quart de tour.")]
    [Range(1, 64)] public int segmentsPer90 = 12;

    [Header("Volume")]
    [Tooltip("Pleine : un coin jusqu'au sol. Dalle : un plan epais incline.")]
    public Body body = Body.Pleine;
    [Tooltip("Epaisseur de la dalle, mesuree perpendiculairement a la pente.")]
    [Min(0.01f)] public float thickness = 0.2f;
    [Tooltip("Dalle : coupe du bout bas. Horizontale = posee a plat sur le sol, en pointe.")]
    public Cut startCut = Cut.Horizontale;
    [Tooltip("Dalle : coupe du bout haut. Verticale = bute contre le chant de la plateforme.")]
    public Cut endCut = Cut.Verticale;

    [Header("Materiaux")]
    [Tooltip("Decoche : un seul materiau. Coche : un materiau par partie sur le MeshRenderer, " +
             "element 0 = dessus et dessous, 1 = tranche (flancs et bouts).")]
    public bool separateMaterials = false;

    [System.NonSerialized] Mesh generatedMesh;
    [System.NonSerialized] Vector3 pivotOffset; // centre de la boite englobante retire aux sommets (voir BuildMesh)

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
        ResolveDimensions();
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this != null) Rebuild();
        };
    }
#endif

    // ---------------------------------------------------------------- Dimensions

    bool IsCurved => Mathf.Abs(turn) > 0.01f;
    float HalfWidth => Mathf.Max(0.01f, width) * 0.5f;
    float CenterRadius => Mathf.Max(0f, innerRadius) + HalfWidth;

    // Deduit le denivele de la hauteur de la boite englobante, puis recalcule les champs (longueur, angle) qui ne
    // pilotent pas, pour afficher les valeurs reelles dans l'Inspector.
    void ResolveDimensions()
    {
        float target = Mathf.Max(0.01f, height);
        float lo = 0.01f, hi = target;
        if (BoxHeight(lo) >= target)
            rise = lo;
        else
        {
            // La hauteur de boite croit avec le denivele : dichotomie.
            for (int i = 0; i < 40; i++)
            {
                float mid = (lo + hi) * 0.5f;
                if (BoxHeight(mid) < target) lo = mid;
                else hi = mid;
            }
            rise = (lo + hi) * 0.5f;
        }

        if (IsCurved)
        {
            length = RunFor(rise);
            angle = Mathf.Atan2(rise, length) * Mathf.Rad2Deg;
        }
        else if (sizeBy == SizeMode.Longueur)
            angle = Mathf.Atan2(rise, Mathf.Max(0.01f, length)) * Mathf.Rad2Deg;
        else
            length = RunFor(rise);
    }

    // Longueur du dessus (ligne mediane) pour un denivele donne.
    float RunFor(float r)
    {
        if (IsCurved) return Mathf.Max(0.01f, CenterRadius * Mathf.Abs(turn) * Mathf.Deg2Rad);
        if (sizeBy == SizeMode.Longueur) return Mathf.Max(0.01f, length);
        return r / Mathf.Tan(Mathf.Clamp(angle, 1f, 80f) * Mathf.Deg2Rad);
    }

    // Decalage vertical entre dessus et dessous de la dalle (epaisseur perpendiculaire / cos de l'angle).
    float SlabDrop(float r)
    {
        float l = RunFor(r);
        return Mathf.Max(0.01f, thickness) * Mathf.Sqrt(l * l + r * r) / l;
    }

    // Hauteur de la boite englobante pour un denivele donne (voir BuildProfile).
    float BoxHeight(float r)
    {
        if (body == Body.Pleine) return r;
        float d = SlabDrop(r);
        if (startCut == Cut.Verticale) return r + d;
        if (endCut == Cut.Verticale) return Mathf.Max(r, d);
        return r;
    }

    public void Rebuild()
    {
        MeshFilter mf = GetComponent<MeshFilter>();
        if (mf == null) return;
        ResolveDimensions();

        // Apres un rechargement du code, la reference est perdue mais le mesh existe encore (DontSave : jamais nettoye
        // par Unity) : on reprend le sien, reconnu a son nom, au lieu d en creer un de plus a chaque rechargement.
        string meshName = "Ramp_" + GetInstanceID();
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

    // ---------------------------------------------------------------- Profil

    // Profil convexe dans le plan (s, y) : s = abscisse le long du chemin, pied du dessus en (0, 0). Les points suivent
    // le pourtour : segment 0 = dessus, 1 = bout d'arrivee, 2 = dessous, 3 = bout de depart (dalle seulement).
    List<Vector2> BuildProfile()
    {
        float h = rise;
        float l = RunFor(rise);
        List<Vector2> p = new List<Vector2>();
        p.Add(new Vector2(0f, 0f));
        p.Add(new Vector2(l, h));

        if (body == Body.Pleine)
        {
            p.Add(new Vector2(l, 0f));
            return p;
        }

        // Dessous parallele au dessus, decale verticalement de d.
        float d = SlabDrop(h);
        float run = d * l / h; // decalage horizontal equivalent (d / tan de l'angle)

        p.Add(endCut == Cut.Verticale ? new Vector2(l, h - d) : new Vector2(l + run, h));
        p.Add(startCut == Cut.Verticale ? new Vector2(0f, -d) : new Vector2(run, 0f));
        return p;
    }

    // Haut et bas du profil a l'abscisse s (profil convexe : une verticale le coupe en un seul segment).
    static void Envelope(List<Vector2> prof, float s, out float top, out float bottom)
    {
        top = float.MinValue;
        bottom = float.MaxValue;
        int n = prof.Count;
        for (int i = 0; i < n; i++)
        {
            Vector2 a = prof[i];
            Vector2 b = prof[(i + 1) % n];
            if (s < Mathf.Min(a.x, b.x) - 1e-4f || s > Mathf.Max(a.x, b.x) + 1e-4f) continue;
            if (Mathf.Abs(b.x - a.x) < 1e-5f)
            {
                top = Mathf.Max(top, Mathf.Max(a.y, b.y));
                bottom = Mathf.Min(bottom, Mathf.Min(a.y, b.y));
            }
            else
            {
                float y = Mathf.Lerp(a.y, b.y, Mathf.Clamp01((s - a.x) / (b.x - a.x)));
                top = Mathf.Max(top, y);
                bottom = Mathf.Min(bottom, y);
            }
        }
    }

    // Abscisses ou couper le balayage : les sommets du profil, plus les facettes du virage.
    List<float> BuildStations(List<Vector2> prof)
    {
        float sMin = float.MaxValue, sMax = float.MinValue;
        List<float> st = new List<float>();
        foreach (Vector2 q in prof)
        {
            st.Add(q.x);
            sMin = Mathf.Min(sMin, q.x);
            sMax = Mathf.Max(sMax, q.x);
        }
        if (IsCurved)
        {
            float step = CenterRadius * (Mathf.PI * 0.5f) / segmentsPer90;
            int count = Mathf.Min(4096, Mathf.FloorToInt((sMax - sMin) / step));
            for (int k = 1; k <= count; k++) st.Add(sMin + k * step);
        }
        st.Sort();

        List<float> unique = new List<float>();
        foreach (float s in st)
            if (unique.Count == 0 || s - unique[unique.Count - 1] > 1e-4f) unique.Add(s);
        return unique;
    }

    // ---------------------------------------------------------------- Chemin

    // Position d'un point : s = abscisse le long de la ligne mediane, y = hauteur, x = decalage lateral (+X au depart).
    // Droit : (x, y, s). Virage : arc autour d'un centre pose sur l'axe X, du cote ou l'on tourne.
    Vector3 Map(float s, float y, float x)
    {
        if (!IsCurved) return new Vector3(x, y, s);
        float sgn = Mathf.Sign(turn);
        float rc = CenterRadius;
        float phi = s / rc;
        float r = rc - sgn * x;
        return new Vector3(sgn * (rc - r * Mathf.Cos(phi)), y, r * Mathf.Sin(phi));
    }

    // Direction du chemin (horizontale, unitaire).
    Vector3 Tangent(float s)
    {
        if (!IsCurved) return Vector3.forward;
        float phi = s / CenterRadius;
        return new Vector3(Mathf.Sign(turn) * Mathf.Sin(phi), 0f, Mathf.Cos(phi));
    }

    // Direction des x croissants (horizontale, unitaire, perpendiculaire au chemin).
    Vector3 Lateral(float s)
    {
        if (!IsCurved) return Vector3.right;
        float phi = s / CenterRadius;
        return new Vector3(Mathf.Cos(phi), 0f, -Mathf.Sign(turn) * Mathf.Sin(phi));
    }

    // Longueur parcourue au decalage x pour une unite parcourue sur la ligne mediane.
    float Stretch(float x)
    {
        if (!IsCurved) return 1f;
        return (CenterRadius - Mathf.Sign(turn) * x) / CenterRadius;
    }

    // ---------------------------------------------------------------- Mesh

    void BuildMesh(Mesh mesh)
    {
        List<Vector2> prof = BuildProfile();
        List<float> stations = BuildStations(prof);
        int n = prof.Count;

        Vector2 pMin = prof[0], pMax = prof[0];
        Vector2 centroid = Vector2.zero;
        foreach (Vector2 q in prof)
        {
            pMin = Vector2.Min(pMin, q);
            pMax = Vector2.Max(pMax, q);
            centroid += q;
        }
        centroid /= n;
        Vector2 pSize = pMax - pMin;

        float hw = HalfWidth;

        List<Vector3> verts = new List<Vector3>();
        List<Vector3> normals = new List<Vector3>();
        List<Vector2> uv0 = new List<Vector2>();
        List<Vector2> uv2 = new List<Vector2>();
        List<int> faceTris = new List<int>();
        List<int> edgeTris = new List<int>();

        // Pourtour : une bande par segment du profil, decoupee aux stations, aretes vives (sommets separes).
        float perimeter = 0f;
        for (int i = 0; i < n; i++)
        {
            Vector2 a = prof[i];
            Vector2 b = prof[(i + 1) % n];
            Vector2 dir = b - a;
            float len = dir.magnitude;
            if (len < 1e-5f) continue;

            Vector2 n2 = new Vector2(dir.y, -dir.x) / len;
            if (Vector2.Dot(n2, (a + b) * 0.5f - centroid) < 0f) n2 = -n2;

            // Fractions du segment ou couper : extremites + stations comprises entre les deux.
            List<float> ts = new List<float>();
            ts.Add(0f);
            if (Mathf.Abs(dir.x) > 1e-5f)
            {
                List<float> inner = new List<float>();
                foreach (float s in stations)
                {
                    float t = (s - a.x) / dir.x;
                    if (t > 1e-4f && t < 1f - 1e-4f) inner.Add(t);
                }
                inner.Sort();
                ts.AddRange(inner);
            }
            ts.Add(1f);

            List<int> tris = i == 0 || i == 2 ? faceTris : edgeTris;
            int first = verts.Count;
            foreach (float t in ts)
            {
                Vector2 q = Vector2.Lerp(a, b, t);
                Vector3 tan = Tangent(q.x);
                Vector3 wanted = tan * n2.x + Vector3.up * n2.y;
                for (int side = -1; side <= 1; side += 2)
                {
                    float x = side * hw;
                    Vector3 nrm = (tan * -dir.y + Vector3.up * (Stretch(x) * dir.x)).normalized;
                    if (Vector3.Dot(nrm, wanted) < 0f) nrm = -nrm;
                    verts.Add(Map(q.x, q.y, x));
                    normals.Add(nrm);
                    uv0.Add(new Vector2(x, perimeter + t * len));
                    uv2.Add(new Vector2(side < 0 ? 0f : 1f, t));
                }
            }
            for (int j = 0; j < ts.Count - 1; j++)
            {
                int v0 = first + j * 2;
                AddTri(tris, verts, v0, v0 + 1, v0 + 3, normals[v0]);
                AddTri(tris, verts, v0, v0 + 3, v0 + 2, normals[v0]);
            }
            perimeter += len;
        }

        // Flancs : a chaque station, un segment vertical entre le bas et le haut du profil.
        for (int side = -1; side <= 1; side += 2)
        {
            float x = side * hw;
            float k = Stretch(x);
            int first = verts.Count;
            foreach (float s in stations)
            {
                Envelope(prof, s, out float top, out float bottom);
                Vector3 nrm = Lateral(s) * side;
                float u = pSize.x > 1e-5f ? (s - pMin.x) / pSize.x : 0f;
                if (side < 0) u = 1f - u;

                verts.Add(Map(s, bottom, x));
                verts.Add(Map(s, top, x));
                normals.Add(nrm);
                normals.Add(nrm);
                // u dans le sens de lecture de chaque flanc (pas de texture en miroir).
                uv0.Add(new Vector2(side * s * k, bottom));
                uv0.Add(new Vector2(side * s * k, top));
                uv2.Add(new Vector2(u, pSize.y > 1e-5f ? (bottom - pMin.y) / pSize.y : 0f));
                uv2.Add(new Vector2(u, pSize.y > 1e-5f ? (top - pMin.y) / pSize.y : 1f));
            }
            for (int j = 0; j < stations.Count - 1; j++)
            {
                int v0 = first + j * 2;
                AddTri(edgeTris, verts, v0, v0 + 1, v0 + 3, normals[v0]);
                AddTri(edgeTris, verts, v0, v0 + 3, v0 + 2, normals[v0]);
            }
        }

        // Pivot au centre de la boite englobante.
        Vector3 bMin = verts[0], bMax = verts[0];
        foreach (Vector3 v in verts)
        {
            bMin = Vector3.Min(bMin, v);
            bMax = Vector3.Max(bMax, v);
        }
        Vector3 center = (bMin + bMax) * 0.5f;
        pivotOffset = center;
        for (int i = 0; i < verts.Count; i++) verts[i] -= center;

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

    // ---------------------------------------------------------------- Bords (pour EdgeLights)

    // Longueur de la surface de marche, sur la ligne mediane.
    public float WalkLength => RunFor(rise);

    // Point au bord de la surface de marche, dans le repere local du GO (pivot compris).
    // s = abscisse sur la ligne mediane (0 = pied, WalkLength = haut). side = -1 bord gauche, +1 bord droit (en montant).
    // outward = ecartement depuis le bord, positif vers l'exterieur. up = decalage vertical depuis le dessus.
    // forward = direction de la passerelle a ce bord (pente comprise), unitaire.
    public void GetEdgePoint(float s, int side, float outward, float up, out Vector3 localPos, out Vector3 forward)
    {
        float l = RunFor(rise);
        float x = side * (HalfWidth + outward);
        localPos = Map(s, rise * s / l + up, x) - pivotOffset;
        forward = (Tangent(s) * Stretch(x) + Vector3.up * (rise / l)).normalized;
    }

    // Ajoute le triangle dans le sens qui le rend visible du cote de la normale voulue ; ignore les triangles plats
    // (pointe de la dalle, ou le flanc a une hauteur nulle).
    static void AddTri(List<int> tris, List<Vector3> verts, int a, int b, int c, Vector3 wanted)
    {
        Vector3 cross = Vector3.Cross(verts[b] - verts[a], verts[c] - verts[a]);
        if (cross.sqrMagnitude < 1e-12f) return;
        tris.Add(a);
        if (Vector3.Dot(cross, wanted) >= 0f) { tris.Add(b); tris.Add(c); }
        else { tris.Add(c); tris.Add(b); }
    }
}
