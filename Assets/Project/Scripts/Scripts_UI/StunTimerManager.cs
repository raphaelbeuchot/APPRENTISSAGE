using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class StunTimerManager : MonoBehaviour
{
    public static StunTimerManager Instance;

    [Header("Prefab")]
    public GameObject stunTimerPrefab;
    public Canvas canvas;

    [Header("Settings")]
    public float verticalOffset = 2.5f;  // Un peu au-dessus de la healthbar

    private Camera mainCamera;
    private Dictionary<Transform, StunTimerUI> stunTimers = new Dictionary<Transform, StunTimerUI>();

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        mainCamera = Camera.main;
        if (mainCamera == null)
        {
            mainCamera = FindObjectOfType<Camera>();
        }
    }

    public void RegisterEnemy(Transform enemy, StunTimerUI timer)
    {
        if (!stunTimers.ContainsKey(enemy))
        {
            stunTimers.Add(enemy, timer);
            Debug.Log($"[StunTimerManager] Registered {enemy.name}");
        }
    }

    public void UnregisterEnemy(Transform enemy)
    {
        if (stunTimers.ContainsKey(enemy))
        {
            if (stunTimers[enemy] != null)
            {
                Destroy(stunTimers[enemy].gameObject);
            }
            stunTimers.Remove(enemy);
            Debug.Log($"[StunTimerManager] Unregistered {enemy.name}");
        }
    }

    private void LateUpdate()
    {
        if (canvas == null || mainCamera == null) return;

        foreach (var kvp in stunTimers)
        {
            Transform enemy = kvp.Key;
            StunTimerUI timer = kvp.Value;

            if (enemy == null || timer == null || !timer.gameObject.activeSelf) continue;

            // Position world space
            Vector3 worldPos = enemy.position + Vector3.up * verticalOffset;
            Vector3 screenPos = mainCamera.WorldToScreenPoint(worldPos);

            // Conversion en local canvas
            RectTransform timerRect = timer.GetComponent<RectTransform>();
            if (timerRect != null)
            {
                Vector2 localPoint;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    canvas.transform as RectTransform,
                    screenPos,
                    null,
                    out localPoint);
                timerRect.localPosition = localPoint;
            }
        }
    }
}