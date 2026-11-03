using System.Collections.Generic;
using UnityEngine;

// Pyramide a base polygonale (3 cotes = tetraedre, 4 = pyramide, beaucoup = cone a facettes), pointe vers +Y.
// Pointe reduite (> 0) = tronc de pyramide ; 1 = prisme.
// Usage : poser sur un GameObject vide, regler, le mesh se met a jour en direct. Le materiau se regle sur le MeshRenderer.
// Le mesh n'est pas sauvegarde : il est reconstruit a partir des reglages a chaque chargement (marche tel quel dans un prefab).
// Largeur / profondeur = boite exacte de la base, quel que soit le nombre de cotes. Pivot au centre de la base.
// UV en metres sur chaque face. Un MeshCollider present sur l'objet recoit le mesh (cocher Convex si besoin).
[ExecuteAlways]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class Pyramid : MonoBehaviour
{
    [Tooltip("Nombre de cotes de la base. 3 = tetraedre, 4 = pyramide, 16 et plus = cone a facettes.")]
    [Range(3, 64)] public int sides = 4;
    [Tooltip("Largeur de la base (X), en metres.")]
    [Min(0.01f)] public float width = 1f;
    [Tooltip("Profondeur de la base (Z), en metres. Tetraedre regulier : profondeur = 0.866 x largeur, hauteur = 0.816 x largeur.")]
    [Min(0.01f)] public float depth = 1f;
    [Tooltip("Hauteur (Y), en metres.")]
    [Min(0.01f)] public float height = 1f;
    [Tooltip("Taille du sommet par rapport a la base. 0 = pointe, 0.5 = tronc de pyramide, 1 = prisme.")]
    [Range(0f, 1f)] public float topScale = 0f;
    [Tooltip("Rotation de la base autour de Y, en degres (la largeur / profondeur restent celles de la base tournee).")]
    [Range(0f, 360f)] public float baseRotation = 0f;

    private Mesh generatedMesh;

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
        string meshName = "Pyramid_" + GetInstanceID();
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

        MeshCollider mc = GetComponent<MeshCollider>();
        if (mc != null)
        {
            mc.sharedMesh = null;
            mc.sharedMesh = generatedMesh;
        }
    }

    // ---------------------------------------------------------------- Mesh

    readonly List<Vector3> verts = new List<Vector3>();
    readonly List<Vector2> uvs = new List<Vector2>();
    readonly List<int> tris = new List<int>();

    void BuildMesh(Mesh mesh)
    {
        // Polygone regulier unitaire, un cote plat vers -Z (pyramide : cotes alignes sur X et Z)
        int n = Mathf.Max(3, sides);
        float start = -Mathf.PI / 2f + Mathf.PI / n + baseRotation * Mathf.Deg2Rad;
        var unit = new Vector2[n];
        Vector2 min = Vector2.positiveInfinity, max = Vector2.negativeInfinity;
        for (int i = 0; i < n; i++)
        {
            float a = start - 2f * Mathf.PI * i / n;
            unit[i] = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            min = Vector2.Min(min, unit[i]);
            max = Vector2.Max(max, unit[i]);
        }

        // Etire pour que la base remplisse exactement largeur x profondeur, centree sur le pivot
        Vector2 size = max - min, mid = (min + max) * 0.5f;
        var bottom = new Vector3[n];
        Vector3 centroid = Vector3.zero;
        for (int i = 0; i < n; i++)
        {
            Vector2 p = unit[i] - mid;
            bottom[i] = new Vector3(p.x / size.x * width, 0f, p.y / size.y * depth);
            centroid += bottom[i];
        }
        centroid /= n;

        // Sommet : base reduite autour de son centre, remontee a la hauteur
        Vector3 apex = centroid + Vector3.up * height;
        var top = new Vector3[n];
        for (int i = 0; i < n; i++) top[i] = apex + (bottom[i] - centroid) * topScale;

        verts.Clear(); uvs.Clear(); tris.Clear();
        Vector3 inside = centroid + Vector3.up * height * 0.25f;
        bool pointed = topScale <= 0f;

        AddFace(bottom, inside);
        for (int i = 0; i < n; i++)
        {
            int j = (i + 1) % n;
            if (pointed) AddFace(new[] { bottom[i], bottom[j], apex }, inside);
            else AddFace(new[] { bottom[i], bottom[j], top[j], top[i] }, inside);
        }
        if (!pointed) AddFace(top, inside);

        mesh.Clear();
        mesh.SetVertices(verts);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();
    }

    // Face plane convexe (sommets en ordre), en eventail, tournee vers l'exterieur ; UV en metres dans son plan.
    void AddFace(Vector3[] poly, Vector3 inside)
    {
        Vector3 normal = Vector3.Cross(poly[1] - poly[0], poly[2] - poly[0]);
        Vector3 center = Vector3.zero;
        foreach (Vector3 p in poly) center += p;
        center /= poly.Length;
        bool flip = Vector3.Dot(normal, center - inside) < 0f;
        normal = (flip ? -normal : normal).normalized;

        Vector3 u = (poly[1] - poly[0]).normalized;
        Vector3 v = Vector3.Cross(normal, u);
        int baseIndex = verts.Count;
        foreach (Vector3 p in poly)
        {
            verts.Add(p);
            uvs.Add(new Vector2(Vector3.Dot(p - poly[0], u), Vector3.Dot(p - poly[0], v)));
        }
        for (int k = 1; k < poly.Length - 1; k++)
        {
            tris.Add(baseIndex);
            tris.Add(baseIndex + (flip ? k + 1 : k));
            tris.Add(baseIndex + (flip ? k : k + 1));
        }
    }
}
