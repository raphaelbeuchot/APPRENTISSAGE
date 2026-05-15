using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class LockableTarget : MonoBehaviour
{
    [Header("Manager")]
    [SerializeField] private LockableTargetManager manager;

    [Header("Lock Visual")]
    [SerializeField] private Image lockPip;
    [SerializeField] private Color pipColor = new Color(1f, 1f, 1f, 1f);

    [Header("Aim")]
    [SerializeField] private Vector3 aimOffset = Vector3.zero;

    [Header("Hit Feedback")]
    [SerializeField] private float hitRecoilAngle = 15f;
    [SerializeField] private float hitRecoilDuration = 0.1f;
    [SerializeField] private float fallAngle = 90f;
    [SerializeField] private float fallDuration = 0.6f;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip hitSound;

    [Header("Reset")]
    [SerializeField] private float resetDuration = 0.4f;
    [SerializeField] private AudioClip resetSound;

    private bool triggered = false;
    private Quaternion originalRotation;
    private Coroutine activeCoroutine;

    private void Start()
    {
        originalRotation = transform.rotation;

        if (lockPip != null)
        {
            lockPip.color = pipColor;
            lockPip.enabled = false;
        }
    }

    public Vector3 GetAimPosition()
    {
        return transform.position + aimOffset;
    }

    public bool IsValidTarget()
    {
        return !triggered;
    }

    public void SetLockedVisual(bool locked)
    {
        if (manager != null)
            manager.SetPipVisible(this, locked);
    }

    public void OnTomatoHit()
    {
        if (triggered) return;
        triggered = true;
        SetLockedVisual(false);

        if (audioSource != null && hitSound != null)
            audioSource.PlayOneShot(hitSound);

        if (activeCoroutine != null)
            StopCoroutine(activeCoroutine);
        activeCoroutine = StartCoroutine(TriggerCoroutine());

        if (manager != null)
            manager.OnTargetTriggered(this);
    }

    public void ResetTarget()
    {
        if (activeCoroutine != null)
            StopCoroutine(activeCoroutine);

        triggered = false;
        activeCoroutine = StartCoroutine(ResetCoroutine());
    }

    private IEnumerator TriggerCoroutine()
    {
        Quaternion recoilRot = originalRotation * Quaternion.Euler(hitRecoilAngle, 0f, 0f);
        Quaternion fallRot = originalRotation * Quaternion.Euler(-fallAngle, 0f, 0f);

        float elapsed = 0f;
        while (elapsed < hitRecoilDuration)
        {
            elapsed += Time.deltaTime;
            transform.rotation = Quaternion.Lerp(originalRotation, recoilRot, Mathf.SmoothStep(0f, 1f, elapsed / hitRecoilDuration));
            yield return null;
        }

        elapsed = 0f;
        while (elapsed < fallDuration)
        {
            elapsed += Time.deltaTime;
            transform.rotation = Quaternion.Lerp(recoilRot, fallRot, Mathf.SmoothStep(0f, 1f, elapsed / fallDuration));
            yield return null;
        }

        transform.rotation = fallRot;
        activeCoroutine = null;
    }

    private IEnumerator ResetCoroutine()
    {
        if (audioSource != null && resetSound != null)
            audioSource.PlayOneShot(resetSound);

        Quaternion startRot = transform.rotation;
        float elapsed = 0f;

        while (elapsed < resetDuration)
        {
            elapsed += Time.deltaTime;
            transform.rotation = Quaternion.Lerp(startRot, originalRotation, Mathf.SmoothStep(0f, 1f, elapsed / resetDuration));
            yield return null;
        }

        transform.rotation = originalRotation;
        activeCoroutine = null;
        Debug.Log("[LockableTarget] " + name + " reset");
    }
}