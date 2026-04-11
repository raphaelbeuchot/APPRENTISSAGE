using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class VictoryUI : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private GameObject victoryPanel;
    [SerializeField] private CanvasGroup victoryCanvasGroup;

    private bool isActive = false;
    private bool isTransitioning = false;

    void Start()
    {
        if (victoryPanel != null)
            victoryPanel.SetActive(true);

        Hide();
    }

    void Update()
    {
        if (isActive && !isTransitioning)
        {
            if (Input.anyKeyDown || Input.GetButtonDown("Submit") || Input.GetButtonDown("Jump") ||
                Input.GetButtonDown("Fire1") || Input.GetButtonDown("Fire2") || Input.GetButtonDown("Fire3"))
            {
                StartCoroutine(LoadNextLevelRoutine());
            }
        }
    }

    public void Show(PlayerHealth playerHealth = null)
    {
        isActive = true;
        isTransitioning = false;

        Time.timeScale = 0f;

        if (victoryCanvasGroup != null)
        {
            victoryCanvasGroup.alpha = 1f;
            victoryCanvasGroup.interactable = true;
            victoryCanvasGroup.blocksRaycasts = true;
        }

        Debug.Log("=== VICTORY === Press any button to continue");
    }

    IEnumerator LoadNextLevelRoutine()
    {
        isTransitioning = true;
        Time.timeScale = 1f;
        LevelManager levelManager = FindObjectOfType<LevelManager>();
        if (levelManager != null)
            levelManager.LoadWheelOrLevelSelect();
        else
            Debug.LogWarning("LevelManager not found!");
        yield break;
    }

    public void Hide()
    {
        isActive = false;

        if (victoryCanvasGroup != null)
        {
            victoryCanvasGroup.alpha = 0f;
            victoryCanvasGroup.interactable = false;
            victoryCanvasGroup.blocksRaycasts = false;
        }
    }
}