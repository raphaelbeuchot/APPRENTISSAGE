using UnityEngine;
using System;
using System.Collections;

public class TomatoThrowSystem : MonoBehaviour
{
    [Header("References")]
    public PlayerStats stats;
    private TargetLockSystem lockSystem;
    private PlayerHealth health;
    private AudioSource audioSource;

    private int currentTomatoCount;
    private bool isThrowing = false;

    public event Action<int, int> OnTomatoCountChanged;

    public bool IsThrowingTomato() => isThrowing;

    void Start()
    {
        lockSystem = GetComponent<TargetLockSystem>();
        health = GetComponent<PlayerHealth>();
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        currentTomatoCount = 0;
        OnTomatoCountChanged?.Invoke(currentTomatoCount, stats.tomatoCount);
    }

    void Update()
    {
        if (!PlayerInputManager.Instance.ThrowTomatoPressed) return;
        if (!CanThrow()) return;
        ThrowTomato();
    }

    bool CanThrow()
    {
        if (stats == null) return false;
        if (currentTomatoCount <= 0) return false;
        if (health != null && health.IsDead()) return false;
        if (lockSystem == null || !lockSystem.IsLocked) return false;
        return true;
    }

    void ThrowTomato()
    {
        if (stats.bottlePrefab == null)
        {
            Debug.LogError("Tomato prefab non assigne dans PlayerStats");
            return;
        }

        Transform target = lockSystem.CurrentTarget;
        if (target == null) return;

        currentTomatoCount--;
        OnTomatoCountChanged?.Invoke(currentTomatoCount, stats.tomatoCount);

        StartCoroutine(ThrowingWindow());

        Vector3 spawnPos = transform.position + Vector3.up * 1.5f;
        GameObject tomato = Instantiate(stats.bottlePrefab, spawnPos, Quaternion.identity);
        TomatoProjectile proj = tomato.GetComponent<TomatoProjectile>();
        if (proj != null)
            proj.Init(stats, target.position + Vector3.up * 0.5f);

        Debug.Log("Tomate lancee ! Restantes : " + currentTomatoCount);
    }

    IEnumerator ThrowingWindow()
    {
        isThrowing = true;
        yield return new WaitForSeconds(0.5f);
        isThrowing = false;
    }

    public void Refill()
    {
        currentTomatoCount = stats.tomatoCount;
        OnTomatoCountChanged?.Invoke(currentTomatoCount, stats.tomatoCount);
        Debug.Log("Tomates rechargees : " + currentTomatoCount);
    }

    private void OnLockStanceEnter() { }
    private void OnLockStanceExit() { }
    private void OnThrowAnimation() { }

    public int GetTomatoCount() => currentTomatoCount;
    public int GetTomatoMax() => stats.tomatoCount;
}