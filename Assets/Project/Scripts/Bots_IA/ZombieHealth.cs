using UnityEngine;
using System;

public class ZombieHealth : MonoBehaviour
{
    [Header("Configuration des Membres")]
    [Range(0f, 1f)]
    public float speedWithOneArm = 1f;

    [Range(0f, 1f)]
    public float speedWithOneArmOneLeg = 0.6f;

    [Range(0f, 1f)]
    public float crawlSpeed = 0.3f;

    [Range(0f, 1f)]
    public float crawlSpeedNoArms = 0.15f;

    [Header("References")]
    public PlayerPhysicsMovement playerMovement;

    [Header("Effets Visuels")]
    public GameObject leftArmVisual;
    public GameObject rightArmVisual;
    public GameObject leftLegVisual;
    public GameObject rightLegVisual;

    [Header("Prefabs")]
    public GameObject limbPrefab;
    public float limbEjectionForce = 10f;

    [Header("Gunshot Recovery")]
    public float gunshotRecoveryDuration = 2f;

    private bool hasLeftArm = true;
    private bool hasRightArm = true;
    private bool hasLeftLeg = true;
    private bool hasRightLeg = true;

    private int damageCount = 0;
    private bool isDead = false;
    private float baseSpeed;
    private bool isRecovering = false;
    private float recoverUntilTime = 0f;

    public event Action<string> OnLimbLost;
    public event Action OnHeadshot;
    public event Action OnDeath;

    public bool HasAtLeastOneArm()
    {
        return hasLeftArm || hasRightArm;
    }

    void Start()
    {
        if (playerMovement == null)
        {
            playerMovement = GetComponent<PlayerPhysicsMovement>();
        }

        if (playerMovement != null)
        {
            baseSpeed = playerMovement.speed;
        }
    }

    void Update()
    {
        if (isRecovering && Time.time >= recoverUntilTime)
        {
            isRecovering = false;
            Debug.Log($"{gameObject.name} recovered from gunshot!");
        }
    }

    public void TakeDamage(bool isHeadshot = false)
    {
        if (isDead) return;

        if (isHeadshot)
        {
            HandleHeadshot();
            return;
        }

        damageCount++;

        StartRecovery();

        switch (damageCount)
        {
            case 1:
                LoseFirstArm();
                break;
            case 2:
                LoseFirstLeg();
                break;
            case 3:
                LoseSecondLeg();
                break;
            case 4:
                LoseSecondArm();
                Debug.Log("TOUS LES MEMBRES PERDUS! Le zombie meurt!");
                HandleDeath();
                break;
            default:
                Debug.Log("Zombie a deja perdu tous ses membres!");
                break;
        }

        UpdateMovementSpeed();
    }

    void StartRecovery()
    {
        isRecovering = true;
        recoverUntilTime = Time.time + gunshotRecoveryDuration;

        ZombieAI zombieAI = GetComponent<ZombieAI>();
        if (zombieAI != null)
        {
            zombieAI.StunByGunshot(gunshotRecoveryDuration);
        }

        // Libérer la cible si le zombie est en train de grabber
        ZombieGrabSystem grabSystem = GetComponent<ZombieGrabSystem>();
        if (grabSystem != null && grabSystem.IsGrabbing())
        {
            grabSystem.ForceRelease();
        }

        Debug.Log($"{gameObject.name} starts recovery for {gunshotRecoveryDuration}s");
    }

    void LoseFirstArm()
    {
        if (UnityEngine.Random.value > 0.5f)
        {
            hasLeftArm = false;
            Debug.Log("Bras GAUCHE arrache!");
            OnLimbLost?.Invoke("LeftArm");
            EjectLimb(leftArmVisual);
        }
        else
        {
            hasRightArm = false;
            Debug.Log("Bras DROIT arrache!");
            OnLimbLost?.Invoke("RightArm");
            EjectLimb(rightArmVisual);
        }
    }

    void LoseFirstLeg()
    {
        if (UnityEngine.Random.value > 0.5f)
        {
            hasLeftLeg = false;
            Debug.Log("Jambe GAUCHE arrachee! (Ralentissement)");
            OnLimbLost?.Invoke("LeftLeg");
            EjectLimb(leftLegVisual);
        }
        else
        {
            hasRightLeg = false;
            Debug.Log("Jambe DROITE arrachee! (Ralentissement)");
            OnLimbLost?.Invoke("RightLeg");
            EjectLimb(rightLegVisual);
        }
    }

    void LoseSecondLeg()
    {
        if (hasLeftLeg)
        {
            hasLeftLeg = false;
            Debug.Log("Jambe GAUCHE arrachee! (ON RAMPE!)");
            OnLimbLost?.Invoke("LeftLeg");
            EjectLimb(leftLegVisual);
        }
        else
        {
            hasRightLeg = false;
            Debug.Log("Jambe DROITE arrachee! (ON RAMPE!)");
            OnLimbLost?.Invoke("RightLeg");
            EjectLimb(rightLegVisual);
        }
    }

    void LoseSecondArm()
    {
        if (hasLeftArm)
        {
            hasLeftArm = false;
            Debug.Log("Dernier bras (GAUCHE) arrache! (Rampe avec 1 bras)");
            OnLimbLost?.Invoke("LeftArm");
            EjectLimb(leftArmVisual);
        }
        else
        {
            hasRightArm = false;
            Debug.Log("Dernier bras (DROIT) arrache! (Rampe avec 1 bras)");
            OnLimbLost?.Invoke("RightArm");
            EjectLimb(rightArmVisual);
        }
    }

    void HandleHeadshot()
    {
        isDead = true;
        Debug.Log("HEADSHOT! MORT INSTANTANEE!");

        OnHeadshot?.Invoke();
        OnDeath?.Invoke();

        if (playerMovement != null)
        {
            playerMovement.enabled = false;
        }

        EjectBody();
    }

    void HandleDeath()
    {
        isDead = true;
        Debug.Log("Le zombie est mort (tous les membres perdus)");

        OnDeath?.Invoke();

        if (playerMovement != null)
        {
            playerMovement.enabled = false;
        }

        Destroy(gameObject, 3f);
    }

    void UpdateMovementSpeed()
    {
        if (playerMovement == null) return;

        int legsLost = (!hasLeftLeg ? 1 : 0) + (!hasRightLeg ? 1 : 0);
        int armsLost = (!hasLeftArm ? 1 : 0) + (!hasRightArm ? 1 : 0);

        float speedMultiplier = 1f;

        if (legsLost == 0)
        {
            if (armsLost == 0)
            {
                speedMultiplier = 1f;
            }
            else
            {
                speedMultiplier = speedWithOneArm;
            }
        }
        else if (legsLost == 1)
        {
            speedMultiplier = speedWithOneArmOneLeg;
        }
        else if (legsLost == 2)
        {
            if (armsLost <= 1)
            {
                speedMultiplier = crawlSpeed;
            }
            else
            {
                speedMultiplier = crawlSpeedNoArms;
            }
        }

        playerMovement.speed = baseSpeed * speedMultiplier;
        Debug.Log("Membres perdus - Bras: " + armsLost + " / Jambes: " + legsLost + " | Vitesse: " + playerMovement.speed + " (x" + speedMultiplier + ")");
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

                limbRb.AddForce(ejectDirection * limbEjectionForce, ForceMode.Impulse);
                limbRb.AddTorque(UnityEngine.Random.insideUnitSphere * 5f, ForceMode.Impulse);
            }
        }
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

            rb.AddForce(ejectDirection * limbEjectionForce * 2f, ForceMode.Impulse);
            rb.AddTorque(UnityEngine.Random.insideUnitSphere * 10f, ForceMode.Impulse);
        }

        Destroy(gameObject, 5f);
    }

    public bool HasLeftArm() => hasLeftArm;
    public bool HasRightArm() => hasRightArm;
    public bool HasLeftLeg() => hasLeftLeg;
    public bool HasRightLeg() => hasRightLeg;
    public bool IsDead() => isDead;
    public bool IsCrawling() => !hasLeftLeg && !hasRightLeg;
    public bool IsRecovering() => isRecovering;
}