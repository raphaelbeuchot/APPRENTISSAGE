using System.Collections.Generic;
using UnityEngine;

// Lumieres (ou tout prefab) alignees le long des bords d'une passerelle : une Ramp (droite ou en virage), ou a defaut
// la boite du mesh (Cube etire, pave quelconque, meme tourne ou incline : on suit son dessus en repere local).
// Reglages ici, generation par le bouton "Generer" de l'Inspector (EdgeLightsEditor) : les instances sont de vrais
// objets enfants (dans EdgeLights_Generated), sauvegardes dans la scene et retouchables a la main ; "Generer" refait tout.
// Espacement mesure sur la ligne mediane : en virage, les lumieres gauche et droite sont face a face, plus serrees
// a l'interieur. L'espacement est ajuste pour tomber juste entre les marges de debut et de fin.
// Boite : toutes les distances sont en metres reels, malgre l'echelle du GO.
// Source Aretes : le long des aretes vives cochees du mesh (MeshEdgeChains) ; boucle fermee = sans marges,
// espacement ajuste pour que le dernier retombe juste avant le premier ; arc < 360 = portion traitee comme une
// chaine ouverte. Boucle : repere comme la tranche (axe = perpendiculaire au plan de la boucle, vers l'exterieur du
// mesh ; exterieur = loin du centre de la boucle) ; chaine ouverte : haut du monde. Tangente lissee aux sommets peu
// anguleux (cylindre a facettes).
// RoundedBlock (source Auto) : le long de sa tranche, au bord de la face haute (dessus / avant / cote selon le profil),
// decalage dans le repere de la tranche (exterieur, extrusion, le long du tour) ; haut des instances = axe d'extrusion.
// CurvedWall (source Auto) : arc de cercle en haut du mur (milieu, face exterieure ou interieure) ; exterieur = loin
// du centre du cercle, hauteur vers le haut local. Anneau complet = boucle, sinon chemin ouvert (marges).
[RequireComponent(typeof(MeshFilter))]
public class EdgeLights : MonoBehaviour
{
    public enum Sides { Gauche, Droite, LesDeux }
    public enum Axis { Auto, X, Z }
    public enum Source { Auto, Aretes }
    public enum Distribution { Espacement, Nombre, AuxCoins }
    public enum WallLine { Milieu, Exterieur, Interieur }

    public const string ContainerName = "EdgeLights_Generated";

    [Tooltip("Auto : la Ramp du GO, sinon la boite du mesh. Aretes : aretes vives du mesh (cylindre, mesh importe...), " +
             "affichees en couleur dans la Scene view quand le GO est selectionne.")]
    public Source source = Source.Auto;
    [Tooltip("CurvedWall seulement : arc suivi en haut du mur. Milieu = au milieu de l'epaisseur ; " +
             "Exterieur / Interieur = au bord haut de la face exterieure / interieure.")]
    public WallLine wallLine = WallLine.Milieu;
    [Tooltip("Aretes seulement : angle mini entre deux faces voisines pour que leur arete compte comme vive.")]
    [Range(1f, 179f)] public float edgeAngle = 30f;
    [Tooltip("Aretes, boucles seulement : portion garnie, en degres de tour (fraction du perimetre ; exact sur un cercle). " +
             "Part du +X local en tournant anti-horaire vu du dessus : tourner le GO deplace l'arc. 360 = toute la boucle.")]
    [Range(1f, 360f)] public float arc = 360f;
    // Aretes : numeros des chaines cochees (liste dessinee par EdgeLightsEditor). Changer de mesh ou de seuil
    // peut renumeroter les chaines : recocher alors.
    [HideInInspector] public List<int> selectedChains = new List<int>();

    [Tooltip("Prefab ou GO de la scene a aligner (ex. sphere avec ProximityTarget). Un GO de la scene est duplique, " +
             "les copies sont actives meme s'il est desactive (pratique pour garder un modele cache).")]
    public GameObject prefab;
    [Tooltip("Bord(s) concerne(s), gauche / droite en montant la rampe (boite : en avancant vers +Z ou +X).")]
    public Sides sides = Sides.LesDeux;
    [Tooltip("Boite seulement (ignore sur une Ramp) : axe local du chemin. Auto = le cote le plus long.")]
    public Axis axis = Axis.Auto;
    [Tooltip("Espacement : distance visee entre deux instances. Nombre : nombre d'instances, a intervalles egaux " +
             "(boucle : sur tout le tour ; ouvert : de la marge de debut a la marge de fin, bouts compris). " +
             "Aux coins : RoundedBlock (milieu de chaque coin, arc ignore) ou Aretes (sommets qui tournent de plus de 45 degres).")]
    public Distribution distribution = Distribution.Espacement;
    [Tooltip("Espacement vise entre deux lumieres, en metres, sur la ligne mediane. Ajuste pour tomber juste aux deux bouts.")]
    [Min(0.05f)] public float spacing = 1f;
    [Tooltip("Nombre d'instances par chemin (par bord sur une Ramp ou une boite, par chaine en Aretes).")]
    [Min(1)] public int count = 4;

    [Header("Position")]
    [Tooltip("Ecartement depuis le bord : positif vers l'exterieur, negatif vers l'interieur du dessus. " +
             "RoundedBlock / Aretes : perpendiculaire au chemin, loin du centre de la boucle.")]
    public float outward = 0f;
    [Tooltip("Decalage vertical depuis le dessus : positif au-dessus, negatif sur la tranche. " +
             "RoundedBlock : le long de l'extrusion depuis le bord de la face haute (Avant : vers l'avant +Z). " +
             "Aretes : boucle = perpendiculaire a son plan, vers l'exterieur du mesh ; chaine ouverte = haut du monde.")]
    public float height = 0.1f;
    [Tooltip("RoundedBlock / Aretes : decalage le long du tour, en metres (fait glisser toutes les instances, et l'arc).")]
    public float alongOffset = 0f;
    [Tooltip("Distance laissee libre au debut du chemin (pied de la rampe), sur la ligne mediane.")]
    [Min(0f)] public float startMargin = 0.25f;
    [Tooltip("Distance laissee libre a la fin du chemin (haut de la rampe), sur la ligne mediane.")]
    [Min(0f)] public float endMargin = 0.25f;

    [Header("Orientation")]
    [Tooltip("Coche : chaque instance regarde dans le sens de la passerelle (pente comprise). " +
             "Decoche : rotation du prefab.")]
    public bool alignToPath = true;
    [Tooltip("Rotation ajoutee a chaque instance, en degres, dans son propre repere, apres l'alignement " +
             "(ou apres la rotation du prefab si l'alignement est decoche).")]
    public Vector3 rotationOffset = Vector3.zero;
    [Tooltip("Coche : les colliders des copies (enfants compris) sont retires a la generation, pour une deco purement " +
             "visuelle. Le prefab n'est pas touche (override 'Removed Component' sur chaque copie).")]
    public bool noColliders = false;

    [Header("Scene view")]
    [Tooltip("Affiche les instances a venir en fantome quand le GO est selectionne (rien n'est cree). " +
             "Decocher si la scene rame.")]
    public bool preview = true;
}
