using System.Collections.Generic;
using UnityEngine;

// Bloc de decor parametrique : un rectangle aux coins arrondis (profil), extrude, eventuellement troue et evase.
// Usage : poser sur un Cube (ou tout GO avec MeshFilter), regler, le mesh se met a jour en direct.
// Le mesh n'est pas sauvegarde : il est reconstruit a partir des reglages a chaque chargement,
// donc le composant marche tel quel dans un prefab.
//
// UV0 : en metres (densite constante quelle que soit la taille), le Tiling du materiau regle la repetition.
//       Faces plates : projection dans le plan de la face. Tranches : u = distance le long du contour,
//       v = distance parcourue en remontant la tranche (arrondis compris).
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
    [Tooltip("Quart de rond sur tout le bord de la face 'haute' du profil : dessus (Dessus), avant +Z (Avant), droite +X (Cote). " +
             "S'applique aussi au bord du trou.")]
    [Min(0f)] public float roundTop = 0f;
    [Tooltip("Quart de rond sur tout le bord de la face opposee : dessous (Dessus), arriere -Z (Avant), gauche -X (Cote). " +
             "S'applique aussi au bord du trou.")]
    [Min(0f)] public float roundBottom = 0f;

    [Header("Evasement")]
    [Tooltip("Echelle du profil sur la face 'haute' (dessus en Dessus, avant +Z en Avant, droite +X en Cote) par rapport " +
             "a la face opposee, variation lineaire entre les deux. 1 = droit, < 1 = se resserre, > 1 = s'evase. " +
             "Tout le profil suit (coins, trou, arrondis). En Dessus il se reduit autour de son centre ; " +
             "en Avant/Cote autour du milieu de la base, pour rester pose au sol.")]
    [Min(0.01f)] public float topScale = 1f;
    [Tooltip("Courbure du flanc entre le bas et le haut (sans effet si Echelle du haut = 1). " +
             "0 = droit, < 0 = creuse (volcan, trompette), > 0 = bombe (dome, tulipe).")]
    [Range(-1f, 1f)] public float flareBend = 0f;
    [Tooltip("Bombement ajoute au flanc, nul en haut et en bas. > 0 = ventre (vase), < 0 = taille pincee (diabolo, colonne).")]
    [Range(-1f, 1f)] public float flareBulge = 0f;
    [Tooltip("Hauteur du ventre : 0 = pres du bas, 1 = pres du haut.")]
    [Range(0.1f, 0.9f)] public float bulgePosition = 0.5f;
    [Tooltip("Nombre d'etages sur le flanc pour suivre le galbe et le ventre (entre les arrondis du pourtour). " +
             "Plus = plus lisse, plus de sommets.")]
    [Range(1, 64)] public int heightSegments = 8;

    [Header("Trou")]
    [Tooltip("Perce le bloc de part en part. Le trou se declare par ses marges, vues dans le plan du profil " +
             "(les lettres G/D/B/H s'affichent dans la scene quand l'objet est selectionne).")]
    public bool hole = false;
    [Tooltip("Une seule marge pour les 4 cotes (trou centre).")]
    public bool linkMargins = true;
    [Min(0f)] public float margin = 0.3f;
    [Tooltip("Marges par cote (si Marges liees est decoche). Des marges differentes decalent le trou.")]
    [Min(0f)] public float marginLeft = 0.3f;
    [Min(0f)] public float marginRight = 0.3f;
    [Min(0f)] public float marginBottom = 0.3f;
    [Min(0f)] public float marginTop = 0.3f;
    [Tooltip("Decoche : coins du trou deduits des coins exterieurs (rayon - marge), largeur constante dans les virages. " +
             "Coche : un rayon libre pour les 4 coins du trou.")]
    public bool customHoleCorners = false;
    [Min(0f)] public float holeRadius = 0.2f;

    [Header("Materiaux")]
    [Tooltip("Decoche : un seul materiau pour tout le bloc. Coche : un materiau par partie sur le MeshRenderer, " +
             "element 0 = faces, 1 = tranche exterieure, 2 = tranche du trou (si Trou est coche).")]
    public bool separateMaterials = false;

    [System.NonSerialized] Mesh generatedMesh;

    struct RingPoint
    {
        public Vector2 p; // position dans le plan du profil (u, v)
        public Vector2 n; // normale dans ce plan, vers l'exterieur du rectangle
        public RingPoint(Vector2 p, Vector2 n) { this.p = p; this.n = n; }
    }

    // Rectangle arrondi dans le plan du profil : centre, demi-tailles, rayons des coins A-D
    struct Rect2
    {
        public Vector2 center;
        public float hu, hv;
        public float[] radii;
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
        string meshName = "RoundedBlock_" + GetInstanceID();
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

    // Autant de materiaux que de sous-meshes : un materiau en trop serait redessine par-dessus le dernier
    // sous-mesh, un materiau manquant laisserait une partie invisible (on recopie alors le dernier present).
    void MatchMaterialCount()
    {
        MeshRenderer mr = GetComponent<MeshRenderer>();
        if (mr == null) return;
        Material[] mats = mr.sharedMaterials;
        int wanted = separateMaterials ? (hole ? 3 : 2) : 1;
        if (mats.Length == wanted || mats.Length == 0) return;

        Material[] fixedMats = new Material[wanted];
        for (int i = 0; i < wanted; i++)
            fixedMats[i] = i < mats.Length ? mats[i] : mats[mats.Length - 1];
        mr.sharedMaterials = fixedMats;
    }

    // ---------------------------------------------------------------- Dimensions

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

    // ---------------------------------------------------------------- Evasement
    // Le profil est mis a l'echelle autour d'un point fixe, d'un facteur qui va de 1 (w = -hw) a topScale (w = +hw),
    // en ligne droite ou galbe, avec un ventre eventuel. Un point (u, v) du profil en w devient
    // anchor + ((u, v) - anchor) * ScaleAt(w).

    float TopScale => Mathf.Max(0.01f, topScale);

    bool HasFlareShape => Mathf.Abs(flareBend) > 0.001f || Mathf.Abs(flareBulge) > 0.001f;

    // Point fixe dans le plan du profil : centre en Dessus, milieu de la base en Avant/Cote (le bloc reste au sol).
    Vector2 GetScaleAnchor(float hv)
    {
        return profileFace == ProfileFace.Dessus ? Vector2.zero : new Vector2(0f, -hv);
    }

    float ScaleAt(float w, float hw)
    {
        return ScaleAtT((w + hw) / (hw * 2f));
    }

    // t = hauteur relative 0-1. Jamais sous 0.01 : un ventre trop pince retournerait la forme.
    float ScaleAtT(float t)
    {
        t = Mathf.Clamp01(t);

        // Galbe : f = t^k. Pour un bloc qui se resserre, k < 1 fait chuter l'echelle tot (flanc creuse) ; pour un bloc
        // qui s'evase, c'est k > 1 qui creuse. On inverse donc le sens selon topScale pour que "< 0" creuse toujours.
        float direction = TopScale < 1f ? 1f : -1f;
        float k = Mathf.Pow(3f, flareBend * direction);
        float f = Mathf.Pow(t, k);
        float s = Mathf.LerpUnclamped(1f, TopScale, f);

        // Ventre : bosse en sinus, nulle aux deux bouts, maximale (et a pente nulle) en bulgePosition
        float p = bulgePosition;
        float g = t < p ? 0.5f * t / p : 0.5f + 0.5f * (t - p) / (1f - p);
        s *= 1f + flareBulge * Mathf.Sin(Mathf.PI * g);

        return Mathf.Max(0.01f, s);
    }

    // Derivee de l'echelle selon w (pente du flanc) : constante en lineaire, par difference finie sinon.
    float ScaleSlope(float w, float hw)
    {
        if (!HasFlareShape) return (TopScale - 1f) / (hw * 2f);
        float t = (w + hw) / (hw * 2f);
        const float e = 0.001f;
        float t0 = Mathf.Max(0f, t - e), t1 = Mathf.Min(1f, t + e);
        return (ScaleAtT(t1) - ScaleAtT(t0)) / ((t1 - t0) * hw * 2f);
    }

    Rect2 GetOuterRect(float hu, float hv)
    {
        float max = Mathf.Min(hu, hv);
        float[] r = linkCorners
            ? new[] { radius, radius, radius, radius }
            : new[] { radiusA, radiusB, radiusC, radiusD };
        for (int i = 0; i < 4; i++) r[i] = Mathf.Clamp(r[i], 0f, max);
        return new Rect2 { center = Vector2.zero, hu = hu, hv = hv, radii = r };
    }

    // Marges G, D, B, H bornees : le trou garde au moins 1 cm dans chaque sens (marges reduites en proportion).
    void GetMargins(float hu, float hv, out float left, out float right, out float bottom, out float top)
    {
        left = Mathf.Max(0.01f, linkMargins ? margin : marginLeft);
        right = Mathf.Max(0.01f, linkMargins ? margin : marginRight);
        bottom = Mathf.Max(0.01f, linkMargins ? margin : marginBottom);
        top = Mathf.Max(0.01f, linkMargins ? margin : marginTop);

        float maxU = hu * 2f - 0.01f;
        if (left + right > maxU) { float k = maxU / (left + right); left *= k; right *= k; }
        float maxV = hv * 2f - 0.01f;
        if (bottom + top > maxV) { float k = maxV / (bottom + top); bottom *= k; top *= k; }
    }

    Rect2 GetHoleRect(Rect2 outer, float left, float right, float bottom, float top)
    {
        float minU = -outer.hu + left, maxU = outer.hu - right;
        float minV = -outer.hv + bottom, maxV = outer.hv - top;
        Rect2 rect = new Rect2
        {
            center = new Vector2((minU + maxU) * 0.5f, (minV + maxV) * 0.5f),
            hu = (maxU - minU) * 0.5f,
            hv = (maxV - minV) * 0.5f
        };

        // Coins A (-u,-v), B (+u,-v), C (+u,+v), D (-u,+v) : chacun touche deux marges
        float[] cornerMargin = { (left + bottom) * 0.5f, (right + bottom) * 0.5f, (right + top) * 0.5f, (left + top) * 0.5f };
        float max = Mathf.Min(rect.hu, rect.hv);
        rect.radii = new float[4];
        for (int k = 0; k < 4; k++)
        {
            float r = customHoleCorners ? holeRadius : outer.radii[k] - cornerMargin[k];
            rect.radii[k] = Mathf.Clamp(r, 0f, max);
        }
        return rect;
    }

    // Rayons d'arrondi du pourtour bornes : pas plus que la demi-taille du profil (le contour retreci
    // s'inverserait), pas plus que la moitie de la plus petite marge avec un trou (les arrondis exterieur et
    // interieur se chevaucheraient), et a eux deux pas plus que l'epaisseur (reduits en proportion).
    void GetEdgeRounds(float maxRound, float hw, out float bottom, out float top)
    {
        bottom = Mathf.Clamp(roundBottom, 0f, maxRound);
        top = Mathf.Clamp(roundTop, 0f, maxRound);
        float sum = bottom + top;
        if (sum > hw * 2f)
        {
            float k = hw * 2f / sum;
            bottom *= k;
            top *= k;
        }
    }

    // ---------------------------------------------------------------- Contours

    // Contour ferme dans le sens trigo, retreci de 'inset' (negatif = elargi). Un rectangle arrondi retreci ou
    // elargi reste un rectangle arrondi : demi-tailles - inset, rayons - inset. Depart au milieu du cote gauche
    // (couture des UV des tranches), puis coins A, B, C, D. Le dernier point = le premier (pour fermer les UV).
    // Chaque coin a toujours cornerSegments + 1 points, meme vif (rayon 0 : points superposes, normales en eventail) :
    // tous les contours, exterieurs comme interieurs, ont ainsi le meme nombre de points et se relient point a point.
    // Les quads entre points superposes sont d'aire nulle, l'arete reste donc nette.
    List<RingPoint> BuildRing(Rect2 rect, float inset)
    {
        float iu = rect.hu - inset;
        float iv = rect.hv - inset;
        Vector2 c = rect.center;
        Vector2[] corners =
        {
            c + new Vector2(-iu, -iv), c + new Vector2(iu, -iv), c + new Vector2(iu, iv), c + new Vector2(-iu, iv)
        };
        Vector2[] signs = { new Vector2(-1, -1), new Vector2(1, -1), new Vector2(1, 1), new Vector2(-1, 1) };
        float[] startAngles = { 180f, 270f, 0f, 90f };

        List<RingPoint> ring = new List<RingPoint>();
        ring.Add(new RingPoint(c + new Vector2(-iu, 0f), Vector2.left));

        for (int k = 0; k < 4; k++)
        {
            float r = Mathf.Max(0f, rect.radii[k] - inset);
            Vector2 center = corners[k] - signs[k] * r;
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

    // Une tranche horizontale du tour : contour decale de 'inset', place en w, normale = normale du contour * inPlane
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

        // Flanc : etages intermediaires pour suivre le galbe et le ventre (un seul etage en lineaire)
        if (HasFlareShape)
        {
            float wFrom = stations[stations.Count - 1].w;
            float wTo = top > 0.0001f ? hw - top : hw;
            for (int s = 1; s < heightSegments; s++)
                stations.Add(new Station
                {
                    inset = 0f, w = Mathf.Lerp(wFrom, wTo, (float)s / heightSegments), inPlane = 1f, wNormal = 0f
                });
        }

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

        // Distance parcourue sur le profil vertical (UV v des tranches, en metres)
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

    // ---------------------------------------------------------------- Construction

    void BuildMesh(Mesh mesh)
    {
        GetExtents(out float hu, out float hv, out float hw);
        Rect2 outer = GetOuterRect(hu, hv);

        bool hasHole = hole;
        Rect2 inner = default;
        float maxRound = Mathf.Min(hu, hv);
        if (hasHole)
        {
            GetMargins(hu, hv, out float mL, out float mR, out float mB, out float mT);
            inner = GetHoleRect(outer, mL, mR, mB, mT);
            maxRound = Mathf.Min(maxRound, Mathf.Min(Mathf.Min(mL, mR), Mathf.Min(mB, mT)) * 0.5f);
        }
        GetEdgeRounds(maxRound, hw, out float roundBot, out float roundTopClamped);
        List<Station> stations = BuildStations(hw, roundBot, roundTopClamped);

        List<Vector3> verts = new List<Vector3>();
        List<Vector3> normals = new List<Vector3>();
        List<Vector2> uv0 = new List<Vector2>();
        List<Vector2> uv2 = new List<Vector2>();
        List<int> capTris = new List<int>();
        List<int> outerTris = new List<int>();
        List<int> innerTris = new List<int>();

        // --- Tranches : exterieure (le contour se retrecit dans les arrondis), et celle du trou (il s'elargit)
        AddWall(outer, stations, 1f, hv, hw, verts, normals, uv0, uv2, outerTris);
        if (hasHole)
            AddWall(inner, stations, -1f, hv, hw, verts, normals, uv0, uv2, innerTris);

        // --- Faces plates (avant = +w, arriere = -w), sur les contours decales par l'arrondi du pourtour.
        // Sans trou : eventail depuis le centre (contour convexe, UV / positions lineaires : exact).
        // Avec trou : bande de quads entre le contour exterieur et celui du trou, relies point a point.
        Vector2 anchor = GetScaleAnchor(hv);
        for (int side = 0; side < 2; side++)
        {
            float w = side == 0 ? hw : -hw;
            float scale = ScaleAt(w, hw);
            float mirror = side == 0 ? 1f : -1f; // la face arriere vue de derriere n'est pas en miroir
            Vector3 n = DirToLocal(0f, 0f, side == 0 ? 1f : -1f);
            float round = side == 0 ? roundTopClamped : roundBot;
            List<RingPoint> outerRing = BuildRing(outer, round);
            int count = outerRing.Count - 1; // le dernier point double le premier : inutile sur une face
            int capStart = verts.Count;

            if (!hasHole)
            {
                AddCapVertex(verts, normals, uv0, uv2, 0f, 0f, w, hu, hv, hw, mirror, n, anchor, scale);
                for (int i = 0; i < count; i++)
                    AddCapVertex(verts, normals, uv0, uv2, outerRing[i].p.x, outerRing[i].p.y, w, hu, hv, hw, mirror, n, anchor, scale);
                // Les points superposes des coins vifs donnent des triangles degeneres sans consequence.
                for (int i = 0; i < count; i++)
                    AddTri(verts, capTris, capStart, capStart + 1 + i, capStart + 1 + (i + 1) % count, n);
            }
            else
            {
                List<RingPoint> innerRing = BuildRing(inner, -round);
                for (int i = 0; i < count; i++)
                    AddCapVertex(verts, normals, uv0, uv2, outerRing[i].p.x, outerRing[i].p.y, w, hu, hv, hw, mirror, n, anchor, scale);
                for (int i = 0; i < count; i++)
                    AddCapVertex(verts, normals, uv0, uv2, innerRing[i].p.x, innerRing[i].p.y, w, hu, hv, hw, mirror, n, anchor, scale);
                for (int i = 0; i < count; i++)
                {
                    int next = (i + 1) % count;
                    AddQuad(verts, capTris, capStart + i, capStart + next, capStart + count + next, capStart + count + i, n);
                }
            }
        }

        mesh.Clear();
        mesh.SetVertices(verts);
        mesh.SetNormals(normals);
        mesh.SetUVs(0, uv0);
        mesh.SetUVs(2, uv2);
        if (separateMaterials)
        {
            mesh.subMeshCount = hasHole ? 3 : 2;
            mesh.SetTriangles(capTris, 0);
            mesh.SetTriangles(outerTris, 1);
            if (hasHole) mesh.SetTriangles(innerTris, 2);
        }
        else
        {
            capTris.AddRange(outerTris);
            capTris.AddRange(innerTris);
            mesh.subMeshCount = 1;
            mesh.SetTriangles(capTris, 0);
        }
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();
    }

    // Une tranche : un contour par station, relies point a point. Normales lisses sur les arrondis.
    // facing = 1 : tranche exterieure (normales vers l'exterieur, le contour se retrecit dans les arrondis).
    // facing = -1 : tranche du trou (normales vers le centre du trou, le contour s'elargit dans les arrondis).
    // UV u = distance le long du contour non decale, pour que les colonnes d'UV restent alignees d'une station a l'autre.
    // UV v = distance parcourue en remontant chaque colonne, evasement compris (sans evasement : Station.v).
    void AddWall(Rect2 rect, List<Station> stations, float facing, float hv, float hw,
        List<Vector3> verts, List<Vector3> normals, List<Vector2> uv0, List<Vector2> uv2, List<int> tris)
    {
        List<RingPoint> baseRing = BuildRing(rect, 0f);
        int ringCount = baseRing.Count;
        float[] dist = new float[ringCount];
        for (int i = 1; i < ringCount; i++)
            dist[i] = dist[i - 1] + Vector2.Distance(baseRing[i - 1].p, baseRing[i].p);
        float perimeter = Mathf.Max(0.0001f, dist[ringCount - 1]);

        Vector2 anchor = GetScaleAnchor(hv);
        int stationCount = stations.Count;
        float[] scales = new float[stationCount];
        float[] slopes = new float[stationCount];
        for (int j = 0; j < stationCount; j++)
        {
            scales[j] = ScaleAt(stations[j].w, hw);
            slopes[j] = ScaleSlope(stations[j].w, hw);
        }

        // Longueur de chaque colonne, station par station. Dans le profil non evase, une station decale le point de
        // 'inset' le long de sa normale : on applique l'echelle a ce deplacement ideal.
        float[] colV = new float[stationCount * ringCount];
        for (int j = 1; j < stationCount; j++)
        {
            Station a = stations[j - 1], b = stations[j];
            float s0 = scales[j - 1], s1 = scales[j];
            for (int i = 0; i < ringCount; i++)
            {
                Vector2 d = (baseRing[i].p - anchor) * (s1 - s0) - baseRing[i].n * (facing * (b.inset * s1 - a.inset * s0));
                float dw = b.w - a.w;
                colV[j * ringCount + i] = colV[(j - 1) * ringCount + i] + Mathf.Sqrt(d.sqrMagnitude + dw * dw);
            }
        }

        int start = verts.Count;
        for (int j = 0; j < stationCount; j++)
        {
            Station st = stations[j];
            float s = scales[j];
            List<RingPoint> ring = st.inset > 0f ? BuildRing(rect, st.inset * facing) : baseRing;
            for (int i = 0; i < ringCount; i++)
            {
                Vector2 p = ring[i].p;
                Vector2 ps = anchor + (p - anchor) * s;
                // Normale deformee par la mise a l'echelle (transposee inverse, multipliee par s) : la composante
                // en w s'incline selon la pente du flanc.
                Vector2 n2 = baseRing[i].n * (st.inPlane * facing);
                float nw = s * st.wNormal - slopes[j] * Vector2.Dot(p - anchor, n2);
                float v = colV[j * ringCount + i];
                float columnLength = Mathf.Max(0.0001f, colV[(stationCount - 1) * ringCount + i]);
                verts.Add(ToLocal(ps.x, ps.y, st.w, hv, hw));
                normals.Add(DirToLocal(n2.x, n2.y, nw).normalized);
                uv0.Add(new Vector2(dist[i], v));
                uv2.Add(new Vector2(dist[i] / perimeter, v / columnLength));
            }
        }
        for (int j = 0; j < stationCount - 1; j++)
        {
            for (int i = 0; i < ringCount - 1; i++)
            {
                int a = start + j * ringCount + i;
                int b = a + 1;
                int c = b + ringCount;
                int d = a + ringCount;
                Vector3 n = (normals[a] + normals[b] + normals[c] + normals[d]).normalized;
                AddQuad(verts, tris, a, b, c, d, n);
            }
        }
    }

    void AddCapVertex(List<Vector3> verts, List<Vector3> normals, List<Vector2> uv0, List<Vector2> uv2,
        float u, float v, float w, float hu, float hv, float hw, float mirror, Vector3 n, Vector2 anchor, float scale)
    {
        Vector2 ps = anchor + (new Vector2(u, v) - anchor) * scale;
        verts.Add(ToLocal(ps.x, ps.y, w, hv, hw));
        normals.Add(n);
        // UV0 en metres sur la face mise a l'echelle, UV2 inchangees (0-1 sur la face)
        float uu = mirror > 0f ? u + hu : hu - u;
        uv0.Add(new Vector2(uu * scale, (v + hv) * scale));
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
    // Lettres A-D sur les coins (si non lies) et G/D/B/H sur les marges du trou (si non liees),
    // sur la face qui porte le profil, pour savoir quel reglage touche quoi.
    void OnDrawGizmosSelected()
    {
        GetExtents(out float hu, out float hv, out float hw);

        if (!linkCorners)
        {
            string[] names = { "A", "B", "C", "D" };
            Vector2[] corners = { new Vector2(-hu, -hv), new Vector2(hu, -hv), new Vector2(hu, hv), new Vector2(-hu, hv) };
            for (int k = 0; k < 4; k++)
                Label(corners[k], names[k], hv, hw);
        }

        if (hole && !linkMargins)
        {
            GetMargins(hu, hv, out float mL, out float mR, out float mB, out float mT);
            float cu = (mL - mR) * 0.5f;
            float cv = (mB - mT) * 0.5f;
            Label(new Vector2(-hu + mL * 0.5f, cv), "G", hv, hw);
            Label(new Vector2(hu - mR * 0.5f, cv), "D", hv, hw);
            Label(new Vector2(cu, -hv + mB * 0.5f), "B", hv, hw);
            Label(new Vector2(cu, hv - mT * 0.5f), "H", hv, hw);
        }
    }

    void Label(Vector2 p, string text, float hv, float hw)
    {
        Vector2 anchor = GetScaleAnchor(hv);
        p = anchor + (p - anchor) * ScaleAt(hw, hw); // les lettres sont sur la face 'haute', a son echelle
        Vector3 world = transform.TransformPoint(ToLocal(p.x, p.y, hw, hv, hw));
        UnityEditor.Handles.Label(world, text, UnityEditor.EditorStyles.boldLabel);
    }
#endif
}

#if UNITY_EDITOR
// Inspecteur : n'affiche que les reglages utiles (rayons par coin si non lies, rubrique Trou si cochee...).
[UnityEditor.CustomEditor(typeof(RoundedBlock))]
[UnityEditor.CanEditMultipleObjects]
public class RoundedBlockEditor : UnityEditor.Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        RoundedBlock block = (RoundedBlock)target;
        List<string> hidden = new List<string>();

        if (block.linkCorners)
            hidden.AddRange(new[] { "radiusA", "radiusB", "radiusC", "radiusD" });
        else
            hidden.Add("radius");

        bool noBulge = Mathf.Abs(block.flareBulge) <= 0.001f;
        if (noBulge)
            hidden.Add("bulgePosition");
        if (noBulge && Mathf.Abs(block.flareBend) <= 0.001f)
            hidden.Add("heightSegments");

        if (!block.hole)
            hidden.AddRange(new[] { "linkMargins", "margin", "marginLeft", "marginRight", "marginBottom", "marginTop",
                                    "customHoleCorners", "holeRadius" });
        else
        {
            if (block.linkMargins)
                hidden.AddRange(new[] { "marginLeft", "marginRight", "marginBottom", "marginTop" });
            else
                hidden.Add("margin");
            if (!block.customHoleCorners)
                hidden.Add("holeRadius");
        }

        DrawPropertiesExcluding(serializedObject, hidden.ToArray());
        serializedObject.ApplyModifiedProperties();
    }
}
#endif
