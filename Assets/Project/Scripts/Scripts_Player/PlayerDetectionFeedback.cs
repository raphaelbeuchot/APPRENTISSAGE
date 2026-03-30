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

    [Header("Hit Scale Effect")]
    [SerializeField] private Vector3 pivotOffset = new Vector3(0f, 0.6f, 0f);
    [SerializeField] private float scalePeakMultiplier = 2f;
    [SerializeField] private float scaleHalfDuration = 0.25f;

    private SkinnedMeshRenderer[] playerMeshRenderers;
    private Material[][] originalMaterials;

    private Transform scaleTarget;
    private Vector3 originalLocalPos;

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

        foreach (Transform t in GetComponentsInChildren<Transform>())
        {
            if (t.name == "T-PoseNEW")
            {
                scaleTarget = t;
                break;
            }
        }

        if (scaleTarget != null)
            originalLocalPos = scaleTarget.localPosition;
        else
            Debug.LogWarning("[PlayerDetectionFeedback] T-PoseNEW non trouve");
    }

    public void OnShotBySentinel()
    {
        StopAllCoroutines();
        isCurrentlyDetected = false;
        StartCoroutine(ShotFlashCoroutine());
    }

    private IEnumerator ShotFlashCoroutine()
    {
        SetColorSilhouette(redMaterial);

        if (scaleTarget != null)
            StartCoroutine(ScaleCoroutine());

        yield return new WaitForSeconds(shotFlashDuration);

        if (isCurrentlyDetected)
            SetWhiteSilhouette(true);
        else
            SetWhiteSilhouette(false);
    }

    private IEnumerator ScaleCoroutine()
    {
        float elapsed = 0f;
        Vector3 normalScale = Vector3.one;
        Vector3 bigScale = Vector3.one * scalePeakMultiplier;

        while (elapsed < scaleHalfDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / scaleHalfDuration);
            scaleTarget.localScale = Vector3.Lerp(normalScale, bigScale, t);
            scaleTarget.localPosition = originalLocalPos + pivotOffset * (1f - scaleTarget.localScale.x);
            yield return null;
        }

        elapsed = 0f;
        while (elapsed < scaleHalfDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / scaleHalfDuration);
            scaleTarget.localScale = Vector3.Lerp(bigScale, normalScale, t);
            scaleTarget.localPosition = originalLocalPos + pivotOffset * (1f - scaleTarget.localScale.x);
            yield return null;
        }

        scaleTarget.localScale = normalScale;
        scaleTarget.localPosition = originalLocalPos;
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