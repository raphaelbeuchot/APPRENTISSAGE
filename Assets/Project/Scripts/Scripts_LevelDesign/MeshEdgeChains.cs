using System.Collections.Generic;
using UnityEngine;

// Aretes vives d'un mesh, chainees en lignes ou en boucles (source "Aretes" d'EdgeLights).
// 1. Soudure des sommets confondus : Unity coupe les meshes aux coutures d'UV et entre faces a normales differentes,
//    sans ca chaque face serait un ilot et toutes ses aretes seraient des bords.
// 2. Arete vive = angle entre les deux faces voisines au-dela du seuil, ou bord ouvert (une seule face),
//    ou arete partagee par plus de deux faces.
// 3. Chainage : on coupe aux sommets ou il n'arrive pas exactement deux aretes vives (coins d'un cube, bouts ouverts).
// Angles calcules avec l'echelle du GO appliquee (un cylindre ecrase garde des angles justes).
public static class MeshEdgeChains
{
    public class Chain
    {
        public List<Vector3> points = new List<Vector3>(); // repere local du mesh ; boucle : dernier point != premier
        public bool closed;
    }

    const float weldPrecision = 1e-4f;

    public static List<Chain> Find(Mesh mesh, Vector3 scale, float angleThreshold)
    {
        var chains = new List<Chain>();
        if (mesh == null) return chains; // dans l'editeur, meme un mesh non Read/Write est lisible

        Vector3[] verts = mesh.vertices;
        int[] tris = mesh.triangles;

        // Soudure par position arrondie
        var weldIndex = new int[verts.Length];
        var weldedPos = new List<Vector3>();
        var lookup = new Dictionary<Vector3Int, int>();
        for (int i = 0; i < verts.Length; i++)
        {
            Vector3 v = verts[i];
            var key = new Vector3Int(Mathf.RoundToInt(v.x / weldPrecision), Mathf.RoundToInt(v.y / weldPrecision),
                                     Mathf.RoundToInt(v.z / weldPrecision));
            if (!lookup.TryGetValue(key, out int id))
            {
                id = weldedPos.Count;
                weldedPos.Add(v);
                lookup[key] = id;
            }
            weldIndex[i] = id;
        }

        // Faces et aretes -> faces voisines
        var faceNormals = new List<Vector3>();
        var edgeFaces = new Dictionary<long, List<int>>();
        for (int t = 0; t + 2 < tris.Length; t += 3)
        {
            int a = weldIndex[tris[t]], b = weldIndex[tris[t + 1]], c = weldIndex[tris[t + 2]];
            if (a == b || b == c || a == c) continue; // triangle degenere
            Vector3 pa = Vector3.Scale(weldedPos[a], scale);
            Vector3 pb = Vector3.Scale(weldedPos[b], scale);
            Vector3 pc = Vector3.Scale(weldedPos[c], scale);
            Vector3 n = Vector3.Cross(pb - pa, pc - pa);
            if (n.sqrMagnitude < 1e-12f) continue;
            int face = faceNormals.Count;
            faceNormals.Add(n.normalized);
            AddEdgeFace(edgeFaces, a, b, face);
            AddEdgeFace(edgeFaces, b, c, face);
            AddEdgeFace(edgeFaces, c, a, face);
        }

        // Aretes vives
        var edges = new List<Vector2Int>();
        foreach (var kv in edgeFaces)
        {
            List<int> faces = kv.Value;
            bool sharp = faces.Count != 2
                         || Vector3.Angle(faceNormals[faces[0]], faceNormals[faces[1]]) > angleThreshold;
            if (sharp) edges.Add(new Vector2Int((int)(kv.Key >> 32), (int)(kv.Key & 0xffffffff)));
        }

        // Sommet -> aretes vives qui y arrivent
        var vertEdges = new Dictionary<int, List<int>>();
        for (int e = 0; e < edges.Count; e++)
        {
            AddVertEdge(vertEdges, edges[e].x, e);
            AddVertEdge(vertEdges, edges[e].y, e);
        }

        var visited = new bool[edges.Count];

        // Chaines ouvertes d'abord : elles partent des sommets de coupure
        foreach (var kv in vertEdges)
        {
            if (kv.Value.Count == 2) continue;
            foreach (int e in kv.Value)
                if (!visited[e]) chains.Add(Walk(kv.Key, e, edges, vertEdges, visited, weldedPos));
        }
        // Le reste ne passe que par des sommets a deux aretes : des boucles
        for (int e = 0; e < edges.Count; e++)
            if (!visited[e]) chains.Add(Walk(edges[e].x, e, edges, vertEdges, visited, weldedPos));

        return chains;
    }

    static Chain Walk(int startVert, int startEdge, List<Vector2Int> edges, Dictionary<int, List<int>> vertEdges,
                      bool[] visited, List<Vector3> pos)
    {
        var chain = new Chain();
        chain.points.Add(pos[startVert]);
        int cur = startVert;
        int e = startEdge;
        while (true)
        {
            visited[e] = true;
            int next = edges[e].x == cur ? edges[e].y : edges[e].x;
            if (next == startVert) { chain.closed = true; break; }
            chain.points.Add(pos[next]);
            List<int> around = vertEdges[next];
            if (around.Count != 2) break;
            int other = around[0] == e ? around[1] : around[0];
            if (visited[other]) break;
            cur = next;
            e = other;
        }
        return chain;
    }

    static void AddEdgeFace(Dictionary<long, List<int>> map, int a, int b, int face)
    {
        long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
        if (!map.TryGetValue(key, out List<int> list)) map[key] = list = new List<int>(2);
        list.Add(face);
    }

    static void AddVertEdge(Dictionary<int, List<int>> map, int v, int e)
    {
        if (!map.TryGetValue(v, out List<int> list)) map[v] = list = new List<int>(2);
        list.Add(e);
    }
}
