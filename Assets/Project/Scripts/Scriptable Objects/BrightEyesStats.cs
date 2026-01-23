using UnityEngine;
[CreateAssetMenu(fileName = "BrightEyesStats", menuName = "Enemy/Bright Eyes Stats")]
public class BrightEyesStats : ScriptableObject
{
    [Header("Activation States")]
    public bool activeInGreenLight = false;
    public bool activeInAlert = true;
    public bool activeInRedLight = true;

    [Header("Detection")]
    public float detectionRange = 6f;
    public float detectionAngle = 90f;

    [Header("Attraction")]
    public float attractionForce = 5f;
    public float playerSlowdownMultiplier = 0.5f;

    [Header("Movement")]
    public bool canWander = false;
    public float wanderSpeed = 2f;
    public float wanderRadius = 5f;

    [Header("Combat")]
    public float maxHealth = 50f;
    public float contactDamage = 5f;
    public float contactDamageInterval = 1f;
    public float contactDamageRange = 1f;
    public float releaseRecoilForce = 10f;
    public float releaseRecoilRange = 1f;

    [Header("Flame System")]
    public bool canReignite = false;
    public bool startsExtinguished = false;

    [Header("Flame Materials")]
    public Material materialGreenLight;
    public Material materialAlert;
    public Material materialRedLight;

    [Header("Alert Pulse")]
    public float alertPulseIntensityMin = 1.0f;
    public float alertPulseIntensityMax = 3.0f;
    public float alertPulseSpeed = 2.0f;

    [Header("RedLight Pulse")]
    public float redlightPulseIntensityMin = 9.0f;
    public float redlightPulseIntensityMax = 10.0f;
    public float redlightPulseSpeed = 1.0f;

    
}