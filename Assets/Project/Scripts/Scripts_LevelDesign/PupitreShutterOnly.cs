using UnityEngine;
using System.Collections;
using TMPro;

public class PupitreShutterOnly : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private MetalShutterSafe metalShutter;

    [Header("Countdown")]
    [SerializeField] private bool launchCountdown = false;
    [SerializeField] private TextMeshProUGUI countdownText;
    [SerializeField] private string countdownLabel = "Zone 2";
    [SerializeField] private float countdownFontSize = 100f;
    [SerializeField] private float rotationDuration = 2f;
    [SerializeField] private float fadeOutDuration = 1f;
    [SerializeField] private bool clockwise = true;
    [SerializeField] private AudioClip jingleClip;
    private AudioSource audioSource;

    [Header("Game Logic")]
    [SerializeField] private bool launchGameLogic = false;
    [SerializeField] private GameManager gameManager;
    [SerializeField] private float gameStartDelay = 4.5f;

    [Header("Interaction")]
    [SerializeField] private float interactionRange = 1f;

    private Transform player;
    private bool hasActivated = false;

    private void Start()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
            player = playerObj.transform;

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        if (countdownText != null)
            countdownText.gameObject.SetActive(false);
    }

    private void Update()
    {
        if (hasActivated || player == null) return;
        float distance = Vector3.Distance(transform.position, player.position);
        if (distance <= interactionRange && PlayerInputManager.Instance.InteractPressed)
            Activate();
    }

    private void Activate()
    {
        hasActivated = true;
        GetComponent<InteractBubble>()?.Hide();

        if (metalShutter != null)
            metalShutter.StartOpening();
        else
            Debug.LogWarning("[PupitreShutterOnly] Aucun MetalShutter assigne !");

        if (launchCountdown)
            StartCoroutine(CountdownSequence());

        if (launchGameLogic)
            StartCoroutine(StartGameAfterDelay());
    }

    private IEnumerator CountdownSequence()
    {
        if (countdownText == null) yield break;

        if (jingleClip != null)
            audioSource.PlayOneShot(jingleClip);

        countdownText.gameObject.SetActive(true);
        countdownText.fontSize = countdownFontSize;
        countdownText.text = countdownLabel;
        countdownText.transform.localScale = Vector3.one;

        Color c = countdownText.color;
        c.a = 1f;
        countdownText.color = c;

        // Phase 1 : rotation 360
        float elapsed = 0f;
        Quaternion startRotation = countdownText.transform.localRotation;
        float rotationDirection = clockwise ? 360f : -360f;

        while (elapsed < rotationDuration)
        {
            elapsed += Time.deltaTime;
            float angle = Mathf.Lerp(0f, rotationDirection, elapsed / rotationDuration);
            countdownText.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
            yield return null;
        }

        countdownText.transform.localRotation = startRotation;

        // Phase 2 : fade out
        elapsed = 0f;
        while (elapsed < fadeOutDuration)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Lerp(1f, 0f, elapsed / fadeOutDuration);
            c = countdownText.color;
            c.a = alpha;
            countdownText.color = c;
            yield return null;
        }

        // Cleanup
        countdownText.gameObject.SetActive(false);
        countdownText.transform.localRotation = startRotation;
        c = countdownText.color;
        c.a = 1f;
        countdownText.color = c;
    }

    private IEnumerator StartGameAfterDelay()
    {
        yield return new WaitForSeconds(gameStartDelay);

        if (gameManager != null)
            gameManager.StartGameCycle();

        EnemyIconsUI enemyIconsUI = FindObjectOfType<EnemyIconsUI>();
        if (enemyIconsUI != null)
            enemyIconsUI.SpawnIconsForEnemies();

        CorpseIconsUI corpseIconsUI = FindObjectOfType<CorpseIconsUI>();
        if (corpseIconsUI != null)
            corpseIconsUI.SpawnIconsForCorpses();
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, interactionRange);
    }
}