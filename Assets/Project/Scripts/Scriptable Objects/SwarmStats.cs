using UnityEngine;
[CreateAssetMenu(fileName = "SwarmStats", menuName = "1-2-3 Soleil/SwarmStats")]
public class SwarmStats : ScriptableObject
{
    public enum SwarmType { Maggots, Flies }
    [Header("Swarm Type")]
    public SwarmType swarmType;
    [Header("Health")]
    public float maxHealth = 50f;
    [Header("Damage Taken")]
    public float sprayDamageTaken = 75f;
    public float broomDamageTaken = 0f;
    [Header("Movement")]
    public float moveSpeed = 2f;
    public float detectionRange = 6f;
    public bool canCrossObstacles = false;
    public float hoverHeight = 1.5f;
    [Tooltip("Offset Y par rapport au sol (negatif = enfonce dans le sol)")]
    public float groundOffset = -0.25f;
    [Header("Grab Behavior")]
    public int mashesToEscape = 3;
    public float grabEscapeTimeWindow = 3f;
    public float bourradeDuration = 0.8f;
    public float bourradeCooldown = 0.5f;
    public float bourradeDistance = 3f;
    [Header("Player Effects")]
    [Range(0f, 1f)]
    public float slowdownMultiplier = 0.6f;
    public bool dealsDamage = false;
    public float damagePerSecond = 5f;
    public float damageTick = 0.5f;
    [Header("Visual Effects")]
    public bool obscuresVision = false;
    [Range(0f, 1f)]
    public float visionObscurityAmount = 0.5f;
    public GameObject particleSystemPrefab;
}