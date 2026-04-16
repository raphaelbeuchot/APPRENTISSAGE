using UnityEngine;

[CreateAssetMenu(fileName = "NewEnemyStats", menuName = "1-2-3 Soleil/Enemy Stats")]
public class EnemyStats : ScriptableObject
{
    [Header("IDENTIFICATION")]
    public string enemyName = "Basic Enemy";
    public GameObject enemyModel;
    public GameObject[] skinVariants;

    [Header("TYPE D'ENNEMI")]
    public AttackType attackType = AttackType.Hitter;

    [Header("SANTE")]
    public float maxHealth = 100f;

    public LayerMask obstacleMask;

    [Header("MOUVEMENT")]
    public float walkSpeed = 1f;
    public float chaseSpeed = 2f;
    public float rotationSpeed = 30f;
    public float crawlSpeed = 1f;

    [Header("DETECTION")]
    public float detectionRadius = 10f;
    [Range(0f, 360f)]
    public float detectionAngle = 90f;
    public float detectionCheckInterval = 0.5f;
    public LayerMask targetLayer;
    [Range(0.5f, 2f)]
    public float stealthDetectionMultiplier = 1f;

    [Header("BLINDER SETTINGS")]
    public float blinderHealthBarRange = 5f;

    [Header("Chase Persistence")]
    public float chasePersistenceDuration = 2f;

    [Header("ATTAQUE - PARAMETRES GENERAUX")]
    public float attackRange = 1.5f;
    public float attackCooldown = 1f;

    [Header("ATTAQUE - HITTER")]
    public Material windupMaterial;
    public float meleeDamage = 20f;
    public float meleeKnockbackForce = 8f;
    public float meleeAttackDuration = 0.8f;
    public AudioClip hitWindupSound;
    public AudioClip hitLandSound;
    public AudioClip hitWindupFailSound;

    [Header("ATTAQUE - SPITTER")]
    public float spitDamage = 15f;
    public float spitSpeed = 10f;
    public float spitRange = 15f;
    public GameObject spitProjectilePrefab;

    [Header("BLINDER SPECIFIC")]
    public float blinderWanderSpeed = 2f;
    public float blinderChargeSpeed = 3f;
    public float blinderWalkDuration = 2.5f;
    public float blinderStopDuration = 1.2f;
    public float blinderWanderRadius = 4f;
    public float audioDetectionRange = 6f;
    public float blinderKnockbackForce = 10f;
    public float blinderAttackRange = 2f;
    public float lookAroundDuration = 2f;
    public float knockdownDuration = 2f;
    public float aoeKnockbackRadius = 3f;
    public float recoilStunDuration = 3f;
    public float rechargeDelayAfterHit = 1f;

    [Header("BOURRADE")]
    public float bourradeDuration = 1.5f;
    public float bourradeCooldown = 2f;

    [Header("SPRAY STUN SYSTEM")]
    public float sprayStunDuration = 2.5f;
    public float knockbackStunDuration = 0.5f;

    [Header("ROTATION TO IMPACT")]
    public float rotationToImpactDuration = 1.5f;

    [Header("FORCE")]
    [Range(0.5f, 2f)]
    public float forceMultiplier = 1f;
    public bool hasWeapon = false;
    public GameObject weaponPrefab;

    [Header("DAMAGE TAKEN")]
    public float sentinelDamageTaken = 50f;
    public float sentinelKnockbackForce = 5f;
    public float sprayDamageTaken = 25f;
    public float broomDamageTaken = 40f;
    public float bottleDamageTaken = 2f;

    [Header("AUDIO")]
    public AudioClip waterSplashSound;
    public AudioClip lavaSplashSound;
    public AudioClip fallSound;
    public AudioClip[] idleSounds;
    public AudioClip[] chaseSounds;
    public AudioClip attackSound;
    public AudioClip hurtSound;
    public AudioClip deathSound;

    [Header("DEATH EFFECT")]
    public DeathEffectType deathEffectType = DeathEffectType.Ragdoll;

    [Header("Ragdoll Settings")]
    public float ragdollForce = 5f;
    public float ragdollTorque = 10f;
    public float meleeForceMultiplier = 1f;
    public float sentinelForceMultiplier = 1.5f;

    [System.Serializable]
    public class CorpseData
    {
        public float projectionMass = 5f;
        public float projectionDrag = 0.5f;
        public float restingMass = 1f;
        public float restingDrag = 3f;
        public float velocityThreshold = 0.5f;
        public float broomLowMassMultiplier = 0.2f;
        public float broomLowDragMultiplier = 0.3f;
    }

    [Header("CORPSE")]
    public CorpseData corpseData;

    // ============================================
    // ENUMS
    // ============================================

    public enum AttackType
    {
        Hitter,
        Spitter,
        Kamikaze,
        Screamer,
        Blinder
    }

    public enum DeathEffectType { None, Ragdoll, Explosion }

    // ============================================
    // METHODES UTILITAIRES
    // ============================================

    public float GetAdjustedSpeed(float currentHealth, bool isChasing, bool isCrawler)
    {
        if (isCrawler) return crawlSpeed;
        return isChasing ? chaseSpeed : walkSpeed;
    }

    public float GetAdjustedDetectionRadius(float playerStealthLevel)
    {
        return detectionRadius * (1f - (playerStealthLevel * 0.5f));
    }

    public float GetAdjustedDetectionAngle(float playerStealthLevel)
    {
        return detectionAngle * (1f - (playerStealthLevel * 0.3f));
    }

    public float GetTotalBourradeDuration()
    {
        return bourradeDuration + bourradeCooldown;
    }

    public float GetAdjustedAttackDamage()
    {
        switch (attackType)
        {
            case AttackType.Hitter: return meleeDamage * forceMultiplier;
            case AttackType.Spitter: return spitDamage * forceMultiplier;
            default: return 0f;
        }
    }
}