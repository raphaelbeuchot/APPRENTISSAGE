using UnityEngine;

/// <summary>
/// ScriptableObject contenant toutes les statistiques d'un personnage joueur.
/// Permet de créer différents personnages (Marteaux, Golfeur, Doc, etc.) 
/// en créant plusieurs assets basés sur ce script.
/// </summary>
[CreateAssetMenu(fileName = "NewPlayerStats", menuName = "1-2-3 Soleil/Player Stats")]
public class PlayerStats : ScriptableObject
{
    [Header("IDENTIFICATION")]
    [Tooltip("Nom du personnage affiché dans l'UI")]
    public string characterName = "Player";

    [Tooltip("Modèle 3D du personnage")]
    public GameObject characterModel;

    [Header("SANTÉ")]
    [Tooltip("Points de vie maximum")]
    public float maxHealth = 100f;

    [Tooltip("Vitesse diminuée par point de vie perdu (en %)")]
    [Range(0f, 1f)]
    public float speedReductionPerHealthLost = 0.005f; // 0.5% par HP perdu

    [Header("MOUVEMENT")]
    [Tooltip("Vitesse de déplacement normale")]
    public float moveSpeed = 5f;

    [Tooltip("Multiplicateur de vitesse en sprint")]
    [Range(1f, 3f)]
    public float sprintSpeedMultiplier = 1.5f;

    [Header("STAMINA")]
    [Tooltip("Stamina maximum")]
    public float maxStamina = 100f;

    [Tooltip("Vitesse de drain de la stamina par seconde en sprint")]
    public float staminaDrainPerSecond = 20f;

    [Tooltip("Vitesse de régénération de la stamina par seconde")]
    public float staminaRegenPerSecond = 15f;

    [Tooltip("Délai avant régénération après avoir sprinté (en secondes)")]
    public float staminaRegenDelay = 1f;

    [Header("ATTAQUE MELEE")]
    [Tooltip("Portée de l'attaque")]
    public float attackRange = 2f;

    [Tooltip("Dégâts infligés")]
    public float attackDamage = 25f;

    [Tooltip("Force du knockback")]
    public float knockbackForce = 5f;

    [Tooltip("Durée de l'animation d'attaque (en secondes)")]
    public float attackDuration = 0.5f;

    [Tooltip("Cooldown entre deux attaques (en secondes)")]
    public float attackCooldown = 1f;

    [Header("FORCE")]
    [Tooltip("Force globale du personnage (influe damage et knockback)")]
    [Range(0.5f, 2f)]
    public float forceMultiplier = 1f;

    [Header("STEALTH")]
    [Tooltip("Niveau de discrétion (réduit la détection des zombies)")]
    [Range(0f, 1f)]
    public float stealthLevel = 0.5f; // 0 = très visible, 1 = ninja

    [Header("CAPACITÉS SPÉCIALES")]
    [Tooltip("Le personnage peut-il se régénérer ?")]
    public bool canHeal = false;

    [Tooltip("Si oui, vitesse de régénération par seconde")]
    public float healingPerSecond = 5f;

    [Tooltip("Le personnage peut-il soigner les alliés ?")]
    public bool canHealAllies = false;

    [Header("GRAB")]
    [Tooltip("Nombre de pressions nécessaires pour s'échapper d'un grab")]
    public int mashesToEscape = 10;

    [Tooltip("Temps maximum pour s'échapper avant de prendre des dégâts")]
    public float grabEscapeTimeWindow = 3f;

    [Header("ITEMS")]
    [Tooltip("Nombre de cerveaux de distraction au départ")]
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
    /// Calcule la vitesse réelle en fonction de la santé actuelle.
    /// </summary>
    public float GetAdjustedSpeed(float currentHealth)
    {
        float healthLost = maxHealth - currentHealth;
        float speedReduction = healthLost * speedReductionPerHealthLost;
        return moveSpeed * (1f - speedReduction);
    }

    /// <summary>
    /// Calcule les dégâts réels en fonction du multiplicateur de force.
    /// </summary>
    public float GetAdjustedDamage()
    {
        return attackDamage * forceMultiplier;
    }

    /// <summary>
    /// Calcule le knockback réel en fonction du multiplicateur de force.
    /// </summary>
    public float GetAdjustedKnockback()
    {
        return knockbackForce * forceMultiplier;
    }
}