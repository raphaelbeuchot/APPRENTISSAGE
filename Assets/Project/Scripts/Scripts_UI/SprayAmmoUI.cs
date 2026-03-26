using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SprayAmmoUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private MeleeAttackSystem meleeSystem;

    [Header("UI Elements")]
    [SerializeField] private Transform iconContainer;
    [SerializeField] private GameObject sprayIconPrefab;
    [SerializeField] private TextMeshProUGUI ammoCountText;

    [Header("Colors")]
    [SerializeField] private Color fullColor = Color.white;
    [SerializeField] private Color emptyColor = Color.gray;
    [SerializeField] private Color reloadingColor = Color.yellow;

    [Header("Bonus Colors")]
    [SerializeField] private Color bonusFullColor = Color.cyan;
    [SerializeField] private Color bonusEmptyColor = Color.gray;
    [SerializeField] private Color bonusReloadingColor = Color.yellow;

    [Header("Audio")]
    [SerializeField] private AudioClip reloadSound;

    private AudioSource audioSource;
    private Image[] sprayIcons;
    private int maxAmmo;
    private int maxAmmoWithBonus;
    private int maxTotalAmmo;

    void Start()
    {
        if (meleeSystem == null)
            meleeSystem = FindObjectOfType<MeleeAttackSystem>();

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        if (meleeSystem != null && meleeSystem.stats != null)
        {
            float multiplier = ModifierApplier.Instance != null ? ModifierApplier.Instance.sprayAmmoMultiplier : 1f;
            maxTotalAmmo = Mathf.RoundToInt(meleeSystem.stats.totalSprayAmmoStart * multiplier);
        }
        else
        {
            maxTotalAmmo = 50;
        }

        InitializeIcons();
    }

    void InitializeIcons()
    {
        if (meleeSystem == null || iconContainer == null || sprayIconPrefab == null) return;

        maxAmmo = meleeSystem.GetMaxSprayAmmo();
        maxAmmoWithBonus = meleeSystem.GetMaxSprayAmmoWithBonus();

        sprayIcons = new Image[maxAmmoWithBonus];
        for (int i = 0; i < maxAmmoWithBonus; i++)
        {
            GameObject iconGO = Instantiate(sprayIconPrefab, iconContainer);
            sprayIcons[i] = iconGO.GetComponent<Image>();
        }
    }

    void Update()
    {
        if (meleeSystem == null) return;

        bool bottleThrown = meleeSystem.IsBottleThrown();

        CanvasGroup canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        canvasGroup.alpha = bottleThrown ? 0.3f : 1f;

        UpdateIcons();
        UpdateAmmoCount();
    }

    void UpdateIcons()
    {
        int currentAmmo = meleeSystem.GetCurrentSprayAmmo();
        bool isReloading = meleeSystem.IsReloading();

        for (int i = 0; i < sprayIcons.Length; i++)
        {
            if (sprayIcons[i] == null) continue;

            bool isBonus = i >= maxAmmo;

            if (isReloading)
                sprayIcons[i].color = isBonus ? bonusReloadingColor : reloadingColor;
            else if (i < currentAmmo)
                sprayIcons[i].color = isBonus ? bonusFullColor : fullColor;
            else
                sprayIcons[i].color = isBonus ? bonusEmptyColor : emptyColor;
        }
    }

    void UpdateAmmoCount()
    {
        if (ammoCountText == null) return;
        int currentInMag = meleeSystem.GetCurrentSprayAmmo();
        int totalReserve = meleeSystem.GetTotalSprayAmmo();
        int totalAvailable = totalReserve + currentInMag;
        ammoCountText.text = totalAvailable + "/" + maxTotalAmmo;
    }

    public void PlayReloadSound()
    {
        if (audioSource != null && reloadSound != null)
            audioSource.PlayOneShot(reloadSound);
    }
}