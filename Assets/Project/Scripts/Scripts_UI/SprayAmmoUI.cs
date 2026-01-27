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

    [Header("Audio")]
    [SerializeField] private AudioClip reloadSound;
    private AudioSource audioSource;

    private Image[] sprayIcons;
    private int maxAmmo;
    private int maxTotalAmmo;

    void Start()
    {
        if (meleeSystem == null)
        {
            meleeSystem = FindObjectOfType<MeleeAttackSystem>();
        }

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        // Recuperer maxTotalAmmo depuis PlayerStats
        if (meleeSystem != null && meleeSystem.stats != null)
        {
            maxTotalAmmo = meleeSystem.stats.totalSprayAmmoStart;
        }
        else
        {
            maxTotalAmmo = 50; // Fallback si pas de stats
        }

        InitializeIcons();
    }

    void InitializeIcons()
    {
        if (meleeSystem == null || iconContainer == null || sprayIconPrefab == null) return;

        maxAmmo = meleeSystem.GetMaxSprayAmmo();
        sprayIcons = new Image[maxAmmo];

        for (int i = 0; i < maxAmmo; i++)
        {
            GameObject iconGO = Instantiate(sprayIconPrefab, iconContainer);
            sprayIcons[i] = iconGO.GetComponent<Image>();
        }
    }

    void Update()
    {
        if (meleeSystem == null) return;

        bool bottleThrown = meleeSystem.IsBottleThrown();

        // Griser l'UI si bouteille jetée
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

            if (isReloading)
            {
                sprayIcons[i].color = reloadingColor;
            }
            else if (i < currentAmmo)
            {
                sprayIcons[i].color = fullColor;
            }
            else
            {
                sprayIcons[i].color = emptyColor;
            }
        }
    }

    void UpdateAmmoCount()
    {
        if (ammoCountText == null) return;

        int currentInMag = meleeSystem.GetCurrentSprayAmmo();
        int totalReserve = meleeSystem.GetTotalSprayAmmo();

        // Total disponible = réserve + ce qu'il y a dans le chargeur
        int totalAvailable = totalReserve + currentInMag;

        // Utiliser maxTotalAmmo au lieu de "50" hardcodé
        ammoCountText.text = totalAvailable + "/" + maxTotalAmmo;
    }

    public void PlayReloadSound()
    {
        if (audioSource != null && reloadSound != null)
        {
            audioSource.PlayOneShot(reloadSound);
        }
    }
}