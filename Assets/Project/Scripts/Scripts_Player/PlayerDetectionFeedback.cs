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

    private SkinnedMeshRenderer[] playerMeshRenderers;
    private Material[][] originalMaterials;

    private Animator animator;

    [HideInInspector]
    public bool isCurrentlyDetected = false;

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
        SetColorSilhouette(redMaterial);

        yield return new WaitForSeconds(shotFlashDuration);

        if (isCurrentlyDetected)
            SetWhiteSilhouette(true);
        else
            SetWhiteSilhouette(false);
    }

    void SetColorSilhouette(Material colorMaterial)
    {
        if (playerMeshRenderers == null || originalMaterials == null) return;
        for (int i = 0; i < playerMeshRenderers.Length; i++)
        {
            Material[] colorMats = new Material[originalMaterials[i].Length];
            for (int j = 0; j < colorMats.Length; j++)
                colorMats[j] = colorMaterial;
            playerMeshRenderers[i].materials = colorMats;
        }
    }

    public void OnDetected()
    {
        if (isCurrentlyDetected) return;
        isCurrentlyDetected = true;

        if (audioSource != null && detectionSound != null)
            audioSource.PlayOneShot(detectionSound);

        SetWhiteSilhouette(true);
    }

    public void OnNoLongerDetected()
    {
        if (!isCurrentlyDetected) return;
        isCurrentlyDetected = false;
        SetWhiteSilhouette(false);
    }

    void SetWhiteSilhouette(bool white)
    {
        if (playerMeshRenderers == null || originalMaterials == null) return;
        for (int i = 0; i < playerMeshRenderers.Length; i++)
        {
            if (white)
            {
                Material[] whiteMats = new Material[originalMaterials[i].Length];
                for (int j = 0; j < whiteMats.Length; j++)
                    whiteMats[j] = whiteMaterial;
                playerMeshRenderers[i].materials = whiteMats;
            }
            else
            {
                playerMeshRenderers[i].materials = originalMaterials[i];
            }
        }
    }
}