using UnityEngine;

/// <summary>
/// ScriptableObject générique pour tous les types d'ennemis.
/// Remplace ZombieStats et permet de créer différents types d'ennemis
/// (Grabbers, Hitters, Spitters, etc.)
/// </summary>
[CreateAssetMenu(fileName = "NewEnemyStats", menuName = "1-2-3 Soleil/Enemy Stats")]
public class EnemyStats : ScriptableObject
{
    [Header("IDENTIFICATION")]
    [Tooltip("Nom de l'ennemi")]
    public string enemyName = "Basic Enemy";
    [Tooltip("Modèle 3D de l'ennemi")]
    public GameObject enemyModel;
    [Tooltip("Variantes de skin")]
    public GameObject[] skinVariants;


    [Header("TYPE D'ENNEMI")]
    [Tooltip("Type d'attaque de cet ennemi")]
    public AttackType attackType = AttackType.Grabber;

    [Header("SANTÉ")]
    [Tooltip("Points de vie maximum")]
    public float maxHealth = 100f;
    [Tooltip("Seuil de perte de membres (0-1)")]
    [Range(0f, 1f)]
    public float limbLossThreshold = 0.5f;

    public LayerMask obstacleMask;


    [Header("MOUVEMENT")]
    [Tooltip("Vitesse de marche normale")]
    public float walkSpeed = 1f;
    [Tooltip("Vitesse de poursuite")]
    public float chaseSpeed = 2f;
    [Tooltip("Vitesse de rotation")]
    public float rotationSpeed = 30f;
    [Tooltip("Vitesse en mode rampant")]
    public float crawlSpeed = 1f;
    [Tooltip("Réduction de vitesse par % de santé perdu")]
    [Range(0f, 1f)]
    public float speedReductionPerHealthPercent = 0.3f;

    [Header("DÉTECTION")]
    [Tooltip("Rayon de détection des cibles")]
    public float detectionRadius = 10f;
    [Tooltip("Angle de détection (0-360)")]
    [Range(0f, 360f)]
    public float detectionAngle = 90f;
    [Tooltip("Intervalle de vérification de détection")]
    public float detectionCheckInterval = 0.5f;
    [Tooltip("Layer des cibles à détecter")]
    public LayerMask targetLayer;
    [Tooltip("Multiplicateur de détection selon stealth du joueur")]
    [Range(0.5f, 2f)]
    public float stealthDetectionMultiplier = 1f;


    [Header("BLINDER SETTINGS")]
    [Tooltip("Distance à laquelle le Blinder affiche sa barre de vie")]
    public float blinderHealthBarRange = 5f;

    [Header("IDLE / WANDER")]
    [Tooltip("Chance de wander au lieu de rester idle")]
    [Range(0f, 1f)]
    public float idleWanderChance = 0.3f;
    [Tooltip("Intervalle entre deux wanders")]
    public float wanderInterval = 5f;
    [Tooltip("Durée d'un wander")]
    public float wanderDuration = 3f;

    [Header("Chase Persistence")]
    [Tooltip("Duree en secondes pendant laquelle le zombie continue de chercher apres avoir perdu le joueur de vue")]
    public float chasePersistenceDuration = 2f;




    [Header("ATTAQUE - PARAMÈTRES GÉNÉRAUX")]
    [Tooltip("Portée d'attaque")]
    public float attackRange = 1.5f;
    [Tooltip("Cooldown entre deux attaques")]
    public float attackCooldown = 1f;

    [Header("ATTAQUE - GRABBER (si attackType = Grabber)")]
    [Tooltip("Durée du grab avant morsure")]
    public float grabDuration = 4f;
    [Tooltip("Dégâts de la morsure")]
    public float biteDamage = 15f;
    [Tooltip("Réduction de mashes avec 1 seul bras")]
    [Range(0f, 1f)]
    public float oneArmMashReduction = 0.5f;
    [Tooltip("Rayon pour fake grab autour du joueur")]
    public float fakeGrabRange = 3f;
    [Tooltip("Dégat morsure par seconde")]
    public float biteTickDamage = 5f;
    [Header("Grab Settings")]
    public float grabWindupDuration = 0.5f; // Durée telegraph avant grab

    [Header("ATTAQUE - HITTER (si attackType = Hitter)")]
    [Tooltip("Dégâts d'un coup de melee")]
    public float meleeDamage = 20f;
    [Tooltip("Force du knockback melee")]
    public float meleeKnockbackForce = 8f;
    [Tooltip("Durée de l'animation de melee")]
    public float meleeAttackDuration = 0.8f;

    [Header("ATTAQUE - SPITTER (si attackType = Spitter)")]
    [Tooltip("Dégâts du projectile")]
    public float spitDamage = 15f;
    [Tooltip("Vitesse du projectile")]
    public float spitSpeed = 10f;
    [Tooltip("Portée maximale du spit")]
    public float spitRange = 15f;
    [Tooltip("Prefab du projectile")]
    public GameObject spitProjectilePrefab;

    [Header("BLINDER SPECIFIC")]
    [Tooltip("Vitesse de wander du Blinder")]
    public float blinderWanderSpeed = 2f;
    [Tooltip("Vitesse de charge du Blinder")]
    public float blinderChargeSpeed = 3f;
    [Tooltip("Durée marche tout droit")]
    public float blinderWalkDuration = 2.5f;
    [Tooltip("Durée arrêt chasse mouches")]
    public float blinderStopDuration = 1.2f;
    [Tooltip("Rayon destinations random")]
    public float blinderWanderRadius = 4f;
    [Tooltip("Distance détection sons melee")]
    public float audioDetectionRange = 6f;
    [Tooltip("Force knockback charge")]
    public float blinderKnockbackForce = 10f;
    [Tooltip("Distance trigger knockback")]
    public float blinderAttackRange = 2f;
    [Tooltip("Durée look around")]
    public float lookAroundDuration = 2f;
    [Tooltip("Durée knockdown")]
    public float knockdownDuration = 2f;
    [Tooltip("Rayon AOE knockback")]
    public float aoeKnockbackRadius = 3f;
    [Tooltip("Durée recoil stun")]
    public float recoilStunDuration = 3f;
    [Tooltip("Délai avant re-charge après hit")]
    public float rechargeDelayAfterHit = 1f;

    [Header("BOURRADE")]
    [Tooltip("Durée de la projection en arrière")]
    public float bourradeDuration = 1.5f;
    [Tooltip("Durée du cooldown après bourrade")]
    public float bourradeCooldown = 2f;

    [Header("SPRAY STUN SYSTEM")]
    [Tooltip("Durée de stun par spray (secondes)")]
    public float sprayStunDuration = 2.5f;

    [Tooltip("Durée du knockback (secondes)")]
    public float knockbackStunDuration = 0.5f;


    [Header("SYSTÈME DE MEMBRES")]
    [Tooltip("Nombre de bras au départ")]
    public int startingArmCount = 2;
    [Tooltip("Nombre de jambes au départ")]
    public int startingLegCount = 2;

    [Header("Bloated Specific")]
    public float swellDuration = 1.5f;
    public float swellScale = 1.5f;


    [Header("RAMPANT (Crawler)")]
    [Tooltip("Peut devenir rampant après perte des jambes")]
    public bool canBecomeCrawler = true;
    [Tooltip("Nombre de coups de melee pour finisher")]
    public int meleeHitsForFinisher = 3;

    [Header("FORCE")]
    [Tooltip("Multiplicateur de force global")]
    [Range(0.5f, 2f)]
    public float forceMultiplier = 1f;
    [Tooltip("A une arme équipée")]
    public bool hasWeapon = false;
    [Tooltip("Prefab de l'arme")]
    public GameObject weaponPrefab;

    [Header("DAMAGE TAKEN")]
    [Tooltip("Dégâts pris par tir sentinelle")]
    public float sentinelDamageTaken = 50f;  // existant

    [Header("AUDIO")]
    [Tooltip("Son joue quand le zombie tombe dans l'eau")]
    public AudioClip waterSplashSound;

    [Tooltip("Dégâts pris par spray")]
    public float sprayDamageTaken = 25f;

    [Tooltip("Dégâts pris par balai")]
    public float broomDamageTaken = 40f;

    [Tooltip("Dégâts pris par bouteille")]
    public float bottleDamageTaken = 2f;

    [Header("SONS")]
    public AudioClip[] idleSounds;
    public AudioClip[] chaseSounds;
    public AudioClip attackSound;
    public AudioClip hurtSound;
    public AudioClip deathSound;

    // ============================================
    // ENUMS
    // ============================================

    public enum AttackType
    {
        Grabber,    // Grab et morsure (zombie classique)
        Hitter,     // Coup de poing/griffe
        Spitter,    // Projectile à distance
        Exploder,   // Explose au contact
        Screamer,    // Alerte les autres ennemis
        Blinder   //Réagit aux sons de mleeattack
    }

    // ============================================
    // MÉTHODES UTILITAIRES
    // ============================================

    /// <summary>
    /// Calcule la vitesse ajustée selon la santé, le mode chase et le mode crawler
    /// </summary>
    public float GetAdjustedSpeed(float currentHealth, bool isChasing, bool isCrawler)
    {
        if (isCrawler) return crawlSpeed;

        float baseSpeed = isChasing ? chaseSpeed : walkSpeed;
        float healthPercent = currentHealth / maxHealth;
        float speedReduction = (1f - healthPercent) * speedReductionPerHealthPercent;

        return baseSpeed * (1f - speedReduction);
    }

    /// <summary>
    /// Calcule le rayon de détection ajusté selon le stealth du joueur
    /// </summary>
    public float GetAdjustedDetectionRadius(float playerStealthLevel)
    {
        return detectionRadius * (1f - (playerStealthLevel * 0.5f));
    }

    /// <summary>
    /// Calcule l'angle de détection ajusté selon le stealth du joueur
    /// </summary>
    public float GetAdjustedDetectionAngle(float playerStealthLevel)
    {
        return detectionAngle * (1f - (playerStealthLevel * 0.3f));
    }

    /// <summary>
    /// Durée totale de la bourrade (projection + cooldown)
    /// </summary>
    public float GetTotalBourradeDuration()
    {
        return bourradeDuration + bourradeCooldown;
    }

    /// <summary>
    /// Calcule les dégâts d'attaque ajustés selon le multiplicateur de force
    /// </summary>
    public float GetAdjustedAttackDamage()
    {
        switch (attackType)
        {
            case AttackType.Grabber:
                return biteDamage * forceMultiplier;
            case AttackType.Hitter:
                return meleeDamage * forceMultiplier;
            case AttackType.Spitter:
                return spitDamage * forceMultiplier;
            default:
                return 0f;
        }
    }

    // AJOUTE CA A LA FIN DE LA CLASSE ENEMYSTATS :

    public enum DeathEffectType { None, Ragdoll, Explosion }
    public DeathEffectType deathEffectType = DeathEffectType.Ragdoll;
    [Header("Death Effect")]


    [Header("Ragdoll Settings (si Ragdoll)")]
    public float ragdollForce = 5f;
    public float ragdollTorque = 10f;
    public float meleeForceMultiplier = 1f;
    public float sentinelForceMultiplier = 1.5f;

    [Header("Explosion Settings (si Explosion)")]
    public GameObject explosionVFX;
    public AudioClip explosionSound;
    public float explosionSoundVolume = 1f;
    public float explosionVFXScale = 1f;
}