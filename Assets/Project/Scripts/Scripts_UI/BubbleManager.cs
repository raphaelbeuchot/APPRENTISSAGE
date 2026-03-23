using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class BubbleManager : MonoBehaviour
{
    public static BubbleManager Instance;

    [Header("References")]
    public Canvas canvas;
    public RectTransform bubblesContainer;

    

    private Camera mainCamera;
    private Dictionary<InteractBubble, RectTransform> bubbles = new Dictionary<InteractBubble, RectTransform>();

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    private void Start()
    {
        mainCamera = Camera.main;
    }

    public void RegisterBubble(InteractBubble bubble, RectTransform rt)
    {
        if (!bubbles.ContainsKey(bubble))
        {
            rt.SetParent(bubblesContainer, false);
            bubbles.Add(bubble, rt);
        }
    }

    public void UnregisterBubble(InteractBubble bubble)
    {
        if (bubbles.ContainsKey(bubble))
        {
            if (bubbles[bubble] != null)
                Destroy(bubbles[bubble].gameObject);
            bubbles.Remove(bubble);
        }
    }

    private void LateUpdate()
    {
        if (mainCamera == null || canvas == null) return;

        foreach (var kvp in bubbles)
        {
            InteractBubble bubble = kvp.Key;
            RectTransform rt = kvp.Value;

            if (bubble == null || rt == null) continue;

            Vector3 worldPos = bubble.transform.position + Vector3.up * bubble.verticalOffset;
            Vector3 screenPos = mainCamera.WorldToScreenPoint(worldPos);

            if (screenPos.z < 0)
            {
                rt.gameObject.SetActive(false);
                continue;
            }

            Vector2 localPoint;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvas.transform as RectTransform,
                screenPos,
                null,
                out localPoint);

            rt.localPosition = localPoint;
        }
    }
}