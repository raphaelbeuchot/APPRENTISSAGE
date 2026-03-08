using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public class TutoChoiceUI : MonoBehaviour
{
    [Header("Menu Options")]
    [SerializeField] private TextMeshProUGUI withTutoText;
    [SerializeField] private TextMeshProUGUI skipTutoText;

    [Header("Visual Settings")]
    [SerializeField] private Color selectedColor = Color.yellow;
    [SerializeField] private Color unselectedColor = new Color(0.5f, 0.5f, 0.5f, 1f);
    [SerializeField] private float selectedScale = 1.2f;
    [SerializeField] private float transitionSpeed = 10f;

    private int selectedIndex = 0;
    private float navigationCooldown = 0f;
    private float cooldownDuration = 0.2f;
    private Vector3 normalScale = Vector3.one;

    void Start()
    {
        Time.timeScale = 1f;
        UpdateVisuals();
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
            selectedIndex = (selectedIndex - 1 + 2) % 2;
            navigationCooldown = cooldownDuration;
        }
        else if (input.y < -0.5f)
        {
            selectedIndex = (selectedIndex + 1) % 2;
            navigationCooldown = cooldownDuration;
        }

        if (AnyFaceButtonPressed())
        {
            if (selectedIndex == 0)
                StartWithTuto();
            else
                SkipTuto();
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

    void UpdateVisuals()
    {
        SetTextVisual(withTutoText, selectedIndex == 0);
        SetTextVisual(skipTutoText, selectedIndex == 1);
    }

    void SetTextVisual(TextMeshProUGUI txt, bool isSelected)
    {
        if (txt == null) return;
        txt.color = Color.Lerp(txt.color, isSelected ? selectedColor : unselectedColor, Time.unscaledDeltaTime * transitionSpeed);
        txt.transform.localScale = Vector3.Lerp(txt.transform.localScale, isSelected ? normalScale * selectedScale : normalScale, Time.unscaledDeltaTime * transitionSpeed);
    }

    bool AnyFaceButtonPressed()
    {
        return PlayerInputManager.Instance.InteractPressed
            || PlayerInputManager.Instance.ReloadPressed
            || PlayerInputManager.Instance.SprayAttackPressed
            || PlayerInputManager.Instance.BroomAttackPressed;
    }
}