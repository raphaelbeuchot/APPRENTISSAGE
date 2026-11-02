using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;

// Barriere : poteaux + lisses (barres horizontales), une lisse par travee (entre deux poteaux), plus une lisse
// d'extremite avant le premier et apres le dernier poteau (depassements reglables separement).
// Lisse filante (ex. main courante) : chaque morceau va d'axe de poteau a axe de poteau, les morceaux se touchent
// et passent sur / a travers les poteaux, sans trou. Non filante : d'une face de poteau a l'autre.
// Usage : poser sur un GameObject vide, regler, la barriere se reconstruit en direct.
// Etape 1 : trace libre droit, le long de X local, centre sur le pivot ; pieces generees (cube Unity mis a l'echelle,
// pas de mesh cree). Les pieces sont de vrais enfants (dans Barriere_Generee), sauvegardes dans la scene, mais
// reconstruits a chaque changement de reglage : ne pas les retoucher a la main.
// Lisse d'extremite de debut tournee de 180 degres : son cote "bout" est toujours vers l'exterieur (prefab plus tard).
//
// Collision : une seule BoxCollider sur ce GO, geree par le composant, couvrant toute la barriere (depassements compris).
// A l'ajout, l'objet passe sur le layer Obstacle (vu par la grille A* des ennemis ; une barriere basse s'enjambe).
// Limite acceptee : garder le Scale du Transform a 1 (regler les tailles ici).
[SelectionBase]
public class Barriere : MonoBehaviour
{
    public const string ContainerName = "Barriere_Generee";

    [System.Serializable]
    public class Lisse
    {
        [Tooltip("Hauteur du dessus de la lisse depuis le sol (m) ; l'epaisseur se prend vers le bas.")]
        public float hauteur = 0.5f;
        [Tooltip("Coche : lisse continue d'un bout a l'autre, sans trou aux poteaux (main courante).")]
        public bool filante = false;
    }

    [Header("Trace")]
    [Tooltip("Longueur totale hors tout (m), depassements compris = longueur du collider. Le long de X local, " +
             "centree sur le pivot : une barriere de 10 posee sur un cube de 10 tombe pile a ses bords.")]
    [Min(0.1f)] public float longueur = 4f;

    [Header("Poteaux")]
    [Tooltip("Espacement vise entre deux poteaux (m), ajuste pour tomber juste aux deux bouts.")]
    [Min(0.1f)] public float espacement = 1.5f;
    [Tooltip("Largeur des poteaux dans le sens de la barriere (X local, m).")]
    [Min(0.01f)] public float largeurPoteau = 0.12f;
    [Tooltip("Profondeur des poteaux, en travers de la barriere (Z local, m).")]
    [Min(0.01f)] public float profondeurPoteau = 0.12f;
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
    [Tooltip("Epaisseur verticale des lisses (m).")]
    [Min(0.01f)] public float epaisseurLisse = 0.08f;
    [Tooltip("Epaisseur des lisses en profondeur (m).")]
    [Min(0.01f)] public float profondeurLisse = 0.06f;
    [Tooltip("Depassement des lisses avant le premier poteau, depuis sa face (m). 0 = pas de lisse d'extremite " +
             "(une lisse filante s'arrete alors pile a la face du poteau).")]
    [Min(0f)] public float depassementDebut = 0.15f;
    [Tooltip("Depassement des lisses apres le dernier poteau, depuis sa face (m). 0 = pas de lisse d'extremite " +
             "(une lisse filante s'arrete alors pile a la face du poteau).")]
    [Min(0f)] public float depassementFin = 0.15f;

    [Header("Rendu")]
    [Tooltip("Materiau des pieces generees. Vide = materiau par defaut du pipeline.")]
    public Material materiau;

    [Header("Collision")]
    [Tooltip("Boite unique sur toute la barriere. Decoche : la BoxCollider de ce GO est retiree.")]
    public bool collision = true;
    [Tooltip("Hauteur de la boite (m). 0 = le plus haut des poteaux et du dessus des lisses.")]
    [Min(0f)] public float hauteurCollision = 0f;
    [Tooltip("Epaisseur de la boite (m). 0 = toute la profondeur des poteaux et des lisses.")]
    [Min(0f)] public float epaisseurCollision = 0f;

    // Signature des reglages a la derniere construction : OnValidate est aussi appele au chargement, on ne
    // reconstruit que si quelque chose a change (sinon la scene serait modifiee a chaque ouverture).
    [SerializeField, HideInInspector] int builtSignature;

    // Distance entre les axes du premier et du dernier poteau : la longueur hors tout moins les depassements
    // et une largeur de poteau (deux demi-poteaux aux bouts).
    public float LongueurAxes => Mathf.Max(0.01f, longueur - largeurPoteau - depassementDebut - depassementFin);
    public int Travees => Mathf.Max(1, Mathf.RoundToInt(LongueurAxes / espacement));

#if UNITY_EDITOR
    void Reset()
    {
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this == null) return;
            int obstacle = LayerMask.NameToLayer("Obstacle");
            if (obstacle >= 0)
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
            if (this == null || Application.isPlaying) return;
            if (builtSignature == Signature() && transform.Find(ContainerName) != null) return;
            Rebuild();
        };
    }

    public void Rebuild()
    {
        Transform old = transform.Find(ContainerName);
        if (old != null) DestroyImmediate(old.gameObject);

        Transform container = new GameObject(ContainerName).transform;
        container.SetParent(transform, false);
        container.gameObject.layer = gameObject.layer;

        Mesh cube = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
        Material mat = materiau != null ? materiau
            : GraphicsSettings.currentRenderPipeline != null ? GraphicsSettings.currentRenderPipeline.defaultMaterial : null;

        int travees = Travees;
        float axes = LongueurAxes;
        float pas = axes / travees;
        float demi = largeurPoteau * 0.5f;
        float x0 = -longueur * 0.5f + depassementDebut + demi; // axe du premier poteau

        for (int i = 0; i <= travees; i++)
            Piece(container, cube, mat, "Poteau_" + i.ToString("D2"), new Vector3(x0 + i * pas, hauteurPoteau * 0.5f, decalagePoteaux),
                  Quaternion.identity, new Vector3(largeurPoteau, hauteurPoteau, profondeurPoteau));

        Quaternion versDebut = Quaternion.Euler(0f, 180f, 0f);
        for (int r = 0; r < lisses.Count; r++)
        {
            Lisse l = lisses[r];
            float y = l.hauteur - epaisseurLisse * 0.5f; // centre de la lisse
            // Filante : d'axe a axe, les extremites partent de l'axe. Sinon : de face a face, extremites depuis la face.
            float morceau = l.filante ? pas : pas - largeurPoteau;
            float retrait = l.filante ? 0f : demi;
            float debut = depassementDebut + (l.filante ? demi : 0f);
            float fin = depassementFin + (l.filante ? demi : 0f);
            string nom = "Lisse_" + r + "_";

            if (morceau > 0.001f)
                for (int b = 0; b < travees; b++)
                    Piece(container, cube, mat, nom + b.ToString("D2"), new Vector3(x0 + (b + 0.5f) * pas, y, 0f),
                          Quaternion.identity, new Vector3(morceau, epaisseurLisse, profondeurLisse));
            if (debut > 0.001f && (depassementDebut > 0.001f || l.filante))
                Piece(container, cube, mat, nom + "Debut", new Vector3(x0 - retrait - debut * 0.5f, y, 0f),
                      versDebut, new Vector3(debut, epaisseurLisse, profondeurLisse));
            if (fin > 0.001f && (depassementFin > 0.001f || l.filante))
                Piece(container, cube, mat, nom + "Fin", new Vector3(x0 + axes + retrait + fin * 0.5f, y, 0f),
                      Quaternion.identity, new Vector3(fin, epaisseurLisse, profondeurLisse));
        }

        UpdateCollider();
        builtSignature = Signature();
        UnityEditor.EditorUtility.SetDirty(this);
        if (gameObject.scene.IsValid())
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
    }

    void Piece(Transform parent, Mesh mesh, Material mat, string nom, Vector3 pos, Quaternion rot, Vector3 taille)
    {
        GameObject go = new GameObject(nom);
        go.layer = gameObject.layer;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = rot;
        go.transform.localScale = taille;
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = mat;
    }

    void UpdateCollider()
    {
        BoxCollider box = GetComponent<BoxCollider>();
        if (!collision)
        {
            if (box != null) DestroyImmediate(box);
            return;
        }
        if (box == null) box = gameObject.AddComponent<BoxCollider>();

        float h = hauteurCollision;
        if (h <= 0f)
        {
            h = hauteurPoteau;
            foreach (Lisse l in lisses) h = Mathf.Max(h, l.hauteur);
        }
        // Profondeur couverte par les poteaux (decales) et les lisses (sur l'axe)
        float zMin = Mathf.Min(decalagePoteaux - profondeurPoteau * 0.5f, -profondeurLisse * 0.5f);
        float zMax = Mathf.Max(decalagePoteaux + profondeurPoteau * 0.5f, profondeurLisse * 0.5f);
        float epais = epaisseurCollision > 0f ? epaisseurCollision : zMax - zMin;
        box.center = new Vector3(0f, h * 0.5f, (zMin + zMax) * 0.5f);
        box.size = new Vector3(longueur, h, epais);
    }

    // Empreinte stable (FNV-1a) des reglages ; le materiau compte par son nom (stable d'une session a l'autre).
    int Signature()
    {
        var sb = new StringBuilder();
        void Add(float f) => sb.Append(f.ToString("R", CultureInfo.InvariantCulture)).Append('|');
        Add(longueur); Add(espacement); Add(largeurPoteau); Add(profondeurPoteau); Add(decalagePoteaux); Add(hauteurPoteau);
        foreach (Lisse l in lisses) { Add(l.hauteur); Add(l.filante ? 1f : 0f); }
        sb.Append('#');
        Add(epaisseurLisse); Add(profondeurLisse); Add(depassementDebut); Add(depassementFin);
        Add(collision ? 1f : 0f); Add(hauteurCollision); Add(epaisseurCollision); Add(gameObject.layer);
        sb.Append(materiau != null ? materiau.name : "");

        unchecked
        {
            int hash = (int)2166136261;
            foreach (char c in sb.ToString()) hash = (hash ^ c) * 16777619;
            return hash;
        }
    }
#endif
}
