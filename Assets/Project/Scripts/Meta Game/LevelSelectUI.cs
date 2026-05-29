using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using System.Collections.Generic;
using UnityEngine.UI;

public class LevelSelectUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform listContainer;
    [SerializeField] private GameObject levelEntryPrefab;

    [Header("Audio")]
    [SerializeField] private UIAudioPlayer uiAudio;

    [Header("Visual Settings")]
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color selectedColor = Color.yellow;
    [SerializeField] private Color lockedColor = new Color(0.3f, 0.3f, 0.3f, 1f);
    [SerializeField] private Color completedColor = new Color(0.6f, 1f, 0.6f, 1f);
    [SerializeField] private float selectedScale = 1.2f;
    [SerializeField] private float transitionSpeed = 10f;

    [Header("Scroll")]
    [SerializeField] private float lineHeight = 40f;
    [SerializeField] private float lineSpacing = 0f;

    private int visibleCount => Mathf.FloorToInt(listContainer.GetComponent<RectTransform>().rect.height / (lineHeight + lineSpacing));

    private List<LevelData> levels;
    private List<GameObject> entries = new List<GameObject>();
    private int currentSelection = 0;
    private int scrollOffset = 0;
    private float navigationCooldown = 0f;
    private float cooldownDuration = 0.2f;
    private Vector3 normalScale = Vector3.one;

    private int totalItems => levels != null ? levels.Count + 1 : 1;

    void Awake()
    {
        if (uiAudio == null) uiAudio = FindObjectOfType<UIAudioPlayer>();
    }

    void Start()
    {
        Time.timeScale = 1f;

        if (LevelProgressionManager.Instance == null)
        {
            Debug.LogWarning("[LevelSelectUI] LevelProgressionManager introuvable !");
            return;
        }

        levels = LevelProgressionManager.Instance.GetAllLevels();
        BuildList();
        UpdateVisuals();
        int startIndex = LevelProgressionManager.Instance.lastUnlockedLevelIndex;
        if (startIndex > 0 && startIndex < levels.Count)
        {
            currentSelection = startIndex;
            scrollOffset = Mathf.Max(0, currentSelection - 1);
        }
    }

    void Update()
    {
        HandleNavigation();
        UpdateVisuals();
    }

    void HandleNavigation()
    {
        if (navigationCooldown > 0f)
        {
            navigationCooldown -= Time.unscaledDeltaTime;
            return;
        }

        Vector2 input = PlayerInputManager.Instance.MoveInput;

        if (input.y > 0.5f)
        {
            currentSelection = Mathf.Max(0, currentSelection - 1);
            AdjustScroll();
            navigationCooldown = cooldownDuration;
            if (uiAudio != null) uiAudio.PlayUp();
        }
        else if (input.y < -0.5f)
        {
            currentSelection = Mathf.Min(totalItems - 1, currentSelection + 1);
            AdjustScroll();
            navigationCooldown = cooldownDuration;
            if (uiAudio != null) uiAudio.PlayDown();
        }

        if (AnyFaceButtonPressed())
            SelectCurrentOption();

        if (Input.GetButtonDown("Cancel"))
        {
            if (uiAudio != null) uiAudio.PlayB();
            GoBack();
        }
    }

    void AdjustScroll()
    {
        if (currentSelection < scrollOffset)
            scrollOffset = currentSelection;
        else if (currentSelection >= scrollOffset + visibleCount)
            scrollOffset = currentSelection - visibleCount + 1;
    }

    void SelectCurrentOption()
    {
        if (currentSelection == levels.Count)
        {
            if (uiAudio != null) uiAudio.PlayB();
            GoBack();
            return;
        }

        LevelData data = levels[currentSelection];
        if (!LevelProgressionManager.Instance.IsUnlocked(data.sceneIndex))
            return;

        if (uiAudio != null) uiAudio.PlayLevelSelectedAndSurvive();

        LoadingScreenManager.TargetSceneIndex = data.sceneIndex;
        SceneManager.LoadScene(1);
    }

    void GoBack()
    {
        LoadingScreenManager.TargetSceneIndex = 0;
        SceneManager.LoadScene(1);
    }

    void BuildList()
    {
        foreach (GameObject go in entries)
            Destroy(go);
        entries.Clear();

        for (int i = 0; i < levels.Count; i++)
        {
            GameObject entry = Instantiate(levelEntryPrefab, listContainer);
            LayoutElement le = entry.GetComponent<LayoutElement>();
            if (le == null) le = entry.AddComponent<LayoutElement>();
            le.preferredHeight = lineHeight;
            entries.Add(entry);
        }

        GameObject retourEntry = Instantiate(levelEntryPrefab, listContainer);
        LayoutElement lRetour = retourEntry.GetComponent<LayoutElement>();
        if (lRetour == null) lRetour = retourEntry.AddComponent<LayoutElement>();
        lRetour.preferredHeight = lineHeight;
        entries.Add(retourEntry);
    }

    void UpdateVisuals()
    {
        if (levels == null) return;

        for (int i = 0; i < entries.Count; i++)
        {
            int displayIndex = i - scrollOffset;
            bool visible = displayIndex >= 0 && displayIndex < visibleCount;
            entries[i].SetActive(visible);

            if (!visible) continue;

            TMP_Text nameText = entries[i].transform.Find("LevelName")?.GetComponent<TMP_Text>();

            bool isRetour = i == levels.Count;
            bool isSelected = i == currentSelection;

            if (isRetour)
            {
                SetEntryVisual(nameText, "RETOUR", isSelected, true, false);
                continue;
            }

            LevelData data = levels[i];
            bool isUnlocked = LevelProgressionManager.Instance.IsUnlocked(data.sceneIndex);
            bool isCompleted = LevelProgressionManager.Instance.IsCompleted(data.sceneIndex);

            int collected = LevelProgressionManager.Instance.GetCollectedCountForLevel(data.sceneIndex);
            int total = LevelProgressionManager.Instance.GetTotalCollectiblesForLevel(data.sceneIndex);
            bool hasAllCollectibles = total > 0 && collected >= total;

            string displayName = isUnlocked ? data.levelName + (hasAllCollectibles ? " *" : "") : "???";
            SetEntryVisual(nameText, displayName, isSelected, isUnlocked, isCompleted);
        }
    }

    void SetEntryVisual(TMP_Text txt, string label, bool isSelected, bool isUnlocked, bool isCompleted)
    {
        if (txt == null) return;

        txt.text = label;

        Color targetColor;
        if (!isUnlocked)
            targetColor = lockedColor;
        else if (isSelected)
            targetColor = selectedColor;
        else if (isCompleted)
            targetColor = completedColor;
        else
            targetColor = normalColor;

        txt.color = Color.Lerp(txt.color, targetColor, Time.unscaledDeltaTime * transitionSpeed);

        Vector3 targetScale = isSelected ? normalScale * selectedScale : normalScale;
        txt.transform.localScale = Vector3.Lerp(txt.transform.localScale, targetScale, Time.unscaledDeltaTime * transitionSpeed);
    }

    bool AnyFaceButtonPressed()
    {
        return Input.GetButtonDown("Submit");
    }
}