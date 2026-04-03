using UnityEngine;
using System;

[RequireComponent(typeof(Collider))]
public class GoalDoor : MonoBehaviour
{
    [Header("Configuration")]
    [Tooltip("La porte est-elle active des le debut?")]
    public bool isActive = true;

    [Header("Visuels")]
    public Renderer doorRenderer;
    public Material activeMaterial;
    public Material inactiveMaterial;

    [Header("Audio")]
    public AudioClip doorReachedSound;
    public AudioClip doorActivatedSound; // NOUVEAU - son quand la porte s'active apres tous les kills
    private AudioSource audioSource;

    [Header("Effets")]
    public GameObject victoryEffectPrefab;
    public bool destroyPlayerOnReach = false;

    // Evenements
    public event Action<GameObject> OnPlayerReached;

    private bool hasBeenReached = false;

    void Start()
    {
        // S'assurer qu'il y a un trigger collider
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            col.isTrigger = true;
        }
        else
        {
            Debug.LogError("GoalDoor: Pas de Collider trouve! Ajoutez un Box Collider ou Sphere Collider.");
        }

        // Setup audio
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        // Mise a jour visuelle initiale
        UpdateVisuals();
    }

    void OnTriggerEnter(Collider other)
    {
        // Verifie si c'est le joueur qui entre
        if (!isActive || hasBeenReached) return;

        PlayerPhysicsMovement player = other.GetComponent<PlayerPhysicsMovement>();
        if (player != null)
        {
            ReachGoal(other.gameObject);
        }
    }

    void ReachGoal(GameObject player)
    {
        hasBeenReached = true;
        Debug.Log("VICTOIRE! Le joueur a atteint la porte!");

        // Son de victoire
        if (audioSource != null && doorReachedSound != null)
        {
            audioSource.PlayOneShot(doorReachedSound);
        }

        // Effet visuel
        if (victoryEffectPrefab != null)
        {
            Instantiate(victoryEffectPrefab, transform.position, Quaternion.identity);
        }

        // Notifier les autres scripts
        OnPlayerReached?.Invoke(player);

        // Desactiver le mouvement du joueur
        PlayerPhysicsMovement playerMovement = player.GetComponent<PlayerPhysicsMovement>();
        if (playerMovement != null)
        {
            playerMovement.enabled = false;
        }

        // NOUVEAU : Arreter le cycle de la sentinelle
        SentinelCycleManager sentinelCycle = FindObjectOfType<SentinelCycleManager>();
        if (sentinelCycle != null)
        {
            sentinelCycle.StopCycle();
        }
        else
        {
            Debug.LogWarning("[GoalDoor] SentinelCycleManager introuvable !");
        }
        foreach (BombSpawner b in FindObjectsOfType<BombSpawner>())
            b.StopChargeAudio();
        // Option: detruire le joueur
        if (destroyPlayerOnReach)
        {
            Destroy(player, 2f);
        }
    }

    /// <summary>
    /// Active la porte (appelee par LevelManager quand tous ennemis tues)
    /// </summary>
    public void ActivateDoor()
    {
        if (isActive) return; // Deja active

        isActive = true;
        UpdateVisuals();

        // Son d'activation
        if (audioSource != null && doorActivatedSound != null)
        {
            audioSource.PlayOneShot(doorActivatedSound);
        }

        Debug.Log("GoalDoor: Porte activee! Tous les ennemis sont elimines.");
    }

    /// <summary>
    /// Active ou desactive la porte
    /// </summary>
    public void SetActive(bool active)
    {
        isActive = active;
        UpdateVisuals();
        Debug.Log("GoalDoor: Porte " + (active ? "activee" : "desactivee"));
    }

    /// <summary>
    /// Met a jour les visuels selon l'etat actif/inactif
    /// </summary>
    void UpdateVisuals()
    {
        if (doorRenderer != null)
        {
            if (isActive && activeMaterial != null)
            {
                doorRenderer.material = activeMaterial;
            }
            else if (!isActive && inactiveMaterial != null)
            {
                doorRenderer.material = inactiveMaterial;
            }
        }
    }

    /// <summary>
    /// Reinitialise la porte (utile pour rejouer)
    /// </summary>
    public void ResetDoor()
    {
        hasBeenReached = false;
        isActive = true;
        UpdateVisuals();
    }

    // Visualisation dans l'editeur
    void OnDrawGizmos()
    {
        Gizmos.color = isActive ? Color.green : Color.gray;
        Gizmos.DrawWireCube(transform.position, Vector3.one * 2f);

        if (isActive)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position, transform.position + Vector3.up * 3f);
        }
    }
}