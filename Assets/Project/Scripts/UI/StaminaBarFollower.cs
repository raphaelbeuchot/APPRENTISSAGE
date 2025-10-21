using UnityEngine;
using UnityEngine.UI;

public class StaminaBarFollower : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private StaminaSystem staminaSystem;
    [SerializeField] private Image staminaFillImage;
    [SerializeField] private CanvasGroup canvasGroup;


    [Header("Display Settings")]
    [SerializeField] private float showThreshold = 0.99f; // Afficher si stamina < 99%
    [SerializeField] private float fadeSpeed = 5f;

    [Header("Colors")]
    [SerializeField] private Color fullColor = Color.cyan;
    [SerializeField] private Color emptyColor = Color.red;
    [SerializeField] private Color exhaustedColor = Color.gray;

    private Camera mainCamera;
    private bool shouldShow = false;

    void Start()
    {
        mainCamera = Camera.main;

        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }
        }

        if (staminaSystem == null)
        {
            staminaSystem = GetComponentInParent<StaminaSystem>();
        }

        // Commencer invisible
        canvasGroup.alpha = 0f;
    }

    void Update()
    {
        FaceCamera();
        UpdateStaminaBar();
        UpdateVisibility();
    }

    void FaceCamera()
    {
        if (mainCamera == null) return;

        // Faire face à la caméra
        transform.LookAt(transform.position + mainCamera.transform.rotation * Vector3.forward,
                         mainCamera.transform.rotation * Vector3.up);
    }

    void UpdateStaminaBar()
    {
        if (staminaSystem == null || staminaFillImage == null) return;

        float staminaPercent = staminaSystem.GetStaminaPercentage();
        staminaFillImage.fillAmount = staminaPercent;

        // Couleur selon l'état
        if (staminaSystem.IsExhausted())
        {
            staminaFillImage.color = exhaustedColor;
        }
        else
        {
            staminaFillImage.color = Color.Lerp(emptyColor, fullColor, staminaPercent);
        }

        // Déterminer si on doit afficher la barre
        shouldShow = staminaPercent < showThreshold;
    }

    void UpdateVisibility()
    {
        if (canvasGroup == null) return;

        float targetAlpha = shouldShow ? 1f : 0f;
        canvasGroup.alpha = Mathf.Lerp(canvasGroup.alpha, targetAlpha, Time.deltaTime * fadeSpeed);
    }
}