using UnityEngine;
using System;
using System.Collections;

[RequireComponent(typeof(Collider))]
public class GoalDoorNew : MonoBehaviour
{
    [Header("Configuration")]
    public bool isActive = false;
    [SerializeField] private float unlockDelay = 1f;

    

    [HideInInspector] public Renderer doorRenderer;
    [Header("Visuels")]

    public Material activeMaterial;
    public Material inactiveMaterial;

    [Header("Audio")]
    public AudioClip doorReachedSound;
    public AudioClip doorUnlockedSound; // son quand la cle est ramassee et la porte s'ouvre
    public AudioClip doorLockedSound;   // son feedback si le joueur arrive sans cle
    private AudioSource audioSource;

    //public GameObject victoryEffectPrefab;
    //public bool destroyPlayerOnReach = false;

    public event Action<GameObject> OnPlayerReached;

    private bool hasBeenReached = false;

    void Start()
    {

        if (doorRenderer == null)
            doorRenderer = GetComponent<Renderer>();
        Collider col = GetComponent<Collider>();
        if (col != null)
            col.isTrigger = true;
        else
            Debug.LogError("[GoalDoorNew] Pas de Collider trouve!");

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        

        UpdateVisuals();

    }

    void OnTriggerEnter(Collider other)
    {
        if (hasBeenReached) return;

        PlayerPhysicsMovement player = other.GetComponent<PlayerPhysicsMovement>();
        if (player == null) return;

        if (!isActive)
        {
            // feedback porte verrouillée
            if (audioSource != null && doorLockedSound != null)
                audioSource.PlayOneShot(doorLockedSound);
            Debug.Log("[GoalDoorNew] Porte verrouillee - ramassez la cle!");
            return;
        }

        ReachGoal(other.gameObject);
    }

    void ReachGoal(GameObject player)
    {
        hasBeenReached = true;
        Debug.Log("[GoalDoorNew] VICTOIRE!");

        if (audioSource != null && doorReachedSound != null)
            audioSource.PlayOneShot(doorReachedSound);

        //if (victoryEffectPrefab != null)
          //  Instantiate(victoryEffectPrefab, transform.position, Quaternion.identity);

        OnPlayerReached?.Invoke(player);

        PlayerPhysicsMovement playerMovement = player.GetComponent<PlayerPhysicsMovement>();
        if (playerMovement != null)
            playerMovement.enabled = false;

        SentinelCycleManager sentinelCycle = FindObjectOfType<SentinelCycleManager>();
        if (sentinelCycle != null)
            sentinelCycle.StopCycle();
        else
            Debug.LogWarning("[GoalDoorNew] SentinelCycleManager introuvable!");

        foreach (BombSpawner b in FindObjectsOfType<BombSpawner>())
            b.StopChargeAudio();

        //if (destroyPlayerOnReach)
            //Destroy(player, 2f);
    }

    public void UnlockWithKey()
    {
        if (isActive) return;
        StartCoroutine(UnlockAfterDelay());
    }

    private IEnumerator UnlockAfterDelay()
    {
        yield return new WaitForSeconds(unlockDelay);

        isActive = true;
        UpdateVisuals();

        if (audioSource != null && doorUnlockedSound != null)
            audioSource.PlayOneShot(doorUnlockedSound);

        Debug.Log("[GoalDoorNew] Cle ramassee - porte deverrouillee!");
    }

    public void SetActive(bool active)
    {
        isActive = active;
        UpdateVisuals();
    }

    void UpdateVisuals()
    {
        if (doorRenderer == null) return;

        if (isActive && activeMaterial != null)
            doorRenderer.material = activeMaterial;
        else if (!isActive && inactiveMaterial != null)
            doorRenderer.material = inactiveMaterial;
    }

    public void ResetDoor()
    {
        hasBeenReached = false;
        isActive = false;
        UpdateVisuals();
    }

    void OnDrawGizmos()
    {
        Gizmos.color = isActive ? Color.green : Color.red;
        Gizmos.DrawWireCube(transform.position, Vector3.one * 2f);

        if (isActive)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position, transform.position + Vector3.up * 3f);
        }
    }
}