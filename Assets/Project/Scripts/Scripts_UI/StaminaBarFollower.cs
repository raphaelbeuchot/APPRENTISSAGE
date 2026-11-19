using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

// Pips construits au lancement. En editeur (hors jeu) : apercu des vrais pips, jamais sauvegarde
// (HideFlags.DontSave), pour poser la barre sans lancer le jeu.
// Le RectTransform prend la taille de la barre (pips poses depuis le bord gauche) : pivot conseille (0, 0.5).
[ExecuteAlways]
public class StaminaBarFollower : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerPhysicsMovement playerMovement;

    [Header("Config")]
    [SerializeField] private float pipDiameter = 25f;
    [SerializeField] private float outlineExtra = 4f;
    [SerializeField] private float overlapRatio = 0.5f;
    [SerializeField] private float staminaPerPip = 10f;
    [Tooltip("Editeur seulement : stamina max utilisee pour l'apercu hors jeu")]
    [SerializeField] private float previewMaxStamina = 100f;

    [Header("Sprite")]
    [SerializeField] private Sprite pipSprite;

    [Header("Colors")]
    [SerializeField] private Color pipColorActive = new Color(167f / 255f, 192f / 255f, 81f / 255f, 1f);
    [SerializeField] private Color bonusColorActive = new Color(0.5f, 0f, 1f);
    [SerializeField] private Color pipColorEmpty = new Color(0.2f, 0.2f, 0.2f);
    [SerializeField] private Color outlineColorDefault = Color.black;
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
    private float baseMaxStamina;
    private float realMaxStamina;
    private int lastActivePips = -1;

#if UNITY_EDITOR
    private void OnEnable()
    {
        if (!Application.isPlaying) QueuePreview();
    }

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
        if (!isActiveAndEnabled) return;

        BuildPips(previewMaxStamina, previewMaxStamina, true);
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

    void Start()
    {
        if (!Application.isPlaying) return;

        if (playerMovement == null)
            playerMovement = FindObjectOfType<PlayerPhysicsMovement>();

        if (playerMovement == null)
        {
            Debug.LogError("StaminaBarFollower: PlayerPhysicsMovement non trouve !");
            return;
        }

        baseMaxStamina = playerMovement.GetBaseMaxStamina();
        realMaxStamina = playerMovement.GetRealMaxStamina();

        BuildPips(baseMaxStamina, realMaxStamina, false);
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

        normalPipCount = Mathf.Max(1, Mathf.RoundToInt(baseMax / staminaPerPip));
        int bonusPipCount = realMax > baseMax
            ? Mathf.Max(1, Mathf.RoundToInt((realMax - baseMax) / staminaPerPip))
            : 0;
        totalPips = normalPipCount + bonusPipCount;

        float step = pipDiameter * overlapRatio;
        float outlineDiameter = pipDiameter + outlineExtra;

        // Le rectangle = la barre dessinee
        RectTransform selfRT = (RectTransform)transform;
        selfRT.sizeDelta = new Vector2((totalPips - 1) * step + outlineDiameter, outlineDiameter);

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

        lastActivePips = totalPips;
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

    void Update()
    {
        if (!Application.isPlaying) return;
        if (playerMovement == null || pipImages.Count == 0) return;

        float current = playerMovement.GetCurrentStamina();
        int activePips = Mathf.FloorToInt(Mathf.Max(0f, current) / staminaPerPip);
        activePips = Mathf.Clamp(activePips, 0, totalPips);

        if (activePips == lastActivePips) return;
        lastActivePips = activePips;

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
        Color outlineEnd = new Color(outlineStart.r, outlineStart.g, outlineStart.b, 0f);

        while (elapsed < damageFadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / damageFadeDuration;
            pip.color = Color.Lerp(pipStart, pipEnd, t);
            if (isBonus[index])
                outline.color = Color.Lerp(outlineStart, outlineEnd, t);
            yield return null;
        }

        pip.color = pipEnd;
        if (isBonus[index])
            outline.color = outlineEnd;
        fadeCoroutines[index] = null;
    }
}