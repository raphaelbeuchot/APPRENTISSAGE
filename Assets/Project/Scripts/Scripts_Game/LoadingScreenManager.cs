using UnityEngine;
using TMPro;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;

public class LoadingScreenManager : MonoBehaviour
{
    public static int TargetSceneIndex = -1;

    [Header("Loading Text")]
    [SerializeField] private TextMeshProUGUI loadingText;

    [Header("Pip Container")]
    [SerializeField] private RectTransform pipContainer;

    [Header("Pip Config")]
    [SerializeField] private Sprite pipSprite;
    [SerializeField] private int pipCount = 10;
    [SerializeField] private float pipDiameter = 25f;
    [SerializeField] private float pipSpacing = 8f;
    [SerializeField] private float outlineExtra = 4f;

    [Header("Pip Colors")]
    [SerializeField] private Color outlineColorActive = Color.white;
    [SerializeField] private Color outlineColorInactive = Color.black;
    [SerializeField] private Color colorInactive = Color.gray;
    [SerializeField] private Color[] pipActiveColors;

    private List<Image> pipImages = new List<Image>();
    private List<Image> outlineImages = new List<Image>();

    void Start()
    {
        BuildPips();

        if (TargetSceneIndex < 0)
        {
            Debug.LogError("[LoadingScreen] Aucun index de scene cible defini !");
            return;
        }

        StartCoroutine(LoadTargetScene());
    }

    void BuildPips()
    {
        if (pipContainer == null || pipSprite == null) return;

        HorizontalLayoutGroup hlg = pipContainer.GetComponent<HorizontalLayoutGroup>();
        if (hlg == null)
            hlg = pipContainer.gameObject.AddComponent<HorizontalLayoutGroup>();

        hlg.spacing = pipSpacing;
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;
        hlg.childControlWidth = false;
        hlg.childControlHeight = false;

        pipImages.Clear();
        outlineImages.Clear();

        float slotSize = pipDiameter + outlineExtra;

        for (int i = 0; i < pipCount; i++)
        {
            // Slot parent dans le HLG
            GameObject slot = new GameObject("Slot_" + i);
            slot.transform.SetParent(pipContainer, false);
            RectTransform slotRT = slot.AddComponent<RectTransform>();
            slotRT.sizeDelta = new Vector2(slotSize, slotSize);

            // Outline en dessous
            GameObject outlineGO = new GameObject("Outline");
            outlineGO.transform.SetParent(slot.transform, false);
            Image outlineImg = outlineGO.AddComponent<Image>();
            outlineImg.sprite = pipSprite;
            outlineImg.color = outlineColorInactive;
            RectTransform outlineRT = outlineGO.GetComponent<RectTransform>();
            outlineRT.sizeDelta = new Vector2(slotSize, slotSize);
            outlineRT.anchoredPosition = Vector2.zero;
            outlineImages.Add(outlineImg);

            // Pip par-dessus
            GameObject pipGO = new GameObject("Pip");
            pipGO.transform.SetParent(slot.transform, false);
            Image pipImg = pipGO.AddComponent<Image>();
            pipImg.sprite = pipSprite;
            pipImg.color = colorInactive;
            RectTransform pipRT = pipGO.GetComponent<RectTransform>();
            pipRT.sizeDelta = new Vector2(pipDiameter, pipDiameter);
            pipRT.anchoredPosition = Vector2.zero;
            pipImages.Add(pipImg);
        }
    }

    Color GetPipActiveColor(int index)
    {
        if (pipActiveColors != null && index < pipActiveColors.Length)
            return pipActiveColors[index];
        return Color.white;
    }

    IEnumerator LoadTargetScene()
    {
        yield return null;

        AsyncOperation op = SceneManager.LoadSceneAsync(TargetSceneIndex);
        op.allowSceneActivation = false;

        float elapsed = 0f;
        float minDuration = 1.5f;
        float displayFill = 0f;

        while (elapsed < minDuration || op.progress < 0.9f)
        {
            elapsed += Time.unscaledDeltaTime;

            float realFill = op.progress / 0.9f;
            float timeFill = elapsed / minDuration;
            float targetFill = Mathf.Min(realFill, timeFill);

            displayFill = Mathf.Lerp(displayFill, targetFill, Time.unscaledDeltaTime * 4f);
            UpdatePips(displayFill);

            if (loadingText != null)
                loadingText.text = "Loading...";

            yield return null;
        }

        UpdatePips(1f);

        if (loadingText != null)
            loadingText.text = "Loading...";

        yield return new WaitForSeconds(0.3f);
        op.allowSceneActivation = true;
    }

    void UpdatePips(float fillLevel)
    {
        for (int i = 0; i < pipImages.Count; i++)
        {
            bool active = fillLevel > (float)i / pipCount;
            pipImages[i].color = active ? GetPipActiveColor(i) : colorInactive;
            outlineImages[i].color = active ? outlineColorActive : outlineColorInactive;
        }
    }
}