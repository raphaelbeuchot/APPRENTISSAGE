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
// chaine ouverte. Hauteur vers le haut du monde. Tangente lissee aux sommets peu anguleux (cylindre a facettes).
[RequireComponent(typeof(MeshFilter))]
public class EdgeLights : MonoBehaviour
{
    public enum Sides { Gauche, Droite, LesDeux }
    public enum Axis { Auto, X, Z }
    public enum Source { Auto, Aretes }

    public const string ContainerName = "EdgeLights_Generated";

    [Tooltip("Auto : la Ramp du GO, sinon la boite du mesh. Aretes : aretes vives du mesh (cylindre, mesh importe...), " +
             "affichees en couleur dans la Scene view quand le GO est selectionne.")]
    public Source source = Source.Auto;
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
    [Tooltip("Espacement vise entre deux lumieres, en metres, sur la ligne mediane. Ajuste pour tomber juste aux deux bouts.")]
    [Min(0.05f)] public float spacing = 1f;

    [Header("Position")]
    [Tooltip("Ecartement depuis le bord : positif vers l'exterieur, negatif vers l'interieur du dessus.")]
    public float outward = 0f;
    [Tooltip("Decalage vertical depuis le dessus : positif au-dessus, negatif sur la tranche.")]
    public float height = 0.1f;
    [Tooltip("Distance laissee libre au debut du chemin (pied de la rampe), sur la ligne mediane.")]
    [Min(0f)] public float startMargin = 0.25f;
    [Tooltip("Distance laissee libre a la fin du chemin (haut de la rampe), sur la ligne mediane.")]
    [Min(0f)] public float endMargin = 0.25f;

    [Header("Orientation")]
    [Tooltip("Coche : chaque instance regarde dans le sens de la passerelle (pente comprise). " +
             "Decoche : rotation du prefab.")]
    public bool alignToPath = true;
}
