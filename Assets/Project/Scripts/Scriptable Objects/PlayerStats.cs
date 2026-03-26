using UnityEngine;

/// <summary>
/// ScriptableObject contenant toutes les statistiques d'un personnage joueur.
/// Permet de creer differents personnages (Marteaux, Golfeur, Doc, etc.)
/// en creant plusieurs assets bases sur ce script.
/// </summary>
[CreateAssetMenu(fileName = "NewPlayerStats", menuName = "1-2-3 Soleil/Player Stats")]
public class PlayerStats : ScriptableObject
{
    [Header("IDENTIFICATION")]
    [Tooltip("Nom du personnage affiche dans l'UI")]
    public string characterName = "Player";
    [Tooltip("Modele 3D du personnage")]
    public GameObject characterModel;


    [Header("SANTE")]
    [Tooltip("Points de vie maximum")]
    public float maxHealth = 100f;
    [Tooltip("Vitesse diminuee par point de vie perdu (en %)")]
    [Range(0f, 1f)]
    public float speedReductionPerHealthLost = 0.005f;
    [Header("Sentinel Knockback")]
    public float sentinelKnockbackForce = 10f;

    [Header("MOUVEMENT")]
    [Tooltip("Vitesse de deplacement normale")]
    public float moveSpeed = 5f;
    

    [Header("Dash")]
    public float dashDistance = 1f;
    public float dashDuration = 0.35f;
    public float dashStaminaCost = 25f;
    public float dashCooldown = 2.5f;
    public AudioClip dashSound;


    [Header("Crouch")]
    public float crouchSpeedMultiplier = 0.8f; // 80 prcents vitesse en crouch

    [Header("Lock-On System")]
    public float lockOnRange = 10f;
    public float lockOnAngle = 90f; // angle devant la camera

    [Header("STAMINA")]
    [Tooltip("Stamina maximum")]
    public float maxStamina = 100f;
    [Tooltip("Vitesse de drain de la stamina par seconde en sprint")]
    public float staminaDrainPerSecond = 20f;
    [Tooltip("Vitesse de regeneration de la stamina par seconde")]
    public float staminaRegenPerSecond = 15f;
    [Tooltip("Delai avant regeneration apres avoir sprinte (en secondes)")]
    public float staminaRegenDelay = 1f;

    [Header("ATTAQUE MELEE")]
    [Header("Melee Attack")]
    public float meleeStaminaCost = 15f;
    [Tooltip("Angle du cône de l'attaque melee (en degrés)")]
    [Range(0f, 360f)]
    public float meleeConeAngle = 140f;
    [Tooltip("Portee de l'attaque")]
    public float attackRange = 1.2f;
    [Tooltip("Force du knockback")]
    public float knockbackForce = 5f;
    [Tooltip("Duree de l'animation d'attaque (en secondes)")]
    public float attackDuration = 0.5f;
   

    [Header("SPRAY SYSTEM")]
    [Tooltip("Munitions max du spray")]
    public int maxSprayAmmo = 10;
    [Tooltip("Temps de rechargement automatique en secondes")]
    public float sprayReloadTime = 3f;
    [Tooltip("Cadence de tir en maintien gachette (secondes entre chaque spray)")]
    public float sprayFireRate = 0.3f;
    [Tooltip("Reserve totale de munitions au depart")]
    public int totalSprayAmmoStart = 40;
    [Tooltip("Particules effet spray")]
    public GameObject sprayVFX;
    [Tooltip("Son du spray pshit")]
    public AudioClip sprayFrontSound;
    

    [Header("BOTTLE THROW")]
    [Tooltip("Force de lancer de la bouteille")]
    public float bottleThrowForce = 15f;
    [Tooltip("Duree du stun ennemi secondes")]
    public float bottleStunDuration = 0.5f;
    [Tooltip("Prefab de la bouteille jetee")]
    public GameObject bottlePrefab;
    [Tooltip("Son de l'impact blong")]
    public AudioClip bottleImpactSound;



    [Header("BROOM SYSTEM")]
    [Tooltip("Portee de l'attaque balai")]
    public float broomRange = 2f;
    [Tooltip("Angle du cone balai en degres")]
    [Range(0f, 360f)]
    public float broomConeAngle = 270f;
    [Tooltip("Cout en stamina de l'attaque balai")]
    public float broomStaminaCost = 15f;
    
    [Tooltip("Duree totale de l'attaque balai")]
    public float broomAttackDuration = 1.5f;
  
    [Tooltip("Son du balai")]
    public AudioClip broomSound;
    public AudioClip broomHitSound;

    [Tooltip("Force du knockback balai")]
    public float broomKnockbackForce = 8f;

    [Header("BOURRADE")]
    [Tooltip("Force de la bourrade")]
    public float bourradeForce = 12f;

    [Header("FORCE")]
    [Tooltip("Force globale du personnage (influe damage)")]
    [Range(0.5f, 2f)]
    public float forceMultiplier = 1f;

    [Header("STEALTH")]
    [Tooltip("Niveau de discretion (reduit la detection des zombies)")]
    [Range(0f, 1f)]
    public float stealthLevel = 0.5f;

    [Header("CAPACITES SPECIALES")]
    [Tooltip("Le personnage peut-il se regenerer ?")]
    public bool canHeal = false;
    [Tooltip("Si oui, vitesse de regeneration par seconde")]
    public float healingPerSecond = 5f;
    [Tooltip("Le personnage peut-il soigner les allies ?")]
    public bool canHealAllies = false;

    [Header("GRAB")]
    [Header("Grab Escape System")]
    public int mashesToEscape = 5; // Nombre de crans
    [Tooltip("Temps maximum pour s'echapper avant de prendre des degats")]
    public float grabEscapeTimeWindow = 5f;
    [Tooltip("Force du recoil apres grab escape/release")]
    public float recoilForce = 100f;

    [Header("ITEMS")]
    [Tooltip("Nombre de cerveaux de distraction au depart")]
    public int startingBrainCount = 3;

    [Header("SONS")]
    [Tooltip("Son des pas")]
    public AudioClip footstepSound;
    public AudioClip attackSound;        // Swing à vide (existant)
    public AudioClip attackHitSound;     // Impact sur ennemi (nouveau)
    public AudioClip sprayEmptySound;
    [Tooltip("Son de douleur")]
    public AudioClip hurtSound;
    [Tooltip("Son de mort")]
    public AudioClip deathSound;

    /// <summary>
    /// Calcule la vitesse reelle en fonction de la sante actuelle.
    /// </summary>
    public float GetAdjustedSpeed(float currentHealth)
    {
        float healthLost = maxHealth - currentHealth;
        float speedReduction = healthLost * speedReductionPerHealthLost;
        return moveSpeed * (1f - speedReduction);
    }



    /// <summary>
    /// Calcule le knockback reel en fonction du multiplicateur de force.
    /// </summary>
    public float GetAdjustedKnockback()
    {
        return knockbackForce;
    }
}