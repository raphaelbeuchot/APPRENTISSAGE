using System.Collections.Generic;
using UnityEngine;

// Parcours le long d'une chaine d'aretes vives (MeshEdgeChains), partage par EdgeLights et Barriere.
// Points en repere local du mesh ; abscisses en metres reels (echelle du GO appliquee).
// Boucle : remise dans un ordre fixe (depart sur le +X local vu depuis son centre, sens anti-horaire vu du dessus),
// sinon le depart dependrait de l'ordre des sommets du mesh. arcDegrees < 360 : seule cette portion compte
// (fraction du perimetre, exacte sur un cercle) et elle se comporte comme une chaine ouverte.
// Tangente lissee aux sommets peu anguleux (cylindre a facettes : elle suit le vrai cercle).
public class EdgeChainPath
{
    // Sommet dont le virage depasse ce seuil = vrai coin (dessus d'un cube) : tangente non lissee.
    const float smoothMaxTurn = 45f;

    readonly List<Vector3> pts;   // boucle : le premier point est repete a la fin
    readonly float[] cumul;       // longueur cumulee en metres jusqu'a chaque point
    readonly Vector3[] segDir;    // direction de chaque segment (echelle appliquee)
    readonly Vector3?[] vertTan;  // tangente lissee a chaque sommet, null = coin vif ou bout
    readonly bool loop;           // la chaine d'origine est une boucle
    readonly Vector3 scale;
    readonly float length;
    public readonly bool closed;  // boucle entiere (arc 360)

    public EdgeChainPath(MeshEdgeChains.Chain chain, Vector3 scale, float arcDegrees)
    {
        loop = chain.closed;
        this.scale = scale;
        pts =loop ? CanonicalLoop(chain.points, scale) : new List<Vector3>(chain.points);
        if (loop) pts.Add(pts[0]);

        int m = pts.Count - 1;
        cumul = new float[pts.Count];
        segDir = new Vector3[m];
        for (int i = 0; i < m; i++)
        {
            Vector3 d = Vector3.Scale(pts[i + 1] - pts[i], scale);
            cumul[i + 1] = cumul[i] + d.magnitude;
            segDir[i] = d.normalized;
        }

        vertTan = new Vector3?[pts.Count];
        for (int v = 0; v <= m; v++)
        {
            float turn = Turn(v);
            if (turn < 0f || turn > smoothMaxTurn) continue;
            int before = loop && v == 0 ? m - 1 : v - 1;
            int after = loop && v == m ? 0 : v;
            vertTan[v] = (segDir[before] + segDir[after]).normalized;
        }

        float total = cumul[m];
        closed = loop && arcDegrees >= 360f;
        length = loop ? total * Mathf.Clamp(arcDegrees, 0f, 360f) / 360f : total;
    }

    public float Length => length;
    public float TotalLength => cumul[cumul.Length - 1];

    public void Point(float s, out Vector3 localPos, out Vector3 forward)
    {
        int i = Segment(s);
        float segLen = cumul[i + 1] - cumul[i];
        float t = segLen > 1e-6f ? Mathf.Clamp01((s - cumul[i]) / segLen) : 0f;
        localPos = Vector3.Lerp(pts[i], pts[i + 1], t);

        // Direction du segment au milieu, tangente du sommet aux bouts.
        Vector3 seg = segDir[i];
        Vector3 a = vertTan[i] ?? seg;
        Vector3 b = vertTan[i + 1] ?? seg;
        forward = t < 0.5f ? Vector3.Slerp(a, seg, t * 2f) : Vector3.Slerp(seg, b, (t - 0.5f) * 2f);
    }

    // Abscisses des sommets dont le virage depasse minTurn (degres) : les vrais coins.
    // Boucle entiere : dans [0, longueur[ ; sinon strictement entre les deux bouts.
    public List<float> Corners(float minTurn)
    {
        var list = new List<float>();
        int m = pts.Count - 1;
        for (int v = 0; v <= m; v++)
        {
            if (Turn(v) <= minTurn) continue;
            float s = cumul[v];
            if (closed ? (v < m) : (s > 1e-4f && s < length - 1e-4f)) list.Add(s);
        }
        return list;
    }

    // Abscisse du point du chemin le plus proche d'un point (repere local), sur toute la chaine.
    public float Nearest(Vector3 localPoint)
    {
        float best = float.MaxValue, bestS = 0f;
        for (int i = 0; i < pts.Count - 1; i++)
        {
            Vector3 d = Vector3.Scale(pts[i + 1] - pts[i], scale);
            Vector3 p = Vector3.Scale(localPoint - pts[i], scale);
            float len2 = d.sqrMagnitude;
            float t = len2 > 1e-12f ? Mathf.Clamp01(Vector3.Dot(p, d) / len2) : 0f;
            float dist = (p - d * t).sqrMagnitude;
            if (dist < best) { best = dist; bestS = cumul[i] + t * Mathf.Sqrt(len2); }
        }
        return bestS;
    }

    // Points (repere local) entre deux abscisses, pour l'affichage.
    public List<Vector3> Polyline(float from, float to)
    {
        var list = new List<Vector3>();
        if (to <= from) return list;
        list.Add(At(from));
        for (int k = 1; k < pts.Count - 1; k++)
            if (cumul[k] > from && cumul[k] < to) list.Add(pts[k]);
        list.Add(At(to));
        return list;
    }

    // Virage au sommet v en degres, -1 pour un bout de chaine ouverte.
    float Turn(int v)
    {
        int m = pts.Count - 1;
        int before = v - 1, after = v;
        if (loop && v == 0) before = m - 1;
        if (loop && v == m) after = 0;
        if (before < 0 || after >= m) return -1f;
        return Vector3.Angle(segDir[before], segDir[after]);
    }

    Vector3 At(float s)
    {
        int i = Segment(s);
        float segLen = cumul[i + 1] - cumul[i];
        return Vector3.Lerp(pts[i], pts[i + 1], segLen > 1e-6f ? Mathf.Clamp01((s - cumul[i]) / segLen) : 0f);
    }

    int Segment(float s)
    {
        int i = 0;
        while (i < pts.Count - 2 && cumul[i + 1] < s) i++;
        return i;
    }

    // Sens anti-horaire vu du dessus (+X vers +Z), depart au croisement de la demi-droite +X issue du centre.
    // Boucle a plat vue du dessus (verticale) : laissee telle quelle.
    static List<Vector3> CanonicalLoop(List<Vector3> loop, Vector3 scale)
    {
        var p = new List<Vector3>(loop);
        int n = p.Count;
        Vector3 c = Vector3.zero;
        foreach (Vector3 v in p) c += v;
        c /= n;

        float area = 0f;
        for (int i = 0; i < n; i++)
        {
            Vector3 a = Vector3.Scale(p[i] - c, scale), b = Vector3.Scale(p[(i + 1) % n] - c, scale);
            area += a.x * b.z - b.x * a.z;
        }
        if (Mathf.Abs(area) < 1e-6f) return p;
        if (area < 0f) p.Reverse();

        for (int i = 0; i < n; i++)
        {
            Vector3 a = p[i], b = p[(i + 1) % n];
            float za = a.z - c.z, zb = b.z - c.z;
            // Passage de z <= 0 a z > 0 du cote x > 0 : le +X local
            if (!(za <= 0f && zb > 0f)) continue;
            float t = za / (za - zb);
            Vector3 hit = Vector3.Lerp(a, b, t);
            if (hit.x - c.x <= 0f) continue;

            // Croisement sur un sommet (ou presque) : on part de ce sommet, sans point en double
            var result = new List<Vector3>(n + 1);
            if (t < 1e-4f || t > 1f - 1e-4f)
            {
                int start = t < 1e-4f ? i : (i + 1) % n;
                for (int k = 0; k < n; k++) result.Add(p[(start + k) % n]);
            }
            else
            {
                result.Add(hit);
                for (int k = 1; k <= n; k++) result.Add(p[(i + k) % n]);
            }
            return result;
        }
        return p;
    }
}
