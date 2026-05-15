using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

public class LockableTargetManager : MonoBehaviour
{
    public enum UnlockType { SpawnKey, SlideDoor }
    public enum SlideMode { Lateral, Up, Down }

    [Header("Targets")]
    [SerializeField] private List<LockableTarget> targets;
    [SerializeField] private int requiredCount = -1;

    [Header("Timer")]
    [SerializeField] private bool useTimer = false;
    [SerializeField] private float timerDuration = 10f;

    [Header("Timer Pips World")]
    [SerializeField] private Sprite timerPipSprite;
    [SerializeField] private int timerPipCount = 24;
    [SerializeField] private float timerCircleRadius = 0.75f;
    [SerializeField] private float timerPipScale = 0.5f;
    [SerializeField] private float pipPunchScale = 1.3f;
    [SerializeField] private float pipPunchDuration = 0.1f;
    [SerializeField] private float pipFadeOutDuration = 0.6f;
    [SerializeField] private Material timerMatIdle;
    [SerializeField] private Material timerMatActive;
    [SerializeField] private Material timerMatOff;

    [Header("Reset")]
    [SerializeField] private float resetDelay = 0.5f;

    [Header("Unlock")]
    [SerializeField] private UnlockType unlockType = UnlockType.SpawnKey;

    [Header("Key Spawn")]
    [SerializeField] private GameObject keyPrefab;
    [SerializeField] private Transform keySpawnPoint;

    [Header("Door")]
    [SerializeField] private GameObject doorGO;
    [SerializeField] private SlideMode slideMode = SlideMode.Lateral;
    [SerializeField] private float slideDistance = 0f;
    [SerializeField] private float slideDuration = 1f;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip allTargetsSound;
    [SerializeField] private AudioClip timerExpiredSound;
    [SerializeField] private AudioClip resetSound;

    [Header("Lock Pip UI")]
    [SerializeField] private Sprite pipSprite;
    [SerializeField] private RectTransform pipsContainer;
    [SerializeField] private Canvas canvas;
    [SerializeField] private float verticalOffset = 1.5f;
    [SerializeField] private float pipSize = 20f;
    [SerializeField] private Color pipColor = Color.white;

    private int triggeredCount = 0;
    private bool completed = false;
    private bool timerRunning = false;
    private float timerElapsed = 0f;
    private int lastPipsOff = 0;
    private Coroutine punchCoroutine;

    private List<SpriteRenderer> timerPipRenderers = new List<SpriteRenderer>();
    private Dictionary<LockableTarget, Image> pipImages = new Dictionary<LockableTarget, Image>();
    private Camera mainCamera;

    private enum PipState { Idle, Active, Off }

    private bool resetting = false;

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

        if (useTimer && timerPipSprite != null)
        {
            GameObject circleRoot = new GameObject("TimerPips");
            circleRoot.transform.SetParent(transform, false);
            circleRoot.transform.localPosition = Vector3.zero;

            for (int i = 0; i < timerPipCount; i++)
            {
                GameObject pipGO = new GameObject("TimerPip_" + i);
                pipGO.transform.SetParent(circleRoot.transform, false);

                float angle = Mathf.PI * 0.5f - (float)i / timerPipCount * Mathf.PI * 2f;
                pipGO.transform.localPosition = new Vector3(
                    timerCircleRadius * Mathf.Cos(angle),
                    timerCircleRadius * Mathf.Sin(angle),
                    0f);
                pipGO.transform.localScale = Vector3.one * timerPipScale;

                SpriteRenderer sr = pipGO.AddComponent<SpriteRenderer>();
                sr.sprite = timerPipSprite;
                if (timerMatIdle != null)
                    sr.sharedMaterial = timerMatIdle;

                timerPipRenderers.Add(sr);
            }
        }

        SetAllTimerPips(PipState.Idle);
    }

    private void Update()
    {
        if (!useTimer || !timerRunning || completed) return;

        timerElapsed += Time.deltaTime;

        if (timerPipRenderers.Count > 0)
        {
            int total = timerPipRenderers.Count;
            int pipsOff = Mathf.Clamp(Mathf.FloorToInt((timerElapsed / timerDuration) * total), 0, total);

            if (pipsOff > lastPipsOff)
            {
                for (int i = lastPipsOff; i < pipsOff; i++)
                    SetTimerPip(i, PipState.Off);
                lastPipsOff = pipsOff;
            }
        }

        if (timerElapsed >= timerDuration)
            OnTimerExpired();
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
            Vector2 localPoint;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvas.transform as RectTransform,
                screenPos,
                null,
                out localPoint);
            pip.GetComponent<RectTransform>().localPosition = localPoint;
        }
    }

    private void SetTimerPip(int index, PipState state)
    {
        if (index >= timerPipRenderers.Count || timerPipRenderers[index] == null) return;
        SpriteRenderer sr = timerPipRenderers[index];
        switch (state)
        {
            case PipState.Idle: if (timerMatIdle != null) sr.sharedMaterial = timerMatIdle; break;
            case PipState.Active: if (timerMatActive != null) sr.sharedMaterial = timerMatActive; break;
            case PipState.Off: if (timerMatOff != null) sr.sharedMaterial = timerMatOff; break;
        }
    }

    private void SetAllTimerPips(PipState state)
    {
        for (int i = 0; i < timerPipRenderers.Count; i++)
            SetTimerPip(i, state);
    }

    public void SetPipVisible(LockableTarget target, bool visible)
    {
        if (pipImages.TryGetValue(target, out Image pip) && pip != null)
            pip.gameObject.SetActive(visible);
    }

    public void OnTargetTriggered(LockableTarget target)
    {
        if (completed || resetting) return;

        if (useTimer && !timerRunning)
            StartTimer();

        if (useTimer && timerRunning)
        {
            if (punchCoroutine != null) StopCoroutine(punchCoroutine);
            punchCoroutine = StartCoroutine(PipsHitPunchCoroutine());
        }

        triggeredCount++;
        Debug.Log("[LockableTargetManager] " + triggeredCount + " / " + requiredCount);

        if (triggeredCount >= requiredCount)
            OnAllTargetsTriggered();
    }

    private void StartTimer()
    {
        timerRunning = true;
        timerElapsed = 0f;
        lastPipsOff = 0;
        SetAllTimerPips(PipState.Active);
        Debug.Log("[LockableTargetManager] Timer demarre");
    }

    private void OnTimerExpired()
    {
        timerRunning = false;
        resetting = true;

        if (punchCoroutine != null)
        {
            StopCoroutine(punchCoroutine);
            punchCoroutine = null;
            for (int i = 0; i < timerPipRenderers.Count; i++)
                if (timerPipRenderers[i] != null)
                    timerPipRenderers[i].transform.localScale = Vector3.one * timerPipScale;
        }

        SetAllTimerPips(PipState.Off);

        if (audioSource != null && timerExpiredSound != null)
            audioSource.PlayOneShot(timerExpiredSound);

        Debug.Log("[LockableTargetManager] Timer expire - reset");
        StartCoroutine(ResetCoroutine());
    }
    private IEnumerator ResetCoroutine()
    {
        yield return new WaitForSeconds(resetDelay);

        triggeredCount = 0;
        completed = false;
        timerElapsed = 0f;
        lastPipsOff = 0;

        if (audioSource != null && resetSound != null)
            audioSource.PlayOneShot(resetSound);

        foreach (LockableTarget target in targets)
        {
            if (target != null)
                target.ResetTarget();
        }

        for (int i = 0; i < timerPipRenderers.Count; i++)
        {
            if (timerPipRenderers[i] != null)
            {
                timerPipRenderers[i].gameObject.SetActive(true);
                Color c = timerPipRenderers[i].color;
                c.a = 1f;
                timerPipRenderers[i].color = c;
                timerPipRenderers[i].transform.localScale = Vector3.one * timerPipScale;
            }
        }

        SetAllTimerPips(PipState.Idle);

        foreach (LockableTarget target in targets)
            if (target != null)
                target.ForceResetState();

        resetting = false;
        Debug.Log("[LockableTargetManager] Reset complet");
    }
    private void OnAllTargetsTriggered()
    {
        completed = true;
        timerRunning = false;

        if (audioSource != null && allTargetsSound != null)
            audioSource.PlayOneShot(allTargetsSound);

        StartCoroutine(PipsSuccessCoroutine());

        switch (unlockType)
        {
            case UnlockType.SpawnKey:
                if (keyPrefab != null && keySpawnPoint != null)
                    Instantiate(keyPrefab, keySpawnPoint.position, keySpawnPoint.rotation);
                break;
            case UnlockType.SlideDoor:
                StartCoroutine(SlideDoorCoroutine());
                break;
        }

        Debug.Log("[LockableTargetManager] Unlock declenche");
    }

    private IEnumerator PipsHitPunchCoroutine()
    {
        float baseScale = timerPipScale;
        float punchTarget = timerPipScale * pipPunchScale;
        float elapsed = 0f;

        // Gonflement
        while (elapsed < pipPunchDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / pipPunchDuration);
            float scale = Mathf.Lerp(baseScale, punchTarget, t);
            for (int i = 0; i < timerPipRenderers.Count; i++)
                if (timerPipRenderers[i] != null)
                    timerPipRenderers[i].transform.localScale = Vector3.one * scale;
            yield return null;
        }

        // Retour
        elapsed = 0f;
        while (elapsed < pipPunchDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / pipPunchDuration);
            float scale = Mathf.Lerp(punchTarget, baseScale, t);
            for (int i = 0; i < timerPipRenderers.Count; i++)
                if (timerPipRenderers[i] != null)
                    timerPipRenderers[i].transform.localScale = Vector3.one * scale;
            yield return null;
        }

        for (int i = 0; i < timerPipRenderers.Count; i++)
            if (timerPipRenderers[i] != null)
                timerPipRenderers[i].transform.localScale = Vector3.one * baseScale;
    }

    private IEnumerator PipsSuccessCoroutine()
    {
        float baseScale = timerPipScale;
        float punchTarget = timerPipScale * pipPunchScale;
        float elapsed = 0f;

        // Gonflement
        while (elapsed < pipPunchDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / pipPunchDuration);
            float scale = Mathf.Lerp(baseScale, punchTarget, t);
            for (int i = 0; i < timerPipRenderers.Count; i++)
                if (timerPipRenderers[i] != null)
                    timerPipRenderers[i].transform.localScale = Vector3.one * scale;
            yield return null;
        }

        // Fade out
        elapsed = 0f;
        while (elapsed < pipFadeOutDuration)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Lerp(1f, 0f, elapsed / pipFadeOutDuration);
            for (int i = 0; i < timerPipRenderers.Count; i++)
            {
                if (timerPipRenderers[i] != null)
                {
                    Color c = timerPipRenderers[i].color;
                    c.a = alpha;
                    timerPipRenderers[i].color = c;
                }
            }
            yield return null;
        }

        for (int i = 0; i < timerPipRenderers.Count; i++)
            if (timerPipRenderers[i] != null)
                timerPipRenderers[i].gameObject.SetActive(false);
    }

    private IEnumerator SlideDoorCoroutine()
    {
        if (doorGO == null)
        {
            Debug.LogWarning("[LockableTargetManager] doorGO non assigne");
            yield break;
        }

        float distance = slideDistance;

        if (distance <= 0f)
        {
            if (slideMode == SlideMode.Lateral)
            {
                Collider col = doorGO.GetComponent<Collider>();
                if (col != null)
                    distance = col.bounds.size.x;
            }
            else
            {
                Debug.LogWarning("[LockableTargetManager] slideDistance a 0 pour mode Up/Down");
                yield break;
            }
        }

        Vector3 slideDir;
        switch (slideMode)
        {
            case SlideMode.Lateral: slideDir = doorGO.transform.right; break;
            case SlideMode.Up: slideDir = Vector3.up; break;
            case SlideMode.Down: slideDir = Vector3.down; break;
            default: slideDir = doorGO.transform.right; break;
        }

        Vector3 startPos = doorGO.transform.position;
        Vector3 endPos = startPos + slideDir * distance;
        float elapsed = 0f;

        while (elapsed < slideDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / slideDuration);
            doorGO.transform.position = Vector3.Lerp(startPos, endPos, t);
            yield return null;
        }

        doorGO.transform.position = endPos;
        Debug.Log("[LockableTargetManager] Porte ouverte");
    }
}