using System.Collections;
using UnityEngine;

public class PlayerDetectionFeedback : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private AudioClip detectionSound;

    [Header("Shot Flash Settings")]
    [SerializeField] private float shotFlashDuration = 0.4f;

    [Header("Silhouette Materials")]
    [SerializeField] private Material whiteMaterial;
    [SerializeField] private Material redMaterial;
    [SerializeField] private Material brightEyesMaterial;

    private SkinnedMeshRenderer[] playerMeshRenderers;
    private Material[][] originalMaterials;
    private Animator animator;

    [HideInInspector]
    public bool isCurrentlyDetected = false;
    private bool isDetectedByBrightEyes = false;

    private AudioSource audioSource;

    void Start()
    {
        GameManager gm = FindObjectOfType<GameManager>();
        if (gm != null)
            audioSource = gm.GetComponent<AudioSource>();

        playerMeshRenderers = GetComponentsInChildren<SkinnedMeshRenderer>();
        originalMaterials = new Material[playerMeshRenderers.Length][];
        for (int i = 0; i < playerMeshRenderers.Length; i++)
            originalMaterials[i] = playerMeshRenderers[i].materials;

        animator = GetComponentInChildren<Animator>();
        if (animator == null)
            Debug.LogWarning("[PlayerDetectionFeedback] Animator non trouve");
    }

    public void OnShotBySentinel()
    {
        StopAllCoroutines();
        isCurrentlyDetected = false;
        if (animator != null)
            animator.SetTrigger("Shot");
        StartCoroutine(ShotFlashCoroutine());
    }

    private IEnumerator ShotFlashCoroutine()
    {
        SetAllRenderers(redMaterial);
        yield return new WaitForSeconds(shotFlashDuration);
        RefreshSilhouette();
    }

    public void OnDetected()
    {
        if (isCurrentlyDetected) return;
        isCurrentlyDetected = true;
        if (audioSource != null && detectionSound != null)
            audioSource.PlayOneShot(detectionSound);
        RefreshSilhouette();
    }

    public void OnNoLongerDetected()
    {
        if (!isCurrentlyDetected) return;
        isCurrentlyDetected = false;
        RefreshSilhouette();
    }

    public void OnBrightEyesDetected()
    {
        if (isDetectedByBrightEyes) return;
        isDetectedByBrightEyes = true;
        RefreshSilhouette();
    }

    public void OnBrightEyesNoLongerDetected()
    {
        if (!isDetectedByBrightEyes) return;
        isDetectedByBrightEyes = false;
        RefreshSilhouette();
    }

    void RefreshSilhouette()
    {
        if (isCurrentlyDetected)
            SetAllRenderers(whiteMaterial);
        else if (isDetectedByBrightEyes)
            SetAllRenderers(brightEyesMaterial);
        else
            RestoreOriginalMaterials();
    }

    void SetAllRenderers(Material mat)
    {
        if (playerMeshRenderers == null || originalMaterials == null) return;
        for (int i = 0; i < playerMeshRenderers.Length; i++)
        {
            Material[] mats = new Material[originalMaterials[i].Length];
            for (int j = 0; j < mats.Length; j++)
                mats[j] = mat;
            playerMeshRenderers[i].materials = mats;
        }
    }

    void RestoreOriginalMaterials()
    {
        if (playerMeshRenderers == null || originalMaterials == null) return;
        for (int i = 0; i < playerMeshRenderers.Length; i++)
            playerMeshRenderers[i].materials = originalMaterials[i];
    }
}