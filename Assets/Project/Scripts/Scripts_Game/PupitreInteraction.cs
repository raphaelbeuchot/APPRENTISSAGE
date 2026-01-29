using UnityEngine;
using Unity.Cinemachine;
using System.Collections;

public class PupitreInteraction : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CountdownManager countdownManager;
    [SerializeField] private MetalShutter metalShutter;

    [Header("Game Start")]
    [SerializeField] private float gameStartDelay = 4.5f; // Temps du countdown
    [SerializeField] private GameManager gameManager;

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
        Debug.LogError($"[Pupitre] START - isRestart: {isRestart}, PlayerPrefs value: {PlayerPrefs.GetInt(RESTART_KEY, 0)}");

        if (isRestart)
        {
            Debug.Log("[Pupitre] RESTART DETECTE - Activation mode force respawn");
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
            Debug.LogError($"[Pupitre] LATEUPDATE frame {respawnFrameCount} - Player pos: {player.position}");
            respawnFrameCount++;

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

        Debug.LogError($"[Pupitre] RESPAWN CALLED - Player pos AVANT: {player.position}, Pupitre pos: {transform.position}");

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
        directionToPupitre.y = 0;
        player.rotation = Quaternion.LookRotation(directionToPupitre);

        // Reinitialise etats du movement
        PlayerPhysicsMovement movement = player.GetComponent<PlayerPhysicsMovement>();
        if (movement != null)
        {
            movement.ResetAllInputs();
            movement.enabled = true;
            movement.canMove = true;
        }

        Debug.LogError($"[Pupitre] Player position APRES respawn : {player.position}");
    }

    private void ActivatePupitre()
    {
        hasActivated = true;
        Debug.Log("[Pupitre] === ACTIVATION MANUELLE ===");

        StartCoroutine(SinkPupitreCoroutine());
        SwitchToNormalCamera();
        LaunchCountdownAndShutter();

        StartGameLogic(); //  AJOUTER CETTE LIGNE
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

        transform.position = transform.position + Vector3.down * sinkDistance;
        SwitchToNormalCamera();
        LaunchCountdownAndShutter();

        StartGameLogic(); //  AJOUTER CETTE LIGNE
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
            panningExt.EnableFreeCameraMode();
            Debug.Log("[Pupitre] CameraPanning notifie");
        }
    }

    private void LaunchCountdownAndShutter()
    {
        // Vérifier que le GameObject est actif
        if (countdownManager != null && countdownManager.gameObject.activeInHierarchy)
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

    private void StartGameLogic()
    {
        StartCoroutine(StartGameAfterDelay());
    }

    private IEnumerator StartGameAfterDelay()
    {
        Debug.Log($"[Pupitre] Attente {gameStartDelay}s avant de lancer le jeu...");
        yield return new WaitForSeconds(gameStartDelay);

        Debug.Log("[Pupitre] === LANCEMENT DU JEU ===");

        if (gameManager != null)
        {
            gameManager.StartGameCycle();
        }

        EnemyIconsUI enemyIconsUI = FindObjectOfType<EnemyIconsUI>();
        if (enemyIconsUI != null)
        {
            enemyIconsUI.SpawnIconsForEnemies();
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