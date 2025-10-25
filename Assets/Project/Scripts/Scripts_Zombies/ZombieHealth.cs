using UnityEngine;
using System;

public class ZombieHealth : MonoBehaviour
{
    [Header("Zombie Stats")]
    public ZombieStats stats; // Reference au ScriptableObject

    
    [Header("Effets Visuels")]
    public GameObject leftArmVisual;
    public GameObject rightArmVisual;
    public GameObject leftLegVisual;
    public GameObject rightLegVisual;
    

    [Header("Prefabs")]
    public GameObject limbPrefab;

    
    // Etat des membres
    private bool hasLeftArm = true;
    private bool hasRightArm = true;
    private bool hasLeftLeg = true;
    private bool hasRightLeg = true;
    

    // Sante
    private float currentHealth;
    private bool isDead = false;

    // Recovery apres tir
    private bool isRecovering = false;
    private float recoverUntilTime = 0f;

    // Events
    
    public event Action<string> OnLimbLost;
    
    
    public event Action OnHeadshot;
    public event Action OnDeath;
    public event Action<float, float> OnHealthChanged; // current, max

    void Start()
    {
        if (stats == null)
        {
            Debug.LogError("ZombieStats non assigne sur " + gameObject.name);
            return;
        }

        // Initialisation
        currentHealth = stats.maxHealth;
        hasLeftArm = stats.startingArmCount >= 1;
        hasRightArm = stats.startingArmCount >= 2;
        hasLeftLeg = stats.startingLegCount >= 1;
        hasRightLeg = stats.startingLegCount >= 2;
    }

    void Update()
    {
        if (isRecovering && Time.time >= recoverUntilTime)
        {
            isRecovering = false;
            Debug.Log($"{gameObject.name} recovered from gunshot!");
        }
    }

    // ===============================================
    // SYSTEME DE DEGATS
    // ===============================================

    /// <summary>
    /// Degats par melee attack du joueur
    /// </summary>
    public void TakeMeleeDamage(float damage)
    {
        if (isDead) return;

        currentHealth -= damage;
        currentHealth = Mathf.Max(0f, currentHealth);

        Debug.Log($"{gameObject.name} took {damage} melee damage! Health: {currentHealth}/{stats.maxHealth}");

        OnHealthChanged?.Invoke(currentHealth, stats.maxHealth);

        
        // Check perte de membres
        CheckLimbLoss();
        

        // Check mort
        if (currentHealth <= 0f)
        {
            HandleDeath();
        }

        UpdateSpeed();
    }

    /// <summary>
    /// Degats par tir de sentinelle
    /// </summary>
    public void TakeSentinelShot(bool isHeadshot = false)
    {
        if (isDead) return;

        if (isHeadshot)
        {
            HandleHeadshot();
            return;
        }

        // Degats de sentinelle (depuis stats)
        currentHealth -= stats.sentinelDamageTaken;
        currentHealth = Mathf.Max(0f, currentHealth);

        Debug.Log($"{gameObject.name} shot by sentinel! Health: {currentHealth}/{stats.maxHealth}");

        OnHealthChanged?.Invoke(currentHealth, stats.maxHealth);

        // Recovery stun
        StartRecovery();
        
        // Check perte de membres
        CheckLimbLoss();
        

        // Check mort
        if (currentHealth <= 0f)
        {
            HandleDeath();
        }

        UpdateSpeed();
    }

    void StartRecovery()
    {
        isRecovering = true;
        recoverUntilTime = Time.time + 2f; // Tu peux mettre ca dans stats si tu veux

        ZombieAI zombieAI = GetComponent<ZombieAI>();
        /*
         * if (zombieAI != null)
        {
            zombieAI.StunByGunshot(2f);
        }
        */

        // Liberer la cible si grab
        ZombieGrabSystem grabSystem = GetComponent<ZombieGrabSystem>();
        if (grabSystem != null && grabSystem.IsGrabbing())
        {
            grabSystem.ForceRelease();
        }

        Debug.Log($"{gameObject.name} starts recovery");
    }

    
    // ===============================================
    // SYSTEME DE PERTE DE MEMBRES
    // ===============================================

    void CheckLimbLoss()
    {
        float healthPercent = currentHealth / stats.maxHealth;

        // Perte de membres seulement en dessous du seuil
        if (healthPercent > stats.limbLossThreshold)
            return;

        // Determiner quel membre perdre aleatoirement
        if (hasLeftArm && hasRightArm && hasLeftLeg && hasRightLeg)
        {
            // Premier membre : un bras
            LoseRandomArm();
        }
        else if ((hasLeftArm || hasRightArm) && hasLeftLeg && hasRightLeg)
        {
            // Deuxieme membre : une jambe
            LoseRandomLeg();
        }
        else if ((hasLeftArm || hasRightArm) && (hasLeftLeg || hasRightLeg))
        {
            // Troisieme membre : autre jambe
            LoseOtherLeg();
        }
        else if ((hasLeftArm || hasRightArm) && !hasLeftLeg && !hasRightLeg)
        {
            // Dernier membre : dernier bras
            LoseOtherArm();
        }
    }

    void LoseRandomArm()
    {
        if (UnityEngine.Random.value > 0.5f && hasLeftArm)
        {
            hasLeftArm = false;
            Debug.Log("Bras GAUCHE arrache!");
            OnLimbLost?.Invoke("LeftArm");
            EjectLimb(leftArmVisual);
        }
        else if (hasRightArm)
        {
            hasRightArm = false;
            Debug.Log("Bras DROIT arrache!");
            OnLimbLost?.Invoke("RightArm");
            EjectLimb(rightArmVisual);
        }
    }

    void LoseRandomLeg()
    {
        if (UnityEngine.Random.value > 0.5f && hasLeftLeg)
        {
            hasLeftLeg = false;
            Debug.Log("Jambe GAUCHE arrachee!");
            OnLimbLost?.Invoke("LeftLeg");
            EjectLimb(leftLegVisual);
        }
        else if (hasRightLeg)
        {
            hasRightLeg = false;
            Debug.Log("Jambe DROITE arrachee!");
            OnLimbLost?.Invoke("RightLeg");
            EjectLimb(rightLegVisual);
        }
    }

    void LoseOtherLeg()
    {
        if (hasLeftLeg)
        {
            hasLeftLeg = false;
            Debug.Log("Jambe GAUCHE arrachee! (ON RAMPE!)");
            OnLimbLost?.Invoke("LeftLeg");
            EjectLimb(leftLegVisual);
        }
        else if (hasRightLeg)
        {
            hasRightLeg = false;
            Debug.Log("Jambe DROITE arrachee! (ON RAMPE!)");
            OnLimbLost?.Invoke("RightLeg");
            EjectLimb(rightLegVisual);
        }
    }

    void LoseOtherArm()
    {
        if (hasLeftArm)
        {
            hasLeftArm = false;
            Debug.Log("Dernier bras (GAUCHE) arrache!");
            OnLimbLost?.Invoke("LeftArm");
            EjectLimb(leftArmVisual);
        }
        else if (hasRightArm)
        {
            hasRightArm = false;
            Debug.Log("Dernier bras (DROIT) arrache!");
            OnLimbLost?.Invoke("RightArm");
            EjectLimb(rightArmVisual);
        }
    }

    void EjectLimb(GameObject limbVisual)
    {
        if (limbVisual != null)
        {
            limbVisual.SetActive(false);
        }

        if (limbPrefab != null)
        {
            Vector3 spawnPos = limbVisual != null ? limbVisual.transform.position : transform.position;
            GameObject ejectedLimb = Instantiate(limbPrefab, spawnPos, Quaternion.identity);

            Rigidbody limbRb = ejectedLimb.GetComponent<Rigidbody>();
            if (limbRb != null)
            {
                Vector3 ejectDirection = new Vector3(
                    UnityEngine.Random.Range(-1f, 1f),
                    UnityEngine.Random.Range(0.5f, 1.5f),
                    UnityEngine.Random.Range(-1f, 0.5f)
                ).normalized;

                limbRb.AddForce(ejectDirection * 10f, ForceMode.Impulse);
                limbRb.AddTorque(UnityEngine.Random.insideUnitSphere * 5f, ForceMode.Impulse);
            }
        }
    }
    

    // ===============================================
    // MORT
    // ===============================================

    void HandleHeadshot()
    {
        isDead = true;
        Debug.Log("HEADSHOT! MORT INSTANTANEE!");

        OnHeadshot?.Invoke();
        OnDeath?.Invoke();

        EjectBody();
    }

    void HandleDeath()
    {
        isDead = true;
        Debug.Log("Le zombie est mort");

        OnDeath?.Invoke();

        ZombieAI ai = GetComponent<ZombieAI>();
        if (ai != null) ai.enabled = false;

        Destroy(gameObject, 3f);
    }

    void EjectBody()
    {
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            Vector3 ejectDirection = new Vector3(
                UnityEngine.Random.Range(-0.5f, 0.5f),
                1.5f,
                UnityEngine.Random.Range(-1f, 0f)
            ).normalized;

            rb.AddForce(ejectDirection * 20f, ForceMode.Impulse);
            rb.AddTorque(UnityEngine.Random.insideUnitSphere * 10f, ForceMode.Impulse);
        }

        Destroy(gameObject, 5f);
    }

    // ===============================================
    // UPDATE VITESSE
    // ===============================================

    void UpdateSpeed()
    {
        ZombieAI ai = GetComponent<ZombieAI>();
        if (ai != null)
        {
            ai.UpdateSpeed(currentHealth, IsCrawling());
        }
    }

    // ===============================================
    // GETTERS
    // ===============================================

    public bool HasAtLeastOneArm() => hasLeftArm || hasRightArm;
    public bool HasLeftArm() => hasLeftArm;
    public bool HasRightArm() => hasRightArm;
    public bool HasLeftLeg() => hasLeftLeg;
    public bool HasRightLeg() => hasRightLeg;
    public bool IsDead() => isDead;
    public bool IsCrawling() => !hasLeftLeg && !hasRightLeg;
    public bool IsRecovering() => isRecovering;
    public float GetCurrentHealth() => currentHealth;
    public float GetMaxHealth() => stats.maxHealth;
    public int GetArmCount() => (hasLeftArm ? 1 : 0) + (hasRightArm ? 1 : 0);
}