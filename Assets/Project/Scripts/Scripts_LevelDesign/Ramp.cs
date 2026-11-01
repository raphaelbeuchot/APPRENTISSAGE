using System.Collections.Generic;
using UnityEngine;

// Rampe parametrique : un profil (vu de cote, dans le plan ZY) extrude sur la largeur (axe X). La rampe monte vers +Z.
// Usage : poser sur un Cube (ou tout GO avec MeshFilter), regler, le mesh se met a jour en direct.
// Hauteur = celle de la boite englobante (pour placer le decor autour). Le denivele de la surface de marche (le dessus)
// en est deduit : identique sauf pour une dalle coupee a la verticale au depart (elle descend sous le sol), ou dont
// l'epaisseur depasse le denivele. Longueur et angle sont ceux de la surface de marche.
// Pivot = centre de la boite englobante (comme un Cube) : on raccorde au decor par snap de sommets (V).
// Comme RoundedBlock, le mesh n'est pas sauvegarde : il est reconstruit au chargement (compatible prefab).
//
// Coupes des bouts de la dalle :
//   Verticale   : face d'aplomb. Depart : la dalle descend sous le sol. Arrivee : elle bute contre le chant de la plateforme.
//   Horizontale : face de niveau, la dalle finit en pointe. Depart : elle est posee a plat sur le sol.
//                 Arrivee : la face est au niveau du dessus de la plateforme, la dalle se prolonge dessous (dans le bloc).
//
// UV0 : en metres. Faces du pourtour : u = position sur la largeur, v = distance parcourue sur le profil.
//       Flancs : projection dans le plan du profil.
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
    [Tooltip("Longueur : on regle la longueur, l'angle est calcule. Angle : l'inverse.")]
    public SizeMode sizeBy = SizeMode.Longueur;
    [Tooltip("Longueur horizontale du dessus. Recalculee si Size By = Angle.")]
    [Min(0.01f)] public float length = 4f;
    [Tooltip("Angle de la pente, en degres. Recalcule si Size By = Longueur.")]
    [Range(1f, 80f)] public float angle = 14f;
    [Min(0.01f)] public float width = 2f;

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

    // Deduit le denivele de la hauteur de la boite englobante, puis recalcule le champ (longueur ou angle) qui ne
    // pilote pas, pour afficher les valeurs reelles dans l'Inspector.
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

        if (sizeBy == SizeMode.Longueur)
            angle = Mathf.Atan2(rise, Mathf.Max(0.01f, length)) * Mathf.Rad2Deg;
        else
            length = RunFor(rise);
    }

    // Longueur horizontale du dessus pour un denivele donne.
    float RunFor(float r)
    {
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

    // Profil convexe dans le plan (z, y), pied du dessus en (0, 0). Les points suivent le pourtour : segment 0 = dessus,
    // 1 = bout d'arrivee, 2 = dessous, 3 = bout de depart (dalle seulement).
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

    // ---------------------------------------------------------------- Mesh

    void BuildMesh(Mesh mesh)
    {
        List<Vector2> prof = BuildProfile();
        int n = prof.Count;

        // Centre de la boite englobante -> pivot.
        Vector2 min = prof[0], max = prof[0];
        foreach (Vector2 q in prof) { min = Vector2.Min(min, q); max = Vector2.Max(max, q); }
        Vector2 center = (min + max) * 0.5f;
        Vector2 size = max - min;
        for (int i = 0; i < n; i++) prof[i] -= center;

        Vector2 centroid = Vector2.zero;
        foreach (Vector2 q in prof) centroid += q;
        centroid /= n;

        float hw = Mathf.Max(0.01f, width) * 0.5f;

        List<Vector3> verts = new List<Vector3>();
        List<Vector3> normals = new List<Vector3>();
        List<Vector2> uv0 = new List<Vector2>();
        List<Vector2> uv2 = new List<Vector2>();
        List<int> faceTris = new List<int>();
        List<int> edgeTris = new List<int>();

        // Pourtour : un quad par segment du profil, aretes vives (sommets separes).
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
            Vector3 nrm = new Vector3(0f, n2.y, n2.x);

            int v0 = verts.Count;
            verts.Add(new Vector3(-hw, a.y, a.x));
            verts.Add(new Vector3(hw, a.y, a.x));
            verts.Add(new Vector3(hw, b.y, b.x));
            verts.Add(new Vector3(-hw, b.y, b.x));
            for (int k = 0; k < 4; k++) normals.Add(nrm);
            uv0.Add(new Vector2(-hw, perimeter));
            uv0.Add(new Vector2(hw, perimeter));
            uv0.Add(new Vector2(hw, perimeter + len));
            uv0.Add(new Vector2(-hw, perimeter + len));
            uv2.Add(new Vector2(0f, 0f));
            uv2.Add(new Vector2(1f, 0f));
            uv2.Add(new Vector2(1f, 1f));
            uv2.Add(new Vector2(0f, 1f));
            perimeter += len;

            List<int> tris = i == 0 || i == 2 ? faceTris : edgeTris;
            AddTri(tris, verts, v0, v0 + 1, v0 + 2, nrm);
            AddTri(tris, verts, v0, v0 + 2, v0 + 3, nrm);
        }

        // Flancs : le profil est convexe, un eventail depuis le premier point suffit.
        for (int side = -1; side <= 1; side += 2)
        {
            Vector3 nrm = new Vector3(side, 0f, 0f);
            int v0 = verts.Count;
            for (int i = 0; i < n; i++)
            {
                Vector2 q = prof[i];
                verts.Add(new Vector3(side * hw, q.y, q.x));
                normals.Add(nrm);
                // u dans le sens de lecture de chaque flanc (pas de texture en miroir).
                uv0.Add(new Vector2(side * q.x, q.y));
                uv2.Add(new Vector2(
                    size.x > 1e-5f ? (side * q.x) / size.x + 0.5f : 0f,
                    size.y > 1e-5f ? q.y / size.y + 0.5f : 0f));
            }
            for (int i = 1; i < n - 1; i++)
                AddTri(edgeTris, verts, v0, v0 + i, v0 + i + 1, nrm);
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

    // Ajoute le triangle dans le sens qui le rend visible du cote de la normale voulue.
    static void AddTri(List<int> tris, List<Vector3> verts, int a, int b, int c, Vector3 wanted)
    {
        Vector3 cross = Vector3.Cross(verts[b] - verts[a], verts[c] - verts[a]);
        tris.Add(a);
        if (Vector3.Dot(cross, wanted) >= 0f) { tris.Add(b); tris.Add(c); }
        else { tris.Add(c); tris.Add(b); }
    }
}
