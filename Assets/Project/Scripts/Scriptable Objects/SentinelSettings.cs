using UnityEngine;

[CreateAssetMenu(fileName = "NewSentinelSettings", menuName = "1-2-3 Soleil/Sentinel Settings")]
public class SentinelSettings : ScriptableObject
{
    [Header("IDENTIFICATION")]
    public string sentinelName = "Main Sentinel";
    public GameObject sentinelModel;

    [Header("DETECTION")]
    public float detectionRadius = 1000f;
    public LayerMask targetLayers;
    public float movementThreshold = 0.1f;

    [Header("DEGATS")]
    public float playerDamage = 25f;
    public float zombieDamage = 60f;
    public bool headshotInstakill = true;
    [Range(0f, 1f)]
    public float headshotChance = 0.1f;
    public float stunDuration = 2f;
    public float stunZombieDuration = 3f;

    [Header("ETAT : GREENLIGHT")]
    public float greenlightMinDuration = 3f;
    public float greenlightMaxDuration = 8f;
    public AnimationClip greenlightAnimation;
    public AudioClip greenlightAmbientSound;

    [Header("ETAT : ALERT")]
    public AnimationClip alertAnimation;

    [Header("Alert Sound - Single Shot")]
    [Tooltip("Son joue une fois, vitesse selon distance")]
    public AudioClip alertSound;

    public AudioClip alertIgnitionSound;


    [Tooltip("Pitch minimum (loin du sentinel)")]
    [Range(0.5f, 2f)]
    public float minPitch = 0.7f;

    [Tooltip("Pitch maximum (proche du sentinel)")]
    [Range(0.5f, 2f)]
    public float maxPitch = 1.5f;

    public Color alertLightColor = Color.yellow;
    public GameObject alertParticlesPrefab;

    [Header("ETAT : REDLIGHT")]
    public float redlightMinDuration = 4f;
    public float redlightMaxDuration = 8f;
    public AnimationClip redlightAnimation;
    public AudioClip redlightIgnitionSound;
    public AudioClip redlightSound;
    public AudioClip shootSound;
    public Color redlightLightColor = Color.red;
    public GameObject redlightParticlesPrefab;
    public float redlightScanInterval = 0.2f;

    [Header("Exposure Detection")]
    [Tooltip("Nombre de scans consecutifs requis avant tir (si cible etait cachee)")]
    public int minimumExposureScans = 4;

    public float shootDelay = 0.2f;
    public float shootCooldown = 2f;

    [Header("LINE OF SIGHT & RAYCASTS")]
    [Tooltip("Layers qui bloquent la vision (murs, obstacles, ennemis)")]
    public LayerMask obstacleLayers;
    [Tooltip("Offset en hauteur pour l'origine des raycasts")]
    public Vector3 raycastOffset = new Vector3(0, 3, 0);
    [Tooltip("Afficher les lasers de visee (debug)")]
    public bool showLasers = true;
    [Tooltip("Epaisseur des lasers")]
    public float laserWidth = 0.05f;
    [Tooltip("Couleur des lasers")]
    public Color laserColor = Color.red;
    [Tooltip("Duree du fade-in des lasers")]
    public float laserFadeInDuration = 0.5f;
    [Tooltip("Duree du fade-out des lasers en Release")]
    public float laserFadeOutDuration = 1f;

    [Header("LASERS DE TIR")]
    [Tooltip("Duree du fade-out du laser de tir")]
    public float shootLaserFadeDuration = 0.5f;

    [Header("FLASH DE DETECTION")]
    [Tooltip("Delai avant le debut des flashs en Alert")]
    public float flashStartDelay = 2f;
    [Tooltip("Duree du flash blanc")]
    public float flashDuration = 0.5f;
    [Tooltip("Ecart entre chaque flash")]
    public float flashInterval = 0.2f;
    [Tooltip("Son de detection radar")]
    public AudioClip detectionSound;
    [Tooltip("Duree du fade des lasers en Release")]
    public float laserFadeDuration = 1f;

    [Header("Ricochet System")]
    [Tooltip("Son joue quand le tir touche un obstacle")]
    public AudioClip ricochetSound;

    [Tooltip("VFX spawne sur l'obstacle touche (etincelles)")]
    public GameObject ricochetVFX;

    [Tooltip("Duree du VFX ricochet avant destruction")]
    public float ricochetVFXDuration = 2f;

    [Header("ETAT : RELEASE")]
    public float releaseDuration = 1.5f;
    public AnimationClip releaseAnimation;
    public AudioClip releaseSound;
    public Color releaseLightColor = Color.green;
    public GameObject releaseParticlesPrefab;

    [Header("REGLES SPECIALES")]
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

    [Header("CAMERA SENTINEL MODE")]
    [Tooltip("Distance min camera quand tres proche sentinelle (mode archer)")]
    public float cameraMinDistance = 2f;
    [Tooltip("Distance max camera (distance actuelle normale)")]
    public float cameraMaxDistance = 9f;
    [Tooltip("Offset lateral de base")]
    public float cameraLateralOffset = 1.5f;
    [Tooltip("Offset lateral min quand proche")]
    public float cameraMinLateralOffset = 0.3f;
    [Tooltip("Hauteur camera en mode sentinelle")]
    public float cameraHeightOffset = 2f;
    [Tooltip("Distance a laquelle on commence a reduire les offsets")]
    public float cameraTransitionRange = 20f;

    public enum SentinelType { Main, Small, Multiple }

    public float GetRandomGreenlightDuration()
    {
        return Random.Range(greenlightMinDuration, greenlightMaxDuration);
    }

    public float GetRandomRedlightDuration()
    {
        return Random.Range(redlightMinDuration, redlightMaxDuration);
    }

    public float GetFullCycleDuration()
    {
        float avgGreenlight = (greenlightMinDuration + greenlightMaxDuration) / 2f;
        return avgGreenlight + (redlightMinDuration + redlightMaxDuration) / 2f + releaseDuration;
    }
}