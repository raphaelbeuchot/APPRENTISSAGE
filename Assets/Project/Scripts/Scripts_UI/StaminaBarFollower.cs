using UnityEngine;
using UnityEngine.UI;

public class StaminaBarFollower : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerPhysicsMovement playerMovement;
    [SerializeField] private Image staminaFillImage;
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("Bonus Bar")]
    [SerializeField] private Image bonusBarFill;
    [SerializeField] private RectTransform bonusBarContainer;
    [SerializeField] private RectTransform mainBarContainer;

    [Header("Config")]
    [SerializeField] private float baseContainerWidth = 300f;
    [SerializeField] private float baseContainerHeight = 15f;

    [Header("Display Settings")]
    [SerializeField] private float fadeSpeed = 5f;

    [Header("Colors")]
    [SerializeField] private Color fullColor = Color.cyan;
    [SerializeField] private Color emptyColor = Color.red;
    [SerializeField] private Color depletedColor = Color.gray;

    private Camera mainCamera;
    private bool bonusInitialized = false;

    void Start()
    {
        mainCamera = Camera.main;

        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        if (playerMovement == null)
        {
            playerMovement = GetComponentInParent<PlayerPhysicsMovement>();
            if (playerMovement == null)
                playerMovement = FindObjectOfType<PlayerPhysicsMovement>();
        }

        canvasGroup.alpha = 0f;
    }

    void Update()
    {
        if (playerMovement == null) return;

        float baseMax = playerMovement.GetBaseMaxStamina();
        float realMax = playerMovement.GetRealMaxStamina();

        if (!bonusInitialized)
        {
            InitializeBonusBar(baseMax, realMax);
            bonusInitialized = true;
        }

        UpdateStaminaBar(baseMax, realMax);
        UpdateVisibility();
    }

    void InitializeBonusBar(float baseMax, float realMax)
    {
        bool hasBonus = realMax > baseMax;

        if (bonusBarContainer != null)
        {
            bonusBarContainer.gameObject.SetActive(hasBonus);

            if (hasBonus)
            {
                float actualWidth = mainBarContainer != null ? mainBarContainer.rect.width : baseContainerWidth;
                float bonusWidth = actualWidth * ((realMax / baseMax) - 1f);
                bonusBarContainer.sizeDelta = new Vector2(bonusWidth, baseContainerHeight);
            }
        }
    }

    void UpdateStaminaBar(float baseMax, float realMax)
    {
        if (staminaFillImage == null) return;

        float currentStamina = playerMovement.GetCurrentStamina();
        bool hasBonus = realMax > baseMax;

        if (bonusBarFill != null && hasBonus)
        {
            float bonusStamina = realMax - baseMax;
            float bonusFill = Mathf.Clamp01((currentStamina - baseMax) / bonusStamina);
            bonusBarFill.fillAmount = bonusFill;
        }

        float mainFill = Mathf.Clamp01(Mathf.Min(currentStamina, baseMax) / baseMax);
        staminaFillImage.fillAmount = mainFill;

        if (currentStamina <= 0f)
            staminaFillImage.color = depletedColor;
        else
            staminaFillImage.color = Color.Lerp(emptyColor, fullColor, mainFill);
    }

    void UpdateVisibility()
    {
        if (canvasGroup == null) return;
        canvasGroup.alpha = Mathf.Lerp(canvasGroup.alpha, 1f, Time.deltaTime * fadeSpeed);
    }
}