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

    [Header("Damage Settings - InstantKill")]
    [Tooltip("Kill trigger point: where character dies (CenterImmersed or TouchBottom)")]
    public KillTrigger killTrigger = KillTrigger.CenterImmersed;

    [Header("Damage Settings - DamageOverTime")]
    [Tooltip("Degats par seconde (DamageZone only)")]
    public float damagePerSecond = 10f;

    [Tooltip("Delai avant premier degat (DamageZone only)")]
    public float damageDelay = 1f;

    [Tooltip("Intervalle entre chaque tick de degats (DamageZone only)")]
    public float damageInterval = 1f;

    [Header("Movement Effects - Water Only")]
    [Tooltip("Multiplicateur de vitesse pour le swim mode player (ex: 0.5 = 50% speed)")]
    public float swimSpeedMultiplier = 0.5f;

    [Tooltip("Profondeur d'immersion pour activer le swim mode (en metres depuis la surface)")]
    public float swimActivationDepth = 0.3f;

    [Header("AI Behavior")]
    [Tooltip("Desactive l'AI des ennemis dans ce contenu")]
    public bool disableEnemyAI = true;

    [Header("Fall Damage - Empty Pits Only")]
    [Tooltip("Degats par metre au-dela du seuil d'immunite")]
    public float fallDamageMultiplier = 10f;

    [Tooltip("Hauteur minimum de chute sans degats (en metres)")]
    public float fallImmunityThreshold = 3f;

    [Header("Special Properties")]
    [Tooltip("Ce contenu est un liquide (affecte la physique et les effets visuels)")]
    public bool isLiquid = false;

    public enum ContentCategory
    {
        Empty,          // Pit vide - fall damage possible
        Water,          // Water - swim pour player, instakill pour zombies
        InstantKill,    // Lava, Acid - mort instantanee pour tous
        Spikes,         // Spikes au fond - mort instantanee au contact
        DamageZone      // RESERVE pour futur (feu, acide DoT, maggots pool)
    }

    public enum KillTrigger
    {
        CenterImmersed,  // Kill quand characterCenter < fillSurfaceHeight (liquides)
        TouchBottom      // Kill quand bas collider touche floor (spikes)
    }
}