using System.Collections.Generic;
using UnityEngine;

// Mur droit parametrique perce d'ouvertures : fenetres, portes (ouverture qui mange un bord) ou niches (creusees sans traverser),
// chacune avec un ebrasement eventuel (faces interieures inclinees : etroite au fond, plus large cote face, comme une meurtriere).
// Usage : poser sur un Cube (ou tout GO avec MeshFilter), regler, le mesh se met a jour en direct.
// Le mesh n'est pas sauvegarde : il est reconstruit a partir des reglages a chaque chargement (comme RoundedBlock).
// Repere : mur dans le plan XY (largeur X, hauteur Y), epaisseur en Z, pivot au milieu de la base.
// Face avant = -Z (celle que voit une camera qui regarde vers +Z) : gauche = -X, droite = +X.
//
// UV0 : en metres. Faces : projection dans le plan du mur. Tranches : u = distance le long du contour, v = profondeur.
// UV2 (canal TEXCOORD2) : 0-1 sur chaque face.
//
// Coins A-D (mur et ouvertures) : A bas gauche, B bas droite, C haut droite, D haut gauche, vus de face.
// Deux ouvertures qui se chevauchent (ou se touchent presque) : la seconde n'est pas percee.
[ExecuteAlways]
[RequireComponent(typeof(MeshFilter))]
public class PerforatedWall : MonoBehaviour
{
    public enum OpeningEdge { Aucun, Bas, Haut, Gauche, Droite }
    public enum BackMode { Uniforme, [InspectorName("Aléatoire")] Aleatoire }

    [System.Serializable]
    public class Opening
    {
        [Tooltip("Bord mange par l'ouverture. Bas = porte (posee au sol). Aucun = fenetre ou niche entierement dans le mur.")]
        public OpeningEdge edge = OpeningEdge.Aucun;
        [Tooltip("Position horizontale du centre de l'ouverture (0 = milieu du mur).")]
        public float x = 0f;
        [Tooltip("Hauteur du bas de l'ouverture depuis le sol.")]
        [Min(0f)] public float y = 0.9f;
        [Min(0.01f)] public float width = 1f;
        [Min(0.01f)] public float height = 1.2f;
        [Tooltip("Un seul rayon pour les coins de l'ouverture. Les coins poses sur un bord mange restent vifs.")]
        public bool linkCorners = true;
        [Min(0f)] public float radius = 0.1f;
        [Tooltip("Rayons par coin de l'ouverture (si decoche). Rayon = demi-largeur en haut d'une ouverture assez haute : arche.")]
        [Min(0f)] public float radiusA = 0.1f;
        [Min(0f)] public float radiusB = 0.1f;
        [Min(0f)] public float radiusC = 0.1f;
        [Min(0f)] public float radiusD = 0.1f;
        [Tooltip("Coche : creuse sans traverser (niche). Decoche : perce de part en part.")]
        public bool niche = false;
        [Tooltip("Profondeur de la niche (bornee : il reste au moins 1 cm de mur derriere).")]
        [Min(0.01f)] public float nicheDepth = 0.1f;
        [Tooltip("Coche : la niche est creusee dans la face arriere (+Z) au lieu de la face avant (-Z).")]
        public bool nicheOnBack = false;
        [Tooltip("Materiaux possibles pour le fond de niche. Vide : le fond prend le materiau des fonds de niche du mur (element 3, ou le materiau unique). " +
                 "Les materiaux utilises sont ajoutes automatiquement a la suite sur le MeshRenderer.")]
        public List<Material> backMaterials = new List<Material>();
        [Tooltip("Uniforme : toutes les copies prennent le premier materiau. Aleatoire : chaque copie tire le sien dans la liste (tirage memorise).")]
        public BackMode backMode = BackMode.Uniforme;
        [HideInInspector] public int backSeed = 1;
        // Fond retenu pour cette copie (rempli par Expand, null = materiau des fonds de niche du mur)
        [System.NonSerialized] public Material backMaterial;
        [Tooltip("Incline les faces interieures de l'ouverture : elle garde sa taille au fond (face arriere, ou fond de niche) " +
                 "et s'elargit vers la face avant (ou la face creusee d'une niche), comme une meurtriere. " +
                 "Le cote d'un bord mange (bas d'une porte) reste droit.")]
        public bool splay = false;
        [Tooltip("Un seul elargissement pour les 4 cotes.")]
        public bool linkSplay = true;
        [Tooltip("Elargissement de chaque cote, mesure sur la face (metres).")]
        [Min(0f)] public float splaySize = 0.1f;
        [Tooltip("Elargissements par cote (si Ebrasement lie est decoche), vus de face.")]
        [Min(0f)] public float splayTop = 0.1f;
        [Min(0f)] public float splayBottom = 0.1f;
        [Min(0f)] public float splayLeft = 0.1f;
        [Min(0f)] public float splayRight = 0.1f;
        [Tooltip("Repete l'ouverture en rangee ou en trame : copies identiques, groupe centre sur Position X, " +
                 "rangee du bas a Position Y. Une porte ne se repete qu'en X, une ouverture a gauche ou a droite qu'en Y.")]
        public bool repeat = false;
        [Range(1, 50)] public int countX = 3;
        [Range(1, 50)] public int countY = 1;
        [Tooltip("Mur plein entre deux copies, en metres (au moins 1 cm).")]
        [Min(0.01f)] public float gapX = 0.5f;
        [Min(0.01f)] public float gapY = 0.5f;
        public Opening Clone()
        {
            Opening c = (Opening)MemberwiseClone();
            c.backMaterials = backMaterials != null ? new List<Material>(backMaterials) : new List<Material>();
            return c;
        }

        // Fond de la copie (cx, cy) : un tirage par case, stable quand on ajoute ou retire des copies
        Material PickBack(int cx, int cy)
        {
            if (!niche || backMaterials == null || backMaterials.Count == 0) return null;
            if (backMode == BackMode.Uniforme) return backMaterials[0];
            uint h = (uint)backSeed * 2654435761u ^ (uint)(cx + 1) * 2246822519u ^ (uint)(cy + 1) * 3266489917u;
            h ^= h >> 15; h *= 2246822519u; h ^= h >> 13;
            return backMaterials[(int)(h % (uint)backMaterials.Count)];
        }

        float SplaySide(float side) => splay ? (linkSplay ? splaySize : side) : 0f;

        // Centrer : meme mur plein a gauche et a droite du groupe (ebrasement compris), nombre et ecart inchanges
        public void CenterX()
        {
            x = (SplaySide(splayLeft) - SplaySide(splayRight)) * 0.5f;
        }

        // Centrer : meme mur plein sous et au-dessus du groupe (ebrasement compris)
        public void CenterY(float wallHeight)
        {
            int n = CopiesY;
            float total = n * height + (n - 1) * Mathf.Max(0.01f, gapY);
            y = Mathf.Max(0f, (wallHeight - total + SplaySide(splayBottom) - SplaySide(splayTop)) * 0.5f);
        }

        // Nombres de copies effectifs (1 sans repetition, ou dans le sens interdit par le bord mange)
        public int CopiesX => repeat && edge != OpeningEdge.Gauche && edge != OpeningEdge.Droite ? Mathf.Clamp(countX, 1, 50) : 1;
        public int CopiesY => repeat && edge != OpeningEdge.Bas && edge != OpeningEdge.Haut ? Mathf.Clamp(countY, 1, 50) : 1;

        // Les copies, ligne par ligne depuis le bas gauche, chacune comme une ouverture simple
        public List<Opening> Expand()
        {
            List<Opening> list = new List<Opening>();
            int nx = CopiesX, ny = CopiesY;
            float stepX = width + Mathf.Max(0.01f, gapX), stepY = height + Mathf.Max(0.01f, gapY);
            for (int cy = 0; cy < ny; cy++)
                for (int cx = 0; cx < nx; cx++)
                {
                    Opening c = (Opening)MemberwiseClone();
                    c.repeat = false;
                    c.x = x + (cx - (nx - 1) * 0.5f) * stepX;
                    c.y = y + cy * stepY;
                    c.backMaterial = PickBack(cx, cy);
                    list.Add(c);
                }
            return list;
        }

        // Cote mange : 0 = bas (A-B), 1 = droite (B-C), 2 = haut (C-D), 3 = gauche (D-A), -1 = aucun
        public int EdgeSide
        {
            get
            {
                switch (edge)
                {
                    case OpeningEdge.Bas: return 0;
                    case OpeningEdge.Droite: return 1;
                    case OpeningEdge.Haut: return 2;
                    case OpeningEdge.Gauche: return 3;
                    default: return -1;
                }
            }
        }
    }

    [Header("Mur (mètres)")]
    [Tooltip("X = largeur, Y = hauteur, Z = epaisseur. Pivot au milieu de la base.")]
    public Vector3 size = new Vector3(3f, 2.5f, 0.2f);

    [Header("Coins du mur")]
    [Tooltip("Un seul rayon pour les 4 coins du mur.")]
    public bool linkCorners = true;
    [Min(0f)] public float radius = 0f;
    [Tooltip("Rayons par coin (si Coins lies est decoche). Les lettres A-D s'affichent dans la scene quand l'objet est selectionne.")]
    [Min(0f)] public float radiusA = 0f;
    [Min(0f)] public float radiusB = 0f;
    [Min(0f)] public float radiusC = 0f;
    [Min(0f)] public float radiusD = 0f;
    [Tooltip("Nombre de segments par quart de rond (coins du mur et des ouvertures).")]
    [Range(1, 16)] public int cornerSegments = 4;

    public List<Opening> openings = new List<Opening>();

    [Header("Matériaux")]
    [Tooltip("Decoche : un seul materiau. Coche : un materiau par partie (faces, tranche exterieure, interieur des ouvertures, fonds de niche), " +
             "regles juste en dessous ; ce sont les elements 0 a 3 du MeshRenderer, les fonds de palette suivent.")]
    public bool separateMaterials = false;
    // Nombre de materiaux "du mur" en tete du MeshRenderer au dernier passage ; les fonds de palette suivent
    // (0 = mur d'avant les palettes : tous les materiaux presents sont ceux du mur)
    [SerializeField, HideInInspector] int wallMaterialCount;

    // Ancienne ouverture unique (avant la liste) : relue une seule fois puis recopiee dans 'openings'.
    [SerializeField, HideInInspector] bool openingsMigrated;
    [SerializeField, HideInInspector] bool opening = true;
    [SerializeField, HideInInspector] OpeningEdge edge = OpeningEdge.Aucun;
    [SerializeField, HideInInspector] float openingX = 0f;
    [SerializeField, HideInInspector] float openingY = 0.9f;
    [SerializeField, HideInInspector] float openingWidth = 1f;
    [SerializeField, HideInInspector] float openingHeight = 1.2f;
    [SerializeField, HideInInspector] bool linkOpeningCorners = true;
    [SerializeField, HideInInspector] float openingRadius = 0.1f;
    [SerializeField, HideInInspector] float openingRadiusA = 0.1f;
    [SerializeField, HideInInspector] float openingRadiusB = 0.1f;
    [SerializeField, HideInInspector] float openingRadiusC = 0.1f;
    [SerializeField, HideInInspector] float openingRadiusD = 0.1f;
    [SerializeField, HideInInspector] bool niche = false;
    [SerializeField, HideInInspector] float nicheDepth = 0.1f;
    [SerializeField, HideInInspector] bool nicheOnBack = false;
    [SerializeField, HideInInspector] bool splay = false;
    [SerializeField, HideInInspector] bool linkSplay = true;
    [SerializeField, HideInInspector] float splaySize = 0.1f;
    [SerializeField, HideInInspector] float splayTop = 0.1f;
    [SerializeField, HideInInspector] float splayBottom = 0.1f;
    [SerializeField, HideInInspector] float splayLeft = 0.1f;
    [SerializeField, HideInInspector] float splayRight = 0.1f;

    [System.NonSerialized] Mesh generatedMesh;
    // Message affiche dans l'inspecteur quand une ouverture ne peut pas etre percee
    [System.NonSerialized] public string warning;
    // Dernier passage : nombre de parties du mur et fonds de palette utilises (pour la section Materiaux de l'inspecteur)
    [System.NonSerialized] public int lastParts;
    [System.NonSerialized] public List<Material> lastBacks = new List<Material>();

    // Point d'un contour : position, normale lisse sur les arrondis (zero = coin vif, normale du segment),
    // et nature du segment qui part de ce point (tranche d'une ouverture ou tranche exterieure).
    struct CPoint
    {
        public Vector2 p;
        public Vector2 n;
        public bool openingSeg;
    }

    // Rectangle aux coins arrondis : bornes et rayons A-D
    struct RRect
    {
        public float minX, maxX, minY, maxY;
        public float[] r;
    }

    // Une ouverture retenue (percee) : son rectangle au fond et au ras de la face creusee, et ses profondeurs.
    class OpeningGeo
    {
        public int side;
        public RRect hole;      // au fond (face arriere d'une ouverture traversante, ou fond de niche)
        public RRect face;      // au ras de la face creusee (elargi par l'ebrasement)
        public bool isNiche;
        public Material back;   // fond de niche pris dans la palette (null = materiau des fonds de niche du mur)
        public float zBottom;   // profondeur du fond
        public float zDug;      // profondeur de la face creusee

        public bool Present(float z) =>
            z >= Mathf.Min(zBottom, zDug) - 0.00001f && z <= Mathf.Max(zBottom, zDug) + 0.00001f;

        // Le rectangle a la profondeur z : du fond (hole) a la face (face), lineairement
        public RRect At(float z)
        {
            float span = zDug - zBottom;
            float t = Mathf.Abs(span) < 0.000001f ? 1f : Mathf.Clamp01((z - zBottom) / span);
            RRect r = new RRect
            {
                minX = Mathf.Lerp(hole.minX, face.minX, t), maxX = Mathf.Lerp(hole.maxX, face.maxX, t),
                minY = Mathf.Lerp(hole.minY, face.minY, t), maxY = Mathf.Lerp(hole.maxY, face.maxY, t),
                r = new float[4]
            };
            for (int k = 0; k < 4; k++) r.r[k] = Mathf.Lerp(hole.r[k], face.r[k], t);
            return r;
        }
    }

    static readonly Vector2[] Signs = { new Vector2(-1, -1), new Vector2(1, -1), new Vector2(1, 1), new Vector2(-1, 1) };
    static readonly float[] StartAngles = { 180f, 270f, 0f, 90f };

    void OnEnable()
    {
        MigrateLegacyOpening();
        Rebuild();
    }

    void OnDestroy()
    {
        if (generatedMesh == null) return;
        if (Application.isPlaying) Destroy(generatedMesh);
        else DestroyImmediate(generatedMesh);
    }

    // Recopie l'ancienne ouverture unique en tete de liste (une seule fois). Un composant neuf y gagne une fenetre par defaut.
    void MigrateLegacyOpening()
    {
        if (openingsMigrated) return;
        openingsMigrated = true;
        if (openings == null) openings = new List<Opening>();
        if (opening)
        {
            openings.Insert(0, new Opening
            {
                edge = edge, x = openingX, y = openingY, width = openingWidth, height = openingHeight,
                linkCorners = linkOpeningCorners, radius = openingRadius,
                radiusA = openingRadiusA, radiusB = openingRadiusB, radiusC = openingRadiusC, radiusD = openingRadiusD,
                niche = niche, nicheDepth = nicheDepth, nicheOnBack = nicheOnBack,
                splay = splay, linkSplay = linkSplay, splaySize = splaySize,
                splayTop = splayTop, splayBottom = splayBottom, splayLeft = splayLeft, splayRight = splayRight
            });
        }
#if UNITY_EDITOR
        if (!Application.isPlaying) UnityEditor.EditorUtility.SetDirty(this);
#endif
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
            MigrateLegacyOpening(); // Reset remet les champs a zero : un composant neuf repart avec une fenetre
            Rebuild();
        };
    }

    // Toucher au MeshCollider pendant OnValidate provoque des avertissements : on decale d'une frame d'editeur.
    void OnValidate()
    {
        MigrateLegacyOpening();
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
            generatedMesh.name = "PerforatedWall";
            generatedMesh.hideFlags = HideFlags.DontSave;
        }
        int parts = BuildMesh(generatedMesh, out List<Material> backs);
        mf.sharedMesh = generatedMesh;
        MatchMaterialCount(parts, backs);
        lastParts = parts;
        lastBacks = backs;

        MeshCollider mc = GetComponent<MeshCollider>();
        if (mc != null)
        {
            mc.sharedMesh = null; // force la mise a jour du collider
            mc.sharedMesh = generatedMesh;
        }
    }

    // Autant de materiaux que de sous-meshes (meme regle que RoundedBlock) : ceux du mur (regles a la main),
    // puis les fonds de palette utilises (geres par le script, dans l'ordre des sous-meshes).
    void MatchMaterialCount(int parts, List<Material> backs)
    {
        MeshRenderer mr = GetComponent<MeshRenderer>();
        if (mr == null) return;
        Material[] mats = mr.sharedMaterials;
        int wanted = separateMaterials ? parts : 1;
        int known = wallMaterialCount > 0 ? Mathf.Min(wallMaterialCount, mats.Length) : mats.Length;
        if (known == 0) return;

        Material[] fixedMats = new Material[wanted + backs.Count];
        for (int i = 0; i < wanted; i++)
            fixedMats[i] = i < known ? mats[i] : mats[known - 1];
        for (int i = 0; i < backs.Count; i++)
            fixedMats[wanted + i] = backs[i];
        wallMaterialCount = wanted;

        bool same = fixedMats.Length == mats.Length;
        for (int i = 0; same && i < mats.Length; i++) same = fixedMats[i] == mats[i];
        if (!same) mr.sharedMaterials = fixedMats;
    }

    // ---------------------------------------------------------------- Dimensions

    RRect GetWallRect()
    {
        float w = Mathf.Max(0.02f, size.x), h = Mathf.Max(0.02f, size.y);
        RRect rect = new RRect { minX = -w * 0.5f, maxX = w * 0.5f, minY = 0f, maxY = h };
        float[] r = linkCorners
            ? new[] { radius, radius, radius, radius }
            : new[] { radiusA, radiusB, radiusC, radiusD };
        // 0.999 : deux arrondis voisins ne se touchent jamais (pas de points superposes)
        float max = Mathf.Min(w, h) * 0.5f * 0.999f;
        for (int k = 0; k < 4; k++) r[k] = Mathf.Clamp(r[k], 0f, max);
        rect.r = r;
        return rect;
    }

    float Thickness => Mathf.Max(0.01f, size.z);

    // Rectangle de l'ouverture, borne dans le mur (1 cm de marge, ou colle au bord mange).
    static RRect GetOpeningRect(RRect wall, Opening o, int side)
    {
        float W = wall.maxX - wall.minX, H = wall.maxY - wall.minY;
        float w = Mathf.Clamp(o.width, 0.01f, W - 0.02f);
        float h = Mathf.Clamp(o.height, 0.01f, H - 0.02f);
        float x0 = o.x - w * 0.5f;
        float y0 = o.y;

        if (side == 1) x0 = wall.maxX - w;
        else if (side == 3) x0 = wall.minX;
        else x0 = Mathf.Clamp(x0, wall.minX + 0.01f, wall.maxX - 0.01f - w);

        if (side == 0) y0 = wall.minY;
        else if (side == 2) y0 = wall.maxY - h;
        else y0 = Mathf.Clamp(y0, wall.minY + 0.01f, wall.maxY - 0.01f - h);

        RRect rect = new RRect { minX = x0, maxX = x0 + w, minY = y0, maxY = y0 + h };
        float[] r = o.linkCorners
            ? new[] { o.radius, o.radius, o.radius, o.radius }
            : new[] { o.radiusA, o.radiusB, o.radiusC, o.radiusD };
        // Les coins poses sur le bord mange sont vifs : leurs voisins peuvent alors aller jusqu'a toute la longueur du cote
        float max;
        if (side == 0 || side == 2) max = Mathf.Min(w * 0.5f, h);
        else if (side == 1 || side == 3) max = Mathf.Min(w, h * 0.5f);
        else max = Mathf.Min(w, h) * 0.5f;
        max *= 0.999f;
        for (int k = 0; k < 4; k++) r[k] = Mathf.Clamp(r[k], 0f, max);
        if (side >= 0)
        {
            r[side] = 0f;
            r[(side + 1) % 4] = 0f;
        }
        rect.r = r;
        return rect;
    }

    // Contour de l'ouverture au ras de la face, elargi par l'ebrasement (cote par cote). Les coins vifs restent vifs
    // (aretes en onglet), les arrondis grandissent de la moyenne des deux cotes voisins.
    // hasSplay = faux : pas d'ebrasement, le rectangle de l'ouverture est renvoye tel quel.
    static RRect GetSplayRect(Opening o, RRect hole, int side, out bool hasSplay)
    {
        // Cotes : 0 = bas, 1 = droite, 2 = haut, 3 = gauche
        float[] bw = o.linkSplay
            ? new[] { o.splaySize, o.splaySize, o.splaySize, o.splaySize }
            : new[] { o.splayBottom, o.splayRight, o.splayTop, o.splayLeft };
        for (int k = 0; k < 4; k++) bw[k] = Mathf.Max(0f, bw[k]);
        if (side >= 0) bw[side] = 0f;

        hasSplay = o.splay && Mathf.Max(Mathf.Max(bw[0], bw[1]), Mathf.Max(bw[2], bw[3])) >= 0.0001f;
        if (!hasSplay) return hole;

        RRect rect = new RRect
        {
            minX = hole.minX - bw[3], maxX = hole.maxX + bw[1],
            minY = hole.minY - bw[0], maxY = hole.maxY + bw[2],
            r = new float[4]
        };
        // Le coin k touche les cotes k - 1 et k (A : gauche et bas)
        for (int k = 0; k < 4; k++)
            rect.r[k] = hole.r[k] > 0.00001f ? hole.r[k] + (bw[(k + 3) % 4] + bw[k]) * 0.5f : 0f;
        return rect;
    }

    // L'ouverture doit rester dans le mur, arrondis du mur compris. Les deux coins poses sur le bord mange
    // doivent tomber sur sa partie droite (pas dans un arrondi du mur).
    bool OpeningFits(RRect wall, RRect hole, int side)
    {
        List<CPoint> ring = BuildRing(hole);
        foreach (CPoint cp in ring)
        {
            bool onEdge = side >= 0 && IsOnSide(cp.p, wall, side);
            if (!InsideWall(cp.p, wall, onEdge ? -0.0001f : 0.005f)) return false;
        }
        return true;
    }

    // Chevauchement (ou presque contact, moins de 1 cm) des rectangles englobants de deux ouvertures
    static bool Overlaps(RRect a, RRect b)
    {
        const float gap = 0.01f;
        return a.minX < b.maxX + gap && b.minX < a.maxX + gap && a.minY < b.maxY + gap && b.minY < a.maxY + gap;
    }

    static bool IsOnSide(Vector2 p, RRect r, int side)
    {
        const float e = 0.0001f;
        switch (side)
        {
            case 0: return Mathf.Abs(p.y - r.minY) < e;
            case 1: return Mathf.Abs(p.x - r.maxX) < e;
            case 2: return Mathf.Abs(p.y - r.maxY) < e;
            default: return Mathf.Abs(p.x - r.minX) < e;
        }
    }

    static bool InsideWall(Vector2 p, RRect wall, float margin)
    {
        if (p.x < wall.minX + margin || p.x > wall.maxX - margin || p.y < wall.minY + margin || p.y > wall.maxY - margin)
            return false;
        for (int k = 0; k < 4; k++)
        {
            float R = wall.r[k];
            if (R <= 0f) continue;
            Vector2 c = CornerPoint(wall, k) - Signs[k] * R;
            Vector2 d = p - c;
            // Dans le quart de coin (au-dela du centre de l'arrondi, vers le coin) : il faut rester dans le cercle
            if (d.x * Signs[k].x > 0f && d.y * Signs[k].y > 0f && d.magnitude > R - margin)
                return false;
        }
        return true;
    }

    // ---------------------------------------------------------------- Contours

    static Vector2 CornerPoint(RRect r, int k)
    {
        return new Vector2(Signs[k].x < 0f ? r.minX : r.maxX, Signs[k].y < 0f ? r.minY : r.maxY);
    }

    // Un coin : quart de rond (cornerSegments + 1 points) ou un seul point s'il est vif.
    // reverse = parcouru dans le sens horaire ; normalSign = -1 pour un trou (normales vers le vide de l'ouverture).
    void AppendCorner(List<CPoint> list, RRect r, int k, bool reverse, float normalSign, bool openingSeg)
    {
        float R = r.r[k];
        Vector2 corner = CornerPoint(r, k);
        if (R < 0.00001f)
        {
            list.Add(new CPoint { p = corner, n = Vector2.zero, openingSeg = openingSeg });
            return;
        }
        Vector2 c = corner - Signs[k] * R;
        int seg = Mathf.Max(1, cornerSegments);
        for (int s = 0; s <= seg; s++)
        {
            int ss = reverse ? seg - s : s;
            float a = (StartAngles[k] + 90f * ss / seg) * Mathf.Deg2Rad;
            Vector2 d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            list.Add(new CPoint { p = c + d * R, n = d * normalSign, openingSeg = openingSeg });
        }
    }

    // Les contours ne fusionnent jamais leurs points superposes : deux contours construits pareil (ouverture au fond
    // et au ras de la face, avec l'ebrasement) ont ainsi toujours le meme nombre de points et se relient point a point.
    // Les segments de longueur nulle sont ignores par les tranches et la triangulation.

    // Contour plein, sens trigo, depart coin A. Normales vers l'exterieur.
    List<CPoint> BuildRing(RRect r)
    {
        List<CPoint> list = new List<CPoint>();
        for (int k = 0; k < 4; k++) AppendCorner(list, r, k, false, 1f, false);
        return list;
    }

    // Contour d'un trou, sens horaire (A, D, C, B). Normales vers le vide du trou.
    List<CPoint> BuildHoleRing(RRect r)
    {
        List<CPoint> list = new List<CPoint>();
        int[] order = { 0, 3, 2, 1 };
        foreach (int k in order) AppendCorner(list, r, k, true, -1f, true);
        return list;
    }

    // Contour du mur a la profondeur z, entaille par les ouvertures qui mangent un bord et existent a cette profondeur.
    // En longeant un cote (sens trigo), on contourne chaque entaille (sens horaire autour d'elle) dans l'ordre du parcours.
    List<CPoint> BuildNotchedRing(RRect wall, List<OpeningGeo> notches, float z)
    {
        List<CPoint> list = new List<CPoint>();
        for (int k = 0; k < 4; k++)
        {
            AppendCorner(list, wall, k, false, 1f, false);

            List<OpeningGeo> onSide = notches.FindAll(g => g.side == k);
            // Ordre du parcours : bas vers +X, droite vers +Y, haut vers -X, gauche vers -Y
            onSide.Sort((a, b) =>
            {
                float ka = k == 0 || k == 2 ? a.hole.minX : a.hole.minY;
                float kb = k == 0 || k == 2 ? b.hole.minX : b.hole.minY;
                return k <= 1 ? ka.CompareTo(kb) : kb.CompareTo(ka);
            });

            int[] notch = { k, (k + 3) % 4, (k + 2) % 4, (k + 1) % 4 };
            foreach (OpeningGeo g in onSide)
            {
                RRect r = g.At(z);
                // Le dernier coin de l'entaille est sur le bord du mur : le segment qui en part est de la tranche exterieure
                for (int i = 0; i < 4; i++)
                    AppendCorner(list, r, notch[i], true, -1f, i < 3);
            }
        }
        return list;
    }

    static List<Vector2> Positions(List<CPoint> ring)
    {
        List<Vector2> list = new List<Vector2>(ring.Count);
        foreach (CPoint cp in ring) list.Add(cp.p);
        return list;
    }

    // ---------------------------------------------------------------- Construction

    // Retient les ouvertures percables (dans le mur, sans chevauchement avec une ouverture precedente de la liste).
    List<OpeningGeo> CollectOpenings(RRect wall, float hz, List<string> warnings)
    {
        List<OpeningGeo> geos = new List<OpeningGeo>();
        if (openings == null) return geos;

        for (int i = 0; i < openings.Count; i++)
        {
            if (openings[i] == null) continue;
            List<Opening> copies = openings[i].Expand();
            for (int c = 0; c < copies.Count; c++)
            {
                string name = "Ouverture " + (i + 1) + (copies.Count > 1 ? ", copie " + (c + 1) : "");
                TryAddOpening(copies[c], copies.Count > 1, name, wall, hz, geos, warnings);
            }
        }
        return geos;
    }

    // Une ouverture simple (ou une copie d'une ouverture repetee) : retenue si elle tient dans le mur sans chevaucher.
    void TryAddOpening(Opening o, bool isCopy, string name, RRect wall, float hz, List<OpeningGeo> geos, List<string> warnings)
    {
        int side = o.EdgeSide;
        RRect hole = GetOpeningRect(wall, o, side);
        RRect face = GetSplayRect(o, hole, side, out bool hasSplay);

        // Une copie qui sort du mur serait ramenee dedans par le bornage : on la refuse plutot que de l'empiler
        bool pushedX = side != 1 && side != 3 && Mathf.Abs(hole.minX - (o.x - (hole.maxX - hole.minX) * 0.5f)) > 0.0001f;
        bool pushedY = side != 0 && side != 2 && Mathf.Abs(hole.minY - o.y) > 0.0001f;
        if (isCopy && (pushedX || pushedY))
        {
            warnings.Add(name + " : sort du mur, pas percée. Réduisez le nombre de copies ou l'écart.");
            return;
        }
        if (!OpeningFits(wall, hole, side))
        {
            warnings.Add(side >= 0
                ? name + " : tombe dans un arrondi du mur (ou en déborde), pas percée. Déplacez-la ou réduisez les rayons des coins du mur."
                : name + " : déborde du mur ou d'un de ses arrondis, pas percée. Déplacez-la, réduisez-la ou choisissez un bord à manger.");
            return;
        }
        if (hasSplay && !OpeningFits(wall, face, side))
        {
            warnings.Add(name + " : l'ébrasement déborde du mur ou d'un de ses arrondis, pas percée. Réduisez l'ébrasement ou éloignez l'ouverture du bord.");
            return;
        }
        if (geos.Exists(g => Overlaps(g.face, face)))
        {
            warnings.Add(name + " : chevauche une autre ouverture (ou en est à moins de 1 cm), pas percée.");
            return;
        }

        OpeningGeo geo = new OpeningGeo { side = side, hole = hole, face = face, isNiche = o.niche, back = o.backMaterial };
        float depth = Mathf.Clamp(o.nicheDepth, 0.005f, Thickness - 0.01f);
        if (!o.niche) { geo.zDug = -hz; geo.zBottom = hz; }
        else if (!o.nicheOnBack) { geo.zDug = -hz; geo.zBottom = -hz + depth; }
        else { geo.zDug = hz; geo.zBottom = hz - depth; }
        geos.Add(geo);
    }

    // Faces avant et arriere, tranche exterieure, interieur des ouvertures et fonds de niche.
    // Renvoie le nombre de parties (materiaux separes) : 2 mur plein, 3 avec ouvertures, 4 avec au moins une niche.
    // backs : fonds de palette utilises, un sous-mesh chacun apres les parties.
    int BuildMesh(Mesh mesh, out List<Material> backs)
    {
        RRect wall = GetWallRect();
        float hz = Thickness * 0.5f;

        List<string> warnings = new List<string>();
        List<OpeningGeo> geos = CollectOpenings(wall, hz, warnings);
        warning = warnings.Count > 0 ? string.Join("\n", warnings) : null;

        MeshData data = new MeshData { wall = wall, thickness = Thickness };

        // Faces avant (-Z) et arriere (+Z)
        AddWallFace(data, wall, geos, -hz, -1f);
        AddWallFace(data, wall, geos, hz, 1f);

        // Tranche exterieure, entailles des bords manges comprises, par tranches d'epaisseur : une entaille de niche
        // ne va que de la face creusee au fond de niche.
        List<float> levels = new List<float> { -hz, hz };
        foreach (OpeningGeo g in geos)
            if (g.isNiche && g.side >= 0) levels.Add(g.zBottom);
        levels.Sort();
        for (int i = 0; i + 1 < levels.Count; i++)
        {
            float za = levels[i], zb = levels[i + 1];
            if (zb - za < 0.00001f) continue;
            float mid = (za + zb) * 0.5f;
            List<OpeningGeo> notches = geos.FindAll(g => g.side >= 0 && g.Present(mid));
            data.AddLoft(BuildNotchedRing(wall, notches, za), za, BuildNotchedRing(wall, notches, zb), zb, false);
        }

        // Interieur des ouvertures dans le mur (droit, ou incline du fond a la face creusee)
        foreach (OpeningGeo g in geos)
            if (g.side < 0)
                data.AddLoft(BuildHoleRing(g.hole), g.zBottom, BuildHoleRing(g.face), g.zDug, true);

        // Fonds de niche, tournes vers la face creusee
        bool anyNiche = false;
        foreach (OpeningGeo g in geos)
        {
            if (!g.isNiche) continue;
            anyNiche = true;
            data.AddCap(BuildRing(g.hole), null, g.zBottom, Mathf.Sign(g.zDug - g.zBottom), data.BackTris(g.back));
        }

        int parts = anyNiche ? 4 : geos.Count > 0 ? 3 : 2;
        data.Apply(mesh, separateMaterials, parts);
        backs = data.backMats;
        return parts;
    }

    // Une face du mur en z : contour entaille par les bords manges, percee par les ouvertures interieures presentes a z.
    void AddWallFace(MeshData data, RRect wall, List<OpeningGeo> geos, float z, float nz)
    {
        List<OpeningGeo> present = geos.FindAll(g => g.Present(z));
        List<CPoint> outer = BuildNotchedRing(wall, present.FindAll(g => g.side >= 0), z);
        List<List<CPoint>> holes = new List<List<CPoint>>();
        foreach (OpeningGeo g in present)
            if (g.side < 0) holes.Add(BuildHoleRing(g.At(z)));
        data.AddCap(outer, holes, z, nz, data.capTris);
    }

    // Accumule sommets et triangles.
    class MeshData
    {
        public RRect wall;
        public float thickness;
        public List<Vector3> verts = new List<Vector3>();
        public List<Vector3> normals = new List<Vector3>();
        public List<Vector2> uv0 = new List<Vector2>();
        public List<Vector2> uv2 = new List<Vector2>();
        public List<int> capTris = new List<int>();
        public List<int> outerTris = new List<int>();
        public List<int> openTris = new List<int>();
        public List<int> plateTris = new List<int>();
        // Fonds de palette : un materiau = une liste de triangles (un sous-mesh), dans l'ordre d'apparition
        public List<Material> backMats = new List<Material>();
        public List<List<int>> backTris = new List<List<int>>();

        public List<int> BackTris(Material m)
        {
            if (m == null) return plateTris;
            int i = backMats.IndexOf(m);
            if (i < 0) { backMats.Add(m); backTris.Add(new List<int>()); i = backMats.Count - 1; }
            return backTris[i];
        }

        void AddVertex(Vector2 p, float z, Vector3 n, Vector2 uvA, Vector2 uvB)
        {
            verts.Add(new Vector3(p.x, p.y, z));
            normals.Add(n);
            uv0.Add(uvA);
            uv2.Add(uvB);
        }

        // Face plate en z : contour (sens trigo) eventuellement perce de trous (sens horaire), triangulee par oreilles.
        public void AddCap(List<CPoint> outer, List<List<CPoint>> holes, float z, float nz, List<int> tris)
        {
            List<Vector2> poly = Positions(outer);
            if (holes != null && holes.Count > 0)
            {
                List<List<Vector2>> holePts = new List<List<Vector2>>();
                foreach (List<CPoint> h in holes) holePts.Add(Positions(h));
                poly = MergeHoles(poly, holePts);
            }
            List<int> local = new List<int>();
            Triangulate(poly, local);

            Vector3 n = new Vector3(0f, 0f, nz);
            // u croit vers la droite de celui qui regarde la face : vers -X pour une face tournee vers +Z
            bool facesPlusZ = nz > 0f;
            float W = wall.maxX - wall.minX, H = wall.maxY - wall.minY;
            int start = verts.Count;
            foreach (Vector2 p in poly)
            {
                float u = facesPlusZ ? wall.maxX - p.x : p.x - wall.minX;
                float v = p.y - wall.minY;
                AddVertex(p, z, n, new Vector2(u, v), new Vector2(u / W, v / H));
            }
            for (int i = 0; i < local.Count; i += 3)
                AddTri(verts, tris, start + local[i], start + local[i + 1], start + local[i + 2], n);
        }

        // Relie point a point deux contours de meme structure (ringA en zA, ringB en zB) : tranche droite si les deux
        // contours sont identiques, ebrasement sinon. Les points du mur communs aux deux contours donnent des quads
        // verticaux (tranche exterieure), ceux d'une ouverture qui s'elargit des quads inclines.
        // Normales lisses sur les arrondis, nettes ailleurs.
        public void AddLoft(List<CPoint> ringA, float zA, List<CPoint> ringB, float zB, bool allOpening)
        {
            int n = ringA.Count;
            if (ringB.Count != n)
            {
                Debug.LogWarning("[PerforatedWall] Contours de tailles differentes, tranche ignoree.");
                return;
            }
            float perimeter = 0f;
            for (int i = 0; i < n; i++) perimeter += Vector2.Distance(ringA[i].p, ringA[(i + 1) % n].p);
            perimeter = Mathf.Max(0.0001f, perimeter);

            float dist = 0f;
            for (int i = 0; i < n; i++)
            {
                CPoint a0 = ringA[i], a1 = ringA[(i + 1) % n], b0 = ringB[i], b1 = ringB[(i + 1) % n];
                Vector2 d = a1.p - a0.p;
                if (d.sqrMagnitude < 1e-12f) d = b1.p - b0.p;
                float len = d.magnitude;
                if (len < 0.000001f) continue;
                Vector2 segN = new Vector2(d.y, -d.x) / len;

                Vector3 p0 = new Vector3(a0.p.x, a0.p.y, zA), p1 = new Vector3(a1.p.x, a1.p.y, zA);
                Vector3 p2 = new Vector3(b1.p.x, b1.p.y, zB), p3 = new Vector3(b0.p.x, b0.p.y, zB);
                // Normale du quad (par ses diagonales : valable aussi pour un quad reduit a un triangle),
                // orientee comme la normale du segment (hors de la matiere)
                Vector3 flat = Vector3.Cross(p2 - p0, p3 - p1);
                Vector3 hint = new Vector3(segN.x, segN.y, 0f);
                if (flat.sqrMagnitude < 1e-14f) flat = hint;
                if (Vector3.Dot(flat, hint) < 0f) flat = -flat;
                flat.Normalize();

                float aLen = Vector2.Distance(a0.p, a1.p);
                float v0 = zA + thickness * 0.5f, v1 = zB + thickness * 0.5f;
                int start = verts.Count;
                AddVertex(a0.p, zA, LoftNormal(a0, b0, zB - zA, flat), new Vector2(dist, v0), new Vector2(dist / perimeter, v0 / thickness));
                AddVertex(a1.p, zA, LoftNormal(a1, b1, zB - zA, flat), new Vector2(dist + aLen, v0), new Vector2((dist + aLen) / perimeter, v0 / thickness));
                AddVertex(b1.p, zB, LoftNormal(a1, b1, zB - zA, flat), new Vector2(dist + aLen, v1), new Vector2((dist + aLen) / perimeter, v1 / thickness));
                AddVertex(b0.p, zB, LoftNormal(a0, b0, zB - zA, flat), new Vector2(dist, v1), new Vector2(dist / perimeter, v1 / thickness));
                AddQuad(verts, allOpening || a0.openingSeg ? openTris : outerTris, start, start + 1, start + 2, start + 3, flat);
                dist += aLen;
            }
        }

        // Sur un arrondi : perpendiculaire a la pente (de a en zA vers b en zB) et a l'arrondi, du cote de la normale du contour.
        // Ailleurs (coin vif, segment droit) : la normale plate du quad.
        static Vector3 LoftNormal(CPoint a, CPoint b, float dz, Vector3 flat)
        {
            if (a.n.sqrMagnitude <= 0f) return flat;
            float s = dz < 0f ? -1f : 1f;
            float spread = Vector2.Dot(b.p - a.p, a.n);
            return new Vector3(a.n.x * dz * s, a.n.y * dz * s, -spread * s).normalized;
        }

        // parts = nombre de parties : 2 (mur plein), 3 (ouvertures), 4 (niches : leur fond a part)
        public void Apply(Mesh mesh, bool separate, int parts)
        {
            mesh.Clear();
            mesh.indexFormat = verts.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uv0);
            mesh.SetUVs(2, uv2);
            int baseParts = separate ? parts : 1;
            mesh.subMeshCount = baseParts + backTris.Count;
            if (separate)
            {
                mesh.SetTriangles(capTris, 0);
                mesh.SetTriangles(outerTris, 1);
                if (parts > 2) mesh.SetTriangles(openTris, 2);
                if (parts > 3) mesh.SetTriangles(plateTris, 3);
            }
            else
            {
                List<int> all = new List<int>(capTris);
                all.AddRange(outerTris);
                all.AddRange(openTris);
                all.AddRange(plateTris);
                mesh.SetTriangles(all, 0);
            }
            for (int i = 0; i < backTris.Count; i++)
                mesh.SetTriangles(backTris[i], baseParts + i);
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
        }
    }

    // ---------------------------------------------------------------- Triangulation

    static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

    // Relie les trous un par un au contour, du plus a droite au plus a gauche : un seul polygone, triangulable par oreilles.
    // Dans cet ordre, tout ce qui est a droite d'un trou est deja dans le contour.
    static List<Vector2> MergeHoles(List<Vector2> outer, List<List<Vector2>> holes)
    {
        List<List<Vector2>> sorted = new List<List<Vector2>>(holes);
        sorted.Sort((a, b) => MaxX(b).CompareTo(MaxX(a)));
        List<Vector2> poly = outer;
        foreach (List<Vector2> hole in sorted) poly = MergeHole(poly, hole);
        return poly;
    }

    static float MaxX(List<Vector2> loop)
    {
        float m = float.MinValue;
        foreach (Vector2 p in loop) m = Mathf.Max(m, p.x);
        return m;
    }

    // Pont : depuis le point le plus a droite du trou, un rayon horizontal vers la droite ; le pont va jusqu'au premier
    // bord touche (point ajoute sur ce bord). Rien ne peut croiser ce pont, et deux ponts ne partagent jamais un point.
    static List<Vector2> MergeHole(List<Vector2> outer, List<Vector2> hole)
    {
        int hi = 0;
        for (int i = 1; i < hole.Count; i++)
            if (hole[i].x > hole[hi].x) hi = i;
        Vector2 m = hole[hi];

        float best = float.MaxValue;
        int edge = -1;
        for (int i = 0; i < outer.Count; i++)
        {
            Vector2 p = outer[i], q = outer[(i + 1) % outer.Count];
            if ((p.y > m.y) == (q.y > m.y)) continue;
            float x = p.x + (m.y - p.y) * (q.x - p.x) / (q.y - p.y);
            if (x < m.x - 0.000001f || x >= best) continue;
            best = x;
            edge = i;
        }
        if (edge < 0)
        {
            Debug.LogWarning("[PerforatedWall] Trou sans bord a sa droite : non relie, face non percee.");
            return outer;
        }

        List<Vector2> loop = new List<Vector2>(hole.Count + 1);
        for (int i = 0; i <= hole.Count; i++) loop.Add(hole[(hi + i) % hole.Count]);

        // Le rayon touche un sommet existant : on s'y accroche ; sinon on coupe le bord au point touche
        int next = (edge + 1) % outer.Count;
        Vector2 hit = new Vector2(best, m.y);
        int at = edge;
        bool insert = true;
        if (Same(hit, outer[edge])) insert = false;
        else if (Same(hit, outer[next])) { at = next; insert = false; }
        if (!insert) hit = outer[at];

        List<Vector2> merged = new List<Vector2>(outer.Count + hole.Count + 3);
        for (int j = 0; j <= at; j++) merged.Add(outer[j]);
        if (insert) merged.Add(hit);
        merged.AddRange(loop);
        merged.Add(hit);
        for (int j = at + 1; j < outer.Count; j++) merged.Add(outer[j]);
        return merged;
    }

    static bool Same(Vector2 a, Vector2 b) => (a - b).sqrMagnitude < 1e-12f;

    // Triangulation par oreilles d'un polygone simple en sens trigo (les points doubles des ponts sont toleres).
    static void Triangulate(List<Vector2> poly, List<int> result)
    {
        List<int> idx = new List<int>(poly.Count);
        for (int i = 0; i < poly.Count; i++) idx.Add(i);

        int guard = poly.Count * poly.Count + 10;
        while (idx.Count > 3 && guard-- > 0)
        {
            bool clipped = false;
            for (int k = 0; k < idx.Count; k++)
            {
                int ia = idx[(k + idx.Count - 1) % idx.Count], ib = idx[k], ic = idx[(k + 1) % idx.Count];
                Vector2 a = poly[ia], b = poly[ib], c = poly[ic];
                float cross = Cross(b - a, c - b);
                if (Mathf.Abs(cross) < 1e-10f)
                {
                    // Point aligne (ou pointe d'aire nulle) : on le retire sans triangle
                    idx.RemoveAt(k);
                    clipped = true;
                    break;
                }
                if (cross < 0f) continue; // angle rentrant

                bool blocked = false;
                foreach (int j in idx)
                {
                    if (j == ia || j == ib || j == ic) continue;
                    Vector2 p = poly[j];
                    if (Same(p, a) || Same(p, b) || Same(p, c)) continue;
                    if (Cross(b - a, p - a) >= -1e-9f && Cross(c - b, p - b) >= -1e-9f && Cross(a - c, p - c) >= -1e-9f)
                    {
                        blocked = true;
                        break;
                    }
                }
                if (blocked) continue;

                result.Add(ia); result.Add(ib); result.Add(ic);
                idx.RemoveAt(k);
                clipped = true;
                break;
            }
            if (!clipped) break; // polygone degenere : on garde ce qui est triangule
        }
        if (idx.Count == 3 && Mathf.Abs(Cross(poly[idx[1]] - poly[idx[0]], poly[idx[2]] - poly[idx[1]])) >= 1e-10f)
        {
            result.Add(idx[0]); result.Add(idx[1]); result.Add(idx[2]);
        }
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
    // Numero de chaque ouverture (s'il y en a plusieurs) et lettres A-D des coins non lies, sur la face creusee.
    void OnDrawGizmosSelected()
    {
        RRect wall = GetWallRect();
        float hz = Thickness * 0.5f;
        string[] names = { "A", "B", "C", "D" };

        if (!linkCorners)
            for (int k = 0; k < 4; k++)
                Label(CornerPoint(wall, k), -hz, names[k]);

        if (openings == null) return;
        for (int i = 0; i < openings.Count; i++)
        {
            if (openings[i] == null) continue;
            Opening o = openings[i].Expand()[0]; // une ouverture repetee : etiquettes sur la premiere copie
            float z = o.niche && o.nicheOnBack ? hz : -hz;
            RRect hole = GetOpeningRect(wall, o, o.EdgeSide);
            Vector2 center = new Vector2((hole.minX + hole.maxX) * 0.5f, (hole.minY + hole.maxY) * 0.5f);
            if (openings.Count > 1) Label(center, z, (i + 1).ToString());
            if (o.linkCorners) continue;
            for (int k = 0; k < 4; k++)
            {
                // Un peu vers l'interieur de l'ouverture, pour ne pas chevaucher les lettres du mur
                Vector2 p = Vector2.Lerp(CornerPoint(hole, k), center, 0.15f);
                Label(p, z, names[k]);
            }
        }
    }

    void Label(Vector2 p, float z, string text)
    {
        Vector3 world = transform.TransformPoint(new Vector3(p.x, p.y, z));
        UnityEditor.Handles.Label(world, text, UnityEditor.EditorStyles.boldLabel);
    }
#endif
}

#if UNITY_EDITOR
// Inspecteur : n'affiche que les reglages utiles, liste des ouvertures repliables, et previent quand une ouverture
// ne peut pas etre percee.
[UnityEditor.CustomEditor(typeof(PerforatedWall))]
[UnityEditor.CanEditMultipleObjects]
public class PerforatedWallEditor : UnityEditor.Editor
{
    // Bouton Centrer clique pendant ce dessin : index de l'ouverture (-1 = aucun) et sens
    int center = -1;
    bool centerY;
    // Bouton Nouveau tirage clique : index de l'ouverture (-1 = aucun)
    int reroll = -1;

    // Libelles affiches dans l'inspecteur (les noms des champs restent en anglais dans le code)
    static readonly Dictionary<string, string> Labels = new Dictionary<string, string>
    {
        { "size", "Taille" },
        { "linkCorners", "Coins liés" },
        { "radius", "Rayon" },
        { "radiusA", "Rayon A" }, { "radiusB", "Rayon B" }, { "radiusC", "Rayon C" }, { "radiusD", "Rayon D" },
        { "cornerSegments", "Segments par arrondi" },
        { "edge", "Bord mangé" },
        { "x", "Position X" },
        { "y", "Position Y" },
        { "width", "Largeur" },
        { "height", "Hauteur" },
        { "niche", "Niche" },
        { "nicheDepth", "Profondeur" },
        { "nicheOnBack", "Niche à l'arrière" },
        { "backMaterials", "Fonds (palette)" },
        { "backMode", "Répartition" },
        { "splay", "Ébrasement" },
        { "linkSplay", "Ébrasement lié" },
        { "splaySize", "Élargissement" },
        { "splayTop", "Haut" }, { "splayBottom", "Bas" }, { "splayLeft", "Gauche" }, { "splayRight", "Droite" },
        { "repeat", "Répéter" },
        { "countX", "Nombre en X" }, { "countY", "Nombre en Y" },
        { "gapX", "Écart X" }, { "gapY", "Écart Y" },
        { "separateMaterials", "Matériaux séparés" },
    };

    static readonly string[] OpeningFields =
    {
        "edge", "x", "y", "width", "height",
        "linkCorners", "radius", "radiusA", "radiusB", "radiusC", "radiusD",
        "niche", "nicheDepth", "nicheOnBack", "backMaterials", "backMode",
        "splay", "linkSplay", "splaySize", "splayTop", "splayBottom", "splayLeft", "splayRight",
        "repeat", "countX", "countY", "gapX", "gapY",
    };

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        PerforatedWall wall = (PerforatedWall)target;
        List<string> hidden = new List<string>();

        if (wall.linkCorners)
            hidden.AddRange(new[] { "radiusA", "radiusB", "radiusC", "radiusD" });
        else
            hidden.Add("radius");

        int addAt = -1, duplicate = -1, remove = -1;
        center = -1;
        reroll = -1;

        // Comme DrawPropertiesExcluding, avec les libelles en francais et la liste des ouvertures dessinee a part
        UnityEditor.SerializedProperty prop = serializedObject.GetIterator();
        bool enterChildren = true;
        while (prop.NextVisible(enterChildren))
        {
            enterChildren = false;
            if (prop.name == "m_Script" || hidden.Contains(prop.name)) continue;
            if (prop.name == "openings")
            {
                DrawOpenings(prop, ref addAt, ref duplicate, ref remove);
                continue;
            }
            UnityEditor.EditorGUILayout.PropertyField(prop, new GUIContent(Label(prop), prop.tooltip), true);
            if (prop.name == "separateMaterials") DrawMaterialSlots(wall);
        }
        serializedObject.ApplyModifiedProperties();

        // Ajout, duplication, suppression : directement sur les composants (valeurs par defaut d'une ouverture neuve)
        if (addAt >= 0 || duplicate >= 0 || remove >= 0)
        {
            UnityEditor.Undo.RecordObjects(targets, "Ouvertures du mur");
            foreach (Object t in targets)
            {
                PerforatedWall w = (PerforatedWall)t;
                if (remove >= 0 && remove < w.openings.Count)
                    w.openings.RemoveAt(remove);
                else if (duplicate >= 0 && duplicate < w.openings.Count)
                {
                    // La copie est decalee a cote de l'original (sinon elle le chevaucherait)
                    PerforatedWall.Opening copy = w.openings[duplicate].Clone();
                    // (de tout le groupe pour une ouverture repetee)
                    if (copy.edge == PerforatedWall.OpeningEdge.Gauche || copy.edge == PerforatedWall.OpeningEdge.Droite)
                        copy.y += copy.CopiesY * (copy.height + copy.gapY) - copy.gapY + 0.3f;
                    else
                        copy.x += copy.CopiesX * (copy.width + copy.gapX) - copy.gapX + 0.3f;
                    w.openings.Insert(duplicate + 1, copy);
                }
                else if (addAt >= 0)
                    w.openings.Add(new PerforatedWall.Opening());
                w.Rebuild();
                UnityEditor.EditorUtility.SetDirty(w);
            }
            serializedObject.Update();
        }

        // Nouveau tirage des fonds : nouvelle graine
        if (reroll >= 0)
        {
            UnityEditor.Undo.RecordObjects(targets, "Nouveau tirage des fonds");
            foreach (Object t in targets)
            {
                PerforatedWall w = (PerforatedWall)t;
                if (reroll >= w.openings.Count) continue;
                w.openings[reroll].backSeed = Random.Range(1, int.MaxValue);
                w.Rebuild();
                UnityEditor.EditorUtility.SetDirty(w);
            }
            serializedObject.Update();
        }

        // Centrer : position recalculee sur chaque mur selectionne (sa propre taille)
        if (center >= 0)
        {
            UnityEditor.Undo.RecordObjects(targets, centerY ? "Centrer en Y" : "Centrer en X");
            foreach (Object t in targets)
            {
                PerforatedWall w = (PerforatedWall)t;
                if (center >= w.openings.Count) continue;
                if (centerY) w.openings[center].CenterY(w.size.y);
                else w.openings[center].CenterX();
                w.Rebuild();
                UnityEditor.EditorUtility.SetDirty(w);
            }
            serializedObject.Update();
        }

        if (!string.IsNullOrEmpty(wall.warning))
            UnityEditor.EditorGUILayout.HelpBox(wall.warning, UnityEditor.MessageType.Warning);
    }

    static readonly string[] SlotNames = { "Faces", "Tranche extérieure", "Intérieur des ouvertures", "Fonds de niche" };

    // Slots du MeshRenderer sous leur nom (modifies directement sur le renderer) ; fonds de palette en lecture seule
    void DrawMaterialSlots(PerforatedWall wall)
    {
        MeshRenderer mr = wall.GetComponent<MeshRenderer>();
        if (mr == null) return;
        UnityEditor.EditorGUI.indentLevel++;
        if (targets.Length > 1)
        {
            UnityEditor.EditorGUILayout.HelpBox("Plusieurs murs sélectionnés : régler les matériaux mur par mur (ou sur le MeshRenderer).", UnityEditor.MessageType.None);
            UnityEditor.EditorGUI.indentLevel--;
            return;
        }

        Material[] mats = mr.sharedMaterials;
        int count = Mathf.Min(wall.separateMaterials ? wall.lastParts : 1, mats.Length);
        for (int i = 0; i < count; i++)
        {
            string label = wall.separateMaterials ? SlotNames[Mathf.Min(i, SlotNames.Length - 1)] : "Matériau";
            UnityEditor.EditorGUI.BeginChangeCheck();
            Material m = (Material)UnityEditor.EditorGUILayout.ObjectField(label, mats[i], typeof(Material), false);
            if (UnityEditor.EditorGUI.EndChangeCheck())
            {
                UnityEditor.Undo.RecordObject(mr, "Matériau du mur");
                mats[i] = m;
                mr.sharedMaterials = mats;
            }
        }

        if (wall.lastBacks != null && wall.lastBacks.Count > 0)
        {
            List<string> names = wall.lastBacks.ConvertAll(b => b != null ? b.name : "?");
            UnityEditor.EditorGUILayout.LabelField("Fonds de palette", string.Join(", ", names) + " (réglés dans les ouvertures)",
                UnityEditor.EditorStyles.wordWrappedMiniLabel);
        }
        UnityEditor.EditorGUI.indentLevel--;
    }

    static string Label(UnityEditor.SerializedProperty prop) =>
        Labels.TryGetValue(prop.name, out string fr) ? fr : prop.displayName;

    void DrawOpenings(UnityEditor.SerializedProperty list, ref int addAt, ref int duplicate, ref int remove)
    {
        UnityEditor.EditorGUILayout.Space(8);
        UnityEditor.EditorGUILayout.LabelField("Ouvertures", UnityEditor.EditorStyles.boldLabel);

        for (int i = 0; i < list.arraySize; i++)
        {
            UnityEditor.SerializedProperty o = list.GetArrayElementAtIndex(i);
            UnityEditor.EditorGUILayout.BeginHorizontal();
            o.isExpanded = UnityEditor.EditorGUILayout.Foldout(o.isExpanded, "Ouverture " + (i + 1) + " — " + Kind(o), true);
            if (GUILayout.Button("Dupliquer", GUILayout.Width(75))) duplicate = i;
            if (GUILayout.Button("Supprimer", GUILayout.Width(75))) remove = i;
            UnityEditor.EditorGUILayout.EndHorizontal();
            if (!o.isExpanded) continue;

            List<string> hidden = HiddenOpeningFields(o);
            UnityEditor.EditorGUI.indentLevel++;
            foreach (string name in OpeningFields)
            {
                if (hidden.Contains(name)) continue;
                if (name == "linkCorners" || name == "niche" || name == "splay" || name == "repeat") UnityEditor.EditorGUILayout.Space(2);
                UnityEditor.SerializedProperty p = o.FindPropertyRelative(name);
                UnityEditor.EditorGUILayout.PropertyField(p, new GUIContent(Label(p), p.tooltip), true);
                if (name == "backMode" && p.enumValueIndex == (int)PerforatedWall.BackMode.Aleatoire)
                {
                    UnityEditor.EditorGUILayout.BeginHorizontal();
                    GUILayout.Space(UnityEditor.EditorGUI.indentLevel * 15f);
                    if (GUILayout.Button(new GUIContent("Nouveau tirage", "Tire a nouveau un materiau de fond pour chaque copie.")))
                        reroll = i;
                    UnityEditor.EditorGUILayout.EndHorizontal();
                }
            }
            if (o.FindPropertyRelative("repeat").boolValue)
            {
                bool canX = !hidden.Contains("countX"), canY = !hidden.Contains("countY");
                UnityEditor.EditorGUILayout.BeginHorizontal();
                GUILayout.Space(UnityEditor.EditorGUI.indentLevel * 15f);
                if (canX && GUILayout.Button(new GUIContent("Centrer en X", "Centre la trame sur la largeur du mur (ebrasement compris). Nombre et ecart inchanges.")))
                { center = i; centerY = false; }
                if (canY && GUILayout.Button(new GUIContent("Centrer en Y", "Centre la trame sur la hauteur du mur (ebrasement compris). Nombre et ecart inchanges.")))
                { center = i; centerY = true; }
                UnityEditor.EditorGUILayout.EndHorizontal();
            }
            UnityEditor.EditorGUI.indentLevel--;
            UnityEditor.EditorGUILayout.Space(4);
        }

        if (GUILayout.Button("Ajouter une ouverture")) addAt = list.arraySize;
        UnityEditor.EditorGUILayout.Space(4);
    }

    static string Kind(UnityEditor.SerializedProperty o)
    {
        string kind = o.FindPropertyRelative("niche").boolValue ? "niche"
            : o.FindPropertyRelative("edge").enumValueIndex == (int)PerforatedWall.OpeningEdge.Bas ? "porte" : "fenêtre";
        if (!o.FindPropertyRelative("repeat").boolValue) return kind;
        PerforatedWall.OpeningEdge edge = (PerforatedWall.OpeningEdge)o.FindPropertyRelative("edge").enumValueIndex;
        bool vertical = edge == PerforatedWall.OpeningEdge.Gauche || edge == PerforatedWall.OpeningEdge.Droite;
        bool horizontal = edge == PerforatedWall.OpeningEdge.Bas || edge == PerforatedWall.OpeningEdge.Haut;
        int nx = vertical ? 1 : o.FindPropertyRelative("countX").intValue;
        int ny = horizontal ? 1 : o.FindPropertyRelative("countY").intValue;
        return kind + " × " + (nx * ny);
    }

    static List<string> HiddenOpeningFields(UnityEditor.SerializedProperty o)
    {
        List<string> hidden = new List<string>();
        PerforatedWall.OpeningEdge edge = (PerforatedWall.OpeningEdge)o.FindPropertyRelative("edge").enumValueIndex;

        if (edge == PerforatedWall.OpeningEdge.Gauche || edge == PerforatedWall.OpeningEdge.Droite) hidden.Add("x");
        if (edge == PerforatedWall.OpeningEdge.Bas || edge == PerforatedWall.OpeningEdge.Haut) hidden.Add("y");

        if (o.FindPropertyRelative("linkCorners").boolValue)
            hidden.AddRange(new[] { "radiusA", "radiusB", "radiusC", "radiusD" });
        else
            hidden.Add("radius");

        if (!o.FindPropertyRelative("niche").boolValue)
            hidden.AddRange(new[] { "nicheDepth", "nicheOnBack", "backMaterials", "backMode" });
        else if (o.FindPropertyRelative("backMaterials").arraySize == 0)
            hidden.Add("backMode");

        if (!o.FindPropertyRelative("splay").boolValue)
            hidden.AddRange(new[] { "linkSplay", "splaySize", "splayTop", "splayBottom", "splayLeft", "splayRight" });
        else if (o.FindPropertyRelative("linkSplay").boolValue)
            hidden.AddRange(new[] { "splayTop", "splayBottom", "splayLeft", "splayRight" });
        else
        {
            hidden.Add("splaySize");
            // Le cote d'un bord mange reste droit
            switch (edge)
            {
                case PerforatedWall.OpeningEdge.Bas: hidden.Add("splayBottom"); break;
                case PerforatedWall.OpeningEdge.Haut: hidden.Add("splayTop"); break;
                case PerforatedWall.OpeningEdge.Gauche: hidden.Add("splayLeft"); break;
                case PerforatedWall.OpeningEdge.Droite: hidden.Add("splayRight"); break;
            }
        }

        if (!o.FindPropertyRelative("repeat").boolValue)
            hidden.AddRange(new[] { "countX", "countY", "gapX", "gapY" });
        else if (edge == PerforatedWall.OpeningEdge.Bas || edge == PerforatedWall.OpeningEdge.Haut)
            hidden.AddRange(new[] { "countY", "gapY" });
        else if (edge == PerforatedWall.OpeningEdge.Gauche || edge == PerforatedWall.OpeningEdge.Droite)
            hidden.AddRange(new[] { "countX", "gapX" });
        return hidden;
    }
}
#endif
