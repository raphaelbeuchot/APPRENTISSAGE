using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class LockableTargetManager : MonoBehaviour
{
    [Header("Targets")]
    [SerializeField] private List<LockableTarget> targets;
    [SerializeField] private int requiredCount = -1;

    [Header("Key Spawn")]
    [SerializeField] private GameObject keyPrefab;
    [SerializeField] private Transform keySpawnPoint;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip allTargetsSound;

    [Header("Lock Pip UI")]
    [SerializeField] private Sprite pipSprite;
    [SerializeField] private RectTransform pipsContainer;
    [SerializeField] private Canvas canvas;
    [SerializeField] private float verticalOffset = 1.5f;
    [SerializeField] private float pipSize = 20f;
    [SerializeField] private Color pipColor = Color.white;

    private int triggeredCount = 0;
    private bool completed = false;

    private Dictionary<LockableTarget, Image> pipImages = new Dictionary<LockableTarget, Image>();
    private Camera mainCamera;

    private void Start()
    {
        if (requiredCount < 0 || requiredCount > targets.Count)
            requiredCount = targets.Count;

        mainCamera = Camera.main;

        foreach (LockableTarget target in targets)
        {
            if (target == null || pipsContainer == null) continue;

            GameObject pipGO = new GameObject("LockPip_" + target.name);
            pipGO.transform.SetParent(pipsContainer, false);

            Image img = pipGO.AddComponent<Image>();
            img.sprite = pipSprite;
            img.color = pipColor;

            RectTransform rt = pipGO.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(pipSize, pipSize);

            img.gameObject.SetActive(false);
            pipImages[target] = img;
        }
    }

    private void LateUpdate()
    {
        if (mainCamera == null || canvas == null) return;

        foreach (var kvp in pipImages)
        {
            LockableTarget target = kvp.Key;
            Image pip = kvp.Value;

            if (target == null || pip == null) continue;
            if (!pip.gameObject.activeSelf) continue;

            Vector3 worldPos = target.transform.position + Vector3.up * verticalOffset;
            Vector3 screenPos = mainCamera.WorldToScreenPoint(worldPos);

            RectTransform pipRect = pip.GetComponent<RectTransform>();
            Vector2 localPoint;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvas.transform as RectTransform,
                screenPos,
                null,
                out localPoint);
            pipRect.localPosition = localPoint;
        }
    }

    public void SetPipVisible(LockableTarget target, bool visible)
    {
        if (pipImages.TryGetValue(target, out Image pip) && pip != null)
            pip.gameObject.SetActive(visible);
    }

    public void OnTargetTriggered(LockableTarget target)
    {
        if (completed) return;

        triggeredCount++;
        Debug.Log("Targets triggered: " + triggeredCount + " / " + requiredCount);

        if (triggeredCount >= requiredCount)
            OnAllTargetsTriggered();
    }

    private void OnAllTargetsTriggered()
    {
        completed = true;

        if (audioSource != null && allTargetsSound != null)
            audioSource.PlayOneShot(allTargetsSound);

        if (keyPrefab != null && keySpawnPoint != null)
            Instantiate(keyPrefab, keySpawnPoint.position, keySpawnPoint.rotation);

        Debug.Log("All targets triggered - key spawned");
    }
}