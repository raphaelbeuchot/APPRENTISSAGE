using UnityEngine;
using TMPro;
using System.Collections;

public class GameOverUI : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private GameObject gameOverPanel;
    [SerializeField] private CanvasGroup gameOverCanvasGroup;
    [SerializeField] private TextMeshProUGUI gameOverText;

    [SerializeField] private float textAnimDuration = 0.4f;
    [SerializeField] private float textAnimStartScaleX = 8f;


    [Header("Messages")]
    [SerializeField]
    private string[] deathMessages = new string[]
    {
        "RED MEANS DEAD !",
        "DEAD MAN !",
        "RESTLESS IS DEATH !",
        "THE URGE TO BUDGE !",
        "ONE MOVE TOO FAR !"
    };

    [Header("Audio")]
    [SerializeField] private AudioClip gameOverSound;
    private AudioSource audioSource;

    [Header("Settings")]
    [SerializeField] private float delayBeforeRestart = 1f;

    private bool isActive = false;

    void Start()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        if (gameOverPanel != null)
            gameOverPanel.SetActive(true);

        Hide();
    }

    void Update()
    {
        if (!isActive) return;
        if (Input.GetButtonDown("Submit") || Input.GetKeyDown(KeyCode.Return))
        {
            CancelInvoke(nameof(AutoRestart));
            AutoRestart();
        }
    }
    public void Show()
    {
        isActive = true;

        Time.timeScale = 1f;

        SentinelCycleManager cycle = FindObjectOfType<SentinelCycleManager>();
        if (cycle != null) cycle.StopCycle();

        if (gameOverCanvasGroup != null)
        {
            gameOverCanvasGroup.alpha = 1f;
            gameOverCanvasGroup.interactable = true;
            gameOverCanvasGroup.blocksRaycasts = true;
        }

        if (gameOverText != null && deathMessages.Length > 0)
        {
            string randomMessage = deathMessages[Random.Range(0, deathMessages.Length)];
            gameOverText.text = randomMessage;
            StartCoroutine(AnimateText());
        }

        if (audioSource != null && gameOverSound != null)
            audioSource.PlayOneShot(gameOverSound);

        Debug.Log("=== GAME OVER ===");

        Invoke(nameof(AutoRestart), delayBeforeRestart);
    }
    private IEnumerator AnimateText()
    {
        if (gameOverText == null) yield break;

        float elapsed = 0f;
        Vector3 startScale = new Vector3(textAnimStartScaleX, 1f, 1f);
        Vector3 endScale = Vector3.one;

        while (elapsed < textAnimDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / textAnimDuration);
            float smooth = Mathf.SmoothStep(0f, 1f, t);
            gameOverText.transform.localScale = Vector3.LerpUnclamped(startScale, endScale, smooth);
            yield return null;
        }

        gameOverText.transform.localScale = endScale;
    }
    public void Hide()
    {
        CancelInvoke(nameof(AutoRestart));
        isActive = false;

        if (gameOverCanvasGroup != null)
        {
            gameOverCanvasGroup.alpha = 0f;
            gameOverCanvasGroup.interactable = false;
            gameOverCanvasGroup.blocksRaycasts = false;
        }
    }

    void AutoRestart()
    {
        LevelManager levelManager = FindObjectOfType<LevelManager>();
        if (levelManager != null)
            levelManager.RestartLevel();
        else
            UnityEngine.SceneManagement.SceneManager.LoadScene(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene().name
            );
    }
}