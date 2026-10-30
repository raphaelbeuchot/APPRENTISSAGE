using System.Collections.Generic;
using UnityEngine;

// Roue crantee de decor parametrique : profil de dents vu de dessus (plan XZ), extrude en hauteur, eventuellement percee.
// Usage : GameObject vide > Add Component GearShape (MeshFilter / MeshRenderer ajoutes), regler, le mesh se met a jour en direct.
// Le mesh n'est pas sauvegarde : il est reconstruit a partir des reglages a chaque chargement,
// donc le composant marche tel quel dans un prefab. Pas de collider (decor) ; un MeshCollider ajoute a la main suit la forme.
//
// UV0 : en metres (densite constante quelle que soit la taille), le Tiling du materiau regle la repetition.
//       Faces plates : projection XZ. Tranches : u = distance le long du contour, v = hauteur.
[ExecuteAlways]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class GearShape : MonoBehaviour
{
    [Header("Dents")]
    [Min(3)] public int teeth = 10;
    [Tooltip("Rayon au bout des dents (metres).")]
    [Min(0.01f)] public float outerRadius = 0.5f;
    [Tooltip("Rayon au creux entre les dents (metres). Borne sous le rayon exterieur.")]
    [Min(0.01f)] public float innerRadius = 0.4f;
    [Tooltip("Part du pas occupee par la base d'une dent. 1 = dents jointives (pas de creux plat entre elles).")]
    [Range(0.05f, 1f)] public float toothBase = 0.5f;
    [Tooltip("Largeur du bout de la dent, en part de sa base. 0 = pointe, 1 = dent carree. " +
             "Base 1 + bout 0 = etoile.")]
    [Range(0f, 1f)] public float toothTip = 0.6f;
    [Tooltip("Segments des arcs (bout des dents et creux). Plus = plus rond, plus de sommets.")]
    [Range(1, 16)] public int arcSegments = 2;

    [Header("Volume")]
    [Tooltip("Epaisseur (metres). Pivot au centre de la base.")]
    [Min(0.001f)] public float thickness = 0.1f;
    [Tooltip("Rayon du trou central (metres). 0 = pas de trou. Borne sous le rayon interieur.")]
    [Min(0f)] public float holeRadius = 0f;

    [Header("Materiaux")]
    [Tooltip("Decoche : un seul materiau pour toute la roue. Coche : un materiau par partie sur le MeshRenderer, " +
             "element 0 = faces (dessus / dessous), 1 = tranche des dents, 2 = tranche du trou (si Hole Radius > 0).")]
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
        string meshName = "GearShape_" + GetInstanceID();
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

    bool HasHole => Mathf.Min(holeRadius, Mathf.Clamp(innerRadius, 0.005f, Mathf.Max(0.01f, outerRadius) - 0.001f) - 0.01f) > 0.001f;

    // Autant de materiaux que de sous-meshes : un materiau en trop serait redessine par-dessus le dernier
    // sous-mesh, un materiau manquant laisserait une partie invisible (on recopie alors le dernier present).
    void MatchMaterialCount()
    {
        MeshRenderer mr = GetComponent<MeshRenderer>();
        if (mr == null) return;
        Material[] mats = mr.sharedMaterials;
        int wanted = separateMaterials ? (HasHole ? 3 : 2) : 1;
        if (mats.Length == wanted || mats.Length == 0) return;

        Material[] fixedMats = new Material[wanted];
        for (int i = 0; i < wanted; i++)
            fixedMats[i] = i < mats.Length ? mats[i] : mats[mats.Length - 1];
        mr.sharedMaterials = fixedMats;
    }

    // ---------------------------------------------------------------- Contour

    // Contour en coordonnees polaires (x = angle en radians, y = rayon), angles croissants, ferme implicitement.
    // Par dent : creux -> flanc montant -> arc du bout -> flanc descendant -> arc du creux jusqu'a la dent suivante.
    List<Vector2> BuildProfile(float rOut, float rIn)
    {
        List<Vector2> polar = new List<Vector2>();
        int n = Mathf.Max(3, teeth);
        float step = Mathf.PI * 2f / n;
        float baseHalf = step * toothBase * 0.5f;
        float tipHalf = baseHalf * toothTip;
        float gap = step - baseHalf * 2f;
        const float eps = 0.0001f;

        for (int k = 0; k < n; k++)
        {
            float c = k * step;
            polar.Add(new Vector2(c - baseHalf, rIn));

            if (tipHalf > eps)
                for (int s = 0; s <= arcSegments; s++)
                    polar.Add(new Vector2(c - tipHalf + tipHalf * 2f * s / arcSegments, rOut));
            else
                polar.Add(new Vector2(c, rOut));

            // Dents jointives : la fin du creux est le debut de la dent suivante, on ne la double pas
            if (gap > eps)
                for (int s = 0; s < arcSegments; s++)
                    polar.Add(new Vector2(c + baseHalf + gap * s / arcSegments, rIn));
        }
        return polar;
    }

    static Vector3 FromPolar(Vector2 polar, float y)
    {
        return new Vector3(Mathf.Cos(polar.x) * polar.y, y, Mathf.Sin(polar.x) * polar.y);
    }

    // ---------------------------------------------------------------- Construction

    void BuildMesh(Mesh mesh)
    {
        float rOut = Mathf.Max(0.01f, outerRadius);
        float rIn = Mathf.Clamp(innerRadius, 0.005f, rOut - 0.001f);
        float rHole = Mathf.Min(holeRadius, rIn - 0.01f);
        bool hasHole = HasHole;
        float h = Mathf.Max(0.001f, thickness);

        List<Vector2> profile = BuildProfile(rOut, rIn);
        int count = profile.Count;

        List<Vector3> verts = new List<Vector3>();
        List<Vector3> normals = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<int> tris = new List<int>();      // faces
        List<int> outerTris = new List<int>(); // tranche des dents
        List<int> holeTris = new List<int>();  // tranche du trou

        // --- Faces plates (dessus, dessous). Le contour est etoile depuis le centre : chaque rayon le coupe une fois,
        // donc eventail depuis le centre sans trou, et avec trou, bande reliant chaque point au trou sous le meme angle.
        for (int side = 0; side < 2; side++)
        {
            float y = side == 0 ? h : 0f;
            Vector3 n = side == 0 ? Vector3.up : Vector3.down;
            int start = verts.Count;

            for (int i = 0; i < count; i++)
                AddVertex(verts, normals, uvs, FromPolar(profile[i], y), n, side);

            if (!hasHole)
            {
                int center = verts.Count;
                AddVertex(verts, normals, uvs, new Vector3(0f, y, 0f), n, side);
                for (int i = 0; i < count; i++)
                    AddTri(verts, tris, center, start + i, start + (i + 1) % count, n);
            }
            else
            {
                int holeStart = verts.Count;
                for (int i = 0; i < count; i++)
                    AddVertex(verts, normals, uvs, FromPolar(new Vector2(profile[i].x, rHole), y), n, side);
                for (int i = 0; i < count; i++)
                {
                    int next = (i + 1) % count;
                    AddQuad(verts, tris, start + i, start + next, holeStart + next, holeStart + i, n);
                }
            }
        }

        // --- Tranche exterieure : une facette par segment du contour (aretes nettes sur les dents)
        float u = 0f;
        for (int i = 0; i < count; i++)
        {
            Vector3 a = FromPolar(profile[i], 0f);
            Vector3 b = FromPolar(profile[(i + 1) % count], 0f);
            float len = Vector3.Distance(a, b);
            Vector3 mid = (a + b) * 0.5f;
            Vector3 n = Vector3.Cross(Vector3.up, b - a).normalized;
            if (Vector3.Dot(n, mid) < 0f) n = -n;

            int s = verts.Count;
            verts.Add(a); verts.Add(b); verts.Add(b + Vector3.up * h); verts.Add(a + Vector3.up * h);
            for (int k = 0; k < 4; k++) normals.Add(n);
            uvs.Add(new Vector2(u, 0f)); uvs.Add(new Vector2(u + len, 0f));
            uvs.Add(new Vector2(u + len, h)); uvs.Add(new Vector2(u, h));
            AddQuad(verts, outerTris, s, s + 1, s + 2, s + 3, n);
            u += len;
        }

        // --- Tranche du trou : memes angles que le bord du trou sur les faces (pas de jour), normales lisses vers le centre
        if (hasHole)
        {
            int segs = count;
            int s = verts.Count;
            float uu = 0f;
            for (int i = 0; i <= segs; i++)
            {
                float angle = i < count ? profile[i].x : profile[0].x + Mathf.PI * 2f;
                Vector3 radial = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                if (i > 0) uu += Vector3.Distance(radial * rHole, verts[verts.Count - 2]);
                verts.Add(radial * rHole); normals.Add(-radial); uvs.Add(new Vector2(uu, 0f));
                verts.Add(radial * rHole + Vector3.up * h); normals.Add(-radial); uvs.Add(new Vector2(uu, h));
            }
            for (int i = 0; i < segs; i++)
            {
                int a = s + i * 2;
                AddQuad(verts, holeTris, a, a + 2, a + 3, a + 1, (normals[a] + normals[a + 2]).normalized);
            }
        }

        mesh.Clear();
        mesh.SetVertices(verts);
        mesh.SetNormals(normals);
        mesh.SetUVs(0, uvs);
        if (separateMaterials)
        {
            mesh.subMeshCount = hasHole ? 3 : 2;
            mesh.SetTriangles(tris, 0);
            mesh.SetTriangles(outerTris, 1);
            if (hasHole) mesh.SetTriangles(holeTris, 2);
        }
        else
        {
            tris.AddRange(outerTris);
            tris.AddRange(holeTris);
            mesh.subMeshCount = 1;
            mesh.SetTriangles(tris, 0);
        }
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();
    }

    // UV des faces plates en metres, projection XZ ; la face du dessous vue d'en dessous n'est pas en miroir.
    static void AddVertex(List<Vector3> verts, List<Vector3> normals, List<Vector2> uvs, Vector3 p, Vector3 n, int side)
    {
        verts.Add(p);
        normals.Add(n);
        uvs.Add(new Vector2(side == 0 ? p.x : -p.x, p.z));
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
        // Orientation decidee une fois pour le quad ; si le premier triangle est degenere, on prend l'autre.
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
}
