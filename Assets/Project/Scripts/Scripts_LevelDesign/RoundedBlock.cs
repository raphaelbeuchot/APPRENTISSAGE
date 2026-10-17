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

    [Tooltip("Nombre de segments par quart de rond.")]
    [Range(1, 32)] public int cornerSegments = 8;

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

        MeshCollider mc = GetComponent<MeshCollider>();
        if (mc != null)
        {
            mc.sharedMesh = null; // force la mise a jour du collider
            mc.sharedMesh = generatedMesh;
        }
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

    // Contour ferme dans le sens trigo : depart au milieu du cote gauche (couture des UV du tour),
    // puis coins A (-u,-v), B (+u,-v), C (+u,+v), D (-u,+v). Le dernier point = le premier (pour fermer les UV).
    List<RingPoint> BuildRing(float hu, float hv)
    {
        float[] r = GetRadii(hu, hv);
        Vector2[] corners = { new Vector2(-hu, -hv), new Vector2(hu, -hv), new Vector2(hu, hv), new Vector2(-hu, hv) };
        float[] startAngles = { 180f, 270f, 0f, 90f };

        List<RingPoint> ring = new List<RingPoint>();
        ring.Add(new RingPoint(new Vector2(-hu, 0f), Vector2.left));

        for (int k = 0; k < 4; k++)
        {
            Vector2 sign = new Vector2(Mathf.Sign(corners[k].x), Mathf.Sign(corners[k].y));
            if (r[k] > 0.0001f)
            {
                Vector2 center = corners[k] - sign * r[k];
                for (int s = 0; s <= cornerSegments; s++)
                {
                    float a = (startAngles[k] + 90f * s / cornerSegments) * Mathf.Deg2Rad;
                    Vector2 n = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                    ring.Add(new RingPoint(center + n * r[k], n));
                }
            }
            else
            {
                // Coin vif : point double, une normale par cote, pour une arete nette
                ring.Add(new RingPoint(corners[k], Dir2(startAngles[k])));
                ring.Add(new RingPoint(corners[k], Dir2(startAngles[k] + 90f)));
            }
        }

        ring.Add(ring[0]);
        return ring;
    }

    static Vector2 Dir2(float degrees)
    {
        float a = degrees * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(a), Mathf.Sin(a));
    }

    void BuildMesh(Mesh mesh)
    {
        GetExtents(out float hu, out float hv, out float hw);
        List<RingPoint> ring = BuildRing(hu, hv);

        List<Vector3> verts = new List<Vector3>();
        List<Vector3> normals = new List<Vector3>();
        List<Vector2> uv0 = new List<Vector2>();
        List<Vector2> uv2 = new List<Vector2>();
        List<int> tris = new List<int>();

        // --- Tour : une bande de quads, normales lisses sur les arrondis
        float perimeter = 0f;
        for (int i = 1; i < ring.Count; i++) perimeter += Vector2.Distance(ring[i - 1].p, ring[i].p);
        float length = hw * 2f;

        int sideStart = verts.Count;
        float dist = 0f;
        for (int i = 0; i < ring.Count; i++)
        {
            if (i > 0) dist += Vector2.Distance(ring[i - 1].p, ring[i].p);
            Vector3 n = DirToLocal(ring[i].n.x, ring[i].n.y, 0f);
            for (int side = 0; side < 2; side++)
            {
                float w = side == 0 ? -hw : hw;
                verts.Add(ToLocal(ring[i].p.x, ring[i].p.y, w, hv, hw));
                normals.Add(n);
                uv0.Add(new Vector2(dist, w + hw));
                uv2.Add(new Vector2(dist / perimeter, (w + hw) / length));
            }
        }
        for (int i = 0; i < ring.Count - 1; i++)
        {
            int a = sideStart + i * 2;
            Vector3 n = (normals[a] + normals[a + 2]).normalized;
            AddQuad(verts, tris, a, a + 2, a + 3, a + 1, n);
        }

        // --- Faces plates (avant = +w, arriere = -w) : eventail depuis le centre.
        // Le contour est convexe et UV / positions sont lineaires sur la face, donc l'eventail est exact.
        for (int side = 0; side < 2; side++)
        {
            float w = side == 0 ? hw : -hw;
            float mirror = side == 0 ? 1f : -1f; // la face arriere vue de derriere n'est pas en miroir
            Vector3 n = DirToLocal(0f, 0f, side == 0 ? 1f : -1f);
            int capStart = verts.Count;

            AddCapVertex(verts, normals, uv0, uv2, 0f, 0f, w, hu, hv, hw, mirror, n);
            // Le dernier point du contour double le premier : inutile ici. Les coins vifs doubles donnent
            // un triangle degenere sans consequence.
            for (int i = 0; i < ring.Count - 1; i++)
                AddCapVertex(verts, normals, uv0, uv2, ring[i].p.x, ring[i].p.y, w, hu, hv, hw, mirror, n);

            int count = ring.Count - 1;
            for (int i = 0; i < count; i++)
            {
                int b = capStart + 1 + i;
                int c = capStart + 1 + (i + 1) % count;
                AddTri(verts, tris, capStart, b, c, n);
            }
        }

        mesh.Clear();
        mesh.SetVertices(verts);
        mesh.SetNormals(normals);
        mesh.SetUVs(0, uv0);
        mesh.SetUVs(2, uv2);
        mesh.SetTriangles(tris, 0);
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
