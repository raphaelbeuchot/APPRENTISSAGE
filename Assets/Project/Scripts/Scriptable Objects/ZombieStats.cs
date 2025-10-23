using UnityEngine;

[CreateAssetMenu(fileName = "NewZombieStats", menuName = "1-2-3 Soleil/Zombie Stats")]
public class ZombieStats : ScriptableObject
{
    [Header("IDENTIFICATION")]
    public string zombieName = "Basic Zombie";
    public GameObject zombieModel;
    public GameObject[] skinVariants;

    [Header("SANTE")]
    public float maxHealth = 100f;
    [Range(0f, 1f)]
    public float limbLossThreshold = 0.5f;

    [Header("MOUVEMENT")]
    public float walkSpeed = 1f;
    public float chaseSpeed = 2f;
    public float rotationSpeed = 30f;
    public float crawlSpeed = 1f;
    [Range(0f, 1f)]
    public float speedReductionPerHealthPercent = 0.3f;

    [Header("DETECTION")]
    public float detectionRadius = 10f;
    [Range(0f, 360f)]
    public float detectionAngle = 90f;
    public float detectionCheckInterval = 0.5f;
    public LayerMask targetLayer;
    [Range(0.5f, 2f)]
    public float stealthDetectionMultiplier = 1f;

    [Header("IDLE / WANDER")]
    [Range(0f, 1f)]
    public float idleWanderChance = 0.3f;
    public float wanderInterval = 5f;
    public float wanderDuration = 3f;

    [Header("ATTAQUE / GRAB")]
    public float grabRange = 1.5f;
    public float grabDuration = 4f;
    public float biteDamage = 15f;

    [Header("KNOCKBACK")]
    public float knockbackResistance = 0.8f;
    public float upwardForce = 2f;
    public float knockbackDuration = 0.5f;
    public float knockbackGracePeriod = 1f;

    [Header("SYSTEME DE MEMBRES")]
    public int startingArmCount = 2;
    public int startingLegCount = 2;
    [Range(0f, 1f)]
    public float oneArmMashReduction = 0.5f;

    [Header("RAMPANT (Crawler)")]
    public bool canBecomeCrawler = true;
    public int meleeHitsForFinisher = 3;

    [Header("FORCE")]
    [Range(0.5f, 2f)]
    public float forceMultiplier = 1f;
    public bool hasWeapon = false;
    public GameObject weaponPrefab;

    [Header("DEGATS")]
    public float meleeDamageTaken = 25f;
    public float sentinelDamageTaken = 60f;

    [Header("SONS")]
    public AudioClip[] idleSounds;
    public AudioClip[] chaseSounds;
    public AudioClip grabSound;
    public AudioClip biteSound;
    public AudioClip deathSound;

    public float GetAdjustedSpeed(float currentHealth, bool isChasing, bool isCrawler)
    {
        if (isCrawler) return crawlSpeed;
        float baseSpeed = isChasing ? chaseSpeed : walkSpeed;
        float healthPercent = currentHealth / maxHealth;
        float speedReduction = (1f - healthPercent) * speedReductionPerHealthPercent;
        return baseSpeed * (1f - speedReduction);
    }

    public float GetAdjustedDetectionRadius(float playerStealthLevel)
    {
        return detectionRadius * (1f - (playerStealthLevel * 0.5f));
    }

    public float GetAdjustedDetectionAngle(float playerStealthLevel)
    {
        return detectionAngle * (1f - (playerStealthLevel * 0.3f));
    }
}
