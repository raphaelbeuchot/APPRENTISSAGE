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

    [Header("MOUVEMENT")]
    [Tooltip("Vitesse de deplacement normale")]
    public float moveSpeed = 5f;
    [Tooltip("Multiplicateur de vitesse en sprint")]
    [Range(1f, 3f)]
    public float sprintSpeedMultiplier = 1.5f;

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
    [Tooltip("Portee de l'attaque")]
    public float attackRange = 2f;
    [Tooltip("Degats infliges")]
    public float attackDamage = 25f;
    [Tooltip("Force du knockback")]
    public float knockbackForce = 5f;
    [Tooltip("Duree de l'animation d'attaque (en secondes)")]
    public float attackDuration = 0.5f;
    [Tooltip("Cooldown entre deux attaques (en secondes)")]
    public float attackCooldown = 0.5f;

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
    public float mashDecayDelay = 2f; // Délai avant que la jauge redescende
    public float mashDecayRate = 1f; // Vitesse de descente (crans par seconde)
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
    [Tooltip("Son de l'attaque")]
    public AudioClip attackSound;
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
    /// Calcule les degats reels en fonction du multiplicateur de force.
    /// </summary>
    public float GetAdjustedDamage()
    {
        return attackDamage * forceMultiplier;
    }

    /// <summary>
    /// Calcule le knockback reel en fonction du multiplicateur de force.
    /// </summary>
    public float GetAdjustedKnockback()
    {
        return knockbackForce;
    }
}
