using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using TMPro;

// Pips en cercle construits au lancement. En editeur (hors jeu) : apercu des pips au repos, jamais
// sauvegarde (HideFlags.DontSave). Le RectTransform prend la taille du cercle au plus large
// (repos ou pression max), centre sur les pips.
[ExecuteAlways]
public class CyclePressureGaugeUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private SentinelCycleManager cycleManager;
    [SerializeField] private Sprite pipSprite;

    [Header("Config")]
    [SerializeField] private int pipCount = 10;
    [SerializeField] private float radius = 80f;
    [SerializeField] private float releaseFillDuration = 0.5f;

    [Header("Pip Diameter")]
    [SerializeField] private float pipDiameterMin = 15f;
    [SerializeField] private float pipDiameterMax = 35f;
    [SerializeField] private float diameterLerpSpeed = 3f;

    [SerializeField] private float radiusMinMultiplier = 0.7f;

    [Header("Pressure Percentage")]
    [SerializeField] private TextMeshProUGUI pressurePercentageText;

    [Header("Kill Flash")]
    [SerializeField] private float killFlashDuration = 0.3f;

    [Header("Colors")]
    [SerializeField] private Color colorInactive = Color.gray;
    [SerializeField] private Color colorActiveCalm = Color.green;
    [SerializeField] private Color colorActiveTense = Color.red;

    private List<Image> pips = new List<Image>();
    private float releaseTimer = 0f;
    private SentinelCycleManager.GameState lastState;
    private float currentDiameter;
    private bool isFlashing = false;
    private float currentRadius;


    void Start()
    {
        if (!Application.isPlaying) return;

        // Apercu editeur encore present (Enter Play Mode sans rechargement de scene)
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            GameObject child = transform.GetChild(i).gameObject;
            if ((child.hideFlags & HideFlags.DontSaveInEditor) != 0)
                Destroy(child);
        }

        currentRadius = radius;
        currentDiameter = pipDiameterMin;
        BuildPips();
        if (pressurePercentageText == null)
            pressurePercentageText = GetComponentInChildren<TextMeshProUGUI>();
    }

    void OnEnable()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying) { QueuePreview(); return; }
#endif
        EnemyHealth.OnAnyEnemyDeath += OnEnemyKilled;
    }

    void OnDisable()
    {
#if UNITY_EDITOR
        // Pas de destruction directe ici : interdit pendant la desactivation du parent
        if (!Application.isPlaying) { QueuePreview(); return; }
#endif
        EnemyHealth.OnAnyEnemyDeath -= OnEnemyKilled;
    }

#if UNITY_EDITOR
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

        // Ne detruit que les objets d'apercu, jamais un enfant pose a la main (texte du pourcentage)
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            GameObject child = transform.GetChild(i).gameObject;
            if ((child.hideFlags & HideFlags.DontSaveInEditor) != 0)
                DestroyImmediate(child);
        }
        if (!isActiveAndEnabled) return;

        currentDiameter = pipDiameterMin;
        BuildPips();
        foreach (Image img in pips)
        {
            img.color = colorInactive;
            img.gameObject.hideFlags = HideFlags.DontSave;
            foreach (Component c in img.GetComponents<Component>())
                c.hideFlags = HideFlags.DontSave;
        }
    }
#endif

    void BuildPips()
    {
        // Le rectangle = le cercle au plus large : au repos, ou a pression max (rayon reduit, pips plus gros)
        float extentCalm = 2f * radius + pipDiameterMin;
        float extentTense = 2f * radius * radiusMinMultiplier + pipDiameterMax;
        float extent = Mathf.Max(extentCalm, extentTense);
        bool canResize = true;
#if UNITY_EDITOR
        // Hors jeu, une instance de prefab dans une scene ne se redimensionne pas : sinon override parasite
        // dans chaque scene ouverte. La taille est tenue par le prefab (mode Prefab) et recalculee en jeu.
        if (!Application.isPlaying && UnityEditor.PrefabUtility.IsPartOfPrefabInstance(this)) canResize = false;
#endif
        if (canResize)
            ((RectTransform)transform).sizeDelta = new Vector2(extent, extent);

        if (pipSprite == null) return;
        pips.Clear();
        for (int i = 0; i < pipCount; i++)
        {
            GameObject go = new GameObject("Pip_" + i);
            go.transform.SetParent(transform, false);
            Image img = go.AddComponent<Image>();
            img.sprite = pipSprite;
            img.type = Image.Type.Simple;
            img.raycastTarget = false;
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(currentDiameter, currentDiameter);
            float t = (float)i / pipCount;
            float angle = Mathf.PI * 0.5f - t * Mathf.PI * 2f;
            rt.anchoredPosition = new Vector2(radius * Mathf.Cos(angle), radius * Mathf.Sin(angle));
            pips.Add(img);
        }
    }

    void OnEnemyKilled()
    {
       //if (isFlashing) return;
        //StartCoroutine(KillFlashCoroutine());
    }

    private IEnumerator KillFlashCoroutine()
    {
        isFlashing = true;

        for (int i = 0; i < pips.Count; i++)
            pips[i].color = Color.white;

        yield return new WaitForSeconds(killFlashDuration);

        isFlashing = false;
    }

    void Update()
    {
        if (!Application.isPlaying) return;
        if (cycleManager == null || pips.Count == 0) return;

        if (OptionsManager.Instance != null && !OptionsManager.Instance.gaugeVisible)
        {
            for (int i = 0; i < pips.Count; i++)
                pips[i].color = Color.clear;
            return;
        }

        if (!cycleManager.IsGameStarted())
        {
            for (int i = 0; i < pips.Count; i++)
                pips[i].color = colorInactive;
            return;
        }

        float pressureFactor = cycleManager.GetCurrentPressureFactor();
        float targetDiameter = Mathf.Lerp(pipDiameterMin, pipDiameterMax, pressureFactor);
        currentDiameter = Mathf.Lerp(currentDiameter, targetDiameter, Time.deltaTime * diameterLerpSpeed);

        if (pressurePercentageText != null)
            pressurePercentageText.text = Loc.Get("hud.stress", Mathf.RoundToInt(pressureFactor * 100f));

        for (int i = 0; i < pips.Count; i++)
        {
            RectTransform rt = pips[i].rectTransform;
            rt.sizeDelta = new Vector2(currentDiameter, currentDiameter);
        }
        float targetRadius = radius * Mathf.Lerp(1f, radiusMinMultiplier, pressureFactor);
        currentRadius = Mathf.Lerp(currentRadius, targetRadius, Time.deltaTime * diameterLerpSpeed);

        for (int i = 0; i < pips.Count; i++)
        {
            float t = (float)i / pipCount;
            float angle = Mathf.PI * 0.5f - t * Mathf.PI * 2f;
            pips[i].rectTransform.anchoredPosition = new Vector2(currentRadius * Mathf.Cos(angle), currentRadius * Mathf.Sin(angle));
        }

        if (isFlashing) return;

        SentinelCycleManager.GameState state = cycleManager.GetCurrentState();
        float progress = cycleManager.GetCycleProgress();

        if (state == SentinelCycleManager.GameState.Release && lastState != SentinelCycleManager.GameState.Release)
            releaseTimer = 0f;
        if (state == SentinelCycleManager.GameState.Release)
            releaseTimer += Time.deltaTime;
        lastState = state;

        float fillLevel;
        Color activeColor;

        switch (state)
        {
            case SentinelCycleManager.GameState.GreenLight:
                fillLevel = 1f - progress;
                activeColor = colorActiveCalm;
                break;
            case SentinelCycleManager.GameState.Alert:
                fillLevel = progress;
                activeColor = colorActiveTense;
                break;
            case SentinelCycleManager.GameState.RedLight:
                fillLevel = 1f - progress;
                activeColor = colorActiveTense;
                break;
            case SentinelCycleManager.GameState.Release:
                fillLevel = Mathf.Clamp01(releaseTimer / releaseFillDuration);
                activeColor = colorActiveCalm;
                break;
            default:
                fillLevel = 0f;
                activeColor = colorInactive;
                break;
        }

        for (int i = 0; i < pips.Count; i++)
        {
            bool active = fillLevel > (float)i / pipCount;
            pips[i].color = active ? activeColor : colorInactive;
        }
    }
}