using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class EnemyHealthBarManager : MonoBehaviour
{
    public GameObject healthBarPrefab;
    public Canvas canvas;
    public float verticalOffset = 2f;

    private Dictionary<Transform, EnemyHealthBarUI> healthBars = new Dictionary<Transform, EnemyHealthBarUI>();
    private Camera mainCamera;

    private void Start()
    {
        mainCamera = Camera.main;
        if (mainCamera == null)
        {
            mainCamera = FindObjectOfType<Camera>();
        }
    }

    public void RegisterEnemy(Transform enemy, EnemyHealthBarUI bar)
    {
        if (!healthBars.ContainsKey(enemy))
        {
            healthBars.Add(enemy, bar);
        }
    }

    public void UnregisterEnemy(Transform enemy)
    {
        if (healthBars.ContainsKey(enemy))
        {
            Destroy(healthBars[enemy].gameObject);
            healthBars.Remove(enemy);
        }
    }

    private void LateUpdate()
    {
        Debug.Log($"LateUpdate - healthBars count: {healthBars.Count}");

        Debug.Log($"canvas null? {canvas == null}, mainCamera null? {mainCamera == null}");

        if (canvas == null || mainCamera == null) return;

        Debug.Log("Before foreach");
        foreach (var kvp in healthBars)
        {
            Transform enemy = kvp.Key;
            RectTransform barRect = kvp.Value.GetComponent<RectTransform>();

            if (enemy != null && barRect != null)
            {
                Vector3 worldPos = enemy.position + Vector3.up * verticalOffset;
                Vector3 screenPos = mainCamera.WorldToScreenPoint(worldPos);

                Vector2 localPoint;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    canvas.transform as RectTransform,
                    screenPos,
                    null,
                    out localPoint);

                barRect.localPosition = localPoint;
            }
        }
        Debug.Log("After foreach");
    }
}