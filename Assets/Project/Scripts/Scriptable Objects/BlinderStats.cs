using UnityEngine;

[CreateAssetMenu(fileName = "BlinderStats", menuName = "Stats/Blinder Stats")]
public class BlinderStats : ScriptableObject
{
    [Header("Health")]
    public float maxHealth = 100f;

    [Header("Movement")]
    public float wanderSpeed = 2f;
    public float chargeSpeed = 3f;

    [Header("Wander Pattern")]
    public float walkDuration = 2.5f;           // Temps marche tout droit
    public float stopDuration = 1.2f;           // Temps arrêt "chasse mouches"
    public float wanderRadius = 4f;             // Distance destinations random

    [Header("Audio Detection")]
    public float audioDetectionRange = 6f;      // Distance détection sons melee

    [Header("Attack")]
    public float knockbackForce = 10f;
    public float attackRange = 2f;              // Distance pour trigger le knockback
    public float lookAroundDuration = 2f;
    public float knockdownDuration = 2f; 
    public float aoeKnockbackRadius = 3f;
    public float recoilStunDuration = 3f;

}