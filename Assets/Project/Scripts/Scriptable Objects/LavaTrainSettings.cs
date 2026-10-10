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
}
