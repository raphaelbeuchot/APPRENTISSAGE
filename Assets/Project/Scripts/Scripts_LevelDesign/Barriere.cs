using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;

// Barriere : poteaux + lisses (barres horizontales), une lisse par travee (entre deux poteaux), plus une lisse
// d'extremite avant le premier et apres le dernier poteau (depassements reglables separement).
// Lisse filante (ex. main courante) : chaque morceau va d'axe de poteau a axe de poteau, les morceaux se touchent
// et passent sur / a travers les poteaux, sans trou. Non filante : d'une face de poteau a l'autre.
// Usage : poser sur un GameObject, regler, la barriere se reconstruit en direct. Pieces generees (cube Unity mis
// a l'echelle, pas de mesh cree), vrais enfants dans Barriere_Generee, sauvegardes dans la scene mais reconstruits
// a chaque changement de reglage : ne pas les retoucher a la main.
// Lisse d'extremite : son X local pointe toujours vers l'exterieur (cote "bout" pour un futur prefab).
//
// Source Libre : trace droit le long de X local, centre sur le pivot, longueur hors tout. Poser sur un GO vide
// (passe sur le layer Obstacle a l'ajout). Collision : une BoxCollider sur ce GO, geree par le composant.
// Garder le Scale du Transform a 1.
// Source Aretes : poser sur un mesh (plateforme, cylindre...), cocher les aretes vives a garnir (comme EdgeLights).
// Poteaux repartis le long de l'arete, un poteau force a chaque vrai coin ; lisses droites d'un poteau au suivant
// (facettes sur une courbe). Boucle entiere : refermee, sans depassements. Le dossier genere compense l'echelle du
// mesh : les pieces gardent leurs vraies dimensions. Pieces et collisions (une boite par travee, sur des enfants)
// sur le layer Obstacle ; le collider du mesh lui-meme n'est pas touche. Etape A : barriere centree sur l'arete.
[SelectionBase]
public class Barriere : MonoBehaviour
{
    public const string ContainerName = "Barriere_Generee";
    // Virage au-dela duquel un sommet de l'arete est un vrai coin (poteau force).
    const float coinMin = 30f;

    public enum Source { Libre, Aretes }

    [System.Serializable]
    public class Lisse
    {
        [Tooltip("Hauteur du dessus de la lisse depuis le sol (m) ; l'epaisseur se prend vers le bas. " +
                 "Jamais sous le sol : 0 = lisse posee au sol.")]
        public float hauteur = 0.5f;
        [Tooltip("Coche : lisse continue d'un bout a l'autre, sans trou aux poteaux (main courante).")]
        public bool filante = false;
    }

    [Header("Trace")]
    [Tooltip("Libre : barriere droite sur un GO vide. Aretes : le long des aretes vives cochees du mesh de ce GO.")]
    public Source source = Source.Libre;
    [Tooltip("Libre : longueur totale hors tout (m), depassements compris = longueur du collider. Le long de X local, " +
             "centree sur le pivot : une barriere de 10 posee sur un cube de 10 tombe pile a ses bords.")]
    [Min(0.1f)] public float longueur = 4f;
    [Tooltip("Aretes : angle mini entre deux faces voisines pour que leur arete compte comme vive.")]
    [Range(1f, 179f)] public float angleAretes = 30f;
    [Tooltip("Aretes, boucles seulement : portion garnie, en degres de tour. Part du +X local en tournant anti-horaire " +
             "vu du dessus : tourner le GO deplace l'arc. 360 = toute la boucle.")]
    [Range(1f, 360f)] public float arc = 360f;
    // Aretes : numeros des chaines cochees (liste dessinee par BarriereEditor).
    [HideInInspector] public List<int> chainesCochees = new List<int>();

    [Header("Poteaux")]
    [Tooltip("Espacement vise entre deux poteaux (m), ajuste pour tomber juste aux deux bouts (et aux coins).")]
    [Min(0.1f)] public float espacement = 1.5f;
    [Tooltip("Largeur des poteaux dans le sens de la barriere (X local, m).")]
    [Min(0.01f)] public float largeurPoteau = 0.1f;
    [Tooltip("Profondeur des poteaux, en travers de la barriere (Z local, m).")]
    [Min(0.01f)] public float profondeurPoteau = 0.1f;
    [Tooltip("Decalage des poteaux en profondeur (Z local, m) ; les lisses restent sur l'axe de la barriere. " +
             "Ex. (profondeur poteau + profondeur lisse) / 2 = lisses fixees contre la face avant des poteaux.")]
    public float decalagePoteaux = 0f;
    [Min(0.01f)] public float hauteurPoteau = 1f;

    [Header("Lisses")]
    public List<Lisse> lisses = new List<Lisse>
    {
        new Lisse { hauteur = 0.95f, filante = true },
        new Lisse { hauteur = 0.5f, filante = false }
    };
    [Tooltip("Decalage des lisses en profondeur (Z local, m). Meme valeur que le decalage des poteaux = toute la " +
             "barriere recule. Aretes : sur une boucle, + = vers l'interieur ; sur une chaine ouverte, le sens depend " +
             "de la chaine.")]
    public float decalageLisses = 0f;
    [Tooltip("Epaisseur verticale des lisses (m).")]
    [Min(0.01f)] public float epaisseurLisse = 0.1f;
    [Tooltip("Epaisseur des lisses en profondeur (m).")]
    [Min(0.01f)] public float profondeurLisse = 0.1f;
    [Tooltip("Depassement des lisses avant le premier poteau, depuis sa face (m). 0 = pas de lisse d'extremite " +
             "(une lisse filante s'arrete alors pile a la face du poteau). Ignore sur une boucle entiere.")]
    [Min(0f)] public float depassementDebut = 0f;
    [Tooltip("Depassement des lisses apres le dernier poteau, depuis sa face (m). 0 = pas de lisse d'extremite " +
             "(une lisse filante s'arrete alors pile a la face du poteau). Ignore sur une boucle entiere.")]
    [Min(0f)] public float depassementFin = 0f;

    [Header("Rendu")]
    [Tooltip("Materiau des pieces generees. Vide = materiau par defaut du pipeline.")]
    public Material materiau;

    [Header("Collision")]
    [Tooltip("Libre : une boite sur toute la barriere (BoxCollider de ce GO). Aretes : une boite par travee.")]
    public bool collision = true;
    [Tooltip("Hauteur de la boite (m). 0 = le plus haut des poteaux et du dessus des lisses.")]
    [Min(0f)] public float hauteurCollision = 0f;
    [Tooltip("Epaisseur de la boite (m). 0 = toute la profondeur des poteaux et des lisses.")]
    [Min(0f)] public float epaisseurCollision = 0f;

    // Signature des reglages a la derniere construction : OnValidate est aussi appele au chargement, on ne
    // reconstruit que si quelque chose a change (sinon la scene serait modifiee a chaque ouverture).
    [SerializeField, HideInInspector] int builtSignature;
    // La BoxCollider de ce GO a ete creee par la source Libre (a retirer en passant en Aretes).
    [SerializeField, HideInInspector] bool colliderGere;

    // Distance entre les axes du premier et du dernier poteau : la longueur hors tout moins les depassements
    // et une largeur de poteau (deux demi-poteaux aux bouts).
    public float LongueurAxes => Mathf.Max(0.01f, longueur - largeurPoteau - depassementDebut - depassementFin);
    public int Travees => Mathf.Max(1, Mathf.RoundToInt(LongueurAxes / espacement));

#if UNITY_EDITOR
    // A l'ajout : sur un mesh, source Aretes (on ne touche ni a son layer ni a son collider) ;
    // sur un GO vide, source Libre et layer Obstacle.
    void Reset()
    {
        bool surMesh = GetComponent<MeshFilter>() != null;
        source = surMesh ? Source.Aretes : Source.Libre;
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this == null) return;
            int obstacle = LayerMask.NameToLayer("Obstacle");
            if (!surMesh && obstacle >= 0)
            {
                UnityEditor.Undo.RecordObject(gameObject, "Barriere layer");
                gameObject.layer = obstacle;
            }
            Rebuild();
        };
    }

    // Creer / detruire des objets pendant OnValidate provoque des avertissements : on decale d'une frame d'editeur.
    void OnValidate()
    {
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this != null && !Application.isPlaying) RebuildSiChange();
        };
    }

    // Aussi appele par l'Inspector a chaque affichage : un changement d'echelle du mesh (Aretes) n'appelle pas OnValidate.
    public void RebuildSiChange()
    {
        if (builtSignature == Signature() && transform.Find(ContainerName) != null) return;
        Rebuild();
    }

    public void Rebuild()
    {
        Transform old = transform.Find(ContainerName);
        if (old != null) DestroyImmediate(old.gameObject);

        Transform container = new GameObject(ContainerName).transform;
        container.SetParent(transform, false);

        Mesh cube = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
        Material mat = materiau != null ? materiau
            : GraphicsSettings.currentRenderPipeline != null ? GraphicsSettings.currentRenderPipeline.defaultMaterial : null;

        if (source == Source.Libre) BuildLibre(container, cube, mat);
        else BuildAretes(container, cube, mat);

        builtSignature = Signature();
        UnityEditor.EditorUtility.SetDirty(this);
        if (gameObject.scene.IsValid())
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
    }

    // ---------------------------------------------------------------- Source Libre

    void BuildLibre(Transform container, Mesh cube, Material mat)
    {
        int layer = gameObject.layer;
        container.gameObject.layer = layer;

        int travees = Travees;
        float axes = LongueurAxes;
        float pas = axes / travees;
        float demi = largeurPoteau * 0.5f;
        float x0 = -longueur * 0.5f + depassementDebut + demi; // axe du premier poteau

        for (int i = 0; i <= travees; i++)
            Piece(container, cube, mat, layer, "Poteau_" + i.ToString("D2"),
                  new Vector3(x0 + i * pas, hauteurPoteau * 0.5f, decalagePoteaux),
                  Quaternion.identity, new Vector3(largeurPoteau, hauteurPoteau, profondeurPoteau));

        Quaternion versDebut = Quaternion.Euler(0f, 180f, 0f);
        for (int r = 0; r < lisses.Count; r++)
        {
            Lisse l = lisses[r];
            float y = CentreLisse(l);
            // Filante : d'axe a axe, les extremites partent de l'axe. Sinon : de face a face, extremites depuis la face.
            float morceau = l.filante ? pas : pas - largeurPoteau;
            float retrait = l.filante ? 0f : demi;
            float debut = depassementDebut + (l.filante ? demi : 0f);
            float fin = depassementFin + (l.filante ? demi : 0f);
            string nom = "Lisse_" + r + "_";

            if (morceau > 0.001f)
                for (int b = 0; b < travees; b++)
                    Piece(container, cube, mat, layer, nom + b.ToString("D2"), new Vector3(x0 + (b + 0.5f) * pas, y, decalageLisses),
                          Quaternion.identity, new Vector3(morceau, epaisseurLisse, profondeurLisse));
            if (debut > 0.001f && (depassementDebut > 0.001f || l.filante))
                Piece(container, cube, mat, layer, nom + "Debut", new Vector3(x0 - retrait - debut * 0.5f, y, decalageLisses),
                      versDebut, new Vector3(debut, epaisseurLisse, profondeurLisse));
            if (fin > 0.001f && (depassementFin > 0.001f || l.filante))
                Piece(container, cube, mat, layer, nom + "Fin", new Vector3(x0 + axes + retrait + fin * 0.5f, y, decalageLisses),
                      Quaternion.identity, new Vector3(fin, epaisseurLisse, profondeurLisse));
        }

        UpdateColliderLibre();
    }

    void UpdateColliderLibre()
    {
        BoxCollider box = GetComponent<BoxCollider>();
        if (!collision)
        {
            if (box != null && colliderGere) DestroyImmediate(box);
            colliderGere = false;
            return;
        }
        if (box == null) box = gameObject.AddComponent<BoxCollider>();
        colliderGere = true;

        ColliderProfil(out float h, out float zc, out float epais);
        box.center = new Vector3(0f, h * 0.5f, zc);
        box.size = new Vector3(longueur, h, epais);
    }

    // Hauteur, centre en profondeur et epaisseur des boites de collision (reglages ou automatiques).
    void ColliderProfil(out float h, out float zc, out float epais)
    {
        h = hauteurCollision;
        if (h <= 0f)
        {
            h = hauteurPoteau;
            foreach (Lisse l in lisses) h = Mathf.Max(h, DessusLisse(l));
        }
        // Profondeur couverte par les poteaux (decales) et les lisses (sur l'axe)
        float zMin = Mathf.Min(decalagePoteaux - profondeurPoteau * 0.5f, decalageLisses - profondeurLisse * 0.5f);
        float zMax = Mathf.Max(decalagePoteaux + profondeurPoteau * 0.5f, decalageLisses + profondeurLisse * 0.5f);
        zc = (zMin + zMax) * 0.5f;
        epais = epaisseurCollision > 0f ? epaisseurCollision : zMax - zMin;
    }

    // ---------------------------------------------------------------- Source Aretes

    void BuildAretes(Transform container, Mesh cube, Material mat)
    {
        // La BoxCollider eventuellement creee en source Libre n'a plus lieu d'etre (celle du mesh n'est pas touchee).
        if (colliderGere)
        {
            BoxCollider box = GetComponent<BoxCollider>();
            if (box != null) DestroyImmediate(box);
            colliderGere = false;
        }

        int obstacle = LayerMask.NameToLayer("Obstacle");
        int layer = obstacle >= 0 ? obstacle : gameObject.layer;
        container.gameObject.layer = layer;

        // Compense l'echelle du mesh : dans le dossier, 1 unite = 1 metre, axes = ceux du GO.
        // Un point p du mesh (repere local) y vaut Scale(p, echelle).
        Vector3 scale = transform.lossyScale;
        container.localScale = new Vector3(1f / NonNul(scale.x), 1f / NonNul(scale.y), 1f / NonNul(scale.z));

        MeshFilter mf = GetComponent<MeshFilter>();
        if (mf == null || mf.sharedMesh == null) return;
        List<MeshEdgeChains.Chain> chains = MeshEdgeChains.Find(mf.sharedMesh, scale, angleAretes);

        foreach (int c in chainesCochees)
        {
            if (c < 0 || c >= chains.Count || chains[c].points.Count < 2) continue;
            var path = new EdgeChainPath(chains[c], scale, arc);
            var points = new List<Vector3>();
            foreach (float s in AbscissesPoteaux(path))
            {
                path.Point(path.closed ? Mathf.Repeat(s, path.Length) : s, out Vector3 p, out _);
                points.Add(Vector3.Scale(p, scale));
            }
            BuildTrace(container, cube, mat, layer, "C" + c + "_", points, path.closed);
        }
    }

    // Dessus de la lisse : jamais plus bas que son epaisseur (une lisse a 0 est posee au sol, pas enterree).
    float DessusLisse(Lisse l) => Mathf.Max(l.hauteur, epaisseurLisse);
    float CentreLisse(Lisse l) => DessusLisse(l) - epaisseurLisse * 0.5f;

    static float NonNul(float v) => Mathf.Abs(v) < 1e-4f ? 1e-4f : v;

    // Poteaux : un a chaque coin et aux bouts, espacement ajuste entre deux. Boucle entiere : abscisses croissantes
    // depuis le premier coin (peuvent depasser la longueur, ramenees modulo a l'appel), sans repeter le premier.
    List<float> AbscissesPoteaux(EdgeChainPath path)
    {
        float L = path.Length;
        var S = new List<float>();
        List<float> coins = path.Corners(coinMin);

        var bornes = new List<float>();
        if (path.closed)
        {
            bornes.AddRange(coins);
            if (bornes.Count == 0) bornes.Add(0f);
            bornes.Add(bornes[0] + L);
        }
        else
        {
            bornes.Add(0f);
            bornes.AddRange(coins);
            bornes.Add(L);
        }

        for (int k = 0; k + 1 < bornes.Count; k++)
        {
            float a = bornes[k], b = bornes[k + 1];
            int n = Mathf.Max(1, Mathf.RoundToInt((b - a) / espacement));
            for (int j = 0; j < n; j++) S.Add(a + j * (b - a) / n);
        }
        if (!path.closed) S.Add(L);
        return S;
    }

    // Une barriere le long d'une suite de poteaux (repere du dossier, en metres). Boucle : la derniere travee
    // relie le dernier poteau au premier, pas de depassements.
    void BuildTrace(Transform container, Mesh cube, Material mat, int layer, string prefixe, List<Vector3> P, bool boucle)
    {
        int n = P.Count;
        if (n < 2) return;
        Vector3 up = Vector3.up;
        float demi = largeurPoteau * 0.5f;

        // Poteaux, tournes selon la moyenne des travees voisines
        for (int k = 0; k < n; k++)
        {
            Vector3 prev = k > 0 ? P[k - 1] : (boucle ? P[n - 1] : P[k]);
            Vector3 next = k < n - 1 ? P[k + 1] : (boucle ? P[0] : P[k]);
            Quaternion rot = Repere(Vector3.ProjectOnPlane(next - prev, up), up);
            Piece(container, cube, mat, layer, prefixe + "Poteau_" + k.ToString("D2"),
                  P[k] + rot * new Vector3(0f, hauteurPoteau * 0.5f, decalagePoteaux),
                  rot, new Vector3(largeurPoteau, hauteurPoteau, profondeurPoteau));
        }

        ColliderProfil(out float hCol, out float zc, out float epais);
        int travees = boucle ? n : n - 1;
        for (int b = 0; b < travees; b++)
        {
            Vector3 A = P[b], B = P[(b + 1) % n];
            Vector3 d = B - A;
            float len = d.magnitude;
            if (len < 1e-4f) continue;
            Vector3 dir = d / len;
            Quaternion rot = Repere(dir, up);
            Vector3 mid = (A + B) * 0.5f;

            for (int r = 0; r < lisses.Count; r++)
            {
                Lisse l = lisses[r];
                float morceau = l.filante ? len : len - largeurPoteau;
                if (morceau > 0.001f)
                    Piece(container, cube, mat, layer, prefixe + "Lisse_" + r + "_" + b.ToString("D2"),
                          mid + up * CentreLisse(l) + rot * new Vector3(0f, 0f, decalageLisses), rot,
                          new Vector3(morceau, epaisseurLisse, profondeurLisse));
            }

            if (collision)
            {
                // Travee + demi-poteau de chaque cote (pas de fente aux coins), + depassements aux bouts d'un trace ouvert
                float avant = demi + (!boucle && b == 0 ? depassementDebut : 0f);
                float apres = demi + (!boucle && b == travees - 1 ? depassementFin : 0f);
                Vector3 centre = mid + dir * ((apres - avant) * 0.5f);
                Collision(container, layer, prefixe + "Collision_" + b.ToString("D2"),
                          centre + rot * new Vector3(0f, hCol * 0.5f, zc), rot, new Vector3(len + avant + apres, hCol, epais));
            }
        }

        if (boucle) return;
        // Decalage des lisses mesure en travers du sens de parcours (meme cote que les travees)
        Vector3 traversDebut = Repere(P[1] - P[0], up) * Vector3.forward;
        Vector3 traversFin = Repere(P[n - 1] - P[n - 2], up) * Vector3.forward;
        Extremite(container, cube, mat, layer, prefixe, "Debut", P[0], (P[0] - P[1]).normalized, traversDebut, depassementDebut, up);
        Extremite(container, cube, mat, layer, prefixe, "Fin", P[n - 1], (P[n - 1] - P[n - 2]).normalized, traversFin, depassementFin, up);
    }

    // Lisses d'extremite depuis le poteau P vers l'exterieur (X local de la piece = vers l'exterieur).
    void Extremite(Transform container, Mesh cube, Material mat, int layer, string prefixe, string bout,
                   Vector3 P, Vector3 dehors, Vector3 travers, float depassement, Vector3 up)
    {
        Quaternion rot = Repere(dehors, up);
        P += travers * decalageLisses;
        float demi = largeurPoteau * 0.5f;
        for (int r = 0; r < lisses.Count; r++)
        {
            Lisse l = lisses[r];
            if (!(depassement > 0.001f || l.filante)) continue;
            float taille = depassement + (l.filante ? demi : 0f);
            float depart = l.filante ? 0f : demi;
            if (taille <= 0.001f) continue;
            Piece(container, cube, mat, layer, prefixe + "Lisse_" + r + "_" + bout,
                  P + dehors * (depart + taille * 0.5f) + up * CentreLisse(l), rot,
                  new Vector3(taille, epaisseurLisse, profondeurLisse));
        }
    }

    // Rotation dont le X local suit dir (pente comprise) et le Y reste au plus pres de up.
    static Quaternion Repere(Vector3 dir, Vector3 up)
    {
        if (dir.sqrMagnitude < 1e-8f) return Quaternion.identity;
        dir.Normalize();
        Vector3 travers = Vector3.Cross(dir, up);
        if (travers.sqrMagnitude < 1e-8f) return Quaternion.FromToRotation(Vector3.right, dir); // arete verticale
        travers.Normalize();
        return Quaternion.LookRotation(travers, Vector3.Cross(travers, dir));
    }

    // ---------------------------------------------------------------- Pieces

    void Piece(Transform parent, Mesh mesh, Material mat, int layer, string nom, Vector3 pos, Quaternion rot, Vector3 taille)
    {
        GameObject go = new GameObject(nom);
        go.layer = layer;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = rot;
        go.transform.localScale = taille;
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = mat;
    }

    void Collision(Transform parent, int layer, string nom, Vector3 pos, Quaternion rot, Vector3 taille)
    {
        GameObject go = new GameObject(nom);
        go.layer = layer;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = rot;
        go.AddComponent<BoxCollider>().size = taille;
    }

    // Empreinte stable (FNV-1a) des reglages ; le materiau compte par son nom (stable d'une session a l'autre).
    // Aretes : l'echelle et le mesh comptent aussi (ils changent les aretes).
    int Signature()
    {
        var sb = new StringBuilder();
        void Add(float f) => sb.Append(f.ToString("R", CultureInfo.InvariantCulture)).Append('|');
        Add((int)source); Add(longueur); Add(espacement); Add(largeurPoteau); Add(profondeurPoteau); Add(decalagePoteaux); Add(decalageLisses);
        Add(hauteurPoteau);
        foreach (Lisse l in lisses) { Add(l.hauteur); Add(l.filante ? 1f : 0f); }
        sb.Append('#');
        Add(epaisseurLisse); Add(profondeurLisse); Add(depassementDebut); Add(depassementFin);
        Add(collision ? 1f : 0f); Add(hauteurCollision); Add(epaisseurCollision); Add(gameObject.layer);
        sb.Append(materiau != null ? materiau.name : "");
        if (source == Source.Aretes)
        {
            Add(angleAretes); Add(arc);
            foreach (int c in chainesCochees) Add(c);
            Vector3 s = transform.lossyScale;
            Add(s.x); Add(s.y); Add(s.z);
            MeshFilter mf = GetComponent<MeshFilter>();
            if (mf != null && mf.sharedMesh != null) { sb.Append(mf.sharedMesh.name); Add(mf.sharedMesh.vertexCount); }
        }

        unchecked
        {
            int hash = (int)2166136261;
            foreach (char c in sb.ToString()) hash = (hash ^ c) * 16777619;
            return hash;
        }
    }
#endif
}
