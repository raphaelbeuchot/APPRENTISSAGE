using UnityEngine;

[CreateAssetMenu(fileName = "PitContent", menuName = "Level Design/Pit Content Type")]
public class PitContentType : ScriptableObject
{
    [Header("Identity")]
    public string contentName = "Empty";

    [Header("Category")]
    public ContentCategory category = ContentCategory.Empty;

    [Header("Visual")]
    public GameObject contentPrefab;
    public Color previewColor = new Color(0.5f, 0.5f, 1f, 0.4f);

    [Header("Damage Settings")]
    [Tooltip("Dégâts par seconde (DamageZone only)")]
    public float damagePerSecond = 10f;

    [Tooltip("Délai avant premier dégât (DamageZone only)")]
    public float damageDelay = 1f;

    [Tooltip("Intervalle entre chaque tick de dégâts (DamageZone only)")]
    public float damageInterval = 1f;

    [Header("Movement Effects")]
    [Tooltip("Multiplicateur de vitesse (Liquid water only, ex: 0.5 = 50% speed)")]
    public float movementSpeedMultiplier = 1f;

    [Header("AI Behavior")]
    [Tooltip("Désactive l'AI des ennemis dans ce contenu (ex: Fire, InstantKill)")]
    public bool disableEnemyAI = true;

    [Header("Fall Damage")]
    [Tooltip("Dégâts par mètre au-delà du seuil d'immunité")]
    public float fallDamageMultiplier = 10f;

    [Tooltip("Hauteur minimum de chute sans dégâts (en mètres)")]
    public float fallImmunityThreshold = 3f;

    [Header("Special Properties")]
    [Tooltip("Ce contenu est un liquide (pour différencier spikes vs lave/acide)")]
    public bool isLiquid = false;

    public enum ContentCategory
    {
        Empty,          // Pas de damage du fill
        InstantKill,    // Mort instantanée au contact
        Liquid,         // Water - slow only
        DamageZone      // Fire/Acid - DPS
    }
}