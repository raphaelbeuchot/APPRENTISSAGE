using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

public class EnemyHealthBarPipsUI : EnemyHealthBarUI
{
   
    [Header("Config")]
    [SerializeField] private float pipDiameter = 25f;
    [SerializeField] private float outlineExtra = 4f;
    [SerializeField] private float lockOutlineExtra = 10f;
    [SerializeField] private float overlapRatio = 0.5f;
    [SerializeField] private float healthPerPip = 50f;

    [Header("Sprite")]
    [SerializeField] private Sprite pipSprite;
    [SerializeField] private Sprite lockOutlineSprite;

    [Header("Colors")]
    [SerializeField] private Color pipColorActive = new Color(1f, 0.5f, 0f);
    [SerializeField] private Color bonusColorActive = new Color(0.5f, 0f, 1f);
    [SerializeField] private Color outlineColorDefault = Color.black;
    [SerializeField] private Color lockOutlineColor = Color.white;
    [SerializeField] private Color damageFlashColor = Color.red;
    [SerializeField] private float damageFlashDuration = 0.15f;
    [SerializeField] private float damageFadeDuration = 0.4f;

    private List<Image> lockOutlineImages = new List<Image>();
    private List<Image> outlineImages = new List<Image>();
    private List<Image> pipImages = new List<Image>();
    private List<bool> isBonus = new List<bool>();
    private List<bool> pipActive = new List<bool>();
    private List<Coroutine> fadeCoroutines = new List<Coroutine>();
    private int totalPips = 0;
    private int normalPipCount = 0;

    public override void Initialize(float baseMax, float realMax)
    {
        foreach (Transform child in transform)
            Destroy(child.gameObject);

        lockOutlineImages.Clear();
        outlineImages.Clear();
        pipImages.Clear();
        isBonus.Clear();
        pipActive.Clear();
        fadeCoroutines.Clear();

        GameObject lockOutlinesGO = new GameObject("LockOutlinesContainer");
        lockOutlinesGO.transform.SetParent(transform, false);
        lockOutlinesGO.AddComponent<RectTransform>().anchoredPosition = Vector2.zero;
        lockOutlinesGO.SetActive(false);

        GameObject outlinesGO = new GameObject("OutlinesContainer");
        outlinesGO.transform.SetParent(transform, false);
        outlinesGO.AddComponent<RectTransform>().anchoredPosition = Vector2.zero;

        GameObject pipsGO = new GameObject("PipsContainer");
        pipsGO.transform.SetParent(transform, false);
        pipsGO.AddComponent<RectTransform>().anchoredPosition = Vector2.zero;

        normalPipCount = Mathf.Max(1, Mathf.RoundToInt(baseMax / healthPerPip));
        int bonusPipCount = realMax > baseMax ? Mathf.Max(1, Mathf.RoundToInt((realMax - baseMax) / healthPerPip)) : 0;
        totalPips = normalPipCount + bonusPipCount;

        float step = pipDiameter * overlapRatio;
        float outlineDiameter = pipDiameter + outlineExtra;
        float lockOutlineDiameter = pipDiameter + lockOutlineExtra;

        for (int i = 0; i < totalPips; i++)
        {
            float x = i * step;
            bool bonus = i >= normalPipCount;

            GameObject lockOutlineGO = new GameObject("LockOutline_" + i);
            lockOutlineGO.transform.SetParent(lockOutlinesGO.transform, false);
            Image lockOutlineImg = lockOutlineGO.AddComponent<Image>();
            lockOutlineImg.sprite = lockOutlineSprite;
            lockOutlineImg.color = lockOutlineColor;
            RectTransform lockOutlineRT = lockOutlineGO.GetComponent<RectTransform>();
            lockOutlineRT.sizeDelta = new Vector2(lockOutlineDiameter, lockOutlineDiameter);
            lockOutlineRT.anchoredPosition = new Vector2(x, 0f);
            lockOutlineImages.Add(lockOutlineImg);

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
            lockOutlineImages[i].transform.SetSiblingIndex(totalPips - 1 - i);
            outlineImages[i].transform.SetSiblingIndex(totalPips - 1 - i);
            pipImages[i].transform.SetSiblingIndex(totalPips - 1 - i);
        }
    }

    public override void UpdateHealth(float currentHealth, float baseMax, float realMax)
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
            }
        }
    }

    private IEnumerator DamageFlashFade(int index)
    {
        Image pip = pipImages[index];
        pip.color = damageFlashColor;
        yield return new WaitForSeconds(damageFlashDuration);

        float elapsed = 0f;
        Color start = pip.color;
        Color end = new Color(start.r, start.g, start.b, 0f);
        while (elapsed < damageFadeDuration)
        {
            elapsed += Time.deltaTime;
            pip.color = Color.Lerp(start, end, elapsed / damageFadeDuration);
            yield return null;
        }
        pip.color = end;
        fadeCoroutines[index] = null;
    }

    public override void SetLockedOutline(bool locked)
    {
        Transform lockContainer = transform.Find("LockOutlinesContainer");
        if (lockContainer != null)
            lockContainer.gameObject.SetActive(locked);
    }

    public override void Show()
    {
        if (gameObject != null) gameObject.SetActive(true);
    }

    public override void Hide()
    {
        if (gameObject != null) gameObject.SetActive(false);
    }
}