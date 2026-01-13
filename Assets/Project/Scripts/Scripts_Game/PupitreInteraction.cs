using UnityEngine;
using Unity.Cinemachine;
using System.Collections;

public class PupitreInteraction : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CountdownManager countdownManager;
    [SerializeField] private MetalShutter metalShutter;

    [Header("Camera Switch")]
    [SerializeField] private CinemachineCamera startZoneCamera;
    [SerializeField] private CinemachineCamera normalCamera;
    [SerializeField] private int activePriority = 20;
    [SerializeField] private int inactivePriority = 10;

    [Header("Pupitre Animation")]
    [SerializeField] private float sinkDistance = 1f;
    [SerializeField] private float sinkDuration = 0.5f;

    [Header("Interaction")]
    [SerializeField] private float interactionRange = 1f;

    [Header("Restart Spawn")]
    [SerializeField] private float restartSpawnDistance = 1f;

    private Transform player;
    private bool hasActivated = false;
    private const string RESTART_KEY = "AutoStartCountdown";

    private bool needsRespawn = false;
    private int respawnFrameCount = 0;

    private void Start()
    {
        // Auto-find references
        if (countdownManager == null)
            countdownManager = FindAnyObjectByType<CountdownManager>();
        if (metalShutter == null)
            metalShutter = FindAnyObjectByType<MetalShutter>();

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
            player = playerObj.transform;

        // Check si restart
        bool isRestart = PlayerPrefs.GetInt(RESTART_KEY, 0) == 1;
        Debug.Log($"[Pupitre] PlayerPrefs check: {PlayerPrefs.GetInt(RESTART_KEY, 0)}, isRestart: {isRestart}");

        if (isRestart)
        {
            Debug.Log("[Pupitre] RESTART DETECTE - Activation mode force respawn");
            PlayerPrefs.SetInt(RESTART_KEY, 0);
            PlayerPrefs.Save();

            needsRespawn = true;
            respawnFrameCount = 0;
        }
        else
        {
            Debug.Log("[Pupitre] PREMIER LANCEMENT - Etat normal");
            StartCoroutine(InitializeCamerasNextFrame());
        }
    }
    private IEnumerator InitializeCamerasNextFrame()
    {
        yield return new WaitForEndOfFrame();

        if (startZoneCamera != null)
            startZoneCamera.Priority.Value = activePriority;
        if (normalCamera != null)
            normalCamera.Priority.Value = inactivePriority;

        Debug.Log("[Pupitre] Cameras initialisees apres 1 frame");
    }
    private void LateUpdate()
    {
        if (needsRespawn)
        {
            respawnFrameCount++;
            Debug.Log($"[Pupitre] Force respawn frame {respawnFrameCount}");

            // Forcer le respawn pendant les 3 premieres frames
            if (respawnFrameCount <= 3)
            {
                RespawnPlayerAtPupitre();
            }
            else
            {
                Debug.Log("[Pupitre] Fin force respawn, activation auto");
                needsRespawn = false;
                ActivatePupitreOnRestart();
            }
        }
    }

    private void Update()
    {
        if (hasActivated || player == null) return;

        float distance = Vector3.Distance(transform.position, player.position);
        bool playerInRange = distance <= interactionRange;

        if (playerInRange && PlayerInputManager.Instance.InteractPressed)
        {
            ActivatePupitre();
        }
    }

    private void RespawnPlayerAtPupitre()
    {
        if (player == null)
        {
            Debug.LogError("[Pupitre] Player NULL dans RespawnPlayerAtPupitre !");
            return;
        }

        Debug.Log($"[Pupitre] Player position AVANT respawn : {player.position}");

        // Position devant le pupitre (en coordonnees LOCALES du pupitre)
        Vector3 spawnPos = transform.position - transform.forward * restartSpawnDistance;
        spawnPos.y = player.position.y;

        // Reset velocities
        Rigidbody playerRb = player.GetComponent<Rigidbody>();
        if (playerRb != null)
        {
            playerRb.linearVelocity = Vector3.zero;
            playerRb.angularVelocity = Vector3.zero;
        }

        player.position = spawnPos;

        // Faire regarder le player vers le pupitre
        Vector3 directionToPupitre = (transform.position - spawnPos).normalized;
        directionToPupitre.y = 0; // Garder rotation horizontale seulement
        player.rotation = Quaternion.LookRotation(directionToPupitre);

        // Reinitialise etats du movement
        PlayerPhysicsMovement movement = player.GetComponent<PlayerPhysicsMovement>();
        if (movement != null)
        {
            movement.ResetAllInputs();
            movement.enabled = true;
            movement.canMove = true;
        }

        Debug.Log($"[Pupitre] Player position APRES respawn : {player.position}");
    }

    private void ActivatePupitre()
    {
        hasActivated = true;
        Debug.Log("[Pupitre] === ACTIVATION MANUELLE ===");

        StartCoroutine(SinkPupitreCoroutine());

        SwitchToNormalCamera();
        LaunchCountdownAndShutter();
    }

    private IEnumerator SinkPupitreCoroutine()
    {
        Vector3 startPos = transform.position;
        Vector3 targetPos = startPos + Vector3.down * sinkDistance;
        float elapsed = 0f;

        while (elapsed < sinkDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / sinkDuration;
            transform.position = Vector3.Lerp(startPos, targetPos, t);
            yield return null;
        }

        transform.position = targetPos;
    }

    private void ActivatePupitreOnRestart()
    {
        hasActivated = true;
        Debug.Log("[Pupitre] === ACTIVATION AUTO (RESTART) ===");

        StartCoroutine(SinkPupitreCoroutine());

        SwitchToNormalCamera();
        LaunchCountdownAndShutter();
    }

    private void SwitchToNormalCamera()
    {
        Debug.Log($"[Pupitre] AVANT switch - StartZone priority: {startZoneCamera?.Priority.Value}, Normal priority: {normalCamera?.Priority.Value}");

        if (normalCamera != null)
            normalCamera.Priority.Value = activePriority;
        if (startZoneCamera != null)
            startZoneCamera.Priority.Value = inactivePriority;

        Debug.Log($"[Pupitre] APRES switch - StartZone priority: {startZoneCamera?.Priority.Value}, Normal priority: {normalCamera?.Priority.Value}");

        CameraPanningExtension panningExt = FindAnyObjectByType<CameraPanningExtension>();
        if (panningExt != null)
        {
            panningExt.OnPlayerExitStartZone();
            Debug.Log("[Pupitre] CameraPanning notifie");
        }
    }

    private void LaunchCountdownAndShutter()
    {
        if (countdownManager != null)
        {
            countdownManager.StartCountdown();
            Debug.Log("[Pupitre] Countdown lance");
        }

        if (metalShutter != null)
        {
            metalShutter.StartOpening();
            Debug.Log("[Pupitre] Rideau leve");
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, interactionRange);

        // Visualise spawn point restart
        Gizmos.color = Color.yellow;
        Vector3 spawnPos = transform.position - transform.forward * restartSpawnDistance;
        Gizmos.DrawWireSphere(spawnPos, 0.5f);
        Gizmos.DrawLine(transform.position, spawnPos);
    }

}