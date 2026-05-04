using UnityEngine;
using UnityEngine.UI;

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

    private bool triggered = false;

    private void Awake()
    {
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

        StartCoroutine(TriggerCoroutine());

        if (manager != null)
            manager.OnTargetTriggered(this);
    }

    private System.Collections.IEnumerator TriggerCoroutine()
    {
        Quaternion startRot = transform.rotation;
        Quaternion recoilRot = startRot * Quaternion.Euler(hitRecoilAngle, 0f, 0f);
        Quaternion fallRot = startRot * Quaternion.Euler(-fallAngle, 0f, 0f);

        float elapsed = 0f;
        while (elapsed < hitRecoilDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / hitRecoilDuration);
            transform.rotation = Quaternion.Lerp(startRot, recoilRot, t);
            yield return null;
        }

        elapsed = 0f;
        while (elapsed < fallDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / fallDuration);
            transform.rotation = Quaternion.Lerp(recoilRot, fallRot, t);
            yield return null;
        }

        transform.rotation = fallRot;
    }
}