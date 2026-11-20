using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

// Pips construits au lancement. En editeur (hors jeu) : apercu des vrais pips, jamais sauvegarde
// (HideFlags.DontSave), pour poser la barre sans lancer le jeu.
// Le RectTransform prend la taille de la barre (pips poses depuis le bord gauche) : pivot conseille (0, 0.5).
[ExecuteAlways]
public class PlayerHealthUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerHealth playerHealth;
    public enum ScreenSideFilter { Aucun, Left, Right }
    [Tooltip("Multi : si Player Health est vide, prendre le joueur de ce cote (PlayerScreenSide). Aucun = premier joueur trouve (solo).")]
    [SerializeField] private ScreenSideFilter side = ScreenSideFilter.Aucun;

    [Header("Config")]
    [SerializeField] private bool showHealthBar = true;
    public bool ShouldShow => showHealthBar;
    [SerializeField] private float pipDiameter = 25f;
    [SerializeField] private float outlineExtra = 4f;
    [SerializeField] private float overlapRatio = 0.5f;
    [SerializeField] private float healthPerPip = 10f;
    [Tooltip("Editeur seulement : vie max utilisee pour l'apercu hors jeu")]
    [SerializeField] private float previewMaxHealth = 100f;

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

    private void OnEnable()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying) { QueuePreview(); return; }
#endif
        if (!showHealthBar) { gameObject.SetActive(false); return; }
        StartCoroutine(InitCoroutine());
    }

#if UNITY_EDITOR
    private void OnDisable()
    {
        // Pas de destruction directe ici : interdit pendant la desactivation du parent
        if (!Application.isPlaying) QueuePreview();
    }

    private void OnValidate()
    {
        if (!Application.isPlaying) QueuePreview();
    }

    void QueuePreview()
    {
        UnityEditor.EditorApplication.delayCall -= RefreshPreview;
        UnityEditor.EditorApplication.delayCall += RefreshPreview;
    }

    void RefreshPreview()
    {
        if (this == null || Application.isPlaying) return;
        ClearPreview();
        if (!isActiveAndEnabled || !showHealthBar) return;

        BuildPips(previewMaxHealth, previewMaxHealth, true);
        foreach (Transform t in transform.GetComponentsInChildren<Transform>(true))
        {
            if (t == transform) continue;
            t.gameObject.hideFlags = HideFlags.DontSave;
            foreach (Component c in t.GetComponents<Component>())
                c.hideFlags = HideFlags.DontSave;
        }
    }

    // Ne detruit que les objets d'apercu, jamais un enfant pose a la main
    void ClearPreview()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            GameObject child = transform.GetChild(i).gameObject;
            if ((child.hideFlags & HideFlags.DontSaveInEditor) != 0)
                DestroyImmediate(child);
        }
    }
#endif

    private IEnumerator InitCoroutine()
    {
        if (playerHealth == null && side != ScreenSideFilter.Aucun)
            playerHealth = FindPlayerHealthOnSide(side == ScreenSideFilter.Right ? PlayerScreenSide.Side.Right : PlayerScreenSide.Side.Left);

        if (playerHealth == null && side == ScreenSideFilter.Aucun)
            playerHealth = FindObjectOfType<PlayerHealth>();

        if (playerHealth == null)
        {
            Debug.LogError("PlayerHealthUI: PlayerHealth non trouve ! (cote : " + side + ")");
            yield break;
        }

        yield return null;

        Debug.Log($"[PlayerHealthUI] base={playerHealth.GetBaseMaxHealth()} real={playerHealth.GetMaxHealth()} multiplier={ModifierApplier.Instance?.playerMaxHealthMultiplier}");

        baseMaxHealth = playerHealth.GetBaseMaxHealth();
        realMaxHealth = playerHealth.GetMaxHealth();

        BuildPips(baseMaxHealth, realMaxHealth, false);

        if (!subscribed)
        {
            playerHealth.OnHealthChanged += OnHealthChanged;
            subscribed = true;
        }
        OnHealthChanged(playerHealth.GetCurrentHealth(), realMaxHealth);
    }

    static PlayerHealth FindPlayerHealthOnSide(PlayerScreenSide.Side wanted)
    {
        foreach (PlayerHealth ph in FindObjectsOfType<PlayerHealth>())
        {
            PlayerScreenSide s = ph.GetComponentInParent<PlayerScreenSide>();
            if (s != null && s.side == wanted) return ph;
        }
        return null;
    }

    void BuildPips(float baseMax, float realMax, bool preview)
    {
        if (!preview)
            foreach (Transform child in transform)
                Destroy(child.gameObject);

        outlineImages.Clear();
        pipImages.Clear();
        isBonus.Clear();
        pipActive.Clear();
        fadeCoroutines.Clear();

        normalPipCount = Mathf.Max(1, Mathf.RoundToInt(baseMax / healthPerPip));
        int bonusPipCount = realMax > baseMax
            ? Mathf.Max(1, Mathf.RoundToInt((realMax - baseMax) / healthPerPip))
            : 0;
        totalPips = normalPipCount + bonusPipCount;

        float step = pipDiameter * overlapRatio;
        float outlineDiameter = pipDiameter + outlineExtra;

        // Le rectangle = la barre dessinee
        if (CanResizeSelf())
            ((RectTransform)transform).sizeDelta = new Vector2((totalPips - 1) * step + outlineDiameter, outlineDiameter);

        RectTransform outlinesRT = CreateStretchedContainer("OutlinesContainer");
        RectTransform pipsRT = CreateStretchedContainer("PipsContainer");

        for (int i = 0; i < totalPips; i++)
        {
            float x = outlineDiameter * 0.5f + i * step;
            bool bonus = i >= normalPipCount;

            GameObject outlineGO = new GameObject("Outline_" + i);
            outlineGO.transform.SetParent(outlinesRT, false);
            Image outlineImg = outlineGO.AddComponent<Image>();
            outlineImg.sprite = pipSprite;
            outlineImg.color = outlineColorDefault;
            outlineImg.raycastTarget = false;
            RectTransform outlineRT = outlineGO.GetComponent<RectTransform>();
            outlineRT.anchorMin = outlineRT.anchorMax = new Vector2(0f, 0.5f);
            outlineRT.sizeDelta = new Vector2(outlineDiameter, outlineDiameter);
            outlineRT.anchoredPosition = new Vector2(x, 0f);
            outlineImages.Add(outlineImg);

            GameObject pipGO = new GameObject("Pip_" + i);
            pipGO.transform.SetParent(pipsRT, false);
            Image pipImg = pipGO.AddComponent<Image>();
            pipImg.sprite = pipSprite;
            pipImg.color = bonus ? bonusColorActive : pipColorActive;
            pipImg.raycastTarget = false;
            RectTransform pipRT = pipGO.GetComponent<RectTransform>();
            pipRT.anchorMin = pipRT.anchorMax = new Vector2(0f, 0.5f);
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

    // Hors jeu, une instance de prefab dans une scene ne se redimensionne pas : sinon override parasite
    // dans chaque scene ouverte. La taille est tenue par le prefab (mode Prefab) et recalculee en jeu.
    bool CanResizeSelf()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying && UnityEditor.PrefabUtility.IsPartOfPrefabInstance(this)) return false;
#endif
        return true;
    }

    RectTransform CreateStretchedContainer(string name)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        RectTransform rt = (RectTransform)go.transform;
        rt.SetParent(transform, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        return rt;
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