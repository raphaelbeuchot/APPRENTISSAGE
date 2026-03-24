using UnityEngine;
using UnityEngine.UI;

public class InteractBubble : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private Sprite buttonSprite;
    [SerializeField] private bool oneTimeOnly = false;
    [SerializeField] private float detectionRange = 2f;
    public float verticalOffset = 1f;

    [Header("Visual")]
    [SerializeField] private float imageSize = 60f;

    [SerializeField] private bool hideOnRestart = false;


    [Header("Pop Animation")]
    [SerializeField] private float popDuration = 0.2f;
    [SerializeField] private float popScale = 1.75f;
    private float popTimer = 0f;
    private bool isPopping = false;

    private Transform playerTransform;
    private bool isHiddenPermanently = false;
    private bool isVisible = false;
    private RectTransform bubbleRT;

    private void Awake()
    {
        if (hideOnRestart && PlayerPrefs.GetInt("AutoStartCountdown", 0) == 1)
            isHiddenPermanently = true;
    }

    private void Start()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
            playerTransform = playerObj.transform;
        else
            Debug.LogWarning("[InteractBubble] Player introuvable");

        CreateBubble();
        SetVisible(false);
    }

    private void CreateBubble()
    {
        if (BubbleManager.Instance == null)
        {
            Debug.LogWarning("[InteractBubble] BubbleManager introuvable");
            return;
        }

        GameObject go = new GameObject("Bubble_" + gameObject.name);
        bubbleRT = go.AddComponent<RectTransform>();
        bubbleRT.sizeDelta = new Vector2(imageSize, imageSize);

        Image img = go.AddComponent<Image>();
        img.sprite = buttonSprite;
        img.preserveAspect = true;

        BubbleManager.Instance.RegisterBubble(this, bubbleRT);
    }

    private void Update()
    {
        if (isHiddenPermanently || playerTransform == null || bubbleRT == null)
            return;

        float distance = Vector3.Distance(transform.position, playerTransform.position);
        bool shouldShow = distance <= detectionRange;

        if (shouldShow != isVisible)
            SetVisible(shouldShow);

        if (isPopping)
        {
            popTimer += Time.deltaTime;
            float t = popTimer / popDuration;
            float scale = Mathf.Lerp(popScale, 1f, t);
            bubbleRT.localScale = Vector3.one * scale;

            if (popTimer >= popDuration)
            {
                isPopping = false;
                bubbleRT.localScale = Vector3.one;
            }
        }
    }

    public void Hide()
    {
        if (oneTimeOnly)
            isHiddenPermanently = true;

        SetVisible(false);
    }

    public void Show()
    {
        if (isHiddenPermanently)
            return;

        SetVisible(true);
    }

    private void SetVisible(bool visible)
    {
        isVisible = visible;
        if (bubbleRT != null)
        {
            bubbleRT.gameObject.SetActive(visible);
            if (visible)
            {
                isPopping = true;
                popTimer = 0f;
            }
        }
    }

    private void OnDestroy()
    {
        if (BubbleManager.Instance != null)
            BubbleManager.Instance.UnregisterBubble(this);
    }
}