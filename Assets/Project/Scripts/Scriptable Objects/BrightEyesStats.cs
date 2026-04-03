using UnityEngine;

[CreateAssetMenu(fileName = "BrightEyesStats", menuName = "Enemy/Bright Eyes Stats")]
public class BrightEyesStats : ScriptableObject
{
    [Header("Health")]
    public float maxHealth = 100f;

    [Header("Detection")]
    public float detectionRange = 4f;
    public float detectionAngle = 360f;

    [Header("Attraction")]
    public float attractionForce = 1f;
    public float playerSlowdownMultiplier = 0.8f;

    [Header("Flame Material")]
    public Material materialDead;
}