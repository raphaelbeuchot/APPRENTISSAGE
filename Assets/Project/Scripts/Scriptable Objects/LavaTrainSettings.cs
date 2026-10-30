using UnityEngine;

[CreateAssetMenu(fileName = "LavaTrainSettings", menuName = "1-2-3 Soleil/Lava Train Settings")]
public class LavaTrainSettings : ScriptableObject
{
    [Header("Platforms")]
    [Tooltip("Prefab du wagon, instancie par le PlatformTrainManager au Start")]
    public PlatformTrainCar platformPrefab;

    [Header("Speed")]
    public float trainSpeed = 1f;
    [Tooltip("Vitesse d'arret en maintenant X (plus haut = arret plus sec). 2 = ~1.1 s, 3 = ~0.75 s")]
    public float stopInertia = 2f;
    public float startInertia = 25f;

    [Header("Comment")]
    public string holdMessage = "Hold X to stop";
    public bool showMessageOnce = true;

    [Header("Audio")]
    public AudioClip startSound;
    public AudioClip brakeSound;
    public AudioClip releaseSound;

    [Header("Materials")]
    public Material lavaTrainBasic;
    public Material lavaTrainOn;
    public Material lavaTrainOff;

    [Header("Disc Scale")]
    public float discScaleBase = 0.6f;
    public float discScaleOnPlatform = 0.7f;
    public float discScaleHoldingX = 0.5f;
    public float discLerpSpeed = 3.5f;

    [Header("Chain (LavaTrainChain)")]
    [Tooltip("Materiau de la chaine dessinee le long de la spline (Maillon_04). Vide = pas de chaine")]
    public Material chainMaterial;
    public float chainWidth = 0.22f;
    [Tooltip("Decalage vertical par rapport a la spline (negatif = sous les wagons)")]
    public float chainHeightOffset = -0.3f;
    [Tooltip("Repetition de la texture (mode Tile), comme les chaines de zombies")]
    public float chainTextureScale = 4f;
    [Tooltip("Ecart entre deux points de la ligne, en metres")]
    public float chainPointSpacing = 0.25f;
    [Tooltip("Profondeur du creux de la chaine sous le wagon du joueur. 0 = pas de creux")]
    public float chainSagDepth = 0.15f;
    [Tooltip("Distance de chaque cote du wagon sur laquelle la chaine descend, en metres")]
    public float chainSagHalfWidth = 1.5f;
    [Tooltip("Vitesse d'apparition / disparition du creux (metres par seconde)")]
    public float chainSagSpeed = 0.5f;
    [Tooltip("Defilement des maillons : 1 = suivent exactement le train, 0 = immobiles, negatif = sens inverse. Reglable en Play")]
    public float chainScrollFactor = 1f;

    [Header("Gears (LavaTrainGears)")]
    [Tooltip("Prefab de la roue posee a chaque knot (GearShape : forme et materiaux). Vide = pas de roues")]
    public GameObject gearPrefab;
    [Tooltip("Decalage vertical des roues par rapport a la chaine (positif = au-dessus ; pivot GearShape = base de la roue). Reglable en Play")]
    public float gearHeightOffset = 0f;
    [Tooltip("Multiplicateur de la vitesse de rotation des roues (1 = calee sur la chaine, 0.5 = deux fois plus lent). Reglable en Play")]
    public float gearSpinFactor = 0.5f;
}
