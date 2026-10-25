using System.Collections.Generic;
using UnityEngine;

// Pile de boules grossieres empilees en Y (ceps, concretions, rochers empiles, cactus), en un seul mesh.
// Usage : poser sur un GameObject vide, regler, le mesh se met a jour en direct. Le materiau se regle sur le MeshRenderer.
// Le mesh n'est pas sauvegarde : il est reconstruit a partir des reglages a chaque chargement (marche tel quel dans un prefab).
// Meme graine = meme pile ; bouton "Relancer" dans l'inspecteur pour en tirer une autre.
//
// Colliders : les SphereCollider / CapsuleCollider de l'objet sont geres par le composant (ajoutes, regles, retires).
// A l'ajout, l'objet passe sur le layer Obstacle (vu par la grille A* des ennemis).
// Pivot au centre de la base. Limite acceptee : un Scale de Transform != 1 deforme les boules (regler la taille ici).
[ExecuteAlways]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class BallStack : MonoBehaviour
{
    public enum ColliderMode
    {
        Aucun,   // pur decor
        Spheres, // une sphere par boule
        Capsule  // une capsule qui englobe la pile
    }

    [Header("Pile")]
    [Range(1, 20)] public int ballCount = 5;
    [Tooltip("Rayon de la boule du bas (metres).")]
    [Min(0.01f)] public float baseRadius = 0.5f;
    [Tooltip("Taille de la boule du haut par rapport a celle du bas. 1 = toutes pareilles, 0.5 = la derniere fait moitie.")]
    [Range(0.1f, 1.5f)] public float taper = 0.6f;
    [Tooltip("Variation aleatoire du rayon de chaque boule (0.2 = +/- 20 %).")]
    [Range(0f, 0.8f)] public float scaleVariation = 0.2f;
    [Tooltip("Enfoncement de chaque boule dans la precedente. 0 = posees, 0.5 = soudees a mi-rayon.")]
    [Range(0f, 0.9f)] public float overlap = 0.35f;

    [Header("Desordre")]
    [Tooltip("Decalage horizontal aleatoire de chaque boule par rapport a la precedente, en fraction de son rayon. " +
             "Les decalages s'additionnent : la pile peut pencher ou serpenter.")]
    [Range(0f, 1f)] public float horizontalOffset = 0.25f;
    [Tooltip("Rotation aleatoire de chaque boule autour de Y, de -X a +X degres (change l'orientation des facettes et des bosses).")]
    [Range(0f, 180f)] public float ballYRotation = 180f;

    [Header("Boules")]
    [Tooltip("Finesse : 0 = tres facette (20 faces), 3 = presque rond (1280 faces).")]
    [Range(0, 3)] public int detail = 1;
    [Tooltip("Bosselage de la surface (chaque boule cabossee differemment). 0 = boule parfaite.")]
    [Range(0f, 0.5f)] public float bumpiness = 0.12f;
    [Tooltip("Facettes visibles (eclairage plat par face). Decoche = ombrage lisse.")]
    public bool flatShading = true;

    [Header("Hasard")]
    [Tooltip("Meme graine = meme pile. Bouton Relancer dans l'inspecteur pour en tirer une autre.")]
    public int seed = 1;

    [Header("Collision")]
    public ColliderMode colliderMode = ColliderMode.Spheres;

    private Mesh generatedMesh;

    // Boules calculees au dernier Rebuild (centres et rayons, repere local), pour les colliders
    private readonly List<Vector3> ballCenters = new List<Vector3>();
    private readonly List<float> ballRadii = new List<float>();

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
    // A l'ajout : layer Obstacle, colliders d'un eventuel cube retires, graine au hasard.
    void Reset()
    {
        seed = Random.Range(1, 100000);
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this == null) return;
            int obstacle = LayerMask.NameToLayer("Obstacle");
            if (obstacle >= 0)
            {
                UnityEditor.Undo.RecordObject(gameObject, "BallStack layer");
                gameObject.layer = obstacle;
            }
            foreach (BoxCollider box in GetComponents<BoxCollider>()) UnityEditor.Undo.DestroyObjectImmediate(box);
            foreach (MeshCollider mc in GetComponents<MeshCollider>()) UnityEditor.Undo.DestroyObjectImmediate(mc);
            Rebuild();
        };
    }

    // Ajouter / retirer des colliders pendant OnValidate provoque des avertissements : on decale d'une frame d'editeur.
    void OnValidate()
    {
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this != null) Rebuild();
        };
    }

    public void Reroll()
    {
        UnityEditor.Undo.RecordObject(this, "BallStack Relancer");
        seed = Random.Range(1, 100000);
        Rebuild();
    }
#endif

    public void Rebuild()
    {
        MeshFilter mf = GetComponent<MeshFilter>();
        if (mf == null) return;

        if (generatedMesh == null)
        {
            generatedMesh = new Mesh();
            generatedMesh.name = "BallStack";
            generatedMesh.hideFlags = HideFlags.DontSave;
        }
        BuildMesh(generatedMesh);
        mf.sharedMesh = generatedMesh;

#if UNITY_EDITOR
        // Les colliders sont des composants sauvegardes avec la scene : on ne les touche qu'en edition
        if (!Application.isPlaying) SyncColliders();
#endif
    }

    // ---------------------------------------------------------------- Mesh

    void BuildMesh(Mesh mesh)
    {
        System.Random rng = new System.Random(seed);
        ballCenters.Clear();
        ballRadii.Clear();

        List<Vector3> sphereVerts;
        List<int> sphereTris;
        BuildIcosphere(detail, out sphereVerts, out sphereTris);

        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();

        Vector3 center = Vector3.zero;
        float prevRadius = 0f;

        for (int i = 0; i < ballCount; i++)
        {
            float t = ballCount > 1 ? (float)i / (ballCount - 1) : 0f;
            float radius = baseRadius * Mathf.Lerp(1f, taper, t) * (1f + Range(rng, -scaleVariation, scaleVariation));
            radius = Mathf.Max(0.01f, radius);

            if (i == 0)
            {
                center = new Vector3(0f, radius, 0f); // posee au sol
            }
            else
            {
                float angle = Range(rng, 0f, Mathf.PI * 2f);
                float offset = Range(rng, 0f, horizontalOffset) * radius;
                center.x += Mathf.Cos(angle) * offset;
                center.z += Mathf.Sin(angle) * offset;
                center.y += (prevRadius + radius) * (1f - overlap);
            }

            ballCenters.Add(center);
            ballRadii.Add(radius);
            prevRadius = radius;

            Quaternion rotation = Quaternion.Euler(0f, Range(rng, -ballYRotation, ballYRotation), 0f);
            int baseIndex = vertices.Count;
            foreach (Vector3 dir in sphereVerts)
            {
                float bump = 1f + Range(rng, -bumpiness, bumpiness);
                vertices.Add(center + rotation * (dir * radius * bump));
            }
            foreach (int index in sphereTris)
                triangles.Add(baseIndex + index);
        }

        mesh.Clear();
        if (flatShading)
        {
            // Sommets dupliques par triangle : chaque facette a sa propre normale
            List<Vector3> flatVerts = new List<Vector3>(triangles.Count);
            int[] flatTris = new int[triangles.Count];
            for (int i = 0; i < triangles.Count; i++)
            {
                flatVerts.Add(vertices[triangles[i]]);
                flatTris[i] = i;
            }
            mesh.indexFormat = flatVerts.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.SetVertices(flatVerts);
            mesh.SetTriangles(flatTris, 0);
            mesh.SetUVs(0, MeterUVs(flatVerts));
        }
        else
        {
            mesh.indexFormat = vertices.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.SetUVs(0, MeterUVs(vertices));
        }
        mesh.RecalculateNormals();
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();
    }

    // UV0 en metres (projection grossiere x+z / y) : densite constante, le Tiling du materiau regle la repetition
    static List<Vector2> MeterUVs(List<Vector3> verts)
    {
        List<Vector2> uvs = new List<Vector2>(verts.Count);
        foreach (Vector3 v in verts)
            uvs.Add(new Vector2(v.x + v.z, v.y));
        return uvs;
    }

    static float Range(System.Random rng, float min, float max)
    {
        return min + (float)rng.NextDouble() * (max - min);
    }

    // Icosaedre subdivise, sommets sur la sphere unite
    static void BuildIcosphere(int subdivisions, out List<Vector3> verts, out List<int> tris)
    {
        float p = (1f + Mathf.Sqrt(5f)) * 0.5f;
        verts = new List<Vector3>
        {
            new Vector3(-1,  p,  0), new Vector3( 1,  p,  0), new Vector3(-1, -p,  0), new Vector3( 1, -p,  0),
            new Vector3( 0, -1,  p), new Vector3( 0,  1,  p), new Vector3( 0, -1, -p), new Vector3( 0,  1, -p),
            new Vector3( p,  0, -1), new Vector3( p,  0,  1), new Vector3(-p,  0, -1), new Vector3(-p,  0,  1)
        };
        for (int i = 0; i < verts.Count; i++) verts[i] = verts[i].normalized;

        tris = new List<int>
        {
            0, 11, 5,   0, 5, 1,    0, 1, 7,    0, 7, 10,   0, 10, 11,
            1, 5, 9,    5, 11, 4,   11, 10, 2,  10, 7, 6,   7, 1, 8,
            3, 9, 4,    3, 4, 2,    3, 2, 6,    3, 6, 8,    3, 8, 9,
            4, 9, 5,    2, 4, 11,   6, 2, 10,   8, 6, 7,    9, 8, 1
        };

        // Faces tournees vers l'exterieur (convention Unity) quelle que soit la liste d'origine ;
        // la subdivision conserve ensuite l'orientation
        for (int i = 0; i < tris.Count; i += 3)
        {
            Vector3 a = verts[tris[i]], b = verts[tris[i + 1]], c = verts[tris[i + 2]];
            if (Vector3.Dot(Vector3.Cross(b - a, c - a), a + b + c) < 0f)
            {
                int tmp = tris[i + 1];
                tris[i + 1] = tris[i + 2];
                tris[i + 2] = tmp;
            }
        }

        for (int s = 0; s < subdivisions; s++)
        {
            Dictionary<long, int> midpoints = new Dictionary<long, int>();
            List<int> next = new List<int>(tris.Count * 4);
            for (int i = 0; i < tris.Count; i += 3)
            {
                int a = tris[i], b = tris[i + 1], c = tris[i + 2];
                int ab = Midpoint(a, b, verts, midpoints);
                int bc = Midpoint(b, c, verts, midpoints);
                int ca = Midpoint(c, a, verts, midpoints);
                next.AddRange(new[] { a, ab, ca,  b, bc, ab,  c, ca, bc,  ab, bc, ca });
            }
            tris = next;
        }
    }

    static int Midpoint(int a, int b, List<Vector3> verts, Dictionary<long, int> cache)
    {
        long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
        if (cache.TryGetValue(key, out int index)) return index;
        verts.Add(((verts[a] + verts[b]) * 0.5f).normalized);
        index = verts.Count - 1;
        cache[key] = index;
        return index;
    }

    // ---------------------------------------------------------------- Colliders

#if UNITY_EDITOR
    void SyncColliders()
    {
        List<SphereCollider> spheres = new List<SphereCollider>(GetComponents<SphereCollider>());
        CapsuleCollider capsule = GetComponent<CapsuleCollider>();

        int wantedSpheres = colliderMode == ColliderMode.Spheres ? ballCenters.Count : 0;
        while (spheres.Count > wantedSpheres)
        {
            DestroyImmediate(spheres[spheres.Count - 1]);
            spheres.RemoveAt(spheres.Count - 1);
        }
        while (spheres.Count < wantedSpheres)
            spheres.Add(gameObject.AddComponent<SphereCollider>());
        for (int i = 0; i < spheres.Count; i++)
        {
            spheres[i].center = ballCenters[i];
            spheres[i].radius = ballRadii[i];
        }

        if (colliderMode == ColliderMode.Capsule)
        {
            if (capsule == null) capsule = gameObject.AddComponent<CapsuleCollider>();
            FitCapsule(capsule);
        }
        else if (capsule != null)
        {
            DestroyImmediate(capsule);
        }
    }

    // Capsule verticale : de la base au sommet de la pile, rayon = plus grande distance horizontale atteinte par une boule
    void FitCapsule(CapsuleCollider capsule)
    {
        if (ballCenters.Count == 0) return;
        float minY = float.MaxValue, maxY = float.MinValue;
        Vector3 axis = Vector3.zero;
        for (int i = 0; i < ballCenters.Count; i++)
        {
            minY = Mathf.Min(minY, ballCenters[i].y - ballRadii[i]);
            maxY = Mathf.Max(maxY, ballCenters[i].y + ballRadii[i]);
            axis += new Vector3(ballCenters[i].x, 0f, ballCenters[i].z);
        }
        axis /= ballCenters.Count;

        float radius = 0f;
        for (int i = 0; i < ballCenters.Count; i++)
        {
            Vector2 d = new Vector2(ballCenters[i].x - axis.x, ballCenters[i].z - axis.z);
            radius = Mathf.Max(radius, d.magnitude + ballRadii[i]);
        }

        capsule.direction = 1;
        capsule.radius = radius;
        capsule.height = Mathf.Max(maxY - minY, radius * 2f);
        capsule.center = new Vector3(axis.x, (minY + maxY) * 0.5f, axis.z);
    }
#endif
}
