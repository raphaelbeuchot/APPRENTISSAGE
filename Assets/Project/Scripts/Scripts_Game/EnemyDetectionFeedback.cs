using System.Collections;
using UnityEngine;

public class EnemyDetectionFeedback : MonoBehaviour
{
    [Header("Materials")]
    [SerializeField] private Material whiteMaterial;
    [SerializeField] private Material redMaterial;

    [Header("Settings")]
    [SerializeField] private float detectionFlashDuration = 0.3f;
    [SerializeField] private float shotFlashDuration = 0.4f;
    [SerializeField] private AudioClip detectionSound;
    [SerializeField] private float detectionSoundVolume = 0.3f;

    private Renderer[] renderers;
    private Material[][] originalMaterials;
    private AudioSource audioSource;

    [HideInInspector]
    public bool isCurrentlyDetected = false;

    void Start()
    {
        renderers = GetComponentsInChildren<Renderer>();
        originalMaterials = new Material[renderers.Length][];
        for (int i = 0; i < renderers.Length; i++)
        {
            originalMaterials[i] = renderers[i].materials;
        }

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();
    }

    public void OnDetected()
    {
        if (GetComponent<EnemyHealth>()?.IsDead() == true) return;
        isCurrentlyDetected = true;
        StopAllCoroutines();
        StartCoroutine(FlashCoroutine(whiteMaterial, detectionFlashDuration));
        if (detectionSound != null)
            audioSource.PlayOneShot(detectionSound, detectionSoundVolume);
    }

    public void OnShotBySentinel()
    {
        if (GetComponent<EnemyHealth>()?.IsDead() == true) return;
        StopAllCoroutines();
        StartCoroutine(FlashCoroutine(redMaterial, shotFlashDuration));
    }

    private IEnumerator FlashCoroutine(Material flashMaterial, float duration)
    {
        SetMaterial(flashMaterial);
        yield return new WaitForSeconds(duration);
        RestoreMaterials();
    }

    private void SetMaterial(Material mat)
    {
        if (renderers == null || originalMaterials == null) return;
        for (int i = 0; i < renderers.Length; i++)
        {
            Material[] mats = new Material[originalMaterials[i].Length];
            for (int j = 0; j < mats.Length; j++)
                mats[j] = mat;
            renderers[i].materials = mats;
        }
    }

    private void RestoreMaterials()
    {
        if (renderers == null || originalMaterials == null) return;
        for (int i = 0; i < renderers.Length; i++)
            renderers[i].materials = originalMaterials[i];
        isCurrentlyDetected = false;
    }
}