using UnityEngine;
using System;

public class HumanHealth : MonoBehaviour
{
    [Header("Injury System")]
    [SerializeField] private int maxInjuries = 4;
    [SerializeField] private int currentInjuries = 0;

    [Header("Speed Reduction per Injury")]
    [Range(0f, 1f)]
    [SerializeField] private float speedAtInjury1 = 0.85f;
    [Range(0f, 1f)]
    [SerializeField] private float speedAtInjury2 = 0.65f;
    [Range(0f, 1f)]
    [SerializeField] private float speedAtInjury3 = 0.45f;
    [Range(0f, 1f)]
    [SerializeField] private float speedAtInjury4 = 0.25f;

    [Header("References")]
    [SerializeField] private PlayerPhysicsMovement movement;

    private float baseSpeed;
    private bool isDead = false;
    private bool isInKnockbackGrace = false;
    private float knockbackGraceEndTime = 0f;

    public event Action<int> OnInjuryReceived;
    public event Action OnDeath;

    void Start()
    {
        if (movement == null)
        {
            movement = GetComponent<PlayerPhysicsMovement>();
        }

        if (movement != null)
        {
            baseSpeed = movement.speed;
        }
    }

    // Méthode publique pour le GameManager
    public bool IsInKnockbackGracePeriod()
    {
        if (isInKnockbackGrace && Time.time >= knockbackGraceEndTime)
        {
            isInKnockbackGrace = false;
        }
        return isInKnockbackGrace;
    }

    // Méthode à appeler quand le joueur est libéré d'un grab avec knockback
    public void StartKnockbackGracePeriod(float duration = 0.3f)
    {
        isInKnockbackGrace = true;
        knockbackGraceEndTime = Time.time + duration;
    }

    // Optionnel : Reset de la grace period
    public void ResetKnockbackGrace()
    {
        isInKnockbackGrace = false;
        knockbackGraceEndTime = 0f;
    }


    public void TakeDamage()
    {
        if (isDead) return;

        currentInjuries++;
        Debug.Log($"Human injured! Current injuries: {currentInjuries}/{maxInjuries}");

        OnInjuryReceived?.Invoke(currentInjuries);

        if (currentInjuries >= maxInjuries)
        {
            Die();
        }
        else
        {
            UpdateMovementSpeed();
        }
    }

    void UpdateMovementSpeed()
    {
        if (movement == null) return;

        float speedMultiplier = GetSpeedMultiplier();
        movement.speed = baseSpeed * speedMultiplier;

        Debug.Log($"Human speed updated: {movement.speed} (x{speedMultiplier})");
    }

    float GetSpeedMultiplier()
    {
        switch (currentInjuries)
        {
            case 0:
                return 1f;
            case 1:
                return speedAtInjury1;
            case 2:
                return speedAtInjury2;
            case 3:
                return speedAtInjury3;
            case 4:
            default:
                return speedAtInjury4;
        }
    }

    void Die()
    {
        if (isDead) return;

        isDead = true;
        Debug.Log("HUMAN IS DEAD!");

        OnDeath?.Invoke();

        if (movement != null)
        {
            movement.enabled = false;
        }

        //var meleeSystem = GetComponent<MeleeAttackSystem>();
        //if (meleeSystem != null) meleeSystem.enabled = false;
    }

    public void Heal(int amount = 1)
    {
        if (isDead) return;

        currentInjuries = Mathf.Max(0, currentInjuries - amount);
        Debug.Log($"Human healed! Current injuries: {currentInjuries}/{maxInjuries}");

        UpdateMovementSpeed();
    }

    public int GetCurrentInjuries() => currentInjuries;
    public int GetMaxInjuries() => maxInjuries;
    public float GetHealthPercentage() => 1f - ((float)currentInjuries / maxInjuries);
    public bool IsDead() => isDead;
    public bool IsCritical() => currentInjuries >= maxInjuries - 1;
}