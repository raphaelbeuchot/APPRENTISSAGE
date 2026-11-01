using UnityEngine;

// Lumieres (ou tout prefab) alignees le long des bords d'une passerelle Ramp, droite ou en virage.
// Reglages ici, generation par le bouton "Generer" de l'Inspector (EdgeLightsEditor) : les instances sont de vrais
// objets enfants (dans EdgeLights_Generated), sauvegardes dans la scene et retouchables a la main ; "Generer" refait tout.
// Espacement mesure sur la ligne mediane : en virage, les lumieres gauche et droite sont face a face, plus serrees
// a l'interieur. L'espacement est ajuste pour tomber juste entre les marges de debut et de fin.
[RequireComponent(typeof(Ramp))]
public class EdgeLights : MonoBehaviour
{
    public enum Sides { Gauche, Droite, LesDeux }

    public const string ContainerName = "EdgeLights_Generated";

    [Tooltip("Prefab a aligner (ex. sphere avec ProximityTarget).")]
    public GameObject prefab;
    [Tooltip("Bord(s) concerne(s), gauche / droite en montant la rampe.")]
    public Sides sides = Sides.LesDeux;
    [Tooltip("Espacement vise entre deux lumieres, en metres, sur la ligne mediane. Ajuste pour tomber juste aux deux bouts.")]
    [Min(0.05f)] public float spacing = 1f;

    [Header("Position")]
    [Tooltip("Ecartement depuis le bord : positif vers l'exterieur, negatif vers l'interieur du dessus.")]
    public float outward = 0f;
    [Tooltip("Decalage vertical depuis le dessus : positif au-dessus, negatif sur la tranche.")]
    public float height = 0.1f;
    [Tooltip("Distance laissee libre au pied de la rampe, sur la ligne mediane.")]
    [Min(0f)] public float startMargin = 0.25f;
    [Tooltip("Distance laissee libre en haut de la rampe, sur la ligne mediane.")]
    [Min(0f)] public float endMargin = 0.25f;

    [Header("Orientation")]
    [Tooltip("Coche : chaque instance regarde dans le sens de la passerelle (pente comprise). " +
             "Decoche : rotation du prefab.")]
    public bool alignToPath = true;
}
