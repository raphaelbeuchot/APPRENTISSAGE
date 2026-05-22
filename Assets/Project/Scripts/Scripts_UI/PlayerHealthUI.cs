using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

public class PlayerHealthUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerHealth playerHealth;

    [Header("Config")]
    [SerializeField] private float pipDiameter = 25f;
    [SerializeField] private float outlineExtra = 4f;
    [SerializeField] private float overlapRatio = 0.5f;
    [SerializeField] private float healthPerPip = 10f;

    [Header("Sprite")]
    [SerializeField] private Sprite pipSprite;

    [Header("Colors")]
    [SerializeField] private Color pipColorActive = Color.green;
    [SerializeField] private Color bonusColorActive = new Color(0.5f, 0f, 1f);
    [SerializeField] private Color pipColorEmpty = new Color(0.2f, 0.2f, 0.2f);

    [SerializeField] private Color outlineColorDefault = Color.black;
    [SerializeField] private Color outlineColorEmpty = new Color(0.2f, 0.2f, 0.2f, 0f);

    [SerializeField] private Color damageFlashColor = Color.red;
    [SerializeField] private float damageFlashDuration = 0.15f;
    [SerializeField] private float damageFadeDuration = 0.4f;

    private List<Image> outlineImages = new List<Image>();
    private List<Image> pipImages = new List<Image>();
    private List<bool> isBonus = new List<bool>();
    private List<bool> pipActive = new List<bool>();
    private List<Coroutine> fadeCoroutines = new List<Coroutine>();
    private int totalPips = 0;
    private int normalPipCount = 0;
    private float baseMaxHealth;
    private float realMaxHealth;
    private bool subscribed = false;

    IEnumerator OnEnable()
    {
        if (playerHealth == null)
            playerHealth = FindObjectOfType<PlayerHealth>();

        if (playerHealth == null)
        {
            Debug.LogError("PlayerHealthUI: PlayerHealth non trouve !");
            yield break;
        }

        yield return null;

        Debug.Log($"[PlayerHealthUI] base={playerHealth.GetBaseMaxHealth()} real={playerHealth.GetMaxHealth()} multiplier={ModifierApplier.Instance?.playerMaxHealthMultiplier}");

        baseMaxHealth = playerHealth.GetBaseMaxHealth();
        realMaxHealth = playerHealth.GetMaxHealth();

        BuildPips();

        if (!subscribed)
        {
            playerHealth.OnHealthChanged += OnHealthChanged;
            subscribed = true;
        }
        OnHealthChanged(playerHealth.GetCurrentHealth(), realMaxHealth);
    }

    void BuildPips()
    {
        foreach (Transform child in transform)
            Destroy(child.gameObject);

        outlineImages.Clear();
        pipImages.Clear();
        isBonus.Clear();
        pipActive.Clear();
        fadeCoroutines.Clear();

        GameObject outlinesGO = new GameObject("OutlinesContainer");
        outlinesGO.transform.SetParent(transform, false);
        outlinesGO.AddComponent<RectTransform>().anchoredPosition = Vector2.zero;

        GameObject pipsGO = new GameObject("PipsContainer");
        pipsGO.transform.SetParent(transform, false);
        pipsGO.AddComponent<RectTransform>().anchoredPosition = Vector2.zero;

        normalPipCount = Mathf.Max(1, Mathf.RoundToInt(baseMaxHealth / healthPerPip));
        int bonusPipCount = realMaxHealth > baseMaxHealth
            ? Mathf.Max(1, Mathf.RoundToInt((realMaxHealth - baseMaxHealth) / healthPerPip))
            : 0;
        totalPips = normalPipCount + bonusPipCount;

        float step = pipDiameter * overlapRatio;
        float outlineDiameter = pipDiameter + outlineExtra;

        for (int i = 0; i < totalPips; i++)
        {
            float x = i * step;
            bool bonus = i >= normalPipCount;

            GameObject outlineGO = new GameObject("Outline_" + i);
            outlineGO.transform.SetParent(outlinesGO.transform, false);
            Image outlineImg = outlineGO.AddComponent<Image>();
            outlineImg.sprite = pipSprite;
            outlineImg.color = outlineColorDefault;
            RectTransform outlineRT = outlineGO.GetComponent<RectTransform>();
            outlineRT.sizeDelta = new Vector2(outlineDiameter, outlineDiameter);
            outlineRT.anchoredPosition = new Vector2(x, 0f);
            outlineImages.Add(outlineImg);

            GameObject pipGO = new GameObject("Pip_" + i);
            pipGO.transform.SetParent(pipsGO.transform, false);
            Image pipImg = pipGO.AddComponent<Image>();
            pipImg.sprite = pipSprite;
            pipImg.color = bonus ? bonusColorActive : pipColorActive;
            RectTransform pipRT = pipGO.GetComponent<RectTransform>();
            pipRT.sizeDelta = new Vector2(pipDiameter, pipDiameter);
            pipRT.anchoredPosition = new Vector2(x, 0f);
            pipImages.Add(pipImg);

            isBonus.Add(bonus);
            pipActive.Add(true);
            fadeCoroutines.Add(null);
        }

        for (int i = 0; i < totalPips; i++)
        {
            outlineImages[i].transform.SetSiblingIndex(totalPips - 1 - i);
            pipImages[i].transform.SetSiblingIndex(totalPips - 1 - i);
        }
    }

    void OnHealthChanged(float currentHealth, float _)
    {
        if (pipImages.Count == 0) return;

        int activePips = Mathf.CeilToInt(Mathf.Max(0f, currentHealth) / healthPerPip);
        activePips = Mathf.Clamp(activePips, 0, totalPips);

        for (int i = 0; i < totalPips; i++)
        {
            bool shouldBeActive = i < activePips;

            if (!shouldBeActive && pipActive[i])
            {
                pipActive[i] = false;
                if (fadeCoroutines[i] != null)
                    StopCoroutine(fadeCoroutines[i]);
                fadeCoroutines[i] = StartCoroutine(DamageFlashFade(i));
            }
            else if (shouldBeActive && !pipActive[i])
            {
                pipActive[i] = true;
                if (fadeCoroutines[i] != null)
                {
                    StopCoroutine(fadeCoroutines[i]);
                    fadeCoroutines[i] = null;
                }
                pipImages[i].color = isBonus[i] ? bonusColorActive : pipColorActive;
                if (isBonus[i])
                    outlineImages[i].color = outlineColorDefault;
            }
        }
    }

    private IEnumerator DamageFlashFade(int index)
    {
        Image pip = pipImages[index];
        Image outline = outlineImages[index];

        pip.color = damageFlashColor;
        yield return new WaitForSeconds(damageFlashDuration);

        float elapsed = 0f;
        Color pipStart = pip.color;
        Color pipEnd = isBonus[index] ? new Color(pipStart.r, pipStart.g, pipStart.b, 0f) : pipColorEmpty;
        Color outlineStart = outline.color;
        Color outlineEnd = isBonus[index] ? new Color(outlineStart.r, outlineStart.g, outlineStart.b, 0f) : outlineColorEmpty;

        while (elapsed < damageFadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / damageFadeDuration;
            pip.color = Color.Lerp(pipStart, pipEnd, t);
            outline.color = Color.Lerp(outlineStart, outlineEnd, t);
            yield return null;
        }

        pip.color = pipEnd;
        outline.color = outlineEnd;
        fadeCoroutines[index] = null;
    }

    void OnDestroy()
    {
        if (playerHealth != null && subscribed)
            playerHealth.OnHealthChanged -= OnHealthChanged;
    }
}