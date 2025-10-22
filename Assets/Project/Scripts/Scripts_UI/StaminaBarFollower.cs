using UnityEngine;
using UnityEngine.UI;

public class StaminaBarFollower : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerPhysicsMovement playerMovement; // CHANGE
    [SerializeField] private Image staminaFillImage;
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("Display Settings")]
    [SerializeField] private float showThreshold = 0.99f; // Afficher si stamina < 99%
    [SerializeField] private float fadeSpeed = 5f;

    [Header("Colors")]
    [SerializeField] private Color fullColor = Color.cyan;
    [SerializeField] private Color emptyColor = Color.red;
    [SerializeField] private Color depletedColor = Color.gray; // RENAME pour clarte

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

        if (playerMovement == null)
        {
            // Chercher dans le parent ou dans la scene
            playerMovement = GetComponentInParent<PlayerPhysicsMovement>();
            if (playerMovement == null)
            {
                playerMovement = FindObjectOfType<PlayerPhysicsMovement>();
            }
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

        // Faire face a la camera
        transform.LookAt(transform.position + mainCamera.transform.rotation * Vector3.forward,
                         mainCamera.transform.rotation * Vector3.up);
    }

    void UpdateStaminaBar()
    {
        if (playerMovement == null || staminaFillImage == null) return;

        float currentStamina = playerMovement.GetCurrentStamina();
        float maxStamina = playerMovement.GetMaxStamina();

        if (maxStamina <= 0f) return;

        float staminaPercent = currentStamina / maxStamina;
        staminaFillImage.fillAmount = staminaPercent;

        // Couleur selon l'etat
        if (currentStamina <= 0f)
        {
            // Completement epuise
            staminaFillImage.color = depletedColor;
        }
        else
        {
            // Gradient entre vide et plein
            staminaFillImage.color = Color.Lerp(emptyColor, fullColor, staminaPercent);
        }

        // Determiner si on doit afficher la barre
        shouldShow = staminaPercent < showThreshold;
    }

    void UpdateVisibility()
    {
        if (canvasGroup == null) return;

        float targetAlpha = shouldShow ? 1f : 0f;
        canvasGroup.alpha = Mathf.Lerp(canvasGroup.alpha, targetAlpha, Time.deltaTime * fadeSpeed);
    }
}