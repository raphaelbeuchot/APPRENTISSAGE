using UnityEngine;
using System.Collections;

public class TomatoThrowSystem : MonoBehaviour
{
    [Header("References")]
    public PlayerStats stats;

    private TargetLockSystem lockSystem;
    private PlayerHealth health;
    private AudioSource audioSource;

    private int currentTomatoCount;

    void Start()
    {
        lockSystem = GetComponent<TargetLockSystem>();
        health = GetComponent<PlayerHealth>();

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        currentTomatoCount = stats.tomatoCount;
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

        OnThrowAnimation();

        Vector3 spawnPos = transform.position + Vector3.up * 1.5f;
        GameObject tomato = Instantiate(stats.bottlePrefab, spawnPos, Quaternion.identity);

        TomatoProjectile proj = tomato.GetComponent<TomatoProjectile>();
        if (proj != null)
        {
            proj.Init(stats, target.position + Vector3.up * 0.5f);
        }

        Debug.Log("Tomate lancee ! Restantes : " + currentTomatoCount);
    }

    // --- Hooks animations - a brancher plus tard ---
    private void OnLockStanceEnter() { }
    private void OnLockStanceExit() { }
    private void OnThrowAnimation() { }

    public int GetTomatoCount() => currentTomatoCount;
    public int GetTomatoMax() => stats.tomatoCount;
}