using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;


public class MainMenuUI : MonoBehaviour
{
    [Header("Buttons")]
    public GameObject btnStartWithTuto;
    public GameObject btnSkipTuto;
    public GameObject btnCommandes;

    [Header("Commandes Panel")]
    public GameObject commandesPanel;
    public Image commandesImage;
    public Sprite commandesSprite;

    [Header("Navigation")]
    public Color selectedColor = Color.white;
    public Color unselectedColor = new Color(0.5f, 0.5f, 0.5f, 1f);
    public Vector3 selectedScale = new Vector3(1.1f, 1.1f, 1.1f);
    public Vector3 unselectedScale = Vector3.one;
    public float navigationCooldown = 0.2f;

    private int selectedIndex = 0;
    private int optionCount = 3;
    private float lastNavTime = 0f;
    private bool commandesPanelOpen = false;

    void Start()
    {
        if (commandesPanel != null)
        {
            commandesPanel.SetActive(false);

            if (commandesImage != null && commandesSprite != null)
                commandesImage.sprite = commandesSprite;
        }

        Time.timeScale = 1f;
        UpdateVisuals();
    }

    void Update()
    {
        if (commandesPanelOpen)
        {
            if (AnyFaceButtonPressed())
                CloseCommandesPanel();
            return;
        }

        HandleNavigation();
        HandleConfirm();
    }

    void HandleNavigation()
    {
        if (Time.unscaledTime - lastNavTime < navigationCooldown) return;

        Vector2 input = PlayerInputManager.Instance.MoveInput;

        if (input.y < -0.5f)
        {
            selectedIndex = (selectedIndex + 1) % optionCount;
            lastNavTime = Time.unscaledTime;
            UpdateVisuals();
        }
        else if (input.y > 0.5f)
        {
            selectedIndex = (selectedIndex - 1 + optionCount) % optionCount;
            lastNavTime = Time.unscaledTime;
            UpdateVisuals();
        }
    }

    void HandleConfirm()
    {
        if (!AnyFaceButtonPressed()) return;

        switch (selectedIndex)
        {
            case 0: StartWithTuto(); break;
            case 1: SkipTuto(); break;
            case 2: OpenCommandesPanel(); break;
        }
    }

    void StartWithTuto()
    {
        LoadingScreenManager.TargetSceneIndex = 1;
        SceneManager.LoadScene(16);
    }

    void SkipTuto()
    {
        LoadingScreenManager.TargetSceneIndex = 3;
        SceneManager.LoadScene(16);
    }

    void OpenCommandesPanel()
    {
        commandesPanelOpen = true;
        if (commandesPanel != null)
            commandesPanel.SetActive(true);
    }

    void CloseCommandesPanel()
    {
        commandesPanelOpen = false;
        if (commandesPanel != null)
            commandesPanel.SetActive(false);
    }

    void UpdateVisuals()
    {
        SetButtonVisual(btnStartWithTuto, selectedIndex == 0);
        SetButtonVisual(btnSkipTuto, selectedIndex == 1);
        SetButtonVisual(btnCommandes, selectedIndex == 2);
    }

    void SetButtonVisual(GameObject btn, bool isSelected)
    {
        if (btn == null) return;

        btn.transform.localScale = isSelected ? selectedScale : unselectedScale;

        TMP_Text txt = btn.GetComponentInChildren<TMP_Text>();
        if (txt != null)
            txt.color = isSelected ? selectedColor : unselectedColor;
    }
    bool AnyFaceButtonPressed()
    {
        return PlayerInputManager.Instance.InteractPressed
            || PlayerInputManager.Instance.ReloadPressed
            || PlayerInputManager.Instance.SprayAttackPressed
            || PlayerInputManager.Instance.BroomAttackPressed;
    }
}