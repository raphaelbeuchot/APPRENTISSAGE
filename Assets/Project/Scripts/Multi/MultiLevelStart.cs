using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Unity.Cinemachine;

// Depart de niveau multi : remplace PupitreInteraction (pas d'interaction, pas de pupitre).
// Au lancement de la scene : les vcams de depart sont actives et un texte "Get ready" s'affiche
// readyDuration secondes, puis on enchaine compte a rebours, rideau, bascule vers les vcams normales,
// montee camera (CameraPanningExtension), et lancement du cycle de la sentinelle apres gameStartDelay.
// Pas de gel des joueurs : le rideau les bloque. Rien a relancer au respawn (voir MultiRespawn).
public class MultiLevelStart : MonoBehaviour
{
    [Header("References (auto-trouvees si vides)")]
    [SerializeField] private CountdownManager countdownManager;
    [SerializeField] private MetalShutter metalShutter;
    [SerializeField] private GameManager gameManager;

    [Header("Texte d'attente")]
    [SerializeField] private string readyMessage = "Get ready";
    [Tooltip("Duree d'affichage du texte avant le compte a rebours et le rideau. 0 = pas de texte.")]
    [SerializeField] private float readyDuration = 3f;
    [SerializeField] private float readyFadeDuration = 0.4f;
    [SerializeField] private float readyFontSize = 120f;
    [Tooltip("Police du texte (vide = police TMP par defaut).")]
    [SerializeField] private TMP_FontAsset readyFont;

    [Header("Timing")]
    [Tooltip("Delai entre le chargement de la scene et l'affichage du texte d'attente.")]
    [SerializeField] private float startDelay = 1f;
    [Tooltip("Delai entre le lancement du compte a rebours et le debut du cycle de la sentinelle (meme valeur que le pupitre).")]
    [SerializeField] private float gameStartDelay = 4.5f;

    [Header("Cameras (une par joueur, vide = ignore)")]
    [SerializeField] private CinemachineCamera[] startZoneCameras;
    [SerializeField] private CinemachineCamera[] normalCameras;
    [SerializeField] private int activePriority = 20;
    [SerializeField] private int inactivePriority = 10;

    public bool HasStarted { get; private set; }

    private IEnumerator Start()
    {
        if (countdownManager == null)
            countdownManager = FindAnyObjectByType<CountdownManager>();
        if (metalShutter == null)
            metalShutter = FindAnyObjectByType<MetalShutter>();
        if (gameManager == null)
            gameManager = FindAnyObjectByType<GameManager>();

        // Meme principe que PupitreInteraction.InitializeCamerasNextFrame
        yield return new WaitForEndOfFrame();
        SetCameraPriorities(startZoneCameras, activePriority);
        SetCameraPriorities(normalCameras, inactivePriority);

        yield return new WaitForSeconds(startDelay);
        StartLevel();
    }

    public void StartLevel()
    {
        if (HasStarted) return;
        HasStarted = true;
        StartCoroutine(StartSequence());
    }

    private IEnumerator StartSequence()
    {
        if (readyDuration > 0f)
            yield return ShowReadyText();

        GameUIManager gameUIManager = FindAnyObjectByType<GameUIManager>();
        if (gameUIManager != null) gameUIManager.ShowGameplayBars();

        SwitchToNormalCameras();

        if (countdownManager != null && countdownManager.gameObject.activeInHierarchy)
            countdownManager.StartCountdown();
        else
            Debug.LogWarning("[MultiStart] Pas de CountdownManager actif dans la scene");

        if (metalShutter != null)
            metalShutter.StartOpening();
        else
            Debug.LogWarning("[MultiStart] Pas de MetalShutter dans la scene");

        StartCoroutine(StartGameAfterDelay());
    }

    // Canvas overlay cree a la volee : couvre tout l'ecran, donc centre entre les deux moities en split-screen
    private IEnumerator ShowReadyText()
    {
        GameObject canvasObj = new GameObject("MultiReadyCanvas");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        GameObject textObj = new GameObject("ReadyText");
        textObj.transform.SetParent(canvasObj.transform, false);
        RectTransform rect = textObj.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        TextMeshProUGUI text = textObj.AddComponent<TextMeshProUGUI>();
        if (readyFont != null) text.font = readyFont;
        text.text = readyMessage;
        text.fontSize = readyFontSize;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        text.raycastTarget = false;

        float fade = Mathf.Min(readyFadeDuration, readyDuration);
        yield return new WaitForSeconds(readyDuration - fade);

        float elapsed = 0f;
        while (elapsed < fade)
        {
            elapsed += Time.deltaTime;
            Color c = text.color;
            c.a = 1f - Mathf.Clamp01(elapsed / fade);
            text.color = c;
            yield return null;
        }

        Destroy(canvasObj);
    }

    private void SwitchToNormalCameras()
    {
        SetCameraPriorities(normalCameras, activePriority);
        SetCameraPriorities(startZoneCameras, inactivePriority);

        // Toutes les instances : une par vcam joueur en multi
        CameraPanningExtension[] panningExts = FindObjectsByType<CameraPanningExtension>(FindObjectsSortMode.None);
        foreach (CameraPanningExtension panningExt in panningExts)
            panningExt.OnPlayerExitStartZone();
    }

    private static void SetCameraPriorities(CinemachineCamera[] cameras, int priority)
    {
        if (cameras == null) return;
        foreach (CinemachineCamera cam in cameras)
        {
            if (cam != null)
                cam.Priority.Value = priority;
        }
    }

    private IEnumerator StartGameAfterDelay()
    {
        yield return new WaitForSeconds(gameStartDelay);

        Debug.Log("[MultiStart] === LANCEMENT DU JEU ===");

        if (gameManager != null)
            gameManager.StartGameCycle();

        EnemyIconsUI enemyIconsUI = FindAnyObjectByType<EnemyIconsUI>();
        if (enemyIconsUI != null)
            enemyIconsUI.SpawnIconsForEnemies();

        CorpseIconsUI corpseIconsUI = FindAnyObjectByType<CorpseIconsUI>();
        if (corpseIconsUI != null)
            corpseIconsUI.SpawnIconsForCorpses();
    }
}
