using UnityEngine;

[CreateAssetMenu(fileName = "NewSentinelSettings", menuName = "1-2-3 Soleil/Sentinel Settings")]
public class SentinelSettings : ScriptableObject
{
    [Header("IDENTIFICATION")]
    public string sentinelName = "Main Sentinel";
    public GameObject sentinelModel;

    [Header("DÉTECTION")]
    public float detectionRadius = 1000f;
    public LayerMask targetLayers;
    public float movementThreshold = 0.1f;

    [Header("DÉGÂTS")]
    public float playerDamage = 25f;
    public float zombieDamage = 60f;
    public bool headshotInstakill = true;

    [Header("ÉTAT : GREENLIGHT")]
    public float greenlightMinDuration = 3f;
    public float greenlightMaxDuration = 8f;
    public AnimationClip greenlightAnimation;
    public AudioClip greenlightAmbientSound;

    [Header("ÉTAT : ALERT")]
    public float alertDuration = 3f;
    public AnimationClip alertAnimation;
    public AudioClip alertSound;
    public Color alertLightColor = Color.yellow;
    public GameObject alertParticlesPrefab;

    [Header("ÉTAT : REDLIGHT")]
    public float redlightDuration = 5f;
    public AnimationClip redlightAnimation;
    public AudioClip redlightSound;
    public AudioClip shootSound;
    public Color redlightLightColor = Color.red;
    public GameObject redlightParticlesPrefab;
    public float redlightScanInterval = 0.2f;
    public float shootDelay = 0.1f;

    [Header("ÉTAT : RELEASE")]
    public float releaseDuration = 1.5f;
    public AnimationClip releaseAnimation;
    public AudioClip releaseSound;
    public Color releaseLightColor = Color.green;
    public GameObject releaseParticlesPrefab;

    [Header("RÈGLES SPÉCIALES")]
    public bool shootGrabbingZombiesInRedlight = true;
    public bool punishDegrabInRedlight = true;
    public float degrabPunishmentDelay = 0.1f;

    [Header("EFFETS VISUELS")]
    public GameObject bulletPrefab;
    public float bulletSpeed = 100f;
    public bool useInstantRaycast = true;
    public GameObject playerHitEffectPrefab;
    public GameObject zombieHitEffectPrefab;

    [Header("POLISH / CUTSCENE")]
    public bool enableDetectionCutscene = false;
    public float slowMoDuration = 0.5f;
    [Range(0.1f, 1f)]
    public float slowMoTimeScale = 0.3f;

    [Header("VARIATIONS")]
    public SentinelType type = SentinelType.Main;

    public enum SentinelType
    {
        Main,
        Small,
        Multiple
    }

    public float GetRandomGreenlightDuration()
    {
        return Random.Range(greenlightMinDuration, greenlightMaxDuration);
    }

    public float GetFullCycleDuration()
    {
        float avgGreenlight = (greenlightMinDuration + greenlightMaxDuration) / 2f;
        return avgGreenlight + alertDuration + redlightDuration + releaseDuration;
    }
}