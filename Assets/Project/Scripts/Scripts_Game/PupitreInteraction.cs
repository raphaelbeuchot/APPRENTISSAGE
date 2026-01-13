using UnityEngine;
using Unity.Cinemachine;

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

    [Header("Interaction")]
    [SerializeField] private float interactionRange = 2f;

    private Transform player;
    private bool hasActivated = false;
    private const string RESTART_KEY = "AutoStartCountdown";

    private void Start()
    {
        // Auto-find references si pas assignées
        if (countdownManager == null)
            countdownManager = FindAnyObjectByType<CountdownManager>();
        if (metalShutter == null)
            metalShutter = FindAnyObjectByType<MetalShutter>();

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
            player = playerObj.transform;

        // Check si restart
        bool isRestart = PlayerPrefs.GetInt(RESTART_KEY, 0) == 1;

        if (isRestart)
        {
            Debug.Log("[Pupitre] RESTART DETECTE - Auto-activation");
            // Clear le flag
            PlayerPrefs.SetInt(RESTART_KEY, 0);
            PlayerPrefs.Save();

            // Activation automatique
            ActivatePupitreOnRestart();
        }
        else
        {
            Debug.Log("[Pupitre] PREMIER LANCEMENT - Etat normal");
            // Au démarrage normal : StartZone camera active
            if (startZoneCamera != null)
                startZoneCamera.Priority = activePriority;
            if (normalCamera != null)
                normalCamera.Priority = inactivePriority;

            Debug.Log($"[Pupitre] Init - StartZone Priority: {startZoneCamera?.Priority}, Normal Priority: {normalCamera?.Priority}");
        }
    }

    private void Update()
    {
        if (hasActivated || player == null) return;

        // Check distance player
        float distance = Vector3.Distance(transform.position, player.position);
        bool playerInRange = distance <= interactionRange;

        // Check input X
        if (playerInRange && PlayerInputManager.Instance.InteractPressed)
        {
            ActivatePupitre();
        }
    }

    private void ActivatePupitre()
    {
        hasActivated = true;
        Debug.Log("[Pupitre] === ACTIVATION MANUELLE ===");

        // Switch caméra AVANT le countdown
        SwitchToNormalCamera();

        // Lance countdown et rideau
        LaunchCountdownAndShutter();
    }

    private void ActivatePupitreOnRestart()
    {
        hasActivated = true;
        Debug.Log("[Pupitre] === ACTIVATION AUTO (RESTART) ===");

        // Switch caméra IMMÉDIATEMENT
        SwitchToNormalCamera();

        // Lance countdown et rideau
        LaunchCountdownAndShutter();
    }

    private void SwitchToNormalCamera()
    {
        Debug.Log($"[Pupitre] AVANT switch - StartZone: {startZoneCamera?.Priority}, Normal: {normalCamera?.Priority}");

        if (normalCamera != null)
            normalCamera.Priority = activePriority;
        if (startZoneCamera != null)
            startZoneCamera.Priority = inactivePriority;

        Debug.Log($"[Pupitre] APRES switch - StartZone: {startZoneCamera?.Priority}, Normal: {normalCamera?.Priority}");

        // Notifie CameraPanning si présent
        CameraPanningExtension panningExt = FindAnyObjectByType<CameraPanningExtension>();
        if (panningExt != null)
        {
            panningExt.OnPlayerExitStartZone();
            Debug.Log("[Pupitre] CameraPanning notifie");
        }
    }

    private void LaunchCountdownAndShutter()
    {
        // 1. Lance le countdown
        if (countdownManager != null)
        {
            countdownManager.StartCountdown();
            Debug.Log("[Pupitre] Countdown lance");
        }
        else
        {
            Debug.LogError("[Pupitre] CountdownManager NULL !");
        }

        // 2. Lève le rideau
        if (metalShutter != null)
        {
            metalShutter.StartOpening();
            Debug.Log("[Pupitre] Rideau leve");
        }
        else
        {
            Debug.LogWarning("[Pupitre] MetalShutter NULL (optionnel)");
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, interactionRange);
    }
}