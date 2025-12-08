using UnityEngine;

/// <summary>
/// ScriptableObject containing all stats and configuration for swarms (maggots or flies)
/// </summary>
[CreateAssetMenu(fileName = "SwarmStats", menuName = "1-2-3 Soleil/SwarmStats")]
public class SwarmStats : ScriptableObject
{
    public enum SwarmType { Maggots, Flies }

    [Header("Swarm Type")]
    public SwarmType swarmType;

    [Header("Health")]
    [Tooltip("Health points of the swarm")]
    public float maxHealth = 50f;

    [Header("Damage Taken")]
    [Tooltip("Dégâts pris par spray")]
    public float sprayDamageTaken = 75f;  // 3x les dégâts normaux comme actuellement

    [Tooltip("Dégâts pris par balai")]
    public float broomDamageTaken = 0f;

    [Header("Movement")]
    [Tooltip("Movement speed of the swarm")]
    public float moveSpeed = 2f;
    
    [Tooltip("Detection range to start chasing player")]
    public float detectionRange = 6f;
    
    [Tooltip("Use NavMesh for ground movement (true for maggots, false for flies)")]
    public bool useNavMesh = true;
    
    [Tooltip("Can cross obstacles (true for flies, false for maggots)")]
    public bool canCrossObstacles = false;
    
    [Tooltip("Hover height above ground (for flies only)")]
    public float hoverHeight = 1.5f;
    
    [Header("Grab Behavior")]
    [Tooltip("Number of mashes required per swarm to escape")]
    public int mashesToEscape = 3;
    
    [Tooltip("Time window to escape grab (seconds)")]
    public float grabEscapeTimeWindow = 3f;
    
    [Tooltip("Duration the swarm is dispersed after escape (seconds)")]
    public float bourradeDuration = 0.8f;
    
    [Tooltip("Cooldown before swarm can grab again after reformation (seconds)")]
    public float bourradeCooldown = 0.5f;
    
    [Tooltip("Distance swarm is pushed back when player escapes")]
    public float bourradeDistance = 3f;
    
    [Header("Player Effects")]
    [Tooltip("Movement speed multiplier when grabbed (0.5 = 50% speed)")]
    [Range(0f, 1f)]
    public float slowdownMultiplier = 0.6f;
    
    [Tooltip("Does this swarm deal damage over time?")]
    public bool dealsDamage = false;
    
    [Tooltip("Damage per second when grabbed")]
    public float damagePerSecond = 5f;
    
    [Tooltip("Interval between damage ticks (seconds)")]
    public float damageTick = 0.5f;
    
    [Header("Visual Effects")]
    [Tooltip("Does this swarm obscure player vision?")]
    public bool obscuresVision = false;
    
    [Tooltip("Amount of vision obscurity (0-1)")]
    [Range(0f, 1f)]
    public float visionObscurityAmount = 0.5f;
    
    [Tooltip("Particle system prefab for the swarm")]
    public GameObject particleSystemPrefab;
}
