using UnityEngine;
using System;

public class StaminaSystem : MonoBehaviour
{
    [Header("Stamina Settings")]
    [SerializeField] private float maxStamina = 100f;
    [SerializeField] private float currentStamina;

    [Header("Sprint")]
    [SerializeField] private float staminaDrainPerSecond = 20f;
    [SerializeField] private float sprintSpeedMultiplier = 1.8f;
    [SerializeField] private KeyCode sprintKey = KeyCode.LeftShift;

    [Header("Recovery")]
    [SerializeField] private float staminaRegenPerSecond = 15f;
    [SerializeField] private float regenDelayAfterUse = 1f; // Délai avant régénération

    [Header("Exhaustion")]
    [SerializeField] private float exhaustedSpeedMultiplier = 0.6f;
    [SerializeField] private float exhaustionThreshold = 10f; // En dessous = épuisé

    [Header("References")]
    [SerializeField] private PlayerPhysicsMovement movement;

    private float baseSpeed;
    private bool isSprinting = false;
    private bool isExhausted = false;
    private float lastUseTime = 0f;

    // Events
    public event Action<float> OnStaminaChanged; // Passe le pourcentage (0-1)
    public event Action OnExhausted;
    public event Action OnRecovered;

    void Start()
    {
        currentStamina = maxStamina;

        if (movement == null)
        {
            movement = GetComponent<PlayerPhysicsMovement>();
        }

        if (movement != null)
        {
            baseSpeed = movement.speed;
        }
    }

    void Update()
    {
        HandleSprintInput();
        HandleStaminaRecovery();
        UpdateMovementSpeed();
    }

    void HandleSprintInput()
    {
        // Ne peut pas sprinter si épuisé ou pas assez de stamina
        bool wantsToSprint = Input.GetKey(sprintKey);
        bool canSprint = currentStamina > 0 && !isExhausted;

        if (wantsToSprint && canSprint)
        {
            StartSprint();
        }
        else
        {
            StopSprint();
        }
    }

    void StartSprint()
    {
        if (!isSprinting)
        {
            isSprinting = true;
        }

        // Drain stamina
        float drain = staminaDrainPerSecond * Time.deltaTime;
        currentStamina = Mathf.Max(0, currentStamina - drain);
        lastUseTime = Time.time;

        OnStaminaChanged?.Invoke(currentStamina / maxStamina);

        // Vérifier épuisement
        if (currentStamina <= exhaustionThreshold && !isExhausted)
        {
            BecomeExhausted();
        }
    }

    void StopSprint()
    {
        if (isSprinting)
        {
            isSprinting = false;
        }
    }

    void HandleStaminaRecovery()
    {
        // Régénération uniquement si on ne sprint pas et après le délai
        if (!isSprinting && Time.time - lastUseTime >= regenDelayAfterUse)
        {
            float regen = staminaRegenPerSecond * Time.deltaTime;
            currentStamina = Mathf.Min(maxStamina, currentStamina + regen);

            OnStaminaChanged?.Invoke(currentStamina / maxStamina);

            // Récupération de l'épuisement
            if (isExhausted && currentStamina >= maxStamina) // 30% pour sortir de l'épuisement
            {
                RecoverFromExhaustion();
            }
        }
    }

    void UpdateMovementSpeed()
    {
        if (movement == null) return;

        float speedMultiplier = 1f;

        if (isSprinting)
        {
            speedMultiplier = sprintSpeedMultiplier;
        }
        else if (isExhausted)
        {
            speedMultiplier = exhaustedSpeedMultiplier;
        }

        // Note: Ceci s'applique sur la vitesse de base
        // Si HumanHealth a déjà réduit la vitesse, ça se cumule
        movement.SetSpeedMultiplier(speedMultiplier);
    }

    void BecomeExhausted()
    {
        isExhausted = true;
        isSprinting = false;
        Debug.Log("Human is EXHAUSTED!");
        OnExhausted?.Invoke();
    }

    void RecoverFromExhaustion()
    {
        isExhausted = false;
        Debug.Log("Human recovered from exhaustion!");
        OnRecovered?.Invoke();
    }

    // Méthode pour vider complètement la stamina (dash, etc.)
    public void DrainStamina(float amount)
    {
        currentStamina = Mathf.Max(0, currentStamina - amount);
        lastUseTime = Time.time;
        OnStaminaChanged?.Invoke(currentStamina / maxStamina);

        if (currentStamina <= exhaustionThreshold && !isExhausted)
        {
            BecomeExhausted();
        }
    }

    // Getters
    public float GetCurrentStamina() => currentStamina;
    public float GetMaxStamina() => maxStamina;
    public float GetStaminaPercentage() => currentStamina / maxStamina;
    public bool IsSprinting() => isSprinting;
    public bool IsExhausted() => isExhausted;
}